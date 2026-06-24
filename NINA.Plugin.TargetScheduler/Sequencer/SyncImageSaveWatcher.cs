using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;

namespace NINA.Plugin.TargetScheduler.Sequencer {
    public class SyncImageSaveWatcher : ImageSaveWatcher {
        public SyncImageSaveWatcher(IProfile profile, IImageSaveMediator imageSaveMediator) : base(profile, imageSaveMediator) {
        }

        public override bool UpdateExposurePlanCounts => profilePreference.EnableClientUpdatesExposurePlan;

        public override void ImageSaved(object sender, ImageSavedEventArgs imageSavedEventArgs) {
            if (imageSavedEventArgs.MetaData.Image.ImageType != "LIGHT") {
                return;
            }

            TSLogger.Debug("SYNC client: SyncImageSaveWatcher.ImageSaved starting");
            ExposureWaitData waitData = GetWaitData(imageSavedEventArgs);
            base.ImageSaved(sender, imageSavedEventArgs);
            SyncClient.Instance.SubmitCompletedExposure(waitData.ExposureGuid).Wait();
        }
    }
}