using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
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
using System.Linq;

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
        public bool CanContinue(DateTime atTime, IWeatherDataMediator weatherDataMediator, ITarget previousTarget) {
            if (previousTarget == null) { return false; }

            // If we detect that the active target has been edited since it started, we have to abort
            if (TargetEditGuard.Instance.IsEdited(previousTarget.DatabaseId)) {
                TSLogger.Info($"not continuing previous target at {atTime}, {previousTarget.Name}: detected impacting edit to target");
                return false;
            }

            UpdateTargetExposurePlans(previousTarget);

            // Recheck exposure completion
            if (previousTarget.ExposurePlans.Count == 0) {
                TSLogger.Info($"not continuing previous target at {atTime}, {previousTarget.Name}: all exposure plans complete");
                return false;
            }

            // Recheck for moon avoidance
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);
            targetExpert.MoonAvoidanceFilter(atTime, previousTarget, new MoonAvoidanceExpert(observerInfo));
            bool allRejected = previousTarget.ExposurePlans.All(ep => ep.Rejected);
            if (allRejected) {
                TSLogger.Info($"not continuing previous target at {atTime}, {previousTarget.Name}: all remaining exposure plans now rejected for moon avoidance");
                return false;
            }

            // Recheck for humidity
            targetExpert.HumidityFilter(previousTarget, weatherDataMediator);
            allRejected = previousTarget.ExposurePlans.All(ep => ep.Rejected);
            if (allRejected) {
                TSLogger.Info($"not continuing previous target at {atTime}, {previousTarget.Name}: all remaining exposure plans now rejected for humidity");
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

            // In some cases, we will have picked a target but at some point during the minimum time
            // span, we run out of suitable exposures.  Or, we detect that the user made an edit to
            // this target (which we can't recover/continue from).  If so, bail out and perform a full
            // planning run - even if it means we won't run for the minimum time.
            if (nextExposure == null) {
                TSLogger.Info($"not continuing previous target at {atTime}, {previousTarget.Name}: exposure selector did not return a suitable exposure)");
                return false;
            }

            // Special case: be sure that the next exposure is suitable for the current level of twilight.
            // It could be the case that we selected this target when the first exposure was suitable for
            // a brighter level of twilight but once those are complete, we're only left with exposures
            // that are not suitable now.  If so, we bail out but note that this might mean we've started
            // imaging on a target but won't run for the minimum time.

            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(observerInfo, atTime);
            TwilightLevel? twilightLevel = twilightCircumstances.GetCurrentTwilightLevel(atTime);
            targetExpert.ExposureTwilightFilter(nextExposure, atTime, twilightCircumstances, (TwilightLevel)twilightLevel);
            if (nextExposure.Rejected) {
                TSLogger.Info($"not continuing previous target at {atTime}, {previousTarget.Name}: next exposure ({nextExposure.FilterName}) not suitable for current twilight level ({twilightLevel})");
                return false;
            }

            // Be sure that the next exposure can fit in the remaining permitted time span
            if (atTime.AddSeconds(nextExposure.ExposureLength) > previousTarget.BonusTimeSpanEnd) {
                TSLogger.Info($"not continuing previous target at {atTime}, {previousTarget.Name}: minimum/allowed time window exceeded ({previousTarget.BonusTimeSpanEnd})");
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
    }

    /// <summary>
    /// If the active target is edited while running for the minimum time span, we need to detect it and
    /// abort the target.
    ///
    /// Note this applies to edits that alter the set of exposure plans or the ordering of exposures.  Just
    /// changing the name of the target wouldn't trigger this behavior.  But changing the Filter Switch Frequency
    /// on the associated project would.
    /// </summary>
    public class TargetEditGuard {
        private static readonly Lazy<TargetEditGuard> lazy = new Lazy<TargetEditGuard>(() => new TargetEditGuard());
        public static TargetEditGuard Instance { get => lazy.Value; }
        private static object lockObj = new object();

        private int targetId;
        private bool edited;

        private TargetEditGuard() {
            Clear();
        }

        public void Clear() {
            lock (lockObj) {
                targetId = -1;
                edited = false;
                TSLogger.Trace("TargetEditGuard cleared");
            }
        }

        public void MarkEdited(int targetId) {
            lock (lockObj) {
                this.targetId = targetId;
                edited = true;
                TSLogger.Trace($"TargetEditGuard mark edited: {targetId}");
            }
        }

        public bool IsEdited(int targetId) {
            lock (lockObj) {
                TSLogger.Trace($"TargetEditGuard edit check: {targetId}");
                if (!edited) { return false; }
                if (targetId == this.targetId) { return true; }
            }

            TSLogger.Warning($"mismatched targetId in TargetEditGuard: expected {targetId}, was {this.targetId} !?");
            return false;
        }
    }
}