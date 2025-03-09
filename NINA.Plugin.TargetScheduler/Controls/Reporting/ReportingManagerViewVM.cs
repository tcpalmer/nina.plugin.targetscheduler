using CommunityToolkit.Mvvm.Input;
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

namespace NINA.Plugin.TargetScheduler.Controls.Reporting {

    public class ReportingManagerViewVM : BaseVM {
        private SchedulerDatabaseInteraction database;

        public ReportingManagerViewVM(IProfileService profileService) : base(profileService) {
            database = new SchedulerDatabaseInteraction();
            RefreshTableCommand = new AsyncRelayCommand(RefreshTable);
            InitializeCriteria();

            ReportRowCollection = new ReportRowCollection();
            ItemsView = CollectionViewSource.GetDefaultView(ReportRowCollection);
            ItemsView.SortDescriptions.Clear();
            ItemsView.SortDescriptions.Add(new SortDescription("AcquiredDate", ListSortDirection.Descending));
        }

        private void InitializeCriteria() {
            SearchCriteraKey = null;
            selectedTargetId = 0;
            selectedTarget = null;
            TargetChoices = GetTargetChoices();
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

        private AsyncObservableCollection<KeyValuePair<int, string>> GetTargetChoices() {
            Dictionary<int, string> scratch = new Dictionary<int, string>();

            using (var context = database.GetContext()) {
                List<Project> projects = context.GetAllProjects();
                projects.ForEach(p => {
                    p.Targets.ForEach(t => {
                        scratch.Add(t.Id, $"{t.Project.Name} / {t.Name}");
                    });
                });
            }

            AsyncObservableCollection<KeyValuePair<int, string>> choices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(0, "Select")
            };

            var sorted = from entry in scratch orderby entry.Value ascending select entry;
            sorted.ForEach(p => choices.Add(p));
            return choices;
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> targetChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> TargetChoices {
            get => targetChoices;
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

        private Target selectedTarget;
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

        private async Task<bool> RefreshTable() {
            InitializeCriteria();
            RaisePropertyChanged(nameof(SelectedTargetId));
            await LoadRecords();
            return true;
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

        /* TODO:
         *   - Additional summary - sounds like a table
         *
         *                           Total         Accepted      Rejected       Pending
         *                All:        2h0m            1h32m
         *                Lum:        0h8m             0h7m
         *                Red:        0h8m             0h7m
         *
         *   Should pull exposure duration from AI, not from EP(ET) which could change
         *
         *   To fit this, may want to reorg:
         *   - When you select a target, generate new table plus existing with Filter=Any
         *   - The Filter dropdown moves below the table and drives existing summary and AI rows
         *
         *
*/

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
            return $"{SelectedTargetId}";
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
            StarsRange = $"{samples.Min()} - {samples.Max()}";

            samples = GetSamples(acquiredImages, i => { return i.Metadata.HFR; });
            HFRRange = $"{Utils.FormatDbl(samples.Min())} - {Utils.FormatDbl(samples.Max())}";

            samples = GetSamples(acquiredImages, i => { return i.Metadata.FWHM; });
            double min = samples.Min(); double max = samples.Max();
            FWHMRange = min > 0 && max > 0 ? $"{Utils.FormatDbl(min)} - {Utils.FormatDbl(max)}" : "n/a";

            samples = GetSamples(acquiredImages, i => { return i.Metadata.Eccentricity; });
            min = samples.Min(); max = samples.Max();
            EccentricityRange = min > 0 && max > 0 ? $"{Utils.FormatDbl(min)} - {Utils.FormatDbl(max)}" : "n/a";
        }

        public ReportTableSummary() {
        }

        private List<double> GetSamples(List<AcquiredImage> list, Func<AcquiredImage, double> Sample) {
            List<double> samples = new List<double>();
            list.ForEach(i => samples.Add(Sample(i)));
            return samples;
        }
    }

    public class ReportRowCollection : RangeObservableCollection<ReportRowVM> { }

    public class ReportRowVM {
        private AcquiredImage acquiredImage;
        private SchedulerDatabaseInteraction database;

        public ReportRowVM(SchedulerDatabaseInteraction database, AcquiredImage acquiredImage) {
            this.acquiredImage = acquiredImage;
            this.database = database;
        }

        public DateTime AcquiredDate { get { return acquiredImage.AcquiredDate; } }
        public string FilterName { get { return acquiredImage.FilterName; } }
        public string ExposureDuration { get { return Utils.FormatDbl(acquiredImage.Metadata.ExposureDuration); } }
        public string GradingStatus { get { return acquiredImage.GradingStatus.ToString(); } }
        public string RejectReason { get { return acquiredImage.RejectReason; } }

        public string DetectedStars { get { return Utils.FormatInt(acquiredImage.Metadata.DetectedStars); } }
        public string HFR { get { return Utils.FormatDbl(acquiredImage.Metadata.HFR); } }
        public string FWHM { get { return Utils.FormatHF(acquiredImage.Metadata.FWHM); } }
        public string Eccentricity { get { return Utils.FormatHF(acquiredImage.Metadata.Eccentricity); } }
        public string GuidingRMS { get { return Utils.FormatDbl(acquiredImage.Metadata.GuidingRMS); } }

        private ImageData imageData;

        public ImageData ImageData {
            get {
                if (imageData == null) {
                    using (var context = database.GetContext()) {
                        imageData = context.GetImageData(acquiredImage.Id);
                    }
                }

                return imageData;
            }
        }

        private ImageSource thumbnail;

        public ImageSource Thumbnail {
            get {
                if (thumbnail == null) {
                    thumbnail = ImageData != null
                            ? Thumbnails.RestoreThumbnail(imageData.Data)
                            : null;
                }

                return thumbnail;
            }
        }

        public int ThumbnailWidth { get => ImageData != null ? ImageData.Width : 0; }
        public int ThumbnailHeight { get => ImageData != null ? ImageData.Height : 0; }
    }

    public class SummaryRow {
        private TargetAcquisitionSummaryRow row;

        public string RowName { get { return row.Key; } }
        public int Exposures { get { return row.Exposures; } }
        public string Total { get { return Utils.StoHMS(row.TotalTime); } }
        public string Accepted { get { return Utils.StoHMS(row.AcceptedTime); } }
        public string Rejected { get { return Utils.StoHMS(row.RejectedTime); } }
        public string Pending { get { return Utils.StoHMS(row.PendingTime); } }

        public SummaryRow(TargetAcquisitionSummaryRow row) {
            this.row = row;
        }
    }
}