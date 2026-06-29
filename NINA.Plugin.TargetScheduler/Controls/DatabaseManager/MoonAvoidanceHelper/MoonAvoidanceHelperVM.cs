using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.Converters;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager.MoonAvoidanceHelper {

    /// <summary>
    /// View model for the Moon Avoidance Helper dialog (classic Lorentzian avoidance plus relaxation).
    ///
    /// Holds working copies of the avoidance parameters so they can be edited and previewed without
    /// touching the exposure template until the user clicks Save.  The plot shows two curves across a full
    /// lunation (moon age 0-30 days): the classic separation, and the relaxed separation at the assumed
    /// moon altitude.  Relaxation modulates the separation/width parameters exactly as MoonAvoidanceExpert
    /// does at runtime; the assumed altitude is a preview-only input and is not saved.
    /// </summary>
    public class MoonAvoidanceHelperVM : BaseINPC {

        // Days of moon age spanned by the avoidance plot's horizontal axis.
        public const int PlotDays = 30;

        // Reference hour of day for the example: defaulting to 1pm makes the calculation target the upcoming night.
        private const int ReferenceHour = 13;

        private readonly IProfileService profileService;
        private readonly SchedulerDatabaseInteraction database;

        public MoonAvoidanceHelperVM(IProfileService profileService, string profileId,
                                     bool enabled, double separation, int width,
                                     double relaxScale, double relaxMinAltitude, double relaxMaxAltitude, bool moonDownEnabled) {
            this.profileService = profileService;
            this.database = new SchedulerDatabaseInteraction();

            classicEnabled = enabled;
            classicSeparation = separation;
            classicWidth = width;
            this.relaxScale = relaxScale;
            this.relaxMinAltitude = relaxMinAltitude;
            this.relaxMaxAltitude = relaxMaxAltitude;
            this.moonDownEnabled = moonDownEnabled;

            // Preview the relaxed curve at the middle of the relaxation altitude band by default.
            altitude = Math.Round((relaxMinAltitude + relaxMaxAltitude) / 2.0);

            RelaxScaleChoices = new List<string> { RelaxScaleChoicesConverter.OFF };
            for (int d = 1; d <= 8; d++) {
                RelaxScaleChoices.Add(d.ToString());
            }

            exampleDate = DateTime.Now.Date.AddHours(ReferenceHour);
            PreviousDayCommand = new RelayCommand(() => ExampleDate = ExampleDate.AddDays(-1));
            NextDayCommand = new RelayCommand(() => ExampleDate = ExampleDate.AddDays(1));

            ProfileChoices = GetProfileChoices();
            KeyValuePair<string, string> initial = ProfileChoices.FirstOrDefault(p => p.Key == profileId);
            SelectedProfile = initial.Key != null ? initial : ProfileChoices.FirstOrDefault();

            Recompute();
        }

        private bool classicEnabled;

        public bool ClassicEnabled {
            get => classicEnabled;
            set {
                classicEnabled = value;
                RaisePropertyChanged(nameof(ClassicEnabled));
                RaisePropertyChanged(nameof(RelaxEnabled));
                OnAvoidanceParameterChanged();
            }
        }

        private double classicSeparation;

        public double ClassicSeparation {
            get => classicSeparation;
            set {
                // Constrain to whole degrees; always raise so the bound ScrollBar snaps its thumb back to
                // the integer position (even mid-drag) and never reports a fractional value.
                double rounded = Math.Round(value);
                bool changed = classicSeparation != rounded;
                classicSeparation = rounded;
                RaisePropertyChanged(nameof(ClassicSeparation));
                if (changed) {
                    Recompute();
                    OnAvoidanceParameterChanged();
                }
            }
        }

        private int classicWidth;

        public int ClassicWidth {
            get => classicWidth;
            set {
                // Width is already integral; always raise so the bound ScrollBar snaps its thumb to the
                // integer position even when a drag lands on the same value.
                bool changed = classicWidth != value;
                classicWidth = value;
                RaisePropertyChanged(nameof(ClassicWidth));
                if (changed) {
                    Recompute();
                    OnAvoidanceParameterChanged();
                }
            }
        }

        // Relaxation parameters.  These modulate the classic separation/width based on the assumed moon
        // altitude, producing the relaxed curve.

        public List<string> RelaxScaleChoices { get; }

        private double relaxScale;

        public double RelaxScale {
            get => relaxScale;
            set {
                relaxScale = value;
                RaisePropertyChanged(nameof(RelaxScale));
                RaisePropertyChanged(nameof(RelaxEnabled));
                RaisePropertyChanged(nameof(ShowRelaxed));
                Recompute();
                OnAvoidanceParameterChanged();
            }
        }

        // Relaxation altitude controls are only active when classic avoidance is enabled and a scale is set.
        public bool RelaxEnabled => ClassicEnabled && RelaxScale > 0;

        // The relaxed curve is only meaningful (and only shown) when a relaxation scale is set.
        public bool ShowRelaxed => RelaxScale > 0;

        private double relaxMinAltitude;

        public double RelaxMinAltitude {
            get => relaxMinAltitude;
            set {
                double rounded = Math.Round(value);
                bool changed = relaxMinAltitude != rounded;
                relaxMinAltitude = rounded;
                RaisePropertyChanged(nameof(RelaxMinAltitude));
                if (changed) {
                    Recompute();
                    OnAvoidanceParameterChanged();
                }
            }
        }

        private double relaxMaxAltitude;

        public double RelaxMaxAltitude {
            get => relaxMaxAltitude;
            set {
                double rounded = Math.Round(value);
                bool changed = relaxMaxAltitude != rounded;
                relaxMaxAltitude = rounded;
                RaisePropertyChanged(nameof(RelaxMaxAltitude));
                if (changed) {
                    Recompute();
                    OnAvoidanceParameterChanged();
                }
            }
        }

        private double altitude;

        // Assumed moon altitude used to preview the relaxed curve; preview-only, not persisted.
        public double Altitude {
            get => altitude;
            set {
                double rounded = Math.Round(value);
                bool changed = altitude != rounded;
                altitude = rounded;
                RaisePropertyChanged(nameof(Altitude));
                if (changed) {
                    Recompute();
                }
            }
        }

        private bool moonDownEnabled;

        public bool MoonDownEnabled {
            get => moonDownEnabled;
            set {
                moonDownEnabled = value;
                RaisePropertyChanged(nameof(MoonDownEnabled));
                OnAvoidanceParameterChanged();
            }
        }

        private IList<DataPoint> avoidancePoints;

        public IList<DataPoint> AvoidancePoints {
            get => avoidancePoints;
            private set {
                avoidancePoints = value;
                RaisePropertyChanged(nameof(AvoidancePoints));
            }
        }

        private IList<DataPoint> relaxedPoints;

        public IList<DataPoint> RelaxedPoints {
            get => relaxedPoints;
            private set {
                relaxedPoints = value;
                RaisePropertyChanged(nameof(RelaxedPoints));
            }
        }

        // Example selection: cascading Profile -> Project -> Target plus a target
        // date, used to illustrate moon avoidance for a concrete target on a concrete night.  The chart shows
        // the moon-to-target separation and the calculated avoidance (rejection) separation - using the same
        // working avoidance parameters being edited above - across astronomical dusk to dawn.

        // Example chart size (matches the placeholder it replaces in the expander).
        private const double ExampleChartWidth = 600;
        private const double ExampleChartHeight = 200;

        public ICommand PreviousDayCommand { get; }
        public ICommand NextDayCommand { get; }

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
                InvalidateExample();
            }
        }

        private DateTime exampleDate;

        // The example date.  Always normalized to the reference hour so the calculation runs for the upcoming night.
        public DateTime ExampleDate {
            get => exampleDate;
            set {
                exampleDate = value.Date.AddHours(ReferenceHour);
                RaisePropertyChanged(nameof(ExampleDate));
                InvalidateExample();
            }
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

            using (var context = database.GetContext()) {
                return context.GetAllProjects(profileId);
            }
        }

        private bool showExample;

        // Bound to the "Show Example" expander.  Building the chart is deferred until the user opens it.
        public bool ShowExample {
            get => showExample;
            set {
                showExample = value;
                RaisePropertyChanged(nameof(ShowExample));
                if (showExample) {
                    RebuildExampleChart();
                }
            }
        }

        private FrameworkElement exampleChart;

        public FrameworkElement ExampleChart {
            get => exampleChart;
            private set {
                exampleChart = value;
                RaisePropertyChanged(nameof(ExampleChart));
            }
        }

        // The parameter-independent example data (chart data + moon geometry).  Invalidated when the
        // target/date/profile changes; reused as the avoidance parameters are tweaked.
        private AvoidanceExampleData cachedExampleData;

        // The current example chart control, kept so an avoidance-parameter change can refresh just its curves
        // without rebuilding the whole control on every scrollbar tick.
        private MoonAvoidExample exampleChartControl;

        private AvoidanceExampleParameters CurrentExampleParameters() {
            return new AvoidanceExampleParameters {
                ClassicEnabled = ClassicEnabled,
                ClassicSeparation = ClassicSeparation,
                ClassicWidth = ClassicWidth,
                RelaxScale = RelaxScale,
                RelaxMinAltitude = RelaxMinAltitude,
                RelaxMaxAltitude = RelaxMaxAltitude,
                MoonDownEnabled = MoonDownEnabled
            };
        }

        // Drop the cached geometry (target/date/profile changed) and rebuild the chart if it's showing.
        private void InvalidateExample() {
            cachedExampleData = null;
            if (ShowExample) {
                RebuildExampleChart();
            }
        }

        // An avoidance parameter changed; refresh just the example's avoidance curves on the existing chart if
        // it's showing.  The cached moon geometry is unaffected, so this is cheap.
        private void OnAvoidanceParameterChanged() {
            if (!ShowExample) {
                return;
            }

            if (exampleChartControl == null || cachedExampleData == null) {
                RebuildExampleChart();
                return;
            }

            try {
                AvoidanceExampleChartBuilder.ComputeCurves(cachedExampleData, CurrentExampleParameters(),
                    out IList<DataPoint> separation, out IList<DataPoint> avoidance, out IList<bool> rejected);
                exampleChartControl.SetAvoidanceCurves(separation, avoidance, rejected);
            } catch (Exception ex) {
                TSLogger.Error($"failed to update moon avoidance example curves: {ex.Message} {ex.StackTrace}");
            }
        }

        private void RebuildExampleChart() {
            try {
                if (cachedExampleData == null) {
                    IProfile profile = ProfileLoader.GetProfile(profileService, SelectedProfile.Key);
                    cachedExampleData = AvoidanceExampleChartBuilder.BuildData(profileService, profile, SelectedProject, SelectedTarget, ExampleDate);
                }

                if (cachedExampleData == null) {
                    exampleChartControl = null;
                    ExampleChart = null;
                    return;
                }

                exampleChartControl = AvoidanceExampleChartBuilder.BuildChart(cachedExampleData, CurrentExampleParameters(), ExampleChartWidth, ExampleChartHeight);
                ExampleChart = exampleChartControl;
            } catch (Exception ex) {
                TSLogger.Error($"failed to build moon avoidance example chart: {ex.Message} {ex.StackTrace}");
                exampleChartControl = null;
                ExampleChart = null;
            }
        }

        // The relaxed separation parameter at the assumed altitude (mirrors MoonAvoidanceExpert).
        private double RelaxedSeparationParameter() {
            if (RelaxScale > 0 && Altitude <= RelaxMaxAltitude) {
                return ClassicSeparation + (RelaxScale * (Altitude - RelaxMaxAltitude));
            }

            return ClassicSeparation;
        }

        // The relaxed width parameter at the assumed altitude (mirrors MoonAvoidanceExpert).
        private double RelaxedWidthParameter() {
            if (RelaxScale > 0 && Altitude <= RelaxMaxAltitude) {
                double span = RelaxMaxAltitude - RelaxMinAltitude;
                return span > 0 ? ClassicWidth * ((Altitude - RelaxMinAltitude) / span) : 0;
            }

            return ClassicWidth;
        }

        // Rebuild both curves (assigning new lists so the bound plot re-renders immediately).
        private void Recompute() {
            double relaxedSeparation = RelaxedSeparationParameter();
            double relaxedWidth = RelaxedWidthParameter();

            // If relaxation drives separation or width to zero or below, avoidance is off (required separation 0).
            bool relaxedOff = relaxedSeparation <= 0 || relaxedWidth <= 0;

            List<DataPoint> classic = new List<DataPoint>(PlotDays + 1);
            List<DataPoint> relaxed = new List<DataPoint>(PlotDays + 1);
            for (int day = 0; day <= PlotDays; day++) {
                classic.Add(new DataPoint(day, AstrometryUtils.GetMoonAvoidanceLorentzianSeparation(day, ClassicSeparation, ClassicWidth)));

                double relaxedValue = relaxedOff
                    ? 0
                    : AstrometryUtils.GetMoonAvoidanceLorentzianSeparation(day, relaxedSeparation, relaxedWidth);
                relaxed.Add(new DataPoint(day, relaxedValue));
            }

            AvoidancePoints = classic;
            RelaxedPoints = relaxed;
        }
    }
}
