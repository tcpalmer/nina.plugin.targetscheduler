using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Utility;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    /// <summary>
    /// Extend TakeExposure so we can establish the association between an image id and the scheduler
    /// exposure plan that initiated the exposure.  We also have a reference to the InstructionWrapper
    /// that contains this so we can handle parent relationships.  Works in conjunction with the provided
    /// IImageSaveWatcher.
    ///
    /// We also handle ROI < 1 by using parts of TakeSubframeExposure here rather than a separate instruction.
    ///
    /// If synchronization is enabled, we handle getting exposures to registered clients and then waiting for
    /// them to complete.
    ///
    /// This is far from ideal.  If the core TakeExposure instruction changes, we'd be doing something different
    /// until this code was updated.  Ideally, NINA would provide a way to track some metadata or id all the way
    /// through the image pipeline to the save operation.
    /// </summary>
    public class PlanTakeExposure : SchedulerTakeExposure {
        private bool synchronizationEnabled;
        private int syncExposureTimeout;
        private IImageSaveWatcher imageSaveWatcher;
        private IDeepSkyObjectContainer dsoContainer;
        private ITarget target;
        private IExposure exposure;
        private Task imageProcessingTask;

        public PlanTakeExposure(
            IDeepSkyObjectContainer dsoContainer,
            bool synchronizationEnabled,
            int syncExposureTimeout,
            IProfileService profileService,
            ICameraMediator cameraMediator,
            IImagingMediator imagingMediator,
            IImageSaveMediator imageSaveMediator,
            IImageHistoryVM imageHistoryVM,
            IImageSaveWatcher imageSaveWatcher,
            ITarget target,
            IExposure exposure) : base(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM) {
            this.dsoContainer = dsoContainer;
            this.synchronizationEnabled = synchronizationEnabled;
            this.syncExposureTimeout = syncExposureTimeout;
            this.imageSaveWatcher = imageSaveWatcher;
            this.target = target;
            this.exposure = exposure;
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            var capture = new CaptureSequence() {
                ExposureTime = ExposureTime,
                Binning = Binning,
                Gain = Gain,
                Offset = Offset,
                ImageType = ImageType,
                ProgressExposureCount = ExposureCount,
                TotalExposureCount = ExposureCount + 1,
            };

            ObservableRectangle rect = GetObservableRectangle();
            if (rect != null) {
                capture.EnableSubSample = true;
                capture.SubSambleRectangle = rect;
            }

            var inputTarget = RetrieveTarget(dsoContainer);
            string exposureId = "";
            target.ExposureSelector.ExposureTaken(exposure); // we assume the exposure won't be canceled ...

            if (synchronizationEnabled) {
                exposureId = Guid.NewGuid().ToString();
                progress?.Report(new ApplicationStatus() { Status = "Target Scheduler: waiting for sync clients to accept exposure" });
                await TrySendExposureToClients(exposureId, inputTarget, token);
                progress?.Report(new ApplicationStatus() { Status = "" });
            }

            var exposureData = await imagingMediator.CaptureImage(capture, token, progress);

            if (IsLightSequence()) {
                imageHistoryVM.Add(exposureData.MetaData.Image.Id, ImageType);
            }

            imageProcessingTask = ProcessImageData(inputTarget, exposureData, progress, token);
            await imageProcessingTask;

            // If any sync clients accepted this exposure, we have to wait for them to finish before continuing
            if (synchronizationEnabled) {
                progress?.Report(new ApplicationStatus() { Status = "Target Scheduler: waiting for sync clients to complete exposure" });
                await SyncServer.Instance.WaitForClientExposureCompletion(exposureId, token);
                progress?.Report(new ApplicationStatus() { Status = "" });
            }

            ExposureCount++;
        }

        // Cobbled from NINA TakeExposure - private method
        private async Task ProcessImageData(InputTarget inputTarget, IExposureData exposureData, IProgress<ApplicationStatus> progress, CancellationToken token) {
            try {
                var imageParams = new PrepareImageParameters(null, false);
                if (IsLightSequence()) {
                    imageParams = new PrepareImageParameters(true, true);
                }

                var imageData = await exposureData.ToImageData(progress, token);

                var prepareTask = imagingMediator.PrepareImage(imageData, imageParams, token);

                if (IsLightSequence()) {
                    imageHistoryVM.PopulateStatistics(imageData.MetaData.Image.Id, await imageData.Statistics);
                }

                if (inputTarget != null) {
                    imageData.MetaData.Target.Name = inputTarget.DeepSkyObject.NameAsAscii;
                    imageData.MetaData.Target.Coordinates = inputTarget.InputCoordinates.Coordinates;
                    imageData.MetaData.Target.PositionAngle = inputTarget.PositionAngle;
                }

                var root = ItemUtility.GetRootContainer(this.Parent);
                imageData.MetaData.Sequence.Title = root != null ? root.SequenceTitle : "";

                imageSaveWatcher.WaitForExposure(imageData.MetaData.Image.Id, new ExposureWaitData(target, exposure, imageData.MetaData.Image.Id, token));

                await imageSaveMediator.Enqueue(imageData, prepareTask, progress, token);
            } catch (Exception ex) {
                TSLogger.Error($"exception during exposure image processing: {ex.Message}\n{ex.StackTrace}");
                Logger.Error(ex);
            }
        }

        private async Task TrySendExposureToClients(string exposureId, InputTarget inputTarget, CancellationToken token) {
            await SyncServer.Instance.SyncExposure(exposureId, inputTarget, target.DatabaseId, exposure.DatabaseId, exposure.ExposureLength, syncExposureTimeout, token);
        }

        private InputTarget RetrieveTarget(ISequenceContainer parent) {
            if (parent != null) {
                var container = parent as IDeepSkyObjectContainer;
                if (container != null) {
                    return container.Target;
                } else {
                    return RetrieveTarget(parent.Parent);
                }
            } else {
                return null;
            }
        }
    }
}