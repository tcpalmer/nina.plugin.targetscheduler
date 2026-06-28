using LinqKit;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.MyMessageBox;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview {

    public class PlanPreviewerViewVM : BaseVM {
        private SchedulerDatabaseInteraction database;

        public PlanPreviewerViewVM(IProfileService profileService) : base(profileService) {
            database = new SchedulerDatabaseInteraction();
            InstructionList = new ObservableCollection<TreeViewItem>();

            profileService.ProfileChanged += ProfileService_ProfileChanged;
            profileService.Profiles.CollectionChanged += ProfileService_ProfileChanged;

            InitializeCriteria();

            SetNowCommand = new RelayCommand(SetPreviewTimeNow);
            PlanPreviewCommand = new RelayCommand(RunPlanPreview);
            PlanPreviewResultsCommand = new RelayCommand(RunPlanPreviewResults);
            OpenHtmlReportCommand = new RelayCommand(OpenHtmlReport);
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            InstructionList.Clear();
            SelectedProfileId = profileService.ActiveProfile.Id.ToString();
            ProfileChoices = GetProfileChoices();
        }

        private void InitializeCriteria() {
            PlanDate = DateTime.Now.Date;
            SelectedProfileId = profileService.ActiveProfile.Id.ToString();
            ProfileChoices = GetProfileChoices();

            ShowPlanPreview = true;
            ShowPlanPreviewResults = false;
            TableLoading = false;
        }

        private bool tableLoading;

        public bool TableLoading {
            get => tableLoading;
            set {
                tableLoading = value;
                RaisePropertyChanged(nameof(TableLoading));
            }
        }

        private DateTime planDate = DateTime.MinValue;

        public DateTime PlanDate {
            get => planDate;
            set {
                planDate = value;
                SchedulerPlans = null;
                RaisePropertyChanged(nameof(PlanDate));
            }
        }

        private int planHours = 13;

        public int PlanHours {
            get => planHours;
            set {
                planHours = value;
                SchedulerPlans = null;
                RaisePropertyChanged(nameof(PlanHours));
            }
        }

        private int planMinutes = 0;

        public int PlanMinutes {
            get => planMinutes;
            set {
                planMinutes = value;
                SchedulerPlans = null;
                RaisePropertyChanged(nameof(PlanMinutes));
            }
        }

        private int planSeconds = 0;

        public int PlanSeconds {
            get => planSeconds;
            set {
                planSeconds = value;
                SchedulerPlans = null;
                RaisePropertyChanged(nameof(PlanSeconds));
            }
        }

        private AsyncObservableCollection<KeyValuePair<string, string>> profileChoices;

        public AsyncObservableCollection<KeyValuePair<string, string>> ProfileChoices {
            get {
                return profileChoices;
            }
            set {
                profileChoices = value;
                SchedulerPlans = null;
                RaisePropertyChanged(nameof(ProfileChoices));
            }
        }

        private string selectedProfileId;

        public string SelectedProfileId {
            get => selectedProfileId;
            set {
                selectedProfileId = value;
                SchedulerPlans = null;
                RaisePropertyChanged(nameof(SelectedProfileId));
            }
        }

        private ObservableCollection<TreeViewItem> instructionList;

        public ObservableCollection<TreeViewItem> InstructionList {
            get => instructionList;
            set {
                instructionList = value;
                RaisePropertyChanged(nameof(InstructionList));
            }
        }

        private List<SchedulerPlan> SchedulerPlans { get; set; }
        private PlannerReport plannerReport;

        // One entry per aggregated target block, in plan order; aligns 1:1 with the Target groups produced
        // by PlannerReportModel.BuildGroups().  Used by the Plan Preview tree and both report renderers.
        private List<PlannerChartData> chartDataList;

        private bool showPlanPreview;

        public bool ShowPlanPreview {
            get => showPlanPreview;
            set {
                showPlanPreview = value;
                RaisePropertyChanged(nameof(ShowPlanPreview));
            }
        }

        private bool showPlanPreviewResults;

        public bool ShowPlanPreviewResults {
            get => showPlanPreviewResults;
            set {
                showPlanPreviewResults = value;
                RaisePropertyChanged(nameof(ShowPlanPreviewResults));
            }
        }

        public ICommand SetNowCommand { get; private set; }
        public ICommand PlanPreviewCommand { get; private set; }
        public ICommand PlanPreviewResultsCommand { get; private set; }
        public ICommand OpenHtmlReportCommand { get; private set; }

        private void LoadSchedulerPlans(DateTime atDateTime, IProfileService profileService) {
            /* While the caching here works and detects changes to the preview parameters (like date/time), it's not picking
             * up changes to the database.  For now just disable the caching ... doesn't take long to run anyway.

            if (SchedulerPlans != null) {
                return;
            }*/

            try {
                TSLogger.Debug($"running plan preview for {Utils.FormatDateTimeFull(atDateTime)}, profileId={SelectedProfileId}");

                SchedulerPlanLoader loader = new SchedulerPlanLoader(GetProfile(SelectedProfileId));
                List<IProject> projects = MarkForPreview(loader.LoadActiveProjects(database.GetContext()));
                ProfilePreference profilePreference = loader.GetProfilePreferences(database.GetContext());

                ObservableCollection<TreeViewItem> list = new ObservableCollection<TreeViewItem>();
                string profileName = ProfileChoices.First(p => p.Key == selectedProfileId).Value;

                if (projects == null) {
                    TSLogger.Debug($"no active projects for preview at {atDateTime}, profileId={SelectedProfileId}");
                    InstructionList = list;

                    MyMessageBox.Show($"No active projects/targets were returned by the planner for {Utils.FormatDateTimeFull(atDateTime)} and{Environment.NewLine}profile '{profileName}' - or no active targets were found with active exposure plans.", "Oops");
                    SchedulerPlans = null;
                    plannerReport = null;
                    chartDataList = null;
                    return;
                }

                PreviewPlanner previewPlanner = new PreviewPlanner();
                List<SchedulerPlan> schedulerPlans = previewPlanner.GetPlanPreview(atDateTime, profileService, profilePreference, projects);
                if (schedulerPlans.Count == 0) {
                    TSLogger.Debug($"no imagable projects for preview at {atDateTime}, profileId={SelectedProfileId}");
                    InstructionList = list;

                    MyMessageBox.Show($"No imagable projects/targets were returned by the planner for {Utils.FormatDateTimeFull(atDateTime)} and{Environment.NewLine}profile '{profileName}'.", "Oops");
                    SchedulerPlans = null;
                    plannerReport = null;
                    chartDataList = null;
                    return;
                }

                SchedulerPlans = schedulerPlans;
                plannerReport = previewPlanner.Report;

                // Build chart data (DeepSkyObject / NighttimeData) on the UI thread to match the original
                // chart construction and avoid creating the NighttimeData Ticker off the dispatcher thread.
                IProfile chartProfile = GetProfile(SelectedProfileId);
                _dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                    chartDataList = BuildChartDataList(chartProfile, new NighttimeCalculator(profileService));
                }));
                return;
            } catch (Exception ex) {
                TSLogger.Error($"failed to run plan preview: {ex.Message} {ex.StackTrace}");
                MyMessageBox.Show($"Exception running plan preview - see the TS log for details.", "Oops");
                SchedulerPlans = null;
                plannerReport = null;
                chartDataList = null;
                return;
            }
        }

        private List<IProject> MarkForPreview(List<IProject> projects) {
            if (Common.IsEmpty(projects)) return projects;

            projects.ForEach(p => {
                p.Targets.ForEach(t => { t.IsPreview = true; });
            });

            return projects;
        }

        private void SetPreviewTimeNow() {
            DateTime now = DateTime.Now;
            PlanDate = now.Date;
            PlanHours = now.Hour;
            PlanMinutes = now.Minute;
            PlanSeconds = now.Second;

            RaisePropertyChanged(nameof(PlanDate));
            RaisePropertyChanged(nameof(PlanHours));
            RaisePropertyChanged(nameof(PlanMinutes));
            RaisePropertyChanged(nameof(PlanSeconds));
        }

        private static Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        private void RunPlanPreview() {
            _ = ExecutePlanPreview();
        }

        private async Task<bool> ExecutePlanPreview() {
            return await Task.Run(() => {
                // Slight delay allows the UI thread to update the spinner property before the dispatcher
                // thread starts ... which seems to block the UI updates.
                TableLoading = true;
                ShowPlanPreviewResults = false;
                ShowPlanPreview = false;
                Thread.Sleep(50);

                if (PlanDate == DateTime.MinValue || SelectedProfileId == null) {
                    TableLoading = false;
                    return true;
                }

                DateTime atDateTime = PlanDate.Date.AddHours(PlanHours).AddMinutes(PlanMinutes).AddSeconds(PlanSeconds);
                LoadSchedulerPlans(atDateTime, profileService);

                if (SchedulerPlans == null || SchedulerPlans.Count == 0) {
                    TableLoading = false;
                    return true;
                }

                _dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                    ObservableCollection<TreeViewItem> list = new ObservableCollection<TreeViewItem>();

                    try {
                        int lastTargetId = -1;
                        string lastFilterName = null;
                        TreeViewItem planItem = null;
                        TreeViewItem lastItem = null;
                        SchedulerPlan lastTargetPlan = null;
                        int chartIndex = 0;
                        ProfilePreference profilePreference = GetProfilePreference(SelectedProfileId);

                        foreach (SchedulerPlan plan in SchedulerPlans) {
                            if (plan.IsWait) {
                                AddLastItemEndTime(lastItem, plan.StartTime);
                                planItem = new TreeViewItem();
                                planItem.Header = $"Wait until {Utils.FormatDateTimeFull(plan.WaitForNextTargetTime)}";
                                list.Add(planItem);
                                lastTargetId = -1;
                                lastItem = planItem;
                                continue;
                            }

                            if (plan.PlanTarget.DatabaseId != lastTargetId) {
                                AddLastItemEndTime(lastItem, plan.StartTime);
                                lastTargetId = plan.PlanTarget.DatabaseId;
                                planItem = new TreeViewItem();
                                planItem.Header = GetTargetLabel(plan);
                                planItem.IsExpanded = false;
                                list.Add(planItem);
                                lastItem = planItem;

                                if (chartDataList != null && chartIndex < chartDataList.Count) {
                                    AltitudeChart chart = AltitudeChart.Create(chartDataList[chartIndex++], 600, 200);
                                    planItem.Items.Add(new TreeViewItem { Header = chart, Focusable = false });
                                }
                            }

                            lastTargetPlan = plan;
                            foreach (IInstruction instruction in plan.PlanInstructions) {
                                TreeViewItem instructionItem = new TreeViewItem();

                                if (instruction is PlanMessage
                                    || instruction is PlanBeforeNewTargetContainer
                                    || instruction is PlanPostExposure) {
                                    continue;
                                }

                                if (instruction is PlanSlew) {
                                    if (profilePreference.EnableSlewCenter) {
                                        instructionItem.Header = GetSlewLabel(plan.PlanTarget, (PlanSlew)instruction);
                                        planItem.Items.Add(instructionItem);
                                    }
                                    continue;
                                }

                                if (instruction is PlanSwitchFilter) {
                                    string filterName = ((PlanSwitchFilter)instruction).exposure.FilterName;
                                    if (filterName != lastFilterName) {
                                        lastFilterName = filterName;
                                        instructionItem.Header = $"Switch Filter: {filterName}";
                                        planItem.Items.Add(instructionItem);
                                    }
                                    continue;
                                }

                                if (instruction is PlanSetReadoutMode) {
                                    int? readoutMode = ((PlanSetReadoutMode)instruction).exposure.ReadoutMode;
                                    if (readoutMode != null && readoutMode > 0) {
                                        instructionItem.Header = $"Set readout mode: {readoutMode}";
                                        planItem.Items.Add(instructionItem);
                                    }
                                    continue;
                                }

                                if (instruction is PlanTakeExposure) {
                                    instructionItem.Header = GetTakeExposureLabel((PlanTakeExposure)instruction);
                                    planItem.Items.Add(instructionItem);
                                    continue;
                                }

                                if (instruction is PlanDither) {
                                    instructionItem.Header = "Dither";
                                    planItem.Items.Add(instructionItem);
                                    continue;
                                }

                                TSLogger.Error($"unknown instruction type in plan preview: {instruction.GetType().FullName}");
                                throw new Exception($"unknown instruction type in plan preview: {instruction.GetType().FullName}");
                            }
                        }

                        AddLastItemEndTime(lastItem, lastTargetPlan?.EndTime);
                        if (lastTargetPlan != null) {
                            planItem = new TreeViewItem();
                            planItem.Header = $"End at {Utils.FormatDateTimeFull(lastTargetPlan.EndTime)}";
                            list.Add(planItem);
                        }

                        InstructionList = list;
                        ShowPlanPreviewResults = false;
                        ShowPlanPreview = true;
                    } catch (Exception ex) {
                        TSLogger.Error($"failed to run plan preview: {ex.Message} {ex.StackTrace}");
                        InstructionList.Clear();
                    }
                }));

                TableLoading = false;
                return true;
            });
        }

        private void AddLastItemEndTime(TreeViewItem lastItem, DateTime? endTime) {
            if (lastItem != null && endTime != null) {
                string header = lastItem.Header.ToString();
                if (!header.StartsWith("Wait")) {
                    lastItem.Header = lastItem.Header + $",  end: {Utils.FormatDateTimeFull(endTime)}";
                }
            }
        }

        private FrameworkElement reportContent;

        public FrameworkElement ReportContent {
            get => reportContent;
            set {
                reportContent = value;
                RaisePropertyChanged(nameof(ReportContent));
            }
        }

        private void RunPlanPreviewResults() {
            _ = ExecutePlanPreviewResults();
        }

        private async Task<bool> ExecutePlanPreviewResults() {
            return await Task.Run(() => {
                // Slight delay allows the UI thread to update the spinner property before the dispatcher
                // thread starts ... which seems to block the UI updates.
                TableLoading = true;
                ShowPlanPreviewResults = false;
                ShowPlanPreview = false;
                Thread.Sleep(50);

                if (PlanDate == DateTime.MinValue || SelectedProfileId == null) {
                    TableLoading = false;
                    return true;
                }

                DateTime atDateTime = PlanDate.Date.AddHours(PlanHours).AddMinutes(PlanMinutes).AddSeconds(PlanSeconds);
                LoadSchedulerPlans(atDateTime, profileService);

                if (SchedulerPlans == null || SchedulerPlans.Count == 0) {
                    TableLoading = false;
                    return true;
                }

                _dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                    try {
                        ReportContent = PlanPreviewReportBuilder.Build(plannerReport?.Model, chartDataList);
                        ShowPlanPreview = false;
                        ShowPlanPreviewResults = true;
                    } catch (Exception ex) {
                        TSLogger.Error($"failed to build plan preview report: {ex.Message} {ex.StackTrace}");
                        ReportContent = null;
                    }
                }));

                TableLoading = false;
                return true;
            });
        }

        private void OpenHtmlReport() {
            try {
                if (plannerReport == null) {
                    MyMessageBox.Show("Run a preview and view the report before opening the HTML version.", "Oops");
                    return;
                }

                List<string> chartSvgs = chartDataList?.Select(d => AltitudeSvgExporter.Export(d, 760, 220)).ToList();

                string path = Path.Combine(Path.GetTempPath(), $"TS-Planning-Report-{DateTime.Now:yyyyMMdd-HHmmss}.html");
                File.WriteAllText(path, plannerReport.GenerateHtml(chartSvgs), Encoding.UTF8);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            } catch (Exception ex) {
                TSLogger.Error($"failed to open HTML planner report: {ex.Message} {ex.StackTrace}");
                MyMessageBox.Show("Failed to open the HTML report - see the TS log for details.", "Oops");
            }
        }

        private AsyncObservableCollection<KeyValuePair<string, string>> GetProfileChoices() {
            Dictionary<string, string> profiles = new Dictionary<string, string>();
            profileService.Profiles.ForEach(p => {
                profiles.Add(p.Id.ToString(), p.Name);
            });

            AsyncObservableCollection<KeyValuePair<string, string>> profileChoices = new AsyncObservableCollection<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> entry in profiles) {
                profileChoices.Add(new KeyValuePair<string, string>(entry.Key, entry.Value));
            }

            return profileChoices;
        }

        private IProfile GetProfile(string profileId) {
            foreach (ProfileMeta profileMeta in profileService.Profiles) {
                if (profileMeta.Id.ToString() == profileId) {
                    return ProfileLoader.Load(profileService, profileMeta);
                }
            }

            TSLogger.Error($"failed to get profile for ID={profileId}");
            throw new Exception($"failed to get profile for ID={profileId}");
        }

        private ProfilePreference GetProfilePreference(string profileId) {
            using (var context = database.GetContext()) {
                return context.GetProfilePreference(profileId, true);
            }
        }

        /// <summary>
        /// Builds the per-target chart data for the current <see cref="SchedulerPlans"/>, aggregating
        /// consecutive plans for the same target into one block (matching how the report and the preview tree
        /// group targets).  The returned list aligns 1:1, in order, with the Target groups produced by
        /// <see cref="PlannerReportModel.BuildGroups"/>.
        /// </summary>
        private List<PlannerChartData> BuildChartDataList(IProfile profile, NighttimeCalculator nighttimeCalculator) {
            List<PlannerChartData> result = new List<PlannerChartData>();
            if (SchedulerPlans == null) {
                return result;
            }

            int lastTargetId = -1;
            PlannerChartData current = null;

            foreach (SchedulerPlan plan in SchedulerPlans) {
                if (plan.IsWait) {
                    lastTargetId = -1;
                    continue;
                }

                if (plan.PlanTarget.DatabaseId != lastTargetId) {
                    lastTargetId = plan.PlanTarget.DatabaseId;
                    DateTime referenceDate = NighttimeCalculator.GetReferenceDate(plan.StartTime);
                    current = new PlannerChartData {
                        Dso = BuildDso(profile, plan, referenceDate),
                        NighttimeData = nighttimeCalculator.Calculate(referenceDate),
                        ImagingStart = plan.StartTime,
                        ImagingStop = plan.EndTime
                    };
                    result.Add(current);
                }

                // Carry the stop line out to the end of the last segment, accumulating exposure bands.
                current.ImagingStop = plan.EndTime;
                AppendExposureRun(current.ExposureRuns, plan);
            }

            return result;
        }

        private DeepSkyObject BuildDso(IProfile profile, SchedulerPlan plan, DateTime referenceDate) {
            CustomHorizon customHorizon = GetCustomHorizon(profile, plan.PlanTarget.Project);

            DeepSkyObject dso = new DeepSkyObject(string.Empty, plan.PlanTarget.Coordinates, customHorizon);
            dso.Name = plan.PlanTarget.Name;
            dso.SetDateAndPosition(referenceDate, profile.AstrometrySettings.Latitude, profile.AstrometrySettings.Longitude);
            dso.Refresh();

            return dso;
        }

        private void AppendExposureRun(List<ExposureRun> runs, SchedulerPlan plan) {
            IExposure exposure = plan.PlanInstructions?.OfType<PlanTakeExposure>().FirstOrDefault()?.exposure;
            if (exposure == null) {
                return;
            }

            // Extend the current band while the same filter continues; otherwise start a new band.
            ExposureRun last = runs.Count > 0 ? runs[runs.Count - 1] : null;
            if (last != null && string.Equals(last.FilterName, exposure.FilterName, StringComparison.Ordinal)) {
                last.End = plan.EndTime;
            } else {
                runs.Add(new ExposureRun {
                    Start = plan.StartTime,
                    End = plan.EndTime,
                    FilterName = exposure.FilterName,
                    ExposureTemplateName = exposure.ExposureTemplateName,
                    Color = ExposureFilterColors.GetColor(exposure.ExposureTemplateName, exposure.FilterName)
                });
            }
        }

        private CustomHorizon GetCustomHorizon(IProfile profile, IProject project) {
            // Mirrors TargetSchedulerContainer: use the profile's custom horizon when the project opts in,
            // otherwise generate a constant horizon from the project's minimum altitude.
            return project.UseCustomHorizon && profile.AstrometrySettings.Horizon != null ?
                profile.AstrometrySettings.Horizon :
                HorizonDefinition.GetConstantHorizon(project.MinimumAltitude);
        }

        private string GetTargetLabel(SchedulerPlan plan) {
            string label = $"{plan.PlanTarget.Project.Name} / {plan.PlanTarget.Name}";
            return $"{label} - start: {Utils.FormatDateTimeFull(plan.StartTime)}";
        }

        private string GetSlewLabel(ITarget planTarget, PlanSlew planSlew) {
            string name = "Slew";
            string rotate = $", Rotate: {planTarget.Rotation}°";

            if (planSlew.center) {
                name = "Slew/Rotate/Center";
            }

            return $"{name}: {planTarget.Coordinates.RAString} {planTarget.Coordinates.DecString}{rotate}";
        }

        private string GetTakeExposureLabel(PlanTakeExposure instruction) {
            IExposure planExposure = instruction.exposure;
            StringBuilder sb = new StringBuilder();
            sb.Append("Take Exposure:");
            sb.Append($" {planExposure.ExposureLength} secs, ");
            sb.Append($" Gain={CameraDefault(planExposure.Gain)}, ");
            sb.Append($" Offset={CameraDefault(planExposure.Offset)}, ");
            sb.Append($" Binning={planExposure.BinningMode}");

            return sb.ToString();
        }

        private string CameraDefault(int? value) {
            return value != null ? value.ToString() : "(camera)";
        }
    }
}