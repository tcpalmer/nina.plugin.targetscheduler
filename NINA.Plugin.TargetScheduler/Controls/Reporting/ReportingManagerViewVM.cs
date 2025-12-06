using LinqKit;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Controls.AcquiredImages;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.Reporting {

    public class ReportingManagerViewVM : BaseVM {
        private const string PROFILE_ANY = "XANY";
        private SchedulerDatabaseInteraction database;

        public ReportingManagerViewVM(IProfileService profileService) : base(profileService) {
            database = new SchedulerDatabaseInteraction();
            RefreshTableCommand = new RelayCommand(RefreshTable);
            InitializeCriteria();

            ReportRowCollection = new ReportRowCollection();
            ItemsView = CollectionViewSource.GetDefaultView(ReportRowCollection);
            ItemsView.SortDescriptions.Clear();
            ItemsView.SortDescriptions.Add(new SortDescription("AcquiredDate", ListSortDirection.Descending));
        }

        private void InitializeCriteria() {
            SearchCriteraKey = null;
            selectedProfileId = PROFILE_ANY;
            selectedProjectId = 0;
            selectedTargetId = 0;

            ProfileChoices = GetProfileChoices();
            ProjectChoices = GetProjectChoices(SelectedProfileId);
            TargetChoices = GetTargetChoices(SelectedProjectId);
        }

        private bool tableLoading = false;

        public bool TableLoading {
            get => tableLoading;
            set {
                tableLoading = value;
                RaisePropertyChanged(nameof(TableLoading));
            }
        }

        private ICollectionView itemsView;
        public ICollectionView ItemsView { get => itemsView; set { itemsView = value; } }

        private AsyncObservableCollection<KeyValuePair<string, string>> profileChoices;

        public AsyncObservableCollection<KeyValuePair<string, string>> ProfileChoices {
            get {
                return profileChoices;
            }
            set {
                profileChoices = value;
                RaisePropertyChanged(nameof(ProfileChoices));
            }
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> projectChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> ProjectChoices {
            get {
                return projectChoices;
            }
            set {
                projectChoices = value;
                RaisePropertyChanged(nameof(ProjectChoices));
            }
        }

        private string selectedProfileId = PROFILE_ANY;

        public string SelectedProfileId {
            get => selectedProfileId;
            set {
                selectedProfileId = value;
                selectedProjectId = 0;
                selectedTargetId = 0;

                ProjectChoices = GetProjectChoices(selectedProfileId);
                TargetChoices = GetTargetChoices(selectedProjectId);

                RaisePropertyChanged(nameof(SelectedProfileId));
                RaisePropertyChanged(nameof(SelectedProjectId));
                RaisePropertyChanged(nameof(SelectedTargetId));
            }
        }

        private int selectedProjectId = 0;

        public int SelectedProjectId {
            get => selectedProjectId;
            set {
                selectedProjectId = value;
                selectedTargetId = 0;

                TargetChoices = GetTargetChoices(selectedProjectId);

                RaisePropertyChanged(nameof(SelectedProjectId));
                RaisePropertyChanged(nameof(SelectedTargetId));
            }
        }

        private AsyncObservableCollection<KeyValuePair<string, string>> GetProfileChoices() {
            AsyncObservableCollection<KeyValuePair<string, string>> choices = new AsyncObservableCollection<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>(PROFILE_ANY, "Select")
            };

            profileService.Profiles.OrderBy(p => p.Name).ForEach(p => {
                choices.Add(new KeyValuePair<string, string>(p.Id.ToString(), p.Name));
            });

            return choices;
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> GetProjectChoices(string profileId) {
            AsyncObservableCollection<KeyValuePair<int, string>> projectChoices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(0, "Select")
            };

            List<Project> projects;
            using (var context = database.GetContext()) {
                projects = context.ProjectSet.AsNoTracking().OrderBy(p => p.name).ToList();
            }

            if (profileId != PROFILE_ANY) {
                projects.RemoveAll(p => p.ProfileId != profileId);
            }

            Dictionary<Project, string> dict = new Dictionary<Project, string>();
            projects.ForEach(p => { dict.Add(p, p.Name); });

            foreach (KeyValuePair<Project, string> entry in dict) {
                projectChoices.Add(new KeyValuePair<int, string>(entry.Key.Id, entry.Value));
            }

            return projectChoices;
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> GetTargetChoices(int selectedProjectId) {
            List<Target> targets;
            AsyncObservableCollection<KeyValuePair<int, string>> choices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(0, "...")
            };

            if (selectedProjectId == 0) {
                return choices;
            }

            using (var context = database.GetContext()) {
                targets = context.TargetSet.AsNoTracking().Where(t => t.ProjectId == selectedProjectId).ToList();
            }

            targets.OrderBy(t => t.Name).ForEach(t => {
                choices.Add(new KeyValuePair<int, string>(t.Id, t.Name));
            });

            return choices;
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> targetChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> TargetChoices {
            get {
                return targetChoices;
            }
            set {
                targetChoices = value;
                RaisePropertyChanged(nameof(TargetChoices));
            }
        }

        private int selectedTargetId = 0;

        public int SelectedTargetId {
            get => selectedTargetId;
            set {
                selectedTargetId = value;
                SelectedTarget = selectedTargetId != 0 ? GetTarget(selectedTargetId) : null;
                RaisePropertyChanged(nameof(SelectedTargetId));
                _ = LoadRecords();
            }
        }

        private Target selectedTarget = null;
        public Target SelectedTarget { get => selectedTarget; set => selectedTarget = value; }

        private Target GetTarget(int selectedTargetId) {
            using (var context = database.GetContext()) {
                Target t = context.GetTargetOnly(selectedTargetId);
                t = context.GetTarget(t.ProjectId, selectedTargetId);
                t.Project = context.GetProject(t.ProjectId);
                return t;
            }
        }

        public ICommand RefreshTableCommand { get; private set; }

        private void RefreshTable() {
            SelectedTargetId = 0;
            SelectedProjectId = 0;
            InitializeCriteria();
            _ = LoadRecords();
        }

        private ReportRowCollection reportRowCollection;

        public ReportRowCollection ReportRowCollection {
            get => reportRowCollection;
            set {
                reportRowCollection = value;
                RaisePropertyChanged(nameof(ReportRowCollection));
            }
        }

        private ReportTableSummary reportTableSummary;

        public ReportTableSummary ReportTableSummary {
            get => reportTableSummary;
            set {
                reportTableSummary = value;
                RaisePropertyChanged(nameof(ReportTableSummary));
            }
        }

        private static Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        private async Task<bool> LoadRecords() {
            return await Task.Run(() => {
                if (ReportRowCollection == null || SelectedTargetId == 0) {
                    ReportTableSummary = new ReportTableSummary();
                    _dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                        ReportRowCollection.Clear();
                        ReportRowCollection.AddRange(new List<ReportRowVM>());
                    }));

                    RaisePropertyChanged(nameof(ReportRowCollection));
                    RaisePropertyChanged(nameof(ItemsView));
                    return true;
                }

                string newSearchCriteraKey = GetSearchCriteraKey();
                if (newSearchCriteraKey == SearchCriteraKey) {
                    return true;
                }

                // Slight delay allows the UI thread to update the spinner property before the dispatcher
                // thread starts ... which seems to block the UI updates.
                TableLoading = true;
                Thread.Sleep(50);

                try {
                    SearchCriteraKey = newSearchCriteraKey;

                    List<AcquiredImage> acquiredImages;
                    using (var context = database.GetContext()) {
                        acquiredImages = context.AcquiredImageSet
                        .AsNoTracking()
                        .AsExpandable()
                        .Where(ai => ai.TargetId == SelectedTargetId)
                        .ToList();
                    }

                    // Create an intermediate list so we can add it to the display collection via AddRange while suppressing notifications
                    List<ReportRowVM> reportRowVMs = new List<ReportRowVM>(acquiredImages.Count);
                    acquiredImages.ForEach(ai => { reportRowVMs.Add(new ReportRowVM(database, ai)); });
                    ReportTableSummary = new ReportTableSummary(acquiredImages, SelectedTarget.Project.Name, SelectedTarget.Name);

                    _dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                        ReportRowCollection.Clear();
                        ReportRowCollection.AddRange(reportRowVMs);
                    }));
                } catch (Exception ex) {
                    TSLogger.Error($"exception loading acquired images: {ex.Message} {ex.StackTrace}");
                } finally {
                    RaisePropertyChanged(nameof(ReportRowCollection));
                    RaisePropertyChanged(nameof(ItemsView));
                    TableLoading = false;
                }

                return true;
            });
        }

        private string SearchCriteraKey;

        private string GetSearchCriteraKey() {
            return $"{SelectedProjectId}_{SelectedTargetId}";
        }
    }

    public class ReportTableSummary {
        public List<SummaryRow> AcquisitionSummary { get; private set; }
        public string Title { get; private set; }
        public string DateRange { get; private set; }
        public string StarsRange { get; private set; }
        public string HFRRange { get; private set; }
        public string FWHMRange { get; private set; }
        public string EccentricityRange { get; private set; }

        public ReportTableSummary(List<AcquiredImage> acquiredImages, string projectName, string targetName) {
            Title = $"{projectName} / {targetName}";

            if (Common.IsEmpty(acquiredImages)) return;

            AcquisitionSummary = new List<SummaryRow>();
            new TargetAcquisitionSummary(acquiredImages).Rows.ForEach(row => { AcquisitionSummary.Add(new SummaryRow(row)); });

            DateTime minDate = DateTime.MaxValue;
            acquiredImages.ForEach(x => { if (x.AcquiredDate < minDate) minDate = x.AcquiredDate; });
            DateTime maxDate = DateTime.MinValue;
            acquiredImages.ForEach(x => { if (x.AcquiredDate > maxDate) maxDate = x.AcquiredDate; });
            DateRange = $"{Utils.FormatDateTimeFull(minDate)}  to  {Utils.FormatDateTimeFull(maxDate)}";

            // TODO: should do fixed formatting so they line up

            List<double> samples = GetSamples(acquiredImages, i => { return i.Metadata.DetectedStars; });
            StarsRange = GetRange(samples);

            samples = GetSamples(acquiredImages, i => { return i.Metadata.HFR; });
            HFRRange = GetRange(samples);

            samples = GetSamples(acquiredImages, i => { return i.Metadata.FWHM; });
            FWHMRange = GetRange(samples);

            samples = GetSamples(acquiredImages, i => { return i.Metadata.Eccentricity; });
            EccentricityRange = GetRange(samples);
        }

        public ReportTableSummary() {
        }

        private List<double> GetSamples(List<AcquiredImage> list, Func<AcquiredImage, double> Sample) {
            List<double> samples = new List<double>();
            list.ForEach(i => {
                double sample = Sample(i);
                if (sample > 0) samples.Add(sample);
            });

            return samples;
        }

        private string GetRange(List<double> samples) {
            if (samples.Count == 0) return "n/a";

            double min = samples.Min();
            double max = samples.Max();
            return min == 0 && max == 0 ? "n/a" : $"{Utils.FormatDbl(min)} - {Utils.FormatDbl(max)}";
        }
    }

    public class ReportRowCollection : RangeObservableCollection<ReportRowVM> { }

    public class ReportRowVM : BaseINPC {
        public DateTime AcquiredDate { get; }
        public string FilterName { get; }
        public string ExposureDuration { get; }
        public string GradingStatus { get; }
        public string RejectReason { get; }

        public int DetectedStars { get; }
        public string HFR { get; }
        public string FWHM { get; }
        public string Eccentricity { get; }
        public string GuidingRMS { get; }

        public ImageData ImageData { get; }
        public ImageSource Thumbnail { get; }

        public int ThumbnailWidth { get => ImageData != null ? ImageData.Width : 0; }
        public int ThumbnailHeight { get => ImageData != null ? ImageData.Height : 0; }

        public ReportRowVM() {
        }

        public ReportRowVM(SchedulerDatabaseInteraction database, AcquiredImage acquiredImage) {
            AcquiredDate = acquiredImage.AcquiredDate;
            FilterName = acquiredImage.FilterName;
            ExposureDuration = Utils.FormatDbl(acquiredImage.Metadata.ExposureDuration);
            GradingStatus = acquiredImage.GradingStatus.ToString();
            RejectReason = acquiredImage.RejectReason;
            DetectedStars = acquiredImage.Metadata.DetectedStars;
            HFR = Utils.FormatDbl(acquiredImage.Metadata.HFR);
            FWHM = Utils.FormatHF(acquiredImage.Metadata.FWHM);
            Eccentricity = Utils.FormatHF(acquiredImage.Metadata.Eccentricity);
            GuidingRMS = Utils.FormatDbl(acquiredImage.Metadata.GuidingRMS);

            using (var context = database.GetContext()) {
                ImageData = context.GetImageData(acquiredImage.Id);
                if (ImageData != null) {
                    Thumbnail = Thumbnails.RestoreThumbnail(ImageData.Data);
                }
            }
        }
    }

    public class SummaryRow {
        private TargetAcquisitionSummaryRow row;

        public string RowName { get { return row.Key; } }
        public int Exposures { get { return row.Exposures; } }
        public string Total { get { return Display(row.TotalTime); } }
        public string Accepted { get { return Display(row.AcceptedTime); } }
        public string Rejected { get { return Display(row.RejectedTime); } }
        public string Pending { get { return Display(row.PendingTime); } }

        public SummaryRow(TargetAcquisitionSummaryRow row) {
            this.row = row;
        }

        private string Display(int seconds) {
            return seconds > 0 ? Utils.StoHMS(seconds) : "         -  ";
        }
    }
}