using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyFlatDevice;
using NINA.Equipment.Equipment.MyRotator;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Flats;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Camera;
using NINA.Sequencer.SequenceItem.FilterWheel;
using NINA.Sequencer.SequenceItem.FlatDevice;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Sequencer.SequenceItem.Rotator;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public abstract class TargetSchedulerFlatsBase : SequenceItem, IValidatable {
        protected IProfileService profileService;
        protected ICameraMediator cameraMediator;
        protected IImagingMediator imagingMediator;
        protected IImageSaveMediator imageSaveMediator;
        protected IImageHistoryVM imageHistoryVM;
        protected IFilterWheelMediator filterWheelMediator;
        protected IRotatorMediator rotatorMediator;
        protected IFlatDeviceMediator flatDeviceMediator;

        protected SchedulerDatabaseInteraction database;
        protected FlatsExpert flatsExpert;

        public TargetSchedulerFlatsBase(IProfileService profileService,
                                        ICameraMediator cameraMediator,
                                        IImagingMediator imagingMediator,
                                        IImageSaveMediator imageSaveMediator,
                                        IImageHistoryVM imageHistoryVM,
                                        IFilterWheelMediator filterWheelMediator,
                                        IRotatorMediator rotatorMediator,
                                        IFlatDeviceMediator flatDeviceMediator) {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
            this.filterWheelMediator = filterWheelMediator;
            this.rotatorMediator = rotatorMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            flatCount = profileService.ActiveProfile.FlatWizardSettings.FlatCount;
        }

        public override void Initialize() {
            database = new SchedulerDatabaseInteraction();
            flatsExpert = new FlatsExpert();
            Validate();
        }

        public override void AfterParentChanged() {
            Validate();
        }

        private bool alwaysRepeatFlatSet = false;

        [JsonProperty]
        public bool AlwaysRepeatFlatSet {
            get => alwaysRepeatFlatSet;
            set {
                alwaysRepeatFlatSet = value;
                RaisePropertyChanged(nameof(AlwaysRepeatFlatSet));
            }
        }

        private int flatCount;

        [JsonProperty]
        public int FlatCount {
            get => flatCount;
            set {
                flatCount = value;
                RaisePropertyChanged(nameof(FlatCount));
            }
        }

        private string displayText = "";

        public string DisplayText {
            get => displayText;
            set {
                displayText = value;
                RaisePropertyChanged(nameof(DisplayText));
            }
        }

        public string TotalProgressDisplay {
            get => TotalFlatSets == 0 && CompletedFlatSets == 0 ? "..." : $"{CompletedFlatSets}/{TotalFlatSets}";
        }

        private int totalFlatSets = 0;

        public int TotalFlatSets {
            get => totalFlatSets;
            set {
                totalFlatSets = value;
                RaisePropertyChanged(nameof(TotalFlatSets));
                RaisePropertyChanged(nameof(TotalProgressDisplay));
            }
        }

        private int completedFlatSets = 0;

        public int CompletedFlatSets {
            get => completedFlatSets;
            set {
                completedFlatSets = value;
                RaisePropertyChanged(nameof(CompletedFlatSets));
                RaisePropertyChanged(nameof(TotalProgressDisplay));
            }
        }

        public string SetProgressDisplay {
            get => CompletedIterations == 0 && Iterations == 0 ? "..." : $"{CompletedIterations}/{Iterations}";
        }

        private int iterations = 0;

        public int Iterations {
            get => iterations;
            set {
                iterations = value;
                RaisePropertyChanged(nameof(Iterations));
                RaisePropertyChanged(nameof(SetProgressDisplay));
            }
        }

        private int completedIterations = 0;

        public int CompletedIterations {
            get => completedIterations;
            set {
                completedIterations = value;
                RaisePropertyChanged(nameof(CompletedIterations));
                RaisePropertyChanged(nameof(SetProgressDisplay));
            }
        }

        private string targetName = null;
        public string TargetName { get => targetName; set => targetName = value; }

        protected void InitSync() {
            if (SyncManager.Instance.RunningClient) {
                flatsExpert.InitSync(true, SyncClient.Instance.ServerProfileId);
            }
        }

        protected async Task<bool> TakeFlatSet(LightSession neededFlat, bool applyRotation, IProgress<ApplicationStatus> progress, CancellationToken token) {
            FlatSpec flatSpec = neededFlat.FlatSpec;
            Target target = GetTarget(neededFlat.TargetId);
            if (target == null) {
                return false;
            }

            try {
                TrainedFlatExposureSetting setting = new FlatsExpert().GetTrainedFlatExposureSetting(profileService.ActiveProfile, flatSpec);
                if (setting == null) {
                    TSLogger.Warning($"TS Flats: failed to find trained settings for {flatSpec}");
                    Notification.ShowWarning($"TS Flats: failed to find trained settings for {flatSpec}");
                    return false;
                }

                int flatCount = FlatCount;
                DisplayText = $"{flatSpec.FilterName} {setting.Time.ToString("0.##")}s ({GetFlatSpecDisplay(flatSpec)})";
                Iterations = flatCount;
                CompletedIterations = 0;

                // Set rotation angle, if applicable
                if (applyRotation && flatSpec.Rotation != ImageMetadata.NO_ROTATOR_ANGLE && rotatorMediator.GetInfo().Connected) {
                    TSLogger.Info($"TS Flats: setting rotation angle: {flatSpec.Rotation}");
                    MoveRotatorMechanical rotate = new MoveRotatorMechanical(rotatorMediator) { MechanicalPosition = (float)flatSpec.Rotation };
                    await rotate.Execute(progress, token);
                }

                // Set the camera readout mode
                TSLogger.Info($"TS Flats: setting readout mode: {flatSpec.ReadoutMode}");
                SetReadoutMode setReadoutMode = new SetReadoutMode(cameraMediator) { Mode = (short)flatSpec.ReadoutMode };
                await setReadoutMode.Execute(progress, token);

                // Switch filters
                TSLogger.Info($"TS Flats: switching filter: {flatSpec.FilterName}");
                SwitchFilter switchFilter = new SwitchFilter(profileService, filterWheelMediator) { Filter = Utils.LookupFilter(profileService.ActiveProfile, flatSpec.FilterName) };
                await switchFilter.Execute(progress, token);

                // Set the panel brightness
                TSLogger.Info($"TS Flats: setting panel brightness: {setting.Brightness}");
                try {
                    SetBrightness setBrightness = new SetBrightness(flatDeviceMediator) { Brightness = setting.Brightness };
                    await setBrightness.Execute(progress, token);
                } catch (Exception e) {
                    TSLogger.Warning($"TS Flats: error setting panel brightness: {e.Message}");
                    TSLogger.Warning("TS Flats: continuing with flat exposure but brightness might not be set correctly");
                }

                // Take the exposures
                TakeSubframeExposure takeExposure = new TakeSubframeExposure(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM) {
                    ImageType = CaptureSequence.ImageTypes.FLAT,
                    Gain = setting.Gain != -1 ? setting.Gain : flatSpec.Gain,
                    Offset = setting.Offset != -1 ? setting.Offset : flatSpec.Offset,
                    Binning = flatSpec.BinningMode,
                    ExposureTime = setting.Time,
                    ROI = flatSpec.ROI,
                };

                TSLogger.Info($"TS Flats: {target?.Name} sid: {neededFlat.SessionId}, taking {flatCount} flats: exp:{setting.Time}, brightness: {setting.Brightness}, for {flatSpec}");

                FlatTargetContainer container = new FlatTargetContainer(this, target, flatCount, neededFlat.SessionId);
                container.Add(takeExposure);
                await container.Execute(progress, token);

                return true;
            } catch (Exception ex) {
                TSLogger.Error($"Exception taking automated flat: {ex.Message}\n{ex}");
                return false;
            }
        }

        protected Target GetTarget(int targetId) {
            using (var context = database.GetContext()) {
                Target target = context.GetTargetOnly(targetId);
                if (target == null) {
                    TSLogger.Warning($"TS Flats: failed to load target for id={targetId}");
                    return null;
                }

                Project project = context.GetProject(target.ProjectId);
                target.Project = project;

                return target;
            }
        }

        protected Task BeforeImageSaved(object sender, BeforeImageSavedEventArgs args) {
            if (args.Image.MetaData.Image.ImageType != CaptureSequence.ImageTypes.FLAT) {
                return Task.CompletedTask;
            }

            string overloadedName = args.Image.MetaData.Target.Name;
            (string targetName, string sessionId, string projectName) = FlatsExpert.DeOverloadTargetName(overloadedName);

            TSLogger.Debug($"TS Flats: BeforeImageSaved: {projectName}/{targetName} sid={sessionId} filter={args.Image?.MetaData?.FilterWheel?.Filter}");
            args.Image.MetaData.Target.Name = targetName;
            args.Image.MetaData.Sequence.Title = overloadedName;

            return Task.CompletedTask;
        }

        protected Task BeforeFinalizeImageSaved(object sender, BeforeFinalizeImageSavedEventArgs args) {
            if (args.Image.RawImageData.MetaData.Image.ImageType != CaptureSequence.ImageTypes.FLAT) {
                return Task.CompletedTask;
            }

            (string targetName, string sessionId, string projectName) = FlatsExpert.DeOverloadTargetName(args.Image?.RawImageData?.MetaData?.Sequence?.Title);
            string sessionIdentifier = FlatsExpert.FormatSessionIdentifier(int.Parse(sessionId));
            ImagePattern proto = TargetScheduler.FlatSessionIdImagePattern;
            args.AddImagePattern(new ImagePattern(proto.Key, proto.Description) { Value = sessionIdentifier });

            proto = TargetScheduler.ProjectNameImagePattern;
            args.AddImagePattern(new ImagePattern(proto.Key, proto.Description) { Value = projectName });

            TSLogger.Debug($"TS Flats: BeforeFinalizeImageSaved: for {projectName}/{targetName}: sid={sessionIdentifier}");

            return Task.CompletedTask;
        }

        protected void ImageSaved(object sender, ImageSavedEventArgs args) {
            if (args.MetaData.Image.ImageType != CaptureSequence.ImageTypes.FLAT) {
                return;
            }

            TSLogger.Debug($"TS Flats: ImageSaved: {args.MetaData?.Target?.Name} filter={args.Filter} file={args.PathToImage?.LocalPath}");
        }

        protected void SaveFlatHistory(LightSession neededFlat) {
            if (database == null) {
                database = new SchedulerDatabaseInteraction();
            }

            TSLogger.Info($"TS Flats: saving flat history: {neededFlat}");
            using (var context = database.GetContext()) {
                context.FlatHistorySet.Add(GetFlatHistoryRecord(neededFlat));
                context.SaveChanges();
            }
        }

        protected string GetFlatSpecDisplay(FlatSpec flatSpec) {
            string rot = flatSpec.Rotation != ImageMetadata.NO_ROTATOR_ANGLE ? flatSpec.Rotation.ToString() : "n/a";
            return $"Filter: {flatSpec.FilterName} Gain: {flatSpec.Gain} Offset: {flatSpec.Offset} Binning: {flatSpec.BinningMode} Rotation: {rot} ROI: {flatSpec.ROI}";
        }

        protected async Task CloseCover(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!flatDeviceMediator.GetInfo().Connected) { return; }
            if (!flatDeviceMediator.GetInfo().SupportsOpenClose) { return; }

            CoverState coverState = flatDeviceMediator.GetInfo().CoverState;

            // Last chance to skip if flat device doesn't support open/close
            if (coverState == CoverState.Unknown || coverState == CoverState.NeitherOpenNorClosed) {
                TSLogger.Warning($"TS Flats: flip-flat cover is not in a known state ({coverState}), skipping close)");
                return;
            }

            if (coverState == CoverState.Closed) { return; }

            TSLogger.Info("TS Flats: closing flat device");
            await flatDeviceMediator.CloseCover(progress, token);

            coverState = flatDeviceMediator.GetInfo().CoverState;
            if (coverState != CoverState.Closed) {
                TSLogger.Error("TS Flats: failed to close flat cover");
                throw new SequenceEntityFailedException("TS Flats: failed to close flat cover");
            }
        }

        protected async Task OpenCover(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!flatDeviceMediator.GetInfo().Connected) { return; }

            if (!flatDeviceMediator.GetInfo().SupportsOpenClose) {
                TSLogger.Info("TS Flats: flat panel doesn't support open/close");
                return;
            }

            CoverState coverState = flatDeviceMediator.GetInfo().CoverState;

            // Last chance to skip if flat device doesn't support open/close
            if (coverState == CoverState.Unknown || coverState == CoverState.NeitherOpenNorClosed) {
                TSLogger.Warning($"TS Flats: flip-flat cover is not in a known state ({coverState}), skipping open)");
                return;
            }

            if (coverState == CoverState.Open) {
                TSLogger.Warning("TS Flats: flat panel is unexpectedly already open");
                return;
            }

            TSLogger.Info("TS Flats: opening flat device");
            await flatDeviceMediator.OpenCover(progress, token);
            TSLogger.Info("TS Flats: flat device opened");

            coverState = flatDeviceMediator.GetInfo().CoverState;
            if (coverState != CoverState.Open) {
                TSLogger.Error("TS Flats: failed to open flat cover");
                throw new SequenceEntityFailedException($"TS Flats: failed to open flat cover");
            }
        }

        protected async Task ToggleLight(bool onOff, IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!flatDeviceMediator.GetInfo().Connected) { return; }
            if (flatDeviceMediator.GetInfo().LightOn == onOff) { return; }

            TSLogger.Info($"TS Flats: toggling flat device light: {onOff}");
            await flatDeviceMediator.ToggleLight(onOff, progress, token);

            if (flatDeviceMediator.GetInfo().LightOn != onOff) {
                TSLogger.Error("TS Flats: failed to toggle flat panel light");
                throw new SequenceEntityFailedException($"TS Flats: failed to toggle flat panel light");
            }
        }

        protected double GetCurrentRotation() {
            RotatorInfo info = rotatorMediator.GetInfo();
            return info.Connected ? info.MechanicalPosition : ImageMetadata.NO_ROTATOR_ANGLE;
        }

        public bool Validate() {
            var i = new List<string>();

            CameraInfo cameraInfo = this.cameraMediator.GetInfo();
            if (!cameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            }

            FlatDeviceInfo flatDeviceInfo = flatDeviceMediator.GetInfo();
            if (!flatDeviceInfo.Connected) {
                i.Add(Loc.Instance["LblFlatDeviceNotConnected"]);
            } else {
                if (!flatDeviceInfo.SupportsOnOff) {
                    i.Add(Loc.Instance["LblFlatDeviceCannotControlBrightness"]);
                }
            }

            Issues = i;
            return i.Count == 0;
        }

        protected void LogTrainedFlatDetails() {
            Collection<TrainedFlatExposureSetting> settings = profileService.ActiveProfile.FlatDeviceSettings?.TrainedFlatExposureSettings;

            /* Write training flats for testing.
            BinningMode binning = new BinningMode(1, 1);

            settings.Add(new TrainedFlatExposureSetting() { Filter = 0, Gain = -1, Offset = -1, Binning = binning, Time = 0.78125, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 1, Gain = -1, Offset = -1, Binning = binning, Time = 4.0625, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 2, Gain = -1, Offset = -1, Binning = binning, Time = 2.875, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 3, Gain = -1, Offset = -1, Binning = binning, Time = 2.28125, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 4, Gain = -1, Offset = -1, Binning = binning, Time = 8.8125, Brightness = 30 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 5, Gain = -1, Offset = -1, Binning = binning, Time = 9.125, Brightness = 40 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 6, Gain = -1, Offset = -1, Binning = binning, Time = 6.25, Brightness = 30 });

            settings.Add(new TrainedFlatExposureSetting() { Filter = 0, Gain = 139, Offset = 21, Binning = binning, Time = 0.78125, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 1, Gain = 139, Offset = 21, Binning = binning, Time = 4.0625, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 2, Gain = 139, Offset = 21, Binning = binning, Time = 2.875, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 3, Gain = 139, Offset = 21, Binning = binning, Time = 2.28125, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 4, Gain = 139, Offset = 21, Binning = binning, Time = 8.8125, Brightness = 30 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 5, Gain = 139, Offset = 21, Binning = binning, Time = 9.125, Brightness = 40 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 6, Gain = 139, Offset = 21, Binning = binning, Time = 6.25, Brightness = 30 });
            */

            if (settings == null || settings.Count == 0) {
                TSLogger.Debug("TS Flats: no trained flat exposure details found");
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (TrainedFlatExposureSetting trainedFlat in settings) {
                if (trainedFlat.Filter < 0 || trainedFlat.Filter >= profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Count) {
                    TSLogger.Warning($"out of range trained flat index, skipping: {trainedFlat.Filter}");
                    continue;
                }

                FilterInfo filterInfo = profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters[trainedFlat.Filter];
                if (filterInfo != null) {
                    sb.AppendLine($"    filter[{trainedFlat.Filter}]: {filterInfo.Name} gain: {trainedFlat.Gain} offset: {trainedFlat.Offset} binning: {trainedFlat.Binning} exposure: {trainedFlat.Time} brightness: {trainedFlat.Brightness}");
                }
            }

            TSLogger.Debug($"TS Flats: trained flat exposure details:\n{sb}");
        }

        protected FlatHistory GetFlatHistoryRecord(LightSession neededFlat) {
            return new FlatHistory(neededFlat.TargetId,
                neededFlat.SessionDate,
                DateTime.Now,
                neededFlat.SessionId,
                profileService.ActiveProfile.Id.ToString(),
                FlatHistory.FLAT_TYPE_PANEL,
                neededFlat.FlatSpec.FilterName,
                neededFlat.FlatSpec.Gain,
                neededFlat.FlatSpec.Offset,
                neededFlat.FlatSpec.BinningMode,
                neededFlat.FlatSpec.ReadoutMode,
                neededFlat.FlatSpec.Rotation,
                neededFlat.FlatSpec.ROI);
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// FlatTargetContainer provides the means to get target name and session ID saved such that
    /// they can be picked up as the images come through the pipeline.
    ///
    /// A bit squirrelly with the overload but it's necessary to get the details into image metadata
    /// so that images get the right target name, session ID, and project name.
    /// </summary>
    public class FlatTargetContainer : SequentialContainer, IDeepSkyObjectContainer {
        private TargetSchedulerFlatsBase parent;
        private InputTarget inputTarget;
        private LoopCondition loopCondition;

        public FlatTargetContainer(TargetSchedulerFlatsBase parent, Target target, int count, int sessionId) {
            this.parent = parent;

            string overloadedName = FlatsExpert.GetOverloadTargetName(target?.Name, sessionId, target?.Project?.Name);
            inputTarget = new InputTarget(Angle.Zero, Angle.Zero, null) { TargetName = overloadedName };

            loopCondition = new LoopCondition() { Iterations = count };
            loopCondition.PropertyChanged += LoopCondition_PropertyChanged;
            Conditions.Add(loopCondition);
        }

        private void LoopCondition_PropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == "CompletedIterations") {
                parent.CompletedIterations = loopCondition.CompletedIterations;
            }
        }

        public InputTarget Target { get => inputTarget; set => throw new NotImplementedException(); }

        public NighttimeData NighttimeData => throw new NotImplementedException();

        public override object Clone() {
            throw new NotImplementedException();
        }
    }
}