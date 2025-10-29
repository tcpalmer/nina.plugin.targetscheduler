using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Planning.Exposures {

    /// <summary>
    /// Select the next exposure based on the override exposure order for the target.
    /// </summary>
    public class OverrideOrderExposureSelector : BaseExposureSelector, IExposureSelector {

        public OverrideOrderExposureSelector(IProject project, ITarget target, Target databaseTarget) : base(target) {
            FilterCadence = new FilterCadenceFactory().Generate(project, target, databaseTarget);
        }

        public IExposure Select(DateTime atTime, IProject project, ITarget target) {
            if (AllExposurePlansRejected(target)) {
                TSLogger.Warning($"unexpected: all exposure plans were rejected at exposure selection time for target '{target.Name}' at time {atTime}");
                return null;
            }

            try {
                bool preDither = false;
                foreach (IFilterCadenceItem item in FilterCadence) {
                    if (item.Action == FilterCadenceAction.Dither) {
                        preDither = true;
                        continue;
                    }

                    IExposure exposure = target.AllExposurePlans[item.ReferenceIdx];
                    if (exposure.Rejected) { continue; }

                    exposure.PreDither = preDither;
                    FilterCadence.SetLastSelected(item);
                    return exposure;
                }
            } catch (Exception ex) {
                TSLogger.Warning($"exception in override exposure selector for target '{target.Name}' at time {atTime}: {ex.Message}, aborting target");
                return null;
            }

            TSLogger.Warning($"no acceptable exposure plan in override exposure selector for target '{target.Name}' at time {atTime}");
            return null;
        }

        public void ExposureTaken(IExposure exposure) {
            FilterCadence.Advance();
            UpdateFilterCadences(FilterCadence);
        }

        public void TargetReset() {
        }

        public bool ContainsExposurePlanIdx(int idx) {
            var item = FilterCadence.List.Where(item => item.ReferenceIdx == idx).FirstOrDefault();
            return item != null;
        }
    }
}