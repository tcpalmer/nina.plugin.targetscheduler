using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using System;

namespace NINA.Plugin.TargetScheduler.Planning {

    /// <summary>
    /// Core methods to determine the suitability of a target and target exposures for imaging.  Note that
    /// these methods cause side-effects on the provided target and exposures, for example setting
    /// rejection details.
    /// </summary>
    public class TargetImagingExpert {
        private const int TARGET_VISIBILITY_SAMPLE_INTERVAL = 10;
        private const int TARGET_FUTURE_TEST_SAMPLE_INTERVAL = 60;

        private IProfile profile;
        private ObserverInfo observerInfo;
        private int targetVisibilitySampleInterval = TARGET_VISIBILITY_SAMPLE_INTERVAL;
        private int targetFutureTestSampleInterval = TARGET_FUTURE_TEST_SAMPLE_INTERVAL;

        public TargetImagingExpert(IProfile activeProfile, ProfilePreference profilePreferences, bool isPreview) {
            this.profile = activeProfile;
            this.observerInfo = new ObserverInfo {
                Latitude = activeProfile.AstrometrySettings.Latitude,
                Longitude = activeProfile.AstrometrySettings.Longitude,
                Elevation = activeProfile.AstrometrySettings.Elevation,
            };

            if (isPreview) {
                targetVisibilitySampleInterval *= 3;
                targetFutureTestSampleInterval *= 3;
            }
        }

        public bool Visibility(DateTime atTime, ITarget target) {
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(observerInfo, atTime);
            TargetVisibility targetVisibility = new(target, observerInfo,
                twilightCircumstances.OnDate, twilightCircumstances.Sunset, twilightCircumstances.Sunrise, targetVisibilitySampleInterval);

            if (!Visibility(atTime, target, twilightCircumstances, targetVisibility)) { return false; }
            return CheckMaximumAltitude(atTime, target, targetVisibility);
        }

        /// <summary>
        /// Determine visibility for the target at the provided time.
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="target"></param>
        /// <param name="twilightCircumstances"></param>
        /// <param name="targetVisibility"></param>
        /// <returns></returns>
        public bool Visibility(DateTime atTime, ITarget target, TwilightCircumstances twilightCircumstances, TargetVisibility targetVisibility) {
            if (target.Rejected) { return false; }
            IProject project = target.Project;

            if (!AstrometryUtils.RisesAtLocation(observerInfo, target.Coordinates)) {
                TSLogger.Warning($"target {project.Name}/{target.Name} never rises at location - skipping");
                SetRejected(target, Reasons.TargetNeverRises);
                return false;
            }

            // Get the most inclusive twilight over all incomplete exposure plans
            TimeInterval twilightSpan = twilightCircumstances.GetTwilightSpan(GetOverallTwilight(target));

            // At high latitudes near the summer solsice, you can lose nighttime completely (even below the polar circle)
            if (twilightSpan == null) {
                TSLogger.Warning($"No twilight span for target {project.Name}/{target.Name} on {Utils.FormatDateTimeFull(atTime)} at latitude {observerInfo.Latitude}");
                SetRejected(target, Reasons.TargetAllExposurePlans);
                return false;
            }

            if (!targetVisibility.ImagingPossible) {
                TSLogger.Warning($"Target not visible at all {project.Name}/{target.Name} on {Utils.FormatDateTimeFull(atTime)} at latitude {observerInfo.Latitude}");
                SetRejected(target, Reasons.TargetNotVisible);
                return false;
            }

            // Determine the next time interval of visibility of at least the mimimum time
            VisibilityDetermination viz = targetVisibility.NextVisibleInterval(atTime, twilightSpan, project.HorizonDefinition, project.MinimumTime * 60);
            if (!viz.IsVisible) {
                TSLogger.Trace($"Target not visible for rest of night {project.Name}/{target.Name} on {Utils.FormatDateTimeFull(atTime)} at latitude {observerInfo.Latitude}");
                SetRejected(target, Reasons.TargetNotVisible);
                return false;
            }

            DateTime targetStartTime = viz.StartTime;
            DateTime targetTransitTime = targetVisibility.TransitTime;
            DateTime targetEndTime = viz.StopTime;

            // Clip time span to optional meridian window
            TimeInterval meridianClippedSpan = null;
            if (project.MeridianWindow > 0) {
                TSLogger.Trace($"checking meridian window for {project.Name}/{target.Name}");
                meridianClippedSpan = new MeridianWindowClipper().Clip(targetStartTime, targetTransitTime, targetEndTime, project.MeridianWindow);

                if (meridianClippedSpan == null) {
                    SetRejected(target, Reasons.TargetMeridianWindowClipped);
                    return false;
                }

                target.MeridianWindow = meridianClippedSpan;
                targetStartTime = meridianClippedSpan.StartTime;
                targetEndTime = meridianClippedSpan.EndTime;
            }

            // Recheck minimum time after potential meridian clip
            if (project.MeridianWindow > 0 && target.MeridianWindow.Duration < project.MinimumTime * 60) {
                TSLogger.Trace($"Target not visible for min time after meridian window clip {project.Name}/{target.Name} on {Utils.FormatDateTimeFull(atTime)} at latitude {observerInfo.Latitude}");
                SetRejected(target, Reasons.TargetNotVisible);
                return false;
            }

            // Special handling when profile specifies a pause before MF.  If pause > 0 and that pause would occur in the visibility
            // span, then adjust the target start or end times to avoid the MF safety zone.
            TimeInterval meridianFlipClippedSpan = null;
            if (profile.MeridianFlipSettings?.PauseTimeBeforeMeridian > 0) {
                meridianFlipClippedSpan = new MeridianFlipClipper(atTime, target).Clip(profile, targetStartTime, targetTransitTime, targetEndTime);
                if (meridianFlipClippedSpan == null) {
                    SetRejected(target, Reasons.TargetMeridianFlipClipped);
                    return false;
                }

                targetStartTime = meridianFlipClippedSpan.StartTime;
                targetEndTime = meridianFlipClippedSpan.EndTime;
            }

            // If the start time is in the future, reject ... for now
            DateTime actualStart = atTime > targetStartTime ? atTime : targetStartTime;
            if (actualStart > atTime) {
                target.StartTime = actualStart;
                target.EndTime = targetEndTime;
                string reason = Reasons.TargetNotYetVisible;
                if (meridianClippedSpan != null || meridianFlipClippedSpan != null) {
                    reason = meridianClippedSpan != null ? Reasons.TargetBeforeMeridianWindow : Reasons.TargetMeridianFlipClipped;
                }

                SetRejected(target, reason);
                return false;
            }

            // Otherwise the target is a candidate
            target.StartTime = targetStartTime;
            target.EndTime = targetEndTime;
            target.MinimumTimeSpanEnd = atTime.AddSeconds(project.MinimumTime * 60);
            target.BonusTimeSpanEnd = target.MinimumTimeSpanEnd;
            target.CulminationTime = targetTransitTime;
            return true;
        }

        /// <summary>
        /// Reject targets that are currently above the project's max altitude setting.
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool CheckMaximumAltitude(DateTime atTime, ITarget target, TargetVisibility targetVisibility) {
            if (target.Rejected || target.Project.MaximumAltitude == 0) { return true; }

            double altitude = targetVisibility.GetAltitude(atTime);
            if (altitude > target.Project.MaximumAltitude) {
                SetRejected(target, Reasons.TargetMaxAltitude);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reject target exposures that are not suitable for the current level of twilight.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="currentTwilightLevel"></param>
        public void TwilightFilter(ITarget target, TwilightLevel? currentTwilightLevel) {
            if (target.Rejected) { return; }

            foreach (IExposure exposure in target.ExposurePlans) {
                if (!exposure.Rejected && exposure.IsIncomplete()) {
                    if (currentTwilightLevel.HasValue) {
                        if (currentTwilightLevel > exposure.TwilightLevel)
                            SetRejected(exposure, Reasons.FilterTwilight);
                    } else {
                        SetRejected(exposure, Reasons.FilterTwilight);
                    }
                }
            }
        }

        /// <summary>
        /// Reject target exposures that are not suitable based on moon avoidance settings.
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="target"></param>
        /// <param name="moonExpert"></param>
        public void MoonAvoidanceFilter(DateTime atTime, ITarget target, IMoonAvoidanceExpert moonExpert) {
            if (target.Rejected && target.RejectedReason != Reasons.TargetNotYetVisible) { return; }

            foreach (IExposure exposure in target.ExposurePlans) {
                if (exposure.IsIncomplete()) {
                    if (moonExpert.IsRejected(atTime, target, exposure)) {
                        SetRejected(exposure, Reasons.FilterMoonAvoidance);
                    }
                }
            }
        }

        /// <summary>
        /// Determine if the target is ready to image at the provided time, taking the visibility sampling
        /// interval into account.
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool ReadyNow(DateTime atTime, ITarget target) {
            if (target.Rejected) { return false; }

            TimeSpan diff = atTime - target.StartTime;
            return Math.Abs(diff.TotalSeconds) <= targetVisibilitySampleInterval * 2;
        }

        /// <summary>
        /// Find the next possible time that a target could be imaged, taking into account target
        /// visibility and the suitability of exposures given the state of circumstances (like
        /// moon avoidance) at that time.
        ///
        /// If future imaging is possible, the target's start time will be advanced to that time.
        /// Otherwise, the target will be marked rejected.
        /// </summary>
        /// <param name="target"></param>
        public void CheckFuture(ITarget target, IMoonAvoidanceExpert moonExpert) {
            DateTime atTime = target.StartTime;
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(observerInfo, atTime);
            TargetVisibility targetVisibility = new(target, observerInfo,
                twilightCircumstances.OnDate, twilightCircumstances.Sunset, twilightCircumstances.Sunrise, targetVisibilitySampleInterval);

            while (true) {
                // Check target for maximum altitude, moon avoidance, and twilight at this time

                CheckMaximumAltitude(atTime, target, targetVisibility);

                if (!target.Rejected) {
                    MoonAvoidanceFilter(atTime, target, moonExpert);
                    if (AllExposurePlansRejected(target)) {
                        SetRejected(target, Reasons.TargetMoonAvoidance);
                    }
                }

                if (!target.Rejected) {
                    TwilightFilter(target, twilightCircumstances.GetCurrentTwilightLevel(atTime));
                    if (AllExposurePlansRejected(target)) {
                        SetRejected(target, Reasons.FilterTwilight);
                    }
                }

                // If not rejected, we've found a future time at which the target could be imaged
                if (!target.Rejected) {
                    target.StartTime = atTime;
                    return;
                }

                // Otherwise, advance time and check target visibility at the new time
                atTime = atTime.AddSeconds(targetFutureTestSampleInterval);
                ClearRejections(target);
                if (Visibility(atTime, target, twilightCircumstances, targetVisibility)) {
                    atTime = target.StartTime;
                } else if (VisibleLater(target)) {
                    atTime = target.StartTime;
                } else {
                    return; // no more visibility this night
                }
            }
        }

        public bool VisibleLater(ITarget target) {
            return target.Rejected &&
                  (target.RejectedReason == Reasons.TargetNotYetVisible
                || target.RejectedReason == Reasons.TargetBeforeMeridianWindow
                || target.RejectedReason == Reasons.TargetMeridianFlipClipped
                || target.RejectedReason == Reasons.TargetMaxAltitude);
        }

        public bool AllExposurePlansRejected(ITarget target) {
            foreach (IExposure exposure in target.ExposurePlans) {
                if (!exposure.Rejected) {
                    return false;
                }
            }

            return true;
        }

        public void ClearRejections(ITarget target) {
            target.Rejected = false;
            target.RejectedReason = null;
            target.ExposurePlans.ForEach(e => { e.Rejected = false; e.RejectedReason = null; });
        }

        private TwilightLevel GetOverallTwilight(ITarget target) {
            TwilightLevel twilightLevel = TwilightLevel.Nighttime;
            foreach (IExposure exposure in target.ExposurePlans) {
                // find most permissive (brightest) twilight over all incomplete plans
                if (exposure.TwilightLevel > twilightLevel && exposure.IsIncomplete()) {
                    twilightLevel = exposure.TwilightLevel;
                }
            }

            return twilightLevel;
        }

        private void SetRejected(ITarget target, string reason) {
            target.Rejected = true;
            target.RejectedReason = reason;
        }

        private void SetRejected(IExposure exposure, string reason) {
            exposure.Rejected = true;
            exposure.RejectedReason = reason;
        }
    }
}