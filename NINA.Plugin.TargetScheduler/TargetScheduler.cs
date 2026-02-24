using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Plugin.Interfaces;
using NINA.Plugin.TargetScheduler.API;
using NINA.Plugin.TargetScheduler.Controls.AcquiredImages;
using NINA.Plugin.TargetScheduler.Controls.DatabaseManager;
using NINA.Plugin.TargetScheduler.Controls.PlanPreview;
using NINA.Plugin.TargetScheduler.Controls.Reporting;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Sequencer;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler {

    [Export(typeof(IPluginManifest))]
    public class TargetScheduler : PluginBase, INotifyPropertyChanged {

        // Singleton event mediator for TS events (intra-plugin communication)
        private static readonly Lazy<TargetSchedulerEventMediator> lazy = new Lazy<TargetSchedulerEventMediator>(() => new TargetSchedulerEventMediator());

        public static TargetSchedulerEventMediator EventMediator { get => lazy.Value; }

        // Plugin specific image file patterns
        public static readonly ImagePattern FlatSessionIdImagePattern = new ImagePattern("$$TSSESSIONID$$", "Session identifier for working with TS lights and flats", "Target Scheduler");

        public static readonly ImagePattern ProjectNameImagePattern = new ImagePattern("$$TSPROJECTNAME$$", "TS project name (if available)", "Target Scheduler");

        private static Server APIServer;

        private IProfileService profileService;
        private IApplicationMediator applicationMediator;
        private IFramingAssistantVM framingAssistantVM;
        private IDeepSkyObjectSearchVM deepSkyObjectSearchVM;
        private IPlanetariumFactory planetariumFactory;

        [ImportingConstructor]
        public TargetScheduler(IProfileService profileService,
            IOptionsVM options,
            IApplicationMediator applicationMediator,
            IFramingAssistantVM framingAssistantVM,
            IDeepSkyObjectSearchVM deepSkyObjectSearchVM,
            IPlanetariumFactory planetariumFactory) {
            if (Properties.Settings.Default.UpdateSettings) {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }

            this.profileService = profileService;
            this.applicationMediator = applicationMediator;
            this.framingAssistantVM = framingAssistantVM;
            this.deepSkyObjectSearchVM = deepSkyObjectSearchVM;
            this.planetariumFactory = planetariumFactory;

            APIServer = new Server(8188, profileService, new SchedulerDatabaseInteraction());
            APIServer.Start();

            profileService.ProfileChanged += ProfileService_ProfileChanged;

            options.AddImagePattern(FlatSessionIdImagePattern);
            options.AddImagePattern(ProjectNameImagePattern);
        }

        public override Task Initialize() {
            InitPluginHome();
            TSLogger.SetLogLevel(ProfileLogLevel(profileService));

            if (SyncEnabled(profileService)) {
                SyncManager.Instance.Start(profileService);
            }

            //new ExportProfile(profileService.ActiveProfile, false).Export();
            //new ImportProfile(profileService.ActiveProfile, "C:\\Users\\Tom\\AppData\\Local\\NINA\\SchedulerPlugin\\exported-all.zip", true).Import();

            TSLogger.Info("plugin initialized");
            return Task.CompletedTask;
        }

        private void InitPluginHome() {
            if (!Directory.Exists(Common.PLUGIN_HOME)) {
                Directory.CreateDirectory(Common.PLUGIN_HOME);
            }

            SchedulerDatabaseInteraction.BackupDatabase();
        }

        public static bool SyncEnabled(IProfileService profileService) {
            ProfilePreference profilePreference = new SchedulerPlanLoader(profileService.ActiveProfile).GetProfilePreferences();
            return profilePreference.EnableSynchronization;
        }

        private LogLevelEnum ProfileLogLevel(IProfileService profileService) {
            ProfilePreference profilePreference = new SchedulerPlanLoader(profileService.ActiveProfile).GetProfilePreferences();
            return profilePreference.LogLevel;
        }

        private DatabaseManagerVM databaseManagerVM;

        public DatabaseManagerVM DatabaseManagerVM {
            get => databaseManagerVM;
            set {
                databaseManagerVM = value;
                RaisePropertyChanged(nameof(DatabaseManagerVM));
            }
        }

        private PlanPreviewerViewVM planPreviewerViewVM;

        public PlanPreviewerViewVM PlanPreviewerViewVM {
            get => planPreviewerViewVM;
            set {
                planPreviewerViewVM = value;
                RaisePropertyChanged(nameof(PlanPreviewerViewVM));
            }
        }

        private ReportingManagerViewVM reportingManagerViewVM;

        public ReportingManagerViewVM ReportingManagerViewVM {
            get => reportingManagerViewVM;
            set {
                reportingManagerViewVM = value;
                RaisePropertyChanged(nameof(ReportingManagerViewVM));
            }
        }

        private AcquiredImagesManagerViewVM acquiredImagesManagerViewVM;

        public AcquiredImagesManagerViewVM AcquiredImagesManagerViewVM {
            get => acquiredImagesManagerViewVM;
            set {
                acquiredImagesManagerViewVM = value;
                RaisePropertyChanged(nameof(AcquiredImagesManagerViewVM));
            }
        }

        private bool databaseManagerIsExpanded = false;

        public bool DatabaseManagerIsExpanded {
            get { return databaseManagerIsExpanded; }
            set {
                databaseManagerIsExpanded = value;
                if (value && DatabaseManagerVM == null) {
                    DatabaseManagerVM = new DatabaseManagerVM(profileService, applicationMediator, framingAssistantVM, deepSkyObjectSearchVM, planetariumFactory);
                }
            }
        }

        private bool planPreviewIsExpanded = false;

        public bool PlanPreviewIsExpanded {
            get { return planPreviewIsExpanded; }
            set {
                planPreviewIsExpanded = value;
                if (value && PlanPreviewerViewVM == null) {
                    PlanPreviewerViewVM = new PlanPreviewerViewVM(profileService);
                }
            }
        }

        private bool reportingManagerIsExpanded = false;

        public bool ReportingManagerIsExpanded {
            get { return reportingManagerIsExpanded; }
            set {
                reportingManagerIsExpanded = value;
                if (value && ReportingManagerViewVM == null) {
                    ReportingManagerViewVM = new ReportingManagerViewVM(profileService);
                }
            }
        }

        private bool acquiredImagesManagerIsExpanded = false;

        public bool AcquiredImagesManagerIsExpanded {
            get { return acquiredImagesManagerIsExpanded; }
            set {
                acquiredImagesManagerIsExpanded = value;
                if (value && AcquiredImagesManagerViewVM == null) {
                    AcquiredImagesManagerViewVM = new AcquiredImagesManagerViewVM(profileService);
                }
            }
        }

        private void ProcessExited(object sender, EventArgs e) {
            TSLogger.Warning($"process exited");
        }

        public override Task Teardown() {
            ImageGradingController.Instance.Shutdown();

            if (SyncManager.Instance.IsRunning) {
                SyncManager.Instance.Shutdown();
            }

            profileService.ProfileChanged -= ProfileService_ProfileChanged;
            TSLogger.Info("closing log");
            TSLogger.CloseAndFlush();
            return Task.CompletedTask;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ProfileService_ProfileChanged(object? sender, EventArgs e) {
            TSLogger.SetLogLevel(ProfileLogLevel(profileService));

            DatabaseManagerVM = new DatabaseManagerVM(profileService, applicationMediator, framingAssistantVM, deepSkyObjectSearchVM, planetariumFactory);
            PlanPreviewerViewVM = new PlanPreviewerViewVM(profileService);
            AcquiredImagesManagerViewVM = new AcquiredImagesManagerViewVM(profileService);

            RaisePropertyChanged(nameof(DatabaseManagerVM));
            RaisePropertyChanged(nameof(PlanPreviewerViewVM));
            RaisePropertyChanged(nameof(AcquiredImagesManagerViewVM));

            if (profileService.ActiveProfile != null) {
                profileService.ActiveProfile.AstrometrySettings.PropertyChanged -= ProfileService_ProfileChanged;
                profileService.ActiveProfile.AstrometrySettings.PropertyChanged += ProfileService_ProfileChanged;

                if (SyncManager.Instance.IsRunning) {
                    SyncManager.Instance.Shutdown();
                    if (SyncEnabled(profileService)) {
                        SyncManager.Instance.Start(profileService);
                    }
                }
            }
        }
    }
}