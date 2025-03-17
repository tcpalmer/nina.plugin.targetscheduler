using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Camera;
using NINA.Sequencer.SequenceItem.FilterWheel;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public class SyncTakeExposureContainer : SequenceContainer {
        private IProfileService profileService;
        private ICameraMediator cameraMediator;
        private IImagingMediator imagingMediator;
        private IImageSaveMediator imageSaveMediator;
        private IImageHistoryVM imageHistoryVM;
        private IFilterWheelMediator filterWheelMediator;
        private IImageSaveWatcher syncImageSaveWatcher;
        private SyncedExposure syncedExposure;
        private Action<string> UpdateDisplayTextAction;

        private ExposurePlan exposurePlan;
        private ExposureTemplate exposureTemplate;
        private ITarget target;
        private IExposure exposure;

        public SyncTakeExposureContainer(IProfileService profileService,
                                         ICameraMediator cameraMediator,
                                         IImagingMediator imagingMediator,
                                         IImageSaveMediator imageSaveMediator,
                                         IImageHistoryVM imageHistoryVM,
                                         IFilterWheelMediator filterWheelMediator,
                                         IImageSaveWatcher syncImageSaveWatcher,
                                         SyncedExposure syncedExposure,
                                         ITarget target,
                                         IExposure exposure,
                                         Action<String> UpdateDisplayTextAction) : base(new SequentialStrategy()) {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
            this.filterWheelMediator = filterWheelMediator;
            this.syncImageSaveWatcher = syncImageSaveWatcher;
            this.syncedExposure = syncedExposure;
            this.target = target;
            this.exposure = exposure;
            this.UpdateDisplayTextAction = UpdateDisplayTextAction;

            Description = "";
            Category = PlanContainer.INSTRUCTION_CATEGORY;
            LoadExposureDetails();
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Add(GetSwitchFilter());
            Add(GetSetReadoutMode());

            int repeatCount = ExposureRepeatCount();
            if (repeatCount > 1) { TSLogger.Info($"SYNC client taking {repeatCount} exposures for each server exposure"); }
            for (int i = 0; i < repeatCount; i++) {
                Add(GetTakeExposure());
            }

            return base.Execute(progress, token);
        }

        private ISequenceItem GetTakeExposure() {
            return new SyncTakeExposure(target, exposure, exposureTemplate, profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM, syncImageSaveWatcher, syncedExposure, UpdateDisplayTextAction);
        }

        private void LoadExposureDetails() {
            using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                this.exposurePlan = context.GetExposurePlan(syncedExposure.ExposurePlanDatabaseId);
                this.exposureTemplate = GetExposureTemplate(context, exposurePlan);
            }
        }

        private ExposureTemplate GetExposureTemplate(ISchedulerDatabaseContext context, ExposurePlan exposurePlan) {
            // Get the template being used by the server instance
            ExposureTemplate serverExposureTemplate = context.GetExposureTemplate(exposurePlan.ExposureTemplateId);

            // If this (client) instance has a template by the same name, use that
            ExposureTemplate et = context.GetExposureTemplates(SyncClient.Instance.ProfileId)
                .Where(et => et.Name == serverExposureTemplate.Name)
                .FirstOrDefault();
            if (et != null) { return et; }

            // Otherwise use the same as the server
            return serverExposureTemplate;
        }

        private int ExposureRepeatCount() {
            double clientExposureLength = exposure.ExposureLength > 0 ? exposure.ExposureLength : exposureTemplate.defaultExposure;
            int count = (int)Math.Truncate(syncedExposure.ExposureLength / clientExposureLength);
            return count < 1 ? 1 : count;
        }

        private ISequenceItem GetSwitchFilter() {
            SwitchFilter switchFilter = new SwitchFilter(profileService, filterWheelMediator);
            switchFilter.Filter = Utils.LookupFilter(profileService.ActiveProfile, exposureTemplate.FilterName);
            switchFilter.Category = PlanContainer.INSTRUCTION_CATEGORY;
            return switchFilter;
        }

        private ISequenceItem GetSetReadoutMode() {
            SetReadoutMode setReadoutMode = new SetReadoutMode(cameraMediator);
            setReadoutMode.Mode = GetReadoutMode(exposureTemplate.ReadoutMode);
            setReadoutMode.Category = PlanContainer.INSTRUCTION_CATEGORY;
            return setReadoutMode;
        }

        private short GetReadoutMode(int? readoutMode) {
            return (short)((readoutMode == null || readoutMode < 0) ? 0 : readoutMode);
        }

        public override object Clone() {
            throw new NotImplementedException();
        }
    }
}