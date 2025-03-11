using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using System;

namespace NINA.Plugin.TargetScheduler.Planning.Exposures {

    /// <summary>
    /// Determine the appropriate exposure selector implementation based on project/target settings.
    /// </summary>
    public class ExposureSelectionExpert {

        public ExposureSelectionExpert() {
        }

        public IExposureSelector GetExposureSelector(IProject project, ITarget target, Target databaseTarget) {
            if (project.SmartExposureOrder) {
                return new SmartExposureSelector(project, target, databaseTarget);
            }

            if (databaseTarget.OverrideExposureOrders.Count > 0) {
                return new OverrideOrderExposureSelector(project, target, databaseTarget);
            }

            if (project.FilterSwitchFrequency == 0) {
                return new RepeatUntilDoneExposureSelector(project, target, databaseTarget);
            }

            return new BasicExposureSelector(project, target, databaseTarget);
        }
    }

    public interface IExposureSelector {

        IExposure Select(DateTime atTime, IProject project, ITarget target);

        void ExposureTaken(IExposure exposure);

        void TargetReset();
    }
}