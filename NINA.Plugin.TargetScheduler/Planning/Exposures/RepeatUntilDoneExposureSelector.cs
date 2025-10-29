using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Planning.Exposures {

    /// <summary>
    /// Select the next exposure so that exposures that are not rejected are repeated until complete.  This
    /// is the implementation for project filter switch frequency = 0.
    /// </summary>
    public class RepeatUntilDoneExposureSelector : BaseExposureSelector, IExposureSelector {

        public RepeatUntilDoneExposureSelector(IProject project, ITarget target, Target databaseTarget) : base(target) {
            DitherManager = GetDitherManager(project, target);
        }

        public IExposure Select(DateTime atTime, IProject project, ITarget target) {
            if (AllExposurePlansRejected(target)) {
                TSLogger.Warning($"unexpected: all exposure plans were rejected at exposure selection time for target '{target.Name}' at time {atTime}");
                return null;
            }

            IExposure exposure = target.ExposurePlans.FirstOrDefault(e => !e.Rejected);
            if (exposure != null) {
                exposure.PreDither = DitherManager.DitherRequired(exposure);
                return exposure;
            }

            TSLogger.Warning($"no acceptable exposure plan in repeat until done exposure selector for target '{target.Name}' at time {atTime}");
            return null;
        }

        public void ExposureTaken(IExposure exposure) {
            if (exposure.PreDither) DitherManager.Reset();
            DitherManager.AddExposure(exposure);
        }

        public void TargetReset() {
            DitherManager.Reset();
        }
    }
}