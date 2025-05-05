using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Plugin.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Sequencer;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NINA.Plugin.TargetScheduler.Dockables {

    [Export(typeof(IDockableVM))]
    public partial class ImagingDockableVM : DockableVM {
        private const string STATUS_INACTIVE = "inactive";
        private const string STATUS_IMAGING = "imaging";
        private const string STATUS_WAITING = "waiting";
        private const string STATUS_PAUSED = "paused";

        private TargetSchedulerContainer tsInstruction = null;

        [ImportingConstructor]
        public ImagingDockableVM(IProfileService profileService, IMessageBroker messageBroker) : base(profileService) {
            Title = "Target Scheduler";

            var resourceDict = new ResourceDictionary();
            resourceDict.Source = new Uri("NINA.Plugin.TargetScheduler;component/Dockables/ImagingTabIcon.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (GeometryGroup)resourceDict["ImagingTabIcon"]; ImageGeometry.Freeze();
            pauseSVG = (GeometryGroup)resourceDict["SS_PauseSVG"]; pauseSVG.Freeze();
            playSVG = (GeometryGroup)resourceDict["SS_PlaySVG"]; playSVG.Freeze();

            PauseCommand = new RelayCommand(RequestPause);
            UnpauseCommand = new RelayCommand(RequestUnpause);

            TargetScheduler.EventMediator.ContainerStarting += ContainerStarting;
            TargetScheduler.EventMediator.ContainerPaused += ContainerPaused;
            TargetScheduler.EventMediator.ContainerUnpaused += ContainerUnpaused;
            TargetScheduler.EventMediator.ContainerStopping += ContainerStopping;
            TargetScheduler.EventMediator.WaitStarting += WaitStarting;
            TargetScheduler.EventMediator.ExposureStarting += ExposureStarting;
            TargetScheduler.EventMediator.ExposureStopping += ExposureStopping;
        }

        public ICommand PauseCommand { get; private set; }
        public ICommand UnpauseCommand { get; private set; }

        private void RequestPause() {
            if (tsInstruction != null) {
                tsInstruction.RequestPause();
                ContainerPauseRequested = true;
            }
        }

        private void RequestUnpause() {
            if (tsInstruction != null) {
                tsInstruction.RequestUnpause();
                ContainerPauseRequested = false;
            }
        }

        private void ContainerStarting(object sender, EventArgs e) {
            if (sender is TargetSchedulerContainer) {
                tsInstruction = sender as TargetSchedulerContainer;
            }
        }

        private void ContainerPaused(object sender, EventArgs e) {
            ContainerIsPaused = true;
            UpdateDisplay(STATUS_PAUSED, e);
        }

        private void ContainerUnpaused(object sender, EventArgs e) {
            ContainerIsPaused = false;
        }

        private void ContainerStopping(object sender, EventArgs e) {
            UpdateDisplay(STATUS_INACTIVE, null);
            tsInstruction = null;
        }

        private void WaitStarting(object sender, WaitStartingEventArgs e) {
            UpdateDisplay(STATUS_WAITING, e);
        }

        private void ExposureStarting(object sender, ExposureStartingEventArgs e) {
            UpdateDisplay(STATUS_IMAGING, e);
        }

        private void ExposureStopping(object sender, EventArgs e) {
            UpdateDisplay(STATUS_INACTIVE, null);
        }

        private GeometryGroup pauseSVG;
        private GeometryGroup playSVG;

        private bool containerPausedRequested = false;
        private bool containerIsPaused = false;

        private string status = STATUS_INACTIVE;
        private bool isInactive = true;
        private bool isImaging = false;
        private bool isPaused = false;
        private bool isWaiting = false;

        private string waitUntil;
        private string targetName;
        private string coordinates;
        private string stopAt;
        private string rotation;
        private string filterName;
        private string exposureLength;

        public GeometryGroup PauseSVG => pauseSVG;
        public GeometryGroup PlaySVG => playSVG;

        public string Status {
            get => status;
            set {
                status = value;
                RaisePropertyChanged(nameof(Status));
            }
        }

        public bool IsInactive {
            get => isInactive;
            set {
                isInactive = value;
                RaisePropertyChanged(nameof(IsInactive));
            }
        }

        public bool IsImaging {
            get => isImaging;
            set {
                isImaging = value;
                RaisePropertyChanged(nameof(IsImaging));
            }
        }

        public bool IsPaused {
            get => isPaused;
            set {
                isPaused = value;
                RaisePropertyChanged(nameof(IsPaused));
            }
        }

        public bool IsWaiting {
            get => isWaiting;
            set {
                isWaiting = value;
                RaisePropertyChanged(nameof(IsWaiting));
            }
        }

        public string WaitUntil {
            get => waitUntil;
            set {
                waitUntil = value;
                RaisePropertyChanged(nameof(WaitUntil));
            }
        }

        public string TargetName {
            get => targetName;
            set {
                targetName = value;
                RaisePropertyChanged(nameof(TargetName));
            }
        }

        public string Coordinates {
            get => coordinates;
            set {
                coordinates = value;
                RaisePropertyChanged(nameof(Coordinates));
            }
        }

        public string StopAt {
            get => stopAt;
            set {
                stopAt = value;
                RaisePropertyChanged(nameof(StopAt));
            }
        }

        public string Rotation {
            get => rotation;
            set {
                rotation = value;
                RaisePropertyChanged(nameof(Rotation));
            }
        }

        public string FilterName {
            get => filterName;
            set {
                filterName = value;
                RaisePropertyChanged(nameof(FilterName));
            }
        }

        public string ExposureLength {
            get => exposureLength;
            set {
                exposureLength = value;
                RaisePropertyChanged(nameof(ExposureLength));
            }
        }

        public bool ContainerPauseRequested {
            get => containerPausedRequested;
            set {
                containerPausedRequested = value;
                RaisePropertyChanged(nameof(ContainerPauseRequested));
            }
        }

        public bool ContainerIsPaused {
            get => containerIsPaused;
            set {
                containerIsPaused = value;
                RaisePropertyChanged(nameof(ContainerIsPaused));
            }
        }

        private void UpdateDisplay(string status, EventArgs eventArgs) {
            TSLogger.Trace($"ImagingDockableVM new state: {status}");

            IsInactive = false;
            IsImaging = false;
            IsPaused = false;
            IsWaiting = false;
            Status = status;

            switch (status) {
                case STATUS_WAITING:
                    IsWaiting = true;
                    ContainerIsPaused = false;
                    ContainerPauseRequested = false;

                    WaitUntil = Utils.FormatDateTime(((WaitStartingEventArgs)eventArgs).WaitUntil);
                    TargetName = GetTargetName(((WaitStartingEventArgs)eventArgs).Target);

                    break;

                case STATUS_IMAGING:
                    IsImaging = true;
                    ContainerPauseRequested = false;

                    ITarget target = ((ExposureStartingEventArgs)eventArgs).Target;
                    IExposure exposure = ((ExposureStartingEventArgs)eventArgs).Exposure;
                    TargetName = GetTargetName(target);
                    Coordinates = GetCoordinates(target);
                    StopAt = $"{target?.EndTime:HH:mm:ss}";
                    Rotation = $"{target?.Rotation}°";
                    FilterName = exposure?.FilterName;
                    ExposureLength = $"{Utils.FormatDbl(exposure.ExposureLength)}s";
                    break;

                case STATUS_PAUSED:
                    IsPaused = true;
                    break;

                case STATUS_INACTIVE:
                    IsInactive = true;
                    ContainerIsPaused = false;
                    ContainerPauseRequested = false;
                    break;

                default:
                    IsInactive = true;
                    ContainerIsPaused = false;
                    ContainerPauseRequested = false;
                    break;
            }
        }

        private string GetTargetName(ITarget target) {
            string projectName = target?.Project?.Name;
            string targetName = target?.Name;

            if (projectName == null || targetName == null) { return "n/a"; }
            return $"{projectName} / {targetName}";
        }

        private string GetCoordinates(ITarget target) {
            Coordinates coords = target?.Coordinates;
            return coords != null ? $"{Utils.GetRAString(coords.RADegrees)} {coords.DecString}" : "n/a";
        }

        public void Dispose() {
            try {
                TargetScheduler.EventMediator.ContainerStarting -= ContainerStarting;
                TargetScheduler.EventMediator.ContainerPaused -= ContainerPaused;
                TargetScheduler.EventMediator.ContainerUnpaused -= ContainerUnpaused;
                TargetScheduler.EventMediator.ContainerStopping -= ContainerStopping;
                TargetScheduler.EventMediator.WaitStarting -= WaitStarting;
                TargetScheduler.EventMediator.ExposureStarting -= ExposureStarting;
                TargetScheduler.EventMediator.ExposureStopping -= ExposureStopping;
            } catch { }
        }
    }
}