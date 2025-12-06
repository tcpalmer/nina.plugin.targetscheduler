using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using LinqKit;
using Microsoft.WindowsAPICodePack.Dialogs;
using NINA.Core.Locale;
using NINA.Core.MyMessageBox;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RelayCommandParam = CommunityToolkit.Mvvm.Input.RelayCommand<object>;

namespace NINA.Plugin.TargetScheduler.Controls.AcquiredImages {

    public class AcquiredImagesManagerViewVM : BaseVM {
        private SchedulerDatabaseInteraction database;

        public AcquiredImagesManagerViewVM(IProfileService profileService) : base(profileService) {
            this.profileService = profileService;
            database = new SchedulerDatabaseInteraction();

            RefreshTableCommand = new AsyncRelayCommand(RefreshTable);
            CsvOutputCommand = new AsyncRelayCommand(CsvOutput);
            PurgeCommand = new AsyncRelayCommand(PurgeRecords);
            PurgeTargetChoices = GetPurgeTargetChoices();

            InitializeCriteria();

            AcquiredImageCollection = new AcquiredImageCollection();
            ItemsView = CollectionViewSource.GetDefaultView(AcquiredImageCollection);
            ItemsView.SortDescriptions.Clear();
            ItemsView.SortDescriptions.Add(new SortDescription("AcquiredDate", ListSortDirection.Descending));

            _ = LoadRecords();
        }

        private const string PROFILE_ANY = "XANY";
        private static readonly int FIXED_DATE_RANGE_OFF = 0;
        private static readonly int FIXED_DATE_RANGE_DEFAULT = 2;

        private void InitializeCriteria() {
            FixedDateRangeChoices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(FIXED_DATE_RANGE_OFF, ""),
                new KeyValuePair<int, string>(1, "Today"),
                new KeyValuePair<int, string>(2, "Last 2 Days"),
                new KeyValuePair<int, string>(7, "Last 7 Days"),
                new KeyValuePair<int, string>(30, "Last 30 Days"),
                new KeyValuePair<int, string>(60, "Last 60 Days"),
                new KeyValuePair<int, string>(90, "Last 90 Days"),
                new KeyValuePair<int, string>(180, "Last 180 Days"),
                new KeyValuePair<int, string>(365, "Last Year")
            };

            // Setting like this allows for initial combo selection
            SelectedFixedDateRange = FIXED_DATE_RANGE_DEFAULT;
            selectedFixedDateRange = FIXED_DATE_RANGE_DEFAULT;

            ProfileChoices = GetProfileChoices();
            ProjectChoices = GetProjectChoices();
            TargetChoices = GetTargetChoices(SelectedProjectId);
            FilterChoices = GetFilterChoices(SelectedTargetId);
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

        public ICollectionView ItemsView {
            get => itemsView;
            set {
                itemsView = value;
            }
        }

        private DateTime fromDate = DateTime.MinValue;

        public DateTime FromDate {
            get => fromDate;
            set {
                fromDate = value.Date;
                SelectedFixedDateRange = FIXED_DATE_RANGE_OFF;
                RaisePropertyChanged(nameof(FromDate));
                _ = LoadRecords();
            }
        }

        private DateTime toDate = DateTime.MinValue;

        public DateTime ToDate {
            get => toDate;
            set {
                toDate = value.AddDays(1).Date.AddSeconds(-1);
                SelectedFixedDateRange = FIXED_DATE_RANGE_OFF;
                RaisePropertyChanged(nameof(ToDate));
                _ = LoadRecords();
            }
        }

        private int selectedFixedDateRange;

        public int SelectedFixedDateRange {
            get => selectedFixedDateRange;
            set {
                selectedFixedDateRange = value;
                RaisePropertyChanged(nameof(SelectedFixedDateRange));

                if (selectedFixedDateRange != FIXED_DATE_RANGE_OFF) {
                    fromDate = DateTime.Now.Date.AddDays((-1 * selectedFixedDateRange) + 1);
                    toDate = DateTime.Now.Date.AddDays(1).Date.AddSeconds(-1);
                    RaisePropertyChanged(nameof(FromDate));
                    RaisePropertyChanged(nameof(ToDate));
                    _ = LoadRecords();
                }
            }
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> fixedDateRangeChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> FixedDateRangeChoices {
            get {
                return fixedDateRangeChoices;
            }
            set {
                fixedDateRangeChoices = value;
                RaisePropertyChanged(nameof(FixedDateRangeChoices));
            }
        }

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

        private string selectedProfileId = PROFILE_ANY;

        public string SelectedProfileId {
            get => selectedProfileId;
            set {
                selectedProfileId = value;
                selectedProjectId = 0;
                selectedTargetId = 0;
                selectedFilterId = 0;

                ProjectChoices = GetProjectChoices(selectedProfileId);
                TargetChoices = GetTargetChoices(selectedProjectId);
                FilterChoices = GetFilterChoices(selectedTargetId);

                RaisePropertyChanged(nameof(SelectedProfileId));
                RaisePropertyChanged(nameof(SelectedProjectId));
                RaisePropertyChanged(nameof(SelectedTargetId));
                RaisePropertyChanged(nameof(SelectedFilterId));

                _ = LoadRecords();
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

        private int selectedProjectId = 0;

        public int SelectedProjectId {
            get => selectedProjectId;
            set {
                selectedProjectId = value;
                selectedTargetId = 0;
                selectedFilterId = 0;

                TargetChoices = GetTargetChoices(selectedProjectId);
                RaisePropertyChanged(nameof(SelectedProjectId));

                _ = LoadRecords();
            }
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> GetTargetChoices(int selectedProjectId) {
            AsyncObservableCollection<KeyValuePair<int, string>> choices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(0, Loc.Instance["LblAny"])
            };

            if (selectedProjectId == 0) {
                return choices;
            }

            List<Target> targets;
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
                selectedFilterId = 0;

                FilterChoices = GetFilterChoices(selectedTargetId);
                RaisePropertyChanged(nameof(SelectedTargetId));
                _ = LoadRecords();
            }
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> GetFilterChoices(int selectedTargetId) {
            List<ExposurePlan> exposurePlans;
            List<ExposureTemplate> exposureTemplates;

            AsyncObservableCollection<KeyValuePair<int, string>> choices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(0, Loc.Instance["LblAny"])
            };

            if (SelectedProjectId == 0 || selectedTargetId == 0) {
                return choices;
            }

            using (var context = database.GetContext()) {
                Target t = context.GetTarget(SelectedProjectId, selectedTargetId);
                exposureTemplates = GetExposureTemplates();
                exposurePlans = t.ExposurePlans;
            }

            exposurePlans.ForEach(ep => {
                ExposureTemplate et = exposureTemplates.Where(et => et.Id == ep.ExposureTemplate.Id).FirstOrDefault();
                choices.Add(new KeyValuePair<int, string>(et.Id, et.FilterName));
            });

            return choices;
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> filterChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> FilterChoices {
            get {
                return filterChoices;
            }
            set {
                filterChoices = value;
                RaisePropertyChanged(nameof(FilterChoices));
            }
        }

        private int selectedFilterId = 0;

        public int SelectedFilterId {
            get => selectedFilterId;
            set {
                selectedFilterId = value;
                RaisePropertyChanged(nameof(SelectedFilterId));
                _ = LoadRecords();
            }
        }

        private DateTime purgeOlderThanDate = DateTime.Now.AddMonths(-9);

        public DateTime PurgeOlderThanDate {
            get => purgeOlderThanDate;
            set {
                purgeOlderThanDate = value.Date;
                RaisePropertyChanged(nameof(PurgeOlderThanDate));
            }
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> GetPurgeTargetChoices() {
            List<Target> targets;
            AsyncObservableCollection<KeyValuePair<int, string>> choices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(0, "All")
            };

            using (var context = database.GetContext()) {
                targets = context.TargetSet.AsNoTracking().ToList();
            }

            targets.Sort((t1, t2) => t1.Name.CompareTo(t2.Name));
            targets.ForEach(t => {
                choices.Add(new KeyValuePair<int, string>(t.Id, t.Name));
            });

            return choices;
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> purgeTargetChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> PurgeTargetChoices {
            get {
                return purgeTargetChoices;
            }
            set {
                purgeTargetChoices = value;
                RaisePropertyChanged(nameof(PurgeTargetChoices));
            }
        }

        private int purgeSelectedTargetId = 0;

        public int PurgeSelectedTargetId {
            get => purgeSelectedTargetId;
            set {
                purgeSelectedTargetId = value;
                RaisePropertyChanged(nameof(PurgeSelectedTargetId));
            }
        }

        public ICommand RefreshTableCommand { get; private set; }

        private async Task<bool> RefreshTable() {
            SearchCriteraKey = null;
            InitializeCriteria();
            await LoadRecords();
            return true;
        }

        public ICommand CsvOutputCommand { get; private set; }

        private async Task<bool> CsvOutput() {
            if (AcquiredImageCollection.Count == 0) {
                MyMessageBox.Show("No records selected for CSV output");
                return true;
            }

            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.Title = "Select CSV Output File";

            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok) {
                string fileName = dialog.FileName;
                string ext = Path.GetExtension(fileName);
                if (ext != ".csv" && ext != ".CSV") {
                    fileName = $"{fileName}.csv";
                }

                if (File.Exists(fileName)) {
                    if (MyMessageBox.Show($"File {fileName} exists, overwrite?", "Overwrite?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                        try { File.Delete(fileName); } catch (Exception e) {
                            TSLogger.Error($"failed to remove existing CSV file {fileName}: {e.Message}");
                            return false;
                        }
                    } else {
                        return true;
                    }
                }

                try {
                    using (var writer = File.AppendText(fileName))
                    using (var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture)) {
                        csv.WriteHeader<CsvAcquiredImage>();
                        csv.NextRecord();
                        foreach (var record in AcquiredImageCollection) {
                            csv.WriteRecord(new CsvAcquiredImage(record));
                            csv.NextRecord();
                        }
                    }
                } catch (Exception e) {
                    TSLogger.Error($"failed to write CSV file {fileName}: {e.Message}");
                    return false;
                }

                TSLogger.Info($"wrote CSV file: {fileName}");
            }

            return true;
        }

        public ICommand PurgeCommand { get; private set; }

        private async Task<bool> PurgeRecords() {
            using (var context = database.GetContext()) {
                int count = context.GetAcquiredImagesCount(PurgeOlderThanDate, PurgeSelectedTargetId);
                if (count == 0) {
                    MyMessageBox.Show("No records selected for deletion");
                    return true;
                }

                if (MyMessageBox.Show($"Delete {count} acquired image records?", "Delete records?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                    TSLogger.Info($"deleting {count} acquired images records");
                    context.DeleteAcquiredImages(PurgeOlderThanDate, PurgeSelectedTargetId);
                    SearchCriteraKey = null;
                    _ = LoadRecords();
                }

                return true;
            }
        }

        private AcquiredImageCollection acquiredImageCollection;

        public AcquiredImageCollection AcquiredImageCollection {
            get => acquiredImageCollection;
            set {
                acquiredImageCollection = value;
                RaisePropertyChanged(nameof(AcquiredImageCollection));
            }
        }

        private static Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        private async Task<bool> LoadRecords() {
            return await Task.Run(() => {
                if (AcquiredImageCollection == null || FromDate == DateTime.MinValue || ToDate == DateTime.MinValue) {
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
                    var predicate = PredicateBuilder.New<AcquiredImage>();

                    long from = Common.DateTimeToUnixSeconds(FromDate);
                    long to = Common.DateTimeToUnixSeconds(ToDate);
                    predicate = predicate.And(a => a.acquiredDate >= from);
                    predicate = predicate.And(a => a.acquiredDate <= to);

                    if (!SelectedProfileId.Equals(PROFILE_ANY)) {
                        predicate = predicate.And(a => a.profileId.Equals(SelectedProfileId));
                    }

                    if (SelectedProjectId != 0) {
                        predicate = predicate.And(a => a.ProjectId == SelectedProjectId);
                    }

                    if (SelectedTargetId != 0) {
                        predicate = predicate.And(a => a.TargetId == SelectedTargetId);
                    }

                    if (SelectedFilterId != 0) {
                        List<ExposureTemplate> exposureTemplates = GetExposureTemplates();
                        ExposureTemplate exposureTemplate = exposureTemplates.Where(et => et.Id == SelectedFilterId).FirstOrDefault();
                        predicate = predicate.And(a => a.FilterName == exposureTemplate.FilterName);
                    }

                    List<AcquiredImage> acquiredImages;
                    using (var context = database.GetContext()) {
                        acquiredImages = context.AcquiredImageSet.AsNoTracking().AsExpandable().Where(predicate).ToList();
                    }

                    // Create an intermediate list so we can add it to the display collection via AddRange while suppressing notifications
                    List<AcquiredImageVM> acquiredImageVMs = new List<AcquiredImageVM>(acquiredImages.Count);
                    acquiredImages.ForEach(a => { acquiredImageVMs.Add(new AcquiredImageVM(a, profileService)); });

                    _dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                        AcquiredImageCollection.Clear();
                        AcquiredImageCollection.AddRange(acquiredImageVMs);
                    }));
                } catch (Exception ex) {
                    TSLogger.Error($"exception loading acquired images: {ex.Message} {ex.StackTrace}");
                } finally {
                    RaisePropertyChanged(nameof(AcquiredImageCollection));
                    RaisePropertyChanged(nameof(ItemsView));
                    TableLoading = false;
                }

                return true;
            });
        }

        private string SearchCriteraKey;

        private string GetSearchCriteraKey() {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{FromDate:yyyy-MM-dd}_{ToDate:yyyy-MM-dd}_");
            sb.Append($"{SelectedProfileId}_{SelectedProjectId}_{SelectedTargetId}_{SelectedFilterId}");
            return sb.ToString();
        }

        private AsyncObservableCollection<KeyValuePair<string, string>> GetProfileChoices() {
            AsyncObservableCollection<KeyValuePair<string, string>> choices = new AsyncObservableCollection<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>(PROFILE_ANY, Loc.Instance["LblAny"])
            };

            profileService.Profiles.OrderBy(p => p.Name).ForEach(p => {
                choices.Add(new KeyValuePair<string, string>(p.Id.ToString(), p.Name));
            });

            return choices;
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> GetProjectChoices(string profileId = PROFILE_ANY) {
            List<Project> projects;
            using (var context = database.GetContext()) {
                projects = context.ProjectSet
                    .AsNoTracking()
                    .OrderBy(p => p.name).ToList();
            }

            if (profileId != PROFILE_ANY) {
                projects.RemoveAll(p => p.ProfileId != profileId);
            }

            AsyncObservableCollection<KeyValuePair<int, string>> choices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(0, Loc.Instance["LblAny"])
            };

            projects.ForEach(p => {
                choices.Add(new KeyValuePair<int, string>(p.Id, p.Name));
            });

            return choices;
        }

        private List<ExposureTemplate> GetExposureTemplates() {
            using (var context = database.GetContext()) {
                Project p = context.GetProject(SelectedProjectId);
                return context.GetExposureTemplates(p.ProfileId);
            }
        }
    }

    public class RangeObservableCollection<T> : ObservableCollection<T> {
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (!_suppressNotification) {
                base.OnCollectionChanged(e);
            }
        }

        public new void Clear() {
            _suppressNotification = true;
            base.Clear();
            _suppressNotification = false;
        }

        public void AddRange(IEnumerable<T> list) {
            if (list == null) {
                throw new ArgumentNullException("list");
            }

            _suppressNotification = true;

            foreach (T item in list) {
                Add(item);
            }

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public class AcquiredImageCollection : RangeObservableCollection<AcquiredImageVM> { }

    public class AcquiredImageVM : BaseINPC {
        private AcquiredImage acquiredImage;
        private string exposureTemplateName;
        private SchedulerDatabaseInteraction database;

        public AcquiredImageVM() {
        }

        public AcquiredImageVM(AcquiredImage acquiredImage, IProfileService profileService) {
            this.acquiredImage = acquiredImage;
            string projectName;
            string targetName;
            string profileName;

            database = new SchedulerDatabaseInteraction();
            UpdateGradingCommand = new RelayCommandParam(UpdateGrading, CanCmdExec);

            NamesItem names = ProjectTargetNameCache.GetNames(acquiredImage.ProjectId, acquiredImage.TargetId);
            if (names == null) {
                using (var context = database.GetContext()) {
                    Project project = context.ProjectSet.AsNoTracking().Where(p => p.Id == acquiredImage.ProjectId).FirstOrDefault();
                    projectName = project?.Name;

                    Target target = context.TargetSet.AsNoTracking().Where(t => t.Project.Id == acquiredImage.ProjectId && t.Id == acquiredImage.TargetId).FirstOrDefault();
                    targetName = target?.Name;
                }

                ProjectTargetNameCache.PutNames(acquiredImage.ProjectId, acquiredImage.TargetId, projectName, targetName);
            } else {
                projectName = names.ProjectName;
                targetName = names.TargetName;
            }

            ProjectName = projectName;
            TargetName = targetName;
            GradingStatusValue = (int)acquiredImage.GradingStatus;

            using (var context = database.GetContext()) {
                var ep = context.GetExposurePlan(acquiredImage.ExposureId);
                exposureTemplateName = ep?.ExposureTemplate?.Name;
            }

            profileName = acquiredImage.profileId != null ? ProfileNameCache.Get(acquiredImage.profileId) : "";
            if (profileName == null) {
                ProfileMeta profileMeta = profileService.Profiles.Where(p => p.Id.ToString() == acquiredImage.profileId).FirstOrDefault();
                profileName = profileMeta != null ? profileMeta.Name : "";
                ProfileNameCache.Put(acquiredImage.profileId, profileName);
            }

            ProfileName = profileName;
        }

        public DateTime AcquiredDate { get { return acquiredImage.AcquiredDate; } }
        public string FilterName { get { return acquiredImage.FilterName; } }
        public string ProjectName { get; private set; }
        public string TargetName { get; private set; }
        public string ExposureTemplateName { get { return exposureTemplateName ?? ""; } }
        public string ProfileName { get; private set; }
        public bool Accepted { get { return acquiredImage.Accepted; } }
        public string GradingStatus { get { return acquiredImage.GradingStatus.ToString(); } }
        public string RejectReason { get { return acquiredImage.RejectReason; } }

        public string FileName { get { return acquiredImage.Metadata.FileName; } }
        public string ExposureDuration { get { return Utils.FormatDbl(acquiredImage.Metadata.ExposureDuration); } }

        public string Gain { get { return Utils.FormatInt(acquiredImage.Metadata.Gain); } }
        public string Offset { get { return Utils.FormatInt(acquiredImage.Metadata.Offset); } }
        public string Binning { get { return acquiredImage.Metadata.Binning; } }

        public string DetectedStars { get { return Utils.FormatInt(acquiredImage.Metadata.DetectedStars); } }
        public string HFR { get { return Utils.FormatDbl(acquiredImage.Metadata.HFR); } }
        public string HFRStDev { get { return Utils.FormatDbl(acquiredImage.Metadata.HFRStDev); } }

        public string FWHM { get { return Utils.FormatHF(acquiredImage.Metadata.FWHM); } }
        public string Eccentricity { get { return Utils.FormatHF(acquiredImage.Metadata.Eccentricity); } }

        public string ADUStDev { get { return Utils.FormatDbl(acquiredImage.Metadata.ADUStDev); } }
        public string ADUMean { get { return Utils.FormatDbl(acquiredImage.Metadata.ADUMean); } }
        public string ADUMedian { get { return Utils.FormatDbl(acquiredImage.Metadata.ADUMedian); } }
        public string ADUMin { get { return Utils.FormatInt(acquiredImage.Metadata.ADUMin); } }
        public string ADUMax { get { return Utils.FormatInt(acquiredImage.Metadata.ADUMax); } }

        public string GuidingRMS { get { return Utils.FormatDbl(acquiredImage.Metadata.GuidingRMS); } }
        public string GuidingRMSArcSec { get { return Utils.FormatDbl(acquiredImage.Metadata.GuidingRMSArcSec); } }
        public string GuidingRMSRA { get { return Utils.FormatDbl(acquiredImage.Metadata.GuidingRMSRA); } }
        public string GuidingRMSRAArcSec { get { return Utils.FormatDbl(acquiredImage.Metadata.GuidingRMSRAArcSec); } }
        public string GuidingRMSDEC { get { return Utils.FormatDbl(acquiredImage.Metadata.GuidingRMSDEC); } }
        public string GuidingRMSDECArcSec { get { return Utils.FormatDbl(acquiredImage.Metadata.GuidingRMSDECArcSec); } }

        public string FocuserPosition { get { return Utils.FormatInt(acquiredImage.Metadata.FocuserPosition); } }
        public string FocuserTemp { get { return Utils.FormatDbl(acquiredImage.Metadata.FocuserTemp); } }
        public string RotatorPosition { get { return Utils.FormatDbl(acquiredImage.Metadata.RotatorPosition); } }
        public string PierSide { get { return acquiredImage.Metadata.PierSide; } }
        public string CameraTemp { get { return Utils.FormatDbl(acquiredImage.Metadata.CameraTemp); } }
        public string CameraTargetTemp { get { return Utils.FormatDbl(acquiredImage.Metadata.CameraTargetTemp); } }
        public string Airmass { get { return Utils.FormatDbl(acquiredImage.Metadata.Airmass); } }

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
                if (thumbnail == null && ImageData != null) {
                    thumbnail = Thumbnails.RestoreThumbnail(ImageData.Data);
                }

                return thumbnail;
            }
        }

        public int ThumbnailWidth { get => ImageData != null ? ImageData.Width : 0; }
        public int ThumbnailHeight { get => ImageData != null ? ImageData.Height : 0; }

        private int gradingStatusValue = 0;

        public int GradingStatusValue {
            get { return gradingStatusValue; }
            set {
                gradingStatusValue = value;
                RaisePropertyChanged(nameof(GradingStatusValue));
            }
        }

        public ICommand UpdateGradingCommand { get; private set; }

        private void UpdateGrading(object obj) {
            GradingStatus oldStatus = acquiredImage.GradingStatus;
            GradingStatus newStatus;
            Enum.TryParse(obj.ToString(), out newStatus);

            using (var context = database.GetContext()) {
                Project project = context.GetProjectOnly(acquiredImage.ProjectId);
                if (project == null || !project.EnableGrader) {
                    MyMessageBox.Show("Associated project has grading disabled or project cannot be found (perhaps it was removed?).", "Oops");
                    return;
                }

                AcquiredImage ai = context.ManualUpdateGrading(acquiredImage, oldStatus, newStatus);
                if (ai != null) {
                    this.acquiredImage = ai;
                    GradingStatusValue = (int)newStatus;
                    RaiseAllPropertiesChanged();
                    TSLogger.Debug($"updated grading status from {oldStatus} to {newStatus} for record ai={acquiredImage.Id}");
                }
            }
        }

        private bool CanCmdExec(object obj) => true;
    }

    internal class CsvAcquiredImage {
        private AcquiredImageVM record;

        public CsvAcquiredImage(AcquiredImageVM record) {
            this.record = record;
        }

        public DateTime AcquiredDate { get => record.AcquiredDate; }
        public string FilePath { get => record.FileName; }

        public string ProjectName { get => record.ProjectName; }
        public string TargetName { get => record.TargetName; }
        public string ProfileName { get => record.ProfileName; }
        public string FilterName { get => record.FilterName; }
        public string GradingStatus { get => record.GradingStatus; }
        public string RejectReason { get => record.RejectReason; }

        public string Duration { get => record.ExposureDuration; }

        public string Binning { get => record.Binning; }
        public string CameraTemp { get => record.CameraTemp; }
        public string CameraTargetTemp { get => record.CameraTargetTemp; }
        public string Gain { get => record.Gain; }
        public string Offset { get => record.Offset; }

        public string ADUStDev { get => record.ADUStDev; }
        public string ADUMean { get => record.ADUMean; }
        public string ADUMedian { get => record.ADUMedian; }
        public string ADUMin { get => record.ADUMin; }
        public string ADUMax { get => record.ADUMax; }

        public string DetectedStars { get => record.DetectedStars; }
        public string HFR { get => record.HFR; }
        public string HFRStDev { get => record.HFRStDev; }
        public string FWHM { get => record.FWHM; }
        public string Eccentricity { get => record.Eccentricity; }

        public string GuidingRMS { get => record.GuidingRMS; }
        public string GuidingRMSArcSec { get => record.GuidingRMSArcSec; }
        public string GuidingRMSRA { get => record.GuidingRMSRA; }
        public string GuidingRMSRAArcSec { get => record.GuidingRMSRAArcSec; }
        public string GuidingRMSDEC { get => record.GuidingRMSDEC; }
        public string GuidingRMSDECArcSec { get => record.GuidingRMSDECArcSec; }

        public string FocuserPosition { get => record.FocuserPosition; }
        public string FocuserTemp { get => record.FocuserTemp; }
        public string RotatorPosition { get => record.RotatorPosition; }
        public string PierSide { get => record.PierSide; }
        public string Airmass { get => record.Airmass; }
    }

    internal class ProjectTargetNameCache {
        private static readonly TimeSpan ITEM_TIMEOUT = TimeSpan.FromHours(12);
        private static readonly MemoryCache _cache = new MemoryCache("Scheduler AcquiredImages Names");

        public static NamesItem GetNames(int projectId, int targetId) {
            return (NamesItem)_cache.Get(GetCacheKey(projectId, targetId));
        }

        public static void PutNames(int projectId, int targetId, string projectName, string targetName) {
            _cache.Add(GetCacheKey(projectId, targetId), new NamesItem(projectName, targetName), DateTime.Now.Add(ITEM_TIMEOUT));
        }

        private static string GetCacheKey(int projectId, int targetId) {
            return $"{projectId}-{targetId}";
        }

        private ProjectTargetNameCache() {
        }
    }

    internal class NamesItem {
        public string ProjectName;
        public string TargetName;

        public NamesItem(string projectName, string targetName) {
            ProjectName = projectName;
            TargetName = targetName;
        }
    }

    internal class ProfileNameCache {
        private static readonly TimeSpan ITEM_TIMEOUT = TimeSpan.FromHours(12);
        private static readonly MemoryCache _cache = new MemoryCache("Scheduler AcquiredImages Profile Names");

        internal static string Get(string profileId) {
            return (string)_cache.Get(profileId);
        }

        internal static void Put(string profileId, string profileName) {
            _cache.Add(profileId, profileName, DateTime.Now.Add(ITEM_TIMEOUT));
        }
    }
}