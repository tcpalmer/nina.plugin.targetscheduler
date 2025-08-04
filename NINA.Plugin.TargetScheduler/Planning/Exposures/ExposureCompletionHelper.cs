﻿using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Planning.Exposures {

    public class ExposureCompletionHelper {
        private bool imageGradingEnabled;
        private double delayGrading;
        private double exposureThrottle;

        public ExposureCompletionHelper(bool imageGradingEnabled, double delayGrading, double exposureThrottle) {
            this.imageGradingEnabled = imageGradingEnabled;
            this.delayGrading = delayGrading;
            this.exposureThrottle = exposureThrottle / 100;
        }

        public double PercentComplete(IExposureCounts exposurePlan) {
            if (imageGradingEnabled) {
                // If delayed grading threshold has not been reached, then 'provisional' completion is based on raw number acquired
                if (IsProvisionalPercentComplete(exposurePlan)) {
                    return Percentage(exposurePlan.Acquired, exposurePlan.Desired);
                }

                return Percentage(exposurePlan.Accepted, exposurePlan.Desired);
            }

            if (exposurePlan.Acquired == 0) { return 0; }
            double throttleAt = (int)(exposureThrottle * exposurePlan.Desired);
            double percent = (exposurePlan.Acquired / throttleAt) * 100;
            return percent < 100 ? percent : 100;
        }

        public double PercentComplete(Target target, bool noExposurePlansIsComplete = false) {
            if (target.ExposurePlans.Count == 0) {
                return noExposurePlansIsComplete ? 100 : 0;
            }

            return target.ExposurePlans.Sum(PercentComplete) / target.ExposurePlans.Count;
        }

        public double PercentComplete(ITarget target) {
            List<IExposure> list = new List<IExposure>();
            list.AddRange(target.ExposurePlans);
            list.AddRange(target.CompletedExposurePlans);

            if (list.Count == 0) { return 0; }
            return list.Sum(PercentComplete) / list.Count;
        }

        public bool HasEnabledPlans(Target target) {
            return target.ExposurePlans.Find(ep => ep.IsEnabled) != null;
        }

        public bool IsIncomplete(ITarget target) {
            return PercentComplete(target) < 100;
        }

        public bool IsProvisionalPercentComplete(IExposureCounts exposurePlan) {
            return imageGradingEnabled && delayGrading > 0 && CurrentDelayThreshold(exposurePlan) < delayGrading;
        }

        public int RemainingExposures(IExposureCounts exposurePlan) {
            if (imageGradingEnabled) {
                return exposurePlan.Accepted >= exposurePlan.Desired ? 0 : exposurePlan.Desired - exposurePlan.Accepted;
            }

            int throttleAt = (int)(exposureThrottle * exposurePlan.Desired);
            return exposurePlan.Acquired >= throttleAt ? 0 : throttleAt - exposurePlan.Acquired;
        }

        public bool IsIncomplete(IExposureCounts exposurePlan) {
            return PercentComplete(exposurePlan) < 100;
        }

        private double Percentage(double num, double denom) {
            if (denom == 0) { return 0; }
            double percent = (num / denom) * 100;
            return percent < 100 ? percent : 100;
        }

        private double CurrentDelayThreshold(IExposureCounts exposurePlan) {
            return exposurePlan.Desired == 0 ? 0 : ((double)exposurePlan.Acquired / (double)exposurePlan.Desired) * 100;
        }
    }
}