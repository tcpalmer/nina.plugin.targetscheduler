using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public abstract class SchedulerTakeExposure : TakeExposure {
        protected IProfileService profileService { get; private set; }
        protected ICameraMediator cameraMediator { get; private set; }
        protected IImagingMediator imagingMediator { get; private set; }
        protected IImageSaveMediator imageSaveMediator { get; private set; }
        protected IImageHistoryVM imageHistoryVM { get; private set; }

        private double roi;

        public double ROI {
            get => roi;
            set {
                roi = Utils.ConvertROI(value);
                RaisePropertyChanged();
            }
        }

        public SchedulerTakeExposure(
            IProfileService profileService,
            ICameraMediator cameraMediator,
            IImagingMediator imagingMediator,
            IImageSaveMediator imageSaveMediator,
            IImageHistoryVM imageHistoryVM) : base(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM) {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
        }

        // From NINA TakeSubframeExposure
        protected ObservableRectangle GetObservableRectangle() {
            var info = cameraMediator.GetInfo();
            ObservableRectangle rect = null;

            if (ROI < 1 && info.CanSubSample) {
                TSLogger.Info($"applying ROI for subframe exposure: {ROI}");
                var centerX = info.XSize / 2d;
                var centerY = info.YSize / 2d;
                var subWidth = info.XSize * ROI;
                var subHeight = info.YSize * ROI;
                var startX = centerX - subWidth / 2d;
                var startY = centerY - subHeight / 2d;
                rect = new ObservableRectangle(startX, startY, subWidth, subHeight);
            }

            if (ROI < 1 && !info.CanSubSample) {
                TSLogger.Warning($"ROI {ROI} was specified, but the camera is not able to take sub frames");
                Logger.Warning($"ROI {ROI} was specified, but the camera is not able to take sub frames");
            }

            return rect;
        }

        protected bool IsLightSequence() {
            return ImageType == CaptureSequence.ImageTypes.SNAPSHOT || ImageType == CaptureSequence.ImageTypes.LIGHT;
        }
    }
}