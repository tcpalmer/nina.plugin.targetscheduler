using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Planning.Exposures {

    /// <summary>
    /// Select the next exposure automatically based on moon avoidance score.
    /// </summary>
    public class SmartExposureSelector : BaseExposureSelector, IExposureSelector {
        private SmartExposureRotateManager SmartExposureRotateManager = null;

        public SmartExposureSelector(IProject project, ITarget target, Target databaseTarget) : base(target) {
            DitherManager = GetDitherManager(project, target);
            if (project.FilterSwitchFrequency > 0) {
                SmartExposureRotateManager = new SmartExposureRotateManager(target, project.FilterSwitchFrequency);
            }
        }

        public IExposure Select(DateTime atTime, IProject project, ITarget target) {
            if (AllExposurePlansRejected(target)) {
                TSLogger.Warning($"unexpected: all exposure plans were rejected at exposure selection time for target '{target.Name}' at time {atTime}");
                return null;
            }

            // Find the accepted exposure with the highest score
            IExposure selected = null;
            double highScore = double.MinValue;

            foreach (IExposure exposure in target.ExposurePlans) {
                if (exposure.Rejected) continue;
                if (exposure.MoonAvoidanceScore > highScore) {
                    selected = exposure;
                    highScore = exposure.MoonAvoidanceScore;
                }
            }

            // If smart exposure filter rotation applies, then select based on state of the filter rotation
            if (SmartExposureRotateManager != null) {
                List<IExposure> candidates = target.ExposurePlans.Where(ep => !ep.Rejected && EqualScore(selected.MoonAvoidanceScore, ep.MoonAvoidanceScore)).ToList();
                if (candidates.Count > 1) {
                    selected = SmartExposureRotateManager.Select(candidates);
                }
            }

            if (selected == null) {
                TSLogger.Warning($"no acceptable exposure plan in smart exposure selector for target '{target.Name}' at time {atTime}");
                return null;
            }

            selected.PreDither = DitherManager.DitherRequired(selected);
            return selected;
        }

        public void ExposureTaken(IExposure exposure) {
            if (exposure.PreDither) DitherManager.Reset();
            DitherManager.AddExposure(exposure);
            SmartExposureRotateManager?.ExposureTaken(exposure);
        }

        public void TargetReset() {
            DitherManager.Reset();
            SmartExposureRotateManager?.Reset();
        }

        private bool EqualScore(double benchmark, double check) {
            return Math.Abs(benchmark - check) < 0.01;
        }
    }
}