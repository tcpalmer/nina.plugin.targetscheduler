using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning {

    public class PreviousTargetExpert {
        private IProfile activeProfile;
        private ProfilePreference profilePreferences;
        private bool isPreview;
        private ObserverInfo observerInfo;

        public PreviousTargetExpert(IProfile profile, ProfilePreference profilePreferences, bool isPreview, ObserverInfo observerInfo) {
            this.activeProfile = profile;
            this.profilePreferences = profilePreferences;
            this.isPreview = isPreview;
            this.observerInfo = observerInfo;
        }

        /// <summary>
        /// Determine if the previous target can continue.  The first time this target came through the planner,
        /// we save the future time at which it's minimum time window will expire.  We can then compare the current
        /// time to that to see if the target is still within that window.  We don't need to check visibility again
        /// because the first run assures that the target is visible for the entire minimum time.
        ///
        /// However, we also need to perform some additional checks:
        /// * if all exposure plans are now complete -> done
        /// * recheck if all exposure plans would now be rejected due to moon avoidance
        /// * confirm that the next selected exposure is suitable for the current level of twilight
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="previousTarget"></param>
        /// <returns></returns>
        public bool CanContinue(DateTime atTime, ITarget previousTarget) {
            if (previousTarget == null) { return false; }

            UpdateTargetExposurePlans(previousTarget);

            // Recheck exposure completion
            if (previousTarget.ExposurePlans.Count == 0) {
                TSLogger.Info($"not continuing previous target {previousTarget.Name}: all exposure plans complete");
                return false;
            }

            // Recheck for moon avoidance
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);
            targetExpert.MoonAvoidanceFilter(atTime, previousTarget, new MoonAvoidanceExpert(observerInfo));
            bool allRejected = true;
            previousTarget.ExposurePlans.ForEach(ep => { if (!ep.Rejected) { allRejected = false; } });
            if (allRejected) {
                TSLogger.Info($"not continuing previous target {previousTarget.Name}: all remaining exposure plans rejected for moon avoidance");
                return false;
            }

            // Special case: if the previous target wouldn't be selected again because its remaining
            // visibility time wouldn't fit within the project minimum, then we allow the target to
            // continue up to the end of visibility.  Otherwise, that time would always be wasted.
            // This should also mitigate the problem of the TS condition checks stopping only because
            // the selected target couldn't fit in a remaining minimum span due to end of visibility.

            if ((previousTarget.EndTime - previousTarget.MinimumTimeSpanEnd).TotalSeconds < previousTarget.Project.MinimumTime * 60) {
                previousTarget.BonusTimeSpanEnd = previousTarget.EndTime;
                TSLogger.Info($"extending allowed time for target {previousTarget.Name} to {previousTarget.BonusTimeSpanEnd} for visibility end allowance");
            }

            IExposure nextExposure = previousTarget.ExposureSelector.Select(atTime, previousTarget.Project, previousTarget);

            // Special case: be sure that the next exposure is suitable for the current level of twilight.
            // It could be the case that we selected this target when the first exposure was suitable for
            // a brighter level of twilight but once those are complete, we're only left with exposures
            // that are not suitable now.  If so, we bail out but note that this might mean we've started
            // imaging on a target but won't run for the minimum time.
            TwilightLevel? twilightLevel = GetTwilightLevel(atTime);
            if (!twilightLevel.HasValue || (twilightLevel.HasValue && twilightLevel > nextExposure.TwilightLevel)) {
                TSLogger.Info($"not continuing previous target {previousTarget.Name}: next exposure ({nextExposure.FilterName}) not suitable for current twilight level ({twilightLevel})");
                return false;
            }

            // Be sure that the next exposure can fit in the remaining permitted time span
            if (atTime.AddSeconds(nextExposure.ExposureLength) > previousTarget.BonusTimeSpanEnd) {
                TSLogger.Info($"not continuing previous target {previousTarget.Name}: minimum/allowed time window exceeded ({previousTarget.BonusTimeSpanEnd})");
                return false;
            }

            previousTarget.SelectedExposure = nextExposure;
            TSLogger.Debug($"continuing previous target {previousTarget.Name}, next filter: {previousTarget.SelectedExposure.FilterName}");

            return true;
        }

        private void UpdateTargetExposurePlans(ITarget previousTarget) {
            if (previousTarget == null) return;

            // If running in a real sequence, reload exposure plans to get latest from database
            if (!previousTarget.IsPreview) {
                previousTarget.AllExposurePlans = GetExposurePlans(previousTarget);
            }

            previousTarget.ExposurePlans.Clear();
            previousTarget.CompletedExposurePlans.Clear();

            previousTarget.AllExposurePlans.ForEach(ep => {
                if (ep.IsIncomplete()) {
                    previousTarget.ExposurePlans.Add(ep);
                } else {
                    SetRejected(ep, Reasons.FilterComplete);
                    previousTarget.CompletedExposurePlans.Add(ep);
                }
            });

            // If this target is using an override exposure order, we need to ensure that it covers all exposure plans.
            // Otherwise, they have to be marked rejected since the target would never reach completion just using
            // the OEO list (this is in the context of whether the current target can continue).
            OverrideOrderExposureSelector oeoExposureSelector = previousTarget.ExposureSelector as OverrideOrderExposureSelector;
            if (oeoExposureSelector != null) {
                for (int i = 0; i < previousTarget.AllExposurePlans.Count; i++) {
                    if (!oeoExposureSelector.ContainsExposurePlanIdx(i)) {
                        SetRejected(previousTarget.AllExposurePlans[i], Reasons.FilterComplete);
                    }
                }
            }
        }

        private List<IExposure> GetExposurePlans(ITarget target) {
            try {
                SchedulerDatabaseInteraction database = new SchedulerDatabaseInteraction();
                using (SchedulerDatabaseContext context = database.GetContext()) {
                    var eps = context.GetExposurePlans(target.DatabaseId);
                    List<IExposure> exposures = new List<IExposure>(eps.Count);
                    eps.ForEach(ep => {
                        exposures.Add(new PlanningExposure(target, ep, ep.ExposureTemplate));
                    });
                    return exposures;
                }
            } catch (Exception ex) {
                TSLogger.Error($"exception reloading target exposure plans {target.Name}: {ex.StackTrace}");
                throw new SequenceEntityFailedException($"Scheduler: exception reloading target exposure plans: {ex.Message}", ex);
            }
        }

        private void SetRejected(IExposure exposure, string reason) {
            exposure.Rejected = true;
            exposure.RejectedReason = reason;
        }

        private TwilightLevel? GetTwilightLevel(DateTime atTime) {
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(observerInfo, atTime);
            return twilightCircumstances.GetCurrentTwilightLevel(atTime);
        }
    }
}