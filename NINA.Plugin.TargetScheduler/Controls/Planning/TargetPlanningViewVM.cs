using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.Converters;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AsyncRelayCommand = CommunityToolkit.Mvvm.Input.AsyncRelayCommand;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.Planning {

    /// <summary>
    /// Planning parameters mirror the TS project and exposure template fields (and their defaults).  They can be
    /// entered manually or seeded from an existing TS target, its project, and one of its exposure plans.  Seeded
    /// values can then be changed freely for experimentation; changes are never written back to the database.
    /// </summary>
    public class TargetPlanningViewVM : BaseVM {
        private readonly SchedulerDatabaseInteraction database;

        public TargetPlanningViewVM(IProfileService profileService, IDeepSkyObjectSearchVM deepSkyObjectSearchVM) : base(profileService) {
            database = new SchedulerDatabaseInteraction();

            // Bound to the UI dispatcher explicitly since this VM may be (re)created off the UI thread on a profile change
            resultsRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) {
                Interval = TimeSpan.FromMilliseconds(ResultsRefreshDelay)
            };
            resultsRefreshTimer.Tick += ResultsRefreshTimer_Tick;

            DeepSkyObjectSearchVM = deepSkyObjectSearchVM;
            Coordinates = new InputCoordinates();
            Coordinates.PropertyChanged += (sender, e) => {
                Modified.Refresh();
                ScheduleResultsRefresh();
            };

            InitializeCombos();

            ProfileChoices = GetProfileChoices();
            string activeProfileId = profileService.ActiveProfile?.Id.ToString();
            KeyValuePair<string, string> initial = ProfileChoices.FirstOrDefault(p => p.Key == activeProfileId);
            SelectedProfile = initial.Key != null ? initial : ProfileChoices.FirstOrDefault();

            ResetToTargetCommand = new RelayCommand(SeedFromTarget);

            // Weak subscription: the search VM is shared and outlives this VM (recreated on profile change)
            PropertyChangedEventManager.AddHandler(deepSkyObjectSearchVM, DeepSkyObjectSearchVM_CoordinatesChanged, nameof(IDeepSkyObjectSearchVM.Coordinates));

            dailyDetailsCommand = new AsyncRelayCommand(ShowDailyDetails);
            CancelDailyDetailsCommand = new RelayCommand(CancelResults);

            imagingSeasonCommand = new AsyncRelayCommand(ShowImagingSeason);
            CancelImagingSeasonCommand = new RelayCommand(CancelResults);
        }

        // Same choices as the Target Management project and exposure template views
        private void InitializeCombos() {
            MinimumTimeChoices = [Utils.MtoHM(5), Utils.MtoHM(10), Utils.MtoHM(20)];
            for (int i = 30; i <= 240; i += 30) {
                MinimumTimeChoices.Add(Utils.MtoHM(i));
            }

            MinimumAltitudeChoices = [AltitudeChoicesConverter.OFF];
            for (int i = 5; i <= 60; i += 5) {
                MinimumAltitudeChoices.Add(i + "°");
            }

            MaximumAltitudeChoices = [AltitudeChoicesConverter.OFF];
            for (int i = 50; i <= 85; i += 5) {
                MaximumAltitudeChoices.Add(i + "°");
            }

            RelaxScaleChoices = [RelaxScaleChoicesConverter.OFF];
            for (int d = 1; d <= 8; d++) {
                RelaxScaleChoices.Add(d.ToString());
            }
        }

        public SeedTracker Modified { get; } = new SeedTracker();

        private void ParameterChanged([CallerMemberName] string propertyName = null) {
            RaisePropertyChanged(propertyName);
            Modified.Refresh();
            ScheduleResultsRefresh();
        }

        #region Target source selection

        private bool useExistingTarget = false;

        public bool UseExistingTarget {
            get => useExistingTarget;
            set {
                // Both radio buttons write here; ignore the redundant update from the one being unchecked
                if (useExistingTarget == value) {
                    return;
                }

                useExistingTarget = value;
                RaisePropertyChanged(nameof(UseExistingTarget));
                RaisePropertyChanged(nameof(UseManualTarget));
                SeedFromTarget();
            }
        }

        public bool UseManualTarget {
            get => !useExistingTarget;
            set => UseExistingTarget = !value;
        }

        private List<KeyValuePair<string, string>> profileChoices;

        public List<KeyValuePair<string, string>> ProfileChoices {
            get => profileChoices;
            private set {
                profileChoices = value;
                RaisePropertyChanged(nameof(ProfileChoices));
            }
        }

        private KeyValuePair<string, string> selectedProfile;

        public KeyValuePair<string, string> SelectedProfile {
            get => selectedProfile;
            set {
                selectedProfile = value;
                RaisePropertyChanged(nameof(SelectedProfile));
                ProjectChoices = LoadProjects(value.Key);
                SelectedProject = ProjectChoices.FirstOrDefault();
            }
        }

        private List<Project> projectChoices;

        public List<Project> ProjectChoices {
            get => projectChoices;
            private set {
                projectChoices = value;
                RaisePropertyChanged(nameof(ProjectChoices));
            }
        }

        private Project selectedProject;

        public Project SelectedProject {
            get => selectedProject;
            set {
                selectedProject = value;
                RaisePropertyChanged(nameof(SelectedProject));
                TargetChoices = value?.Targets ?? new List<Target>();
                SelectedTarget = TargetChoices.FirstOrDefault();
            }
        }

        private List<Target> targetChoices;

        public List<Target> TargetChoices {
            get => targetChoices;
            private set {
                targetChoices = value;
                RaisePropertyChanged(nameof(TargetChoices));
            }
        }

        private Target selectedTarget;

        public Target SelectedTarget {
            get => selectedTarget;
            set {
                selectedTarget = value;
                RaisePropertyChanged(nameof(SelectedTarget));
                ExposurePlanChoices = value?.ExposurePlans ?? new List<ExposurePlan>();
                SelectedExposurePlan = ExposurePlanChoices.FirstOrDefault();
            }
        }

        private List<ExposurePlan> exposurePlanChoices;

        public List<ExposurePlan> ExposurePlanChoices {
            get => exposurePlanChoices;
            private set {
                exposurePlanChoices = value;
                RaisePropertyChanged(nameof(ExposurePlanChoices));
            }
        }

        private ExposurePlan selectedExposurePlan;

        public ExposurePlan SelectedExposurePlan {
            get => selectedExposurePlan;
            set {
                selectedExposurePlan = value;
                RaisePropertyChanged(nameof(SelectedExposurePlan));
                SeedFromTarget();

                // Selecting a target always lands here (via its first exposure plan), so this covers both target and
                // exposure plan changes - even when the seeded values happen to be unchanged
                if (UseExistingTarget) {
                    ScheduleResultsRefresh();
                }
            }
        }

        public ICommand ResetToTargetCommand { get; private set; }

        /// <summary>
        /// Switch to existing target mode and select (and seed from) the given target.  The profile's projects are
        /// reloaded so any just-saved changes in Target Management are picked up.
        /// </summary>
        public void SelectExistingTarget(string profileId, int projectId, int targetId) {
            KeyValuePair<string, string> profile = ProfileChoices.FirstOrDefault(p => p.Key == profileId);
            if (profile.Key == null) {
                TSLogger.Warning($"target planning: profile not found for target selection: {profileId}");
                return;
            }

            SelectedProfile = profile;
            SelectedProject = ProjectChoices.FirstOrDefault(p => p.Id == projectId) ?? SelectedProject;
            SelectedTarget = TargetChoices.FirstOrDefault(t => t.Id == targetId) ?? SelectedTarget;
            UseExistingTarget = true;
        }

        private List<KeyValuePair<string, string>> GetProfileChoices() {
            List<KeyValuePair<string, string>> choices = new List<KeyValuePair<string, string>>();
            foreach (var profile in profileService.Profiles) {
                choices.Add(new KeyValuePair<string, string>(profile.Id.ToString(), profile.Name));
            }

            return choices;
        }

        private List<Project> LoadProjects(string profileId) {
            if (profileId == null) {
                return new List<Project>();
            }

            try {
                using (var context = database.GetContext()) {
                    return context.GetAllProjectsReadOnly(profileId);
                }
            } catch (Exception ex) {
                TSLogger.Error($"failed to load projects for target planning: {ex.Message} {ex.StackTrace}");
                return new List<Project>();
            }
        }

        /// <summary>
        /// Copy the selected target's coordinates, its project's parameters, and the selected exposure plan's
        /// template parameters into the working parameters, then record them as the baseline for change tracking.
        /// </summary>
        private void SeedFromTarget() {
            if (!UseExistingTarget || SelectedProject == null || SelectedTarget == null) {
                Modified.Clear();
                return;
            }

            Coordinates.Coordinates = SelectedTarget.Coordinates;

            MinimumTime = SelectedProject.MinimumTime;
            MinimumAltitude = SelectedProject.MinimumAltitude;
            MaximumAltitude = SelectedProject.MaximumAltitude;
            UseCustomHorizon = SelectedProject.UseCustomHorizon;
            HorizonOffset = SelectedProject.HorizonOffset;
            MeridianWindow = SelectedProject.MeridianWindow;

            ExposureTemplate template = SelectedExposurePlan?.ExposureTemplate;
            if (template != null) {
                TwilightLevel = template.TwilightLevel;
                MinutesOffset = template.MinutesOffset;
                MoonAvoidanceEnabled = template.MoonAvoidanceEnabled;
                MoonAvoidanceSeparation = template.MoonAvoidanceSeparation;
                MoonAvoidanceWidth = template.MoonAvoidanceWidth;
                MoonRelaxScale = template.MoonRelaxScale;
                MoonRelaxMinAltitude = template.MoonRelaxMinAltitude;
                MoonRelaxMaxAltitude = template.MoonRelaxMaxAltitude;
                MoonDownEnabled = template.MoonDownEnabled;
            }

            Modified.Clear();
            Modified.Track("Coordinates", () => (Coordinates.Coordinates.RA, Coordinates.Coordinates.Dec));
            Modified.Track(nameof(MinimumTime), () => MinimumTime);
            Modified.Track(nameof(MinimumAltitude), () => MinimumAltitude);
            Modified.Track(nameof(MaximumAltitude), () => MaximumAltitude);
            Modified.Track(nameof(UseCustomHorizon), () => UseCustomHorizon);
            Modified.Track(nameof(HorizonOffset), () => HorizonOffset);
            Modified.Track(nameof(MeridianWindow), () => MeridianWindow);

            if (template != null) {
                Modified.Track(nameof(TwilightLevel), () => TwilightLevel);
                Modified.Track(nameof(MinutesOffset), () => MinutesOffset);
                Modified.Track(nameof(MoonAvoidanceEnabled), () => MoonAvoidanceEnabled);
                Modified.Track(nameof(MoonAvoidanceSeparation), () => MoonAvoidanceSeparation);
                Modified.Track(nameof(MoonAvoidanceWidth), () => MoonAvoidanceWidth);
                Modified.Track(nameof(MoonRelaxScale), () => MoonRelaxScale);
                Modified.Track(nameof(MoonRelaxMinAltitude), () => MoonRelaxMinAltitude);
                Modified.Track(nameof(MoonRelaxMaxAltitude), () => MoonRelaxMaxAltitude);
                Modified.Track(nameof(MoonDownEnabled), () => MoonDownEnabled);
            }

            Modified.Refresh();
        }

        #endregion

        public IDeepSkyObjectSearchVM DeepSkyObjectSearchVM { get; private set; }

        public InputCoordinates Coordinates { get; private set; }

        private DateTime startDate = DateTime.Now.Date;

        public DateTime StartDate {
            get => startDate;
            set {
                startDate = DateTime.SpecifyKind(value, DateTimeKind.Local);
                RaisePropertyChanged(nameof(StartDate));
                ScheduleResultsRefresh();
            }
        }

        private int planDays = 3;

        public int PlanDays {
            get => planDays;
            set {
                planDays = value;
                RaisePropertyChanged(nameof(PlanDays));

                // The imaging season doesn't depend on the number of days
                if (ResultsShowing != PlanningResults.ImagingSeason) {
                    ScheduleResultsRefresh();
                }
            }
        }

        #region Project parameters

        public List<string> MinimumTimeChoices { get; private set; }

        private int minimumTime = 30;

        public int MinimumTime {
            get => minimumTime;
            set {
                minimumTime = value;
                ParameterChanged();
            }
        }

        public List<string> MinimumAltitudeChoices { get; private set; }

        private double minimumAltitude = 0;

        public double MinimumAltitude {
            get => minimumAltitude;
            set {
                minimumAltitude = value;
                ParameterChanged();
            }
        }

        public List<string> MaximumAltitudeChoices { get; private set; }

        private double maximumAltitude = 0;

        public double MaximumAltitude {
            get => maximumAltitude;
            set {
                maximumAltitude = value;
                ParameterChanged();
            }
        }

        private bool useCustomHorizon = false;

        public bool UseCustomHorizon {
            get => useCustomHorizon;
            set {
                useCustomHorizon = value;
                ParameterChanged();
            }
        }

        private double horizonOffset = 0;

        public double HorizonOffset {
            get => horizonOffset;
            set {
                horizonOffset = value;
                ParameterChanged();
            }
        }

        private int meridianWindow = 0;

        public int MeridianWindow {
            get => meridianWindow;
            set {
                meridianWindow = value;
                ParameterChanged();
            }
        }

        #endregion

        #region Exposure template parameters

        private TwilightLevel twilightLevel = TwilightLevel.Nighttime;

        public TwilightLevel TwilightLevel {
            get => twilightLevel;
            set {
                twilightLevel = value;
                ParameterChanged();
            }
        }

        private int minutesOffset = 0;

        public int MinutesOffset {
            get => minutesOffset;
            set {
                minutesOffset = value;
                ParameterChanged();
            }
        }

        private bool moonAvoidanceEnabled = false;

        public bool MoonAvoidanceEnabled {
            get => moonAvoidanceEnabled;
            set {
                moonAvoidanceEnabled = value;
                ParameterChanged();
                RaisePropertyChanged(nameof(RelaxEnabled));
            }
        }

        private double moonAvoidanceSeparation = 60;

        public double MoonAvoidanceSeparation {
            get => moonAvoidanceSeparation;
            set {
                moonAvoidanceSeparation = value;
                ParameterChanged();
            }
        }

        private int moonAvoidanceWidth = 7;

        public int MoonAvoidanceWidth {
            get => moonAvoidanceWidth;
            set {
                moonAvoidanceWidth = value;
                ParameterChanged();
            }
        }

        public List<string> RelaxScaleChoices { get; private set; }

        private double moonRelaxScale = 0;

        public double MoonRelaxScale {
            get => moonRelaxScale;
            set {
                moonRelaxScale = value;
                ParameterChanged();
                RaisePropertyChanged(nameof(RelaxEnabled));
            }
        }

        public bool RelaxEnabled => MoonAvoidanceEnabled && MoonRelaxScale > 0;

        private double moonRelaxMinAltitude = -15;

        public double MoonRelaxMinAltitude {
            get => moonRelaxMinAltitude;
            set {
                moonRelaxMinAltitude = value;
                ParameterChanged();
            }
        }

        private double moonRelaxMaxAltitude = 5;

        public double MoonRelaxMaxAltitude {
            get => moonRelaxMaxAltitude;
            set {
                moonRelaxMaxAltitude = value;
                ParameterChanged();
            }
        }

        private bool moonDownEnabled = false;

        public bool MoonDownEnabled {
            get => moonDownEnabled;
            set {
                moonDownEnabled = value;
                ParameterChanged();
            }
        }

        #endregion

        #region Results

        private enum PlanningResults { None, DailyDetails, ImagingSeason }

        private PlanningResults resultsShowing = PlanningResults.None;

        // Only one kind of result is shown at a time
        private PlanningResults ResultsShowing {
            get => resultsShowing;
            set {
                resultsShowing = value;
                RaisePropertyChanged(nameof(ShowDailyDetailsResults));
                RaisePropertyChanged(nameof(ShowImagingSeasonResults));
            }
        }

        // Once results are showing, parameter changes regenerate them after this quiet period (ms)
        private const int ResultsRefreshDelay = 300;

        private readonly DispatcherTimer resultsRefreshTimer;
        private CancellationTokenSource resultsRefreshTokenSource;

        // Restart the quiet period on every change so a burst of changes (e.g. seeding from a target) regenerates once
        private void ScheduleResultsRefresh() {
            if (ResultsShowing == PlanningResults.None || resultsRefreshTimer == null) {
                return;
            }

            resultsRefreshTimer.Stop();
            resultsRefreshTimer.Start();
        }

        private void StopResultsRefresh() {
            resultsRefreshTimer.Stop();
            resultsRefreshTokenSource?.Cancel();
        }

        // Stop any pending or in-progress generation, whether started from a button or by a refresh
        private void CancelResults() {
            StopResultsRefresh();
            dailyDetailsCommand.Cancel();
            imagingSeasonCommand.Cancel();
        }

        private async void ResultsRefreshTimer_Tick(object sender, EventArgs e) {
            CancelResults();
            resultsRefreshTokenSource = new CancellationTokenSource();
            CancellationToken token = resultsRefreshTokenSource.Token;

            if (ResultsShowing == PlanningResults.DailyDetails) {
                await GenerateDailyDetails(token);
            } else if (ResultsShowing == PlanningResults.ImagingSeason) {
                await GenerateImagingSeason(token);
            }
        }

        #endregion

        #region Imaging season

        private readonly AsyncRelayCommand imagingSeasonCommand;
        public ICommand ImagingSeasonCommand => imagingSeasonCommand;
        public ICommand CancelImagingSeasonCommand { get; private set; }

        private bool imagingSeasonRunning;

        public bool ImagingSeasonRunning {
            get => imagingSeasonRunning;
            private set {
                imagingSeasonRunning = value;
                RaisePropertyChanged(nameof(ImagingSeasonRunning));
            }
        }

        private ImagingSeasonPlotModel imagingSeasonPlotModel;

        public ImagingSeasonPlotModel ImagingSeasonPlotModel {
            get => imagingSeasonPlotModel;
            private set {
                imagingSeasonPlotModel = value;
                RaisePropertyChanged(nameof(ImagingSeasonPlotModel));
            }
        }

        private string imagingSeasonMessage;

        public string ImagingSeasonMessage {
            get => imagingSeasonMessage;
            private set {
                imagingSeasonMessage = value;
                RaisePropertyChanged(nameof(ImagingSeasonMessage));
            }
        }

        public bool ShowImagingSeasonResults => resultsShowing == PlanningResults.ImagingSeason;

        private async Task ShowImagingSeason(CancellationToken token) {
            StopResultsRefresh();
            dailyDetailsCommand.Cancel();
            ResultsShowing = PlanningResults.ImagingSeason;

            // A chart left from an earlier run may be stale, so start fresh (with the loading indicator)
            ImagingSeasonPlotModel = null;
            ImagingSeasonMessage = null;
            await GenerateImagingSeason(token);
        }

        private async Task GenerateImagingSeason(CancellationToken token) {
            TargetPlanningParameters parameters = GetPlanningParameters();

            // Only show the loading indicator when there's no chart yet; otherwise keep showing the current one
            bool showRunning = ImagingSeasonPlotModel == null;
            if (showRunning) {
                ImagingSeasonRunning = true;
            }

            try {
                List<DailyDetail> season = await Task.Run(() => new ImagingSeasonGenerator(parameters).Generate(token), token);
                if (token.IsCancellationRequested) {
                    return;
                }

                if (season.Count == 0) {
                    ImagingSeasonPlotModel = null;
                    ImagingSeasonMessage = "The target can't be imaged at any time of the year with these parameters";
                } else {
                    ImagingSeasonMessage = null;
                    ImagingSeasonPlotModel = new ImagingSeasonPlotModel(profileService.ActiveProfile.ColorSchemaSettings.ColorSchema,
                        parameters.TargetName, season);
                }
            } catch (OperationCanceledException) {
                TSLogger.Debug("target planning imaging season canceled");
            } catch (Exception ex) {
                TSLogger.Error($"target planning imaging season failed: {ex.Message} {ex.StackTrace}");
                ImagingSeasonPlotModel = null;
                ImagingSeasonMessage = "Failed to determine the imaging season, see the TS log for details";
            } finally {
                if (showRunning) {
                    ImagingSeasonRunning = false;
                }
            }
        }

        #endregion

        #region Daily details

        // Rows per page in the daily details table
        private const int DailyDetailsPageSize = 22;

        private readonly AsyncRelayCommand dailyDetailsCommand;
        public ICommand DailyDetailsCommand => dailyDetailsCommand;
        public ICommand CancelDailyDetailsCommand { get; private set; }

        private bool dailyDetailsRunning;

        public bool DailyDetailsRunning {
            get => dailyDetailsRunning;
            private set {
                dailyDetailsRunning = value;
                RaisePropertyChanged(nameof(DailyDetailsRunning));
            }
        }

        private PagedList<DailyDetailsRow> dailyDetailsResults;

        public PagedList<DailyDetailsRow> DailyDetailsResults {
            get => dailyDetailsResults;
            private set {
                dailyDetailsResults = value;
                RaisePropertyChanged(nameof(DailyDetailsResults));
                RaisePropertyChanged(nameof(ShowDailyDetailsResults));
            }
        }

        public bool ShowDailyDetailsResults => resultsShowing == PlanningResults.DailyDetails && DailyDetailsResults != null;

        private List<DailyDetailsRow> dailyDetailsRows;

        private async Task ShowDailyDetails(CancellationToken token) {
            StopResultsRefresh();
            imagingSeasonCommand.Cancel();
            ResultsShowing = PlanningResults.DailyDetails;
            await GenerateDailyDetails(token);
        }

        private async Task GenerateDailyDetails(CancellationToken token) {
            if (PlanDays < 1) {
                return;
            }

            TargetPlanningParameters parameters = GetPlanningParameters();

            // Only show the loading indicator when there's no table yet; otherwise keep showing the current results
            bool showRunning = DailyDetailsResults == null;
            if (showRunning) {
                DailyDetailsRunning = true;
            }

            try {
                List<DailyDetail> details = await Task.Run(() => new DailyDetailsGenerator(parameters).Generate(token), token);
                if (!token.IsCancellationRequested) {
                    ApplyDailyDetails(details, parameters);
                }
            } catch (OperationCanceledException) {
                TSLogger.Debug("target planning daily details canceled");
            } catch (Exception ex) {
                TSLogger.Error($"target planning daily details failed: {ex.Message} {ex.StackTrace}");
            } finally {
                if (showRunning) {
                    DailyDetailsRunning = false;
                }
            }
        }

        /// <summary>
        /// If the regenerated plan covers the same nights, update the existing rows in place so the table's page and
        /// selected row (with its open details/chart) are kept.  Otherwise replace the table, reselecting the same night
        /// if it's still present on the first page.
        /// </summary>
        private void ApplyDailyDetails(List<DailyDetail> details, TargetPlanningParameters parameters) {
            if (dailyDetailsRows != null && DailyDetailsResults != null
                && dailyDetailsRows.Select(r => r.NightDate).SequenceEqual(details.Select(d => d.NightDate))) {
                for (int i = 0; i < details.Count; i++) {
                    dailyDetailsRows[i].Update(details[i], parameters);
                }

                return;
            }

            DateTime? selectedNight = DailyDetailsResults?.SelectedItem?.NightDate;
            dailyDetailsRows = details.Select(d => new DailyDetailsRow(d, parameters, profileService)).ToList();
            PagedList<DailyDetailsRow> results = new PagedList<DailyDetailsRow>(DailyDetailsPageSize, dailyDetailsRows);

            if (selectedNight.HasValue) {
                int index = dailyDetailsRows.FindIndex(r => r.NightDate == selectedNight.Value);
                if (index >= 0 && index < DailyDetailsPageSize) {
                    results.SelectedItem = dailyDetailsRows[index];
                }
            }

            DailyDetailsResults = results;
        }

        private TargetPlanningParameters GetPlanningParameters() {
            IProfile profile = UseExistingTarget && SelectedProfile.Key != null
                ? ProfileLoader.GetProfile(profileService, SelectedProfile.Key)
                : profileService.ActiveProfile;

            return new TargetPlanningParameters {
                Profile = profile,
                TargetName = UseExistingTarget ? SelectedTarget?.Name : DeepSkyObjectSearchVM.TargetName,
                Coordinates = Coordinates.Coordinates,
                StartDate = StartDate,
                PlanDays = PlanDays,
                MinimumTime = MinimumTime,
                MinimumAltitude = MinimumAltitude,
                MaximumAltitude = MaximumAltitude,
                UseCustomHorizon = UseCustomHorizon,
                HorizonOffset = HorizonOffset,
                MeridianWindow = MeridianWindow,
                TwilightLevel = TwilightLevel,
                MinutesOffset = MinutesOffset,
                MoonAvoidanceEnabled = MoonAvoidanceEnabled,
                MoonAvoidanceSeparation = MoonAvoidanceSeparation,
                MoonAvoidanceWidth = MoonAvoidanceWidth,
                MoonRelaxScale = MoonRelaxScale,
                MoonRelaxMinAltitude = MoonRelaxMinAltitude,
                MoonRelaxMaxAltitude = MoonRelaxMaxAltitude,
                MoonDownEnabled = MoonDownEnabled
            };
        }

        // In manual mode, a target selected in the search box supplies the coordinates
        private void DeepSkyObjectSearchVM_CoordinatesChanged(object sender, PropertyChangedEventArgs e) {
            if (UseManualTarget && DeepSkyObjectSearchVM.Coordinates != null) {
                Coordinates.Coordinates = DeepSkyObjectSearchVM.Coordinates;
            }
        }

        #endregion
    }
}
