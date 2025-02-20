using NINA.Core.Enum;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace NINA.Plugin.TargetScheduler.Database.Schema {

    public class ProfilePreference : INotifyPropertyChanged {
        [Key] public int Id { get; set; }
        [Required] public string ProfileId { get; set; }

        public int logLevel { get; set; }
        public int parkOnWait { get; set; }
        public double exposureThrottle { get; set; }
        public int enableSmartPlanWindow { get; set; }
        public int enableDeleteAcquiredImagesWithTarget { get; set; }
        public int enableSlewCenter { get; set; }

        public int enableSynchronization { get; set; }
        public int syncWaitTimeout { get; set; }
        public int syncActionTimeout { get; set; }
        public int syncSolveRotateTimeout { get; set; }
        public int syncEventContainerTimeout { get; set; }

        public int enableGradeRMS { get; set; }
        public int enableGradeStars { get; set; }
        public int enableGradeHFR { get; set; }
        public int enableGradeFWHM { get; set; }
        public int enableGradeEccentricity { get; set; }
        public int enableMoveRejected { get; set; }
        public double delayGrading { get; set; }
        public int acceptimprovement { get; set; }

        public int maxGradingSampleSize { get; set; }
        public double rmsPixelThreshold { get; set; }
        public double detectedStarsSigmaFactor { get; set; }
        public double hfrSigmaFactor { get; set; }
        public double fwhmSigmaFactor { get; set; }
        public double eccentricitySigmaFactor { get; set; }
        public double autoAcceptLevelHFR { get; set; }
        public double autoAcceptLevelFWHM { get; set; }
        public double autoAcceptLevelEccentricity { get; set; }

        public int enableSimulatedRun { get; set; }
        public int skipSimulatedWaits { get; set; }
        public int skipSimulatedUpdates { get; set; }

        public ProfilePreference() {
        }

        public ProfilePreference(string profileId) {
            LogLevel = LogLevelEnum.DEBUG;
            ProfileId = profileId;
            ParkOnWait = false;
            ExposureThrottle = 125;
            EnableSmartPlanWindow = true;
            EnableDeleteAcquiredImagesWithTarget = true;
            EnableSlewCenter = true;

            EnableGradeRMS = true;
            EnableGradeStars = true;
            EnableGradeHFR = true;
            EnableGradeFWHM = false;
            EnableGradeEccentricity = false;
            EnableMoveRejected = false;
            DelayGrading = 80;
            AcceptImprovement = true;
            MaxGradingSampleSize = 10;
            RMSPixelThreshold = 8;
            DetectedStarsSigmaFactor = 4;
            HFRSigmaFactor = 4;
            FWHMSigmaFactor = 4;
            EccentricitySigmaFactor = 4;
            autoAcceptLevelHFR = 0;
            autoAcceptLevelFWHM = 0;
            autoAcceptLevelEccentricity = 0;

            EnableSynchronization = false;
            SyncWaitTimeout = SyncManager.DEFAULT_SYNC_WAIT_TIMEOUT;
            SyncActionTimeout = SyncManager.DEFAULT_SYNC_ACTION_TIMEOUT;
            SyncSolveRotateTimeout = SyncManager.DEFAULT_SYNC_SOLVEROTATE_TIMEOUT;
            SyncEventContainerTimeout = SyncManager.DEFAULT_SYNC_ACTION_TIMEOUT;

            EnableSimulatedRun = false;
            SkipSimulatedWaits = true;
            SkipSimulatedUpdates = false;
        }

        [NotMapped]
        public LogLevelEnum LogLevel {
            get { return (LogLevelEnum)logLevel; }
            set {
                logLevel = (int)value;
                RaisePropertyChanged(nameof(LogLevel));
            }
        }

        [NotMapped]
        public bool ParkOnWait {
            get { return parkOnWait == 1; }
            set {
                parkOnWait = value ? 1 : 0;
                RaisePropertyChanged(nameof(ParkOnWait));
            }
        }

        [NotMapped]
        public double ExposureThrottle {
            get { return exposureThrottle; }
            set {
                exposureThrottle = value;
                RaisePropertyChanged(nameof(ExposureThrottle));
            }
        }

        [NotMapped]
        public bool EnableSmartPlanWindow {
            get { return enableSmartPlanWindow == 1; }
            set {
                enableSmartPlanWindow = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableSmartPlanWindow));
            }
        }

        [NotMapped]
        public bool EnableDeleteAcquiredImagesWithTarget {
            get { return enableDeleteAcquiredImagesWithTarget == 1; }
            set {
                enableDeleteAcquiredImagesWithTarget = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableDeleteAcquiredImagesWithTarget));
            }
        }

        [NotMapped]
        public bool EnableSlewCenter {
            get { return enableSlewCenter == 1; }
            set {
                enableSlewCenter = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableSlewCenter));
            }
        }

        [NotMapped]
        public bool EnableSynchronization {
            get { return enableSynchronization == 1; }
            set {
                enableSynchronization = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableSynchronization));
            }
        }

        [NotMapped]
        public int SyncWaitTimeout {
            get { return syncWaitTimeout; }
            set {
                syncWaitTimeout = value;
                RaisePropertyChanged(nameof(SyncWaitTimeout));
            }
        }

        [NotMapped]
        public int SyncActionTimeout {
            get { return syncActionTimeout; }
            set {
                syncActionTimeout = value;
                RaisePropertyChanged(nameof(SyncActionTimeout));
            }
        }

        [NotMapped]
        public int SyncSolveRotateTimeout {
            get { return syncSolveRotateTimeout; }
            set {
                syncSolveRotateTimeout = value;
                RaisePropertyChanged(nameof(SyncSolveRotateTimeout));
            }
        }

        [NotMapped]
        public int SyncEventContainerTimeout {
            get { return syncEventContainerTimeout; }
            set {
                syncEventContainerTimeout = value;
                RaisePropertyChanged(nameof(SyncEventContainerTimeout));
            }
        }

        [NotMapped]
        public bool EnableGradeRMS {
            get { return enableGradeRMS == 1; }
            set {
                enableGradeRMS = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableGradeRMS));
            }
        }

        [NotMapped]
        public bool EnableGradeStars {
            get { return enableGradeStars == 1; }
            set {
                enableGradeStars = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableGradeStars));
            }
        }

        [NotMapped]
        public bool EnableGradeHFR {
            get { return enableGradeHFR == 1; }
            set {
                enableGradeHFR = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableGradeHFR));
            }
        }

        [NotMapped]
        public bool EnableGradeFWHM {
            get { return enableGradeFWHM == 1; }
            set {
                enableGradeFWHM = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableGradeFWHM));
            }
        }

        [NotMapped]
        public bool EnableGradeEccentricity {
            get { return enableGradeEccentricity == 1; }
            set {
                enableGradeEccentricity = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableGradeEccentricity));
            }
        }

        [NotMapped]
        public bool EnableMoveRejected {
            get { return enableMoveRejected == 1; }
            set {
                enableMoveRejected = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableMoveRejected));
            }
        }

        [NotMapped]
        public double DelayGrading {
            get { return delayGrading; }
            set {
                delayGrading = value;
                RaisePropertyChanged(nameof(DelayGrading));
            }
        }

        [NotMapped]
        public bool AcceptImprovement {
            get { return acceptimprovement == 1; }
            set {
                acceptimprovement = value ? 1 : 0;
                RaisePropertyChanged(nameof(AcceptImprovement));
            }
        }

        [NotMapped]
        public int MaxGradingSampleSize {
            get { return maxGradingSampleSize; }
            set {
                maxGradingSampleSize = value;
                RaisePropertyChanged(nameof(MaxGradingSampleSize));
            }
        }

        [NotMapped]
        public double RMSPixelThreshold {
            get { return rmsPixelThreshold; }
            set {
                rmsPixelThreshold = value;
                RaisePropertyChanged(nameof(RMSPixelThreshold));
            }
        }

        [NotMapped]
        public double DetectedStarsSigmaFactor {
            get { return detectedStarsSigmaFactor; }
            set {
                detectedStarsSigmaFactor = value;
                RaisePropertyChanged(nameof(DetectedStarsSigmaFactor));
            }
        }

        [NotMapped]
        public double HFRSigmaFactor {
            get { return hfrSigmaFactor; }
            set {
                hfrSigmaFactor = value;
                RaisePropertyChanged(nameof(HFRSigmaFactor));
            }
        }

        [NotMapped]
        public double FWHMSigmaFactor {
            get { return fwhmSigmaFactor; }
            set {
                fwhmSigmaFactor = value;
                RaisePropertyChanged(nameof(FWHMSigmaFactor));
            }
        }

        [NotMapped]
        public double EccentricitySigmaFactor {
            get { return eccentricitySigmaFactor; }
            set {
                eccentricitySigmaFactor = value;
                RaisePropertyChanged(nameof(EccentricitySigmaFactor));
            }
        }

        [NotMapped]
        public double AutoAcceptLevelHFR {
            get { return autoAcceptLevelHFR; }
            set {
                autoAcceptLevelHFR = value;
                RaisePropertyChanged(nameof(AutoAcceptLevelHFR));
            }
        }

        [NotMapped]
        public double AutoAcceptLevelFWHM {
            get { return autoAcceptLevelFWHM; }
            set {
                autoAcceptLevelFWHM = value;
                RaisePropertyChanged(nameof(AutoAcceptLevelFWHM));
            }
        }

        [NotMapped]
        public double AutoAcceptLevelEccentricity {
            get { return autoAcceptLevelEccentricity; }
            set {
                autoAcceptLevelEccentricity = value;
                RaisePropertyChanged(nameof(AutoAcceptLevelEccentricity));
            }
        }

        [NotMapped]
        public bool EnableSimulatedRun {
            get { return enableSimulatedRun == 1; }
            set {
                enableSimulatedRun = value ? 1 : 0;
                RaisePropertyChanged(nameof(EnableSimulatedRun));
            }
        }

        [NotMapped]
        public bool SkipSimulatedWaits {
            get { return skipSimulatedWaits == 1; }
            set {
                skipSimulatedWaits = value ? 1 : 0;
                RaisePropertyChanged(nameof(SkipSimulatedWaits));
            }
        }

        [NotMapped]
        public bool SkipSimulatedUpdates {
            get { return skipSimulatedUpdates == 1; }
            set {
                skipSimulatedUpdates = value ? 1 : 0;
                RaisePropertyChanged(nameof(SkipSimulatedUpdates));
            }
        }

        [NotMapped]
        public bool DoSkipSimulatedWaits => EnableSimulatedRun && SkipSimulatedWaits;

        [NotMapped]
        public bool DoSkipSimulatedUpdates => EnableSimulatedRun && SkipSimulatedUpdates;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}