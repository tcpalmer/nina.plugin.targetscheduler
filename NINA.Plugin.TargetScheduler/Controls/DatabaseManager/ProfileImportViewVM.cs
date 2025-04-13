using CommunityToolkit.Mvvm.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using NINA.Core.MyMessageBox;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.ExportImport;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class ProfileImportViewVM : BaseVM {
        private const string DEFAULT_TYPE_FILTER = "<any>";

        private DatabaseManagerVM managerVM;
        private TreeDataItem profileItem;
        private ProfileMeta ProfileMeta;
        private string ParentProfileId;

        private Dictionary<Target, string> targetsDict;

        public ProfileImportViewVM(DatabaseManagerVM managerVM, TreeDataItem profileItem, IProfileService profileService) : base(profileService) {
            this.managerVM = managerVM;
            this.profileItem = profileItem;
            ProfileMeta = profileItem.Data as ProfileMeta;
            ParentProfileId = (profileItem.Data as ProfileMeta).Id.ToString();

            ImportProfileCommand = new AsyncRelayCommand(ImportProfile);
            SelectZipFileCommand = new AsyncRelayCommand(SelectZipFile);
            ImportZipFilePath = null;

            ImportTargetsCommand = new AsyncRelayCommand(ImportTargets);
            SelectCSVFileCommand = new AsyncRelayCommand(SelectCSVFile);
            ImportCSVFilePath = null;
            InitializeTargetImportCombos();
        }

        private void InitializeTargetImportCombos() {
            TypeFilterChoices = new List<string>() { DEFAULT_TYPE_FILTER };

            ProjectChoices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(-1,"Create New")
            };

            Dictionary<Project, string> projectsDict = GetProjectsDictionary();
            foreach (KeyValuePair<Project, string> entry in projectsDict) {
                ProjectChoices.Add(new KeyValuePair<int, string>(entry.Key.Id, entry.Value));
            }

            TargetChoices = new AsyncObservableCollection<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(-1,"None"),
            };

            targetsDict = GetTargetsDictionary(projectsDict);
            foreach (KeyValuePair<Target, string> entry in targetsDict) {
                TargetChoices.Add(new KeyValuePair<int, string>(entry.Key.Id, entry.Value));
            }
        }

        // Profile import ...

        private bool profileImportIsExpanded = false;

        public bool ProfileImportIsExpanded {
            get { return profileImportIsExpanded; }
            set {
                profileImportIsExpanded = value;
                RaisePropertyChanged(nameof(ProfileImportIsExpanded));
            }
        }

        public ICommand ImportProfileCommand { get; private set; }
        public ICommand SelectZipFileCommand { get; private set; }

        private string importZipFilePath;

        public string ImportZipFilePath {
            get => importZipFilePath;
            set {
                importZipFilePath = value;
                RaisePropertyChanged(nameof(ImportZipFilePath));
                RaisePropertyChanged(nameof(ImportProfileEnabled));
            }
        }

        private bool importImageData = false;

        public bool ImportImageData {
            get => importImageData;
            set {
                importImageData = value;
                RaisePropertyChanged(nameof(ImportImageData));
            }
        }

        public bool ImportProfileEnabled { get => ImportProfileValid(); }

        private bool ImportProfileValid() {
            try {
                if (string.IsNullOrEmpty(ImportZipFilePath)) { return false; }
                if (!File.Exists(ImportZipFilePath)) { return false; }
                return true;
            } catch {
                return false;
            }
        }

        private Task<bool> SelectZipFile() {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.Title = "Select Zip File";
            dialog.Filters.Add(new CommonFileDialogFilter("Zip files", "*.zip"));
            dialog.Multiselect = false;

            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok) {
                ImportZipFilePath = dialog.FileName;
            }

            return Task.FromResult(true);
        }

        private bool importRunning = false;

        public bool ImportRunning {
            get => importRunning;
            set {
                importRunning = value;
                RaisePropertyChanged(nameof(ImportRunning));
            }
        }

        private async Task<bool> ImportProfile() {
            if (MyMessageBox.Show($"This will add all profile elements from the zip file to the current profile.\nIf you have existing projects or exposure templates with the same name as an\nimported item, it will show twice - there is no renaming performed.\n\nContinue?", "Import?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                ImportStatus status = null;

                await Task.Run(() => {
                    ImportRunning = true;
                    Thread.Sleep(50);
                    status = new ImportProfile(ProfileMeta, ImportZipFilePath, ImportImageData).Import();
                    ImportRunning = false;
                });

                if (status.IsSuccess) {
                    string referencedFilters = "";
                    if (Common.IsNotEmpty(status.ReferencedFilters)) {
                        referencedFilters = ".\n\nThe export references the following filters - you should confirm you have the\nsame filters configured in this profile (Options > Equipment > Filter Wheel):\n"
                            + string.Join(", ", status.ReferencedFilters) + ".";
                    }

                    string msg = $"{status.GetDetails()}\n from {ImportZipFilePath}{referencedFilters}";
                    MyMessageBox.Show(msg, "Import Success");
                } else {
                    MyMessageBox.Show(status.GetDetails(), "Import Error");
                }
            }

            return true;
        }

        // Targets import ...

        private bool targetImportIsExpanded = false;

        public bool TargetImportIsExpanded {
            get { return targetImportIsExpanded; }
            set {
                targetImportIsExpanded = value;
                RaisePropertyChanged(nameof(TargetImportIsExpanded));
            }
        }

        private Dictionary<Project, string> GetProjectsDictionary() {
            List<Project> projects;
            using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                projects = context.ProjectSet.AsNoTracking().Where(p => p.ProfileId == ParentProfileId).OrderBy(p => p.name).ToList();
            }

            Dictionary<Project, string> dict = new Dictionary<Project, string>();
            projects.ForEach(p => { dict.Add(p, p.Name); });
            return dict;
        }

        private Dictionary<Target, string> GetTargetsDictionary(Dictionary<Project, string> projectsDict) {
            Dictionary<Target, string> dict = new Dictionary<Target, string>();
            using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                foreach (KeyValuePair<Project, string> entry in projectsDict) {
                    Project p = context.GetProject(entry.Key.Id);
                    foreach (Target target in p.Targets) {
                        if (target.ExposurePlans.Count > 0) {
                            dict.Add(target, target.Name);
                        }
                    }
                }
            }

            // Sort by target name
            IOrderedEnumerable<KeyValuePair<Target, string>> sortedDict = from entry in dict orderby entry.Value ascending select entry;
            return sortedDict.ToDictionary<KeyValuePair<Target, string>, Target, string>(pair => pair.Key, pair => pair.Value);
        }

        private string importCSVFilePath;

        public string ImportCSVFilePath {
            get => importCSVFilePath;
            set {
                importCSVFilePath = value;
                RaisePropertyChanged(nameof(ImportCSVFilePath));
                TypeFilterChoices = GetTypeFilterChoices(importCSVFilePath);
                RaisePropertyChanged(nameof(ImportTargetsEnabled));
            }
        }

        private List<string> typeFilterChoices;

        public List<string> TypeFilterChoices {
            get {
                return typeFilterChoices;
            }
            set {
                typeFilterChoices = value;
                RaisePropertyChanged(nameof(TypeFilterChoices));
            }
        }

        private string selectedTypeFilter = DEFAULT_TYPE_FILTER;

        public string SelectedTypeFilter {
            get => selectedTypeFilter;
            set {
                selectedTypeFilter = value;
                RaisePropertyChanged(nameof(SelectedTypeFilter));
            }
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> projectChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> ProjectChoices {
            get => projectChoices;
            set {
                projectChoices = value;
                RaisePropertyChanged(nameof(ProjectChoices));
            }
        }

        private int selectedProjectId = -1;

        public int SelectedProjectId {
            get => selectedProjectId;
            set {
                selectedProjectId = value;
                RaisePropertyChanged(nameof(SelectedProjectId));
            }
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> targetChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> TargetChoices {
            get => targetChoices;
            set {
                targetChoices = value;
                RaisePropertyChanged(nameof(TargetChoices));
            }
        }

        private int selectedTargetId = -1;

        public int SelectedTargetId {
            get => selectedTargetId;
            set {
                selectedTargetId = value;
                RaisePropertyChanged(nameof(SelectedTargetId));
            }
        }

        public bool ImportTargetsEnabled { get => ImportCSVFileValid(); }

        private bool ImportCSVFileValid() {
            if (ImportCSVFilePath == null) {
                return false;
            }

            try { return new FileInfo(ImportCSVFilePath).Exists == true; } catch (Exception) { return false; }
        }

        public ICommand ImportTargetsCommand { get; private set; }
        public ICommand SelectCSVFileCommand { get; private set; }

        private Task<bool> SelectCSVFile() {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.Title = "Select CSV File";
            dialog.Multiselect = false;
            dialog.Filters.Add(new CommonFileDialogFilter("CSV files", "*.csv"));

            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok) {
                ImportCSVFilePath = dialog.FileName;
            }

            return Task.FromResult(true);
        }

        private Task<bool> ImportTargets() {
            TSLogger.Info($"importing targets from {importCSVFilePath}");
            CsvTargetLoader loader = new CsvTargetLoader();

            try {
                string typeFilter = SelectedTypeFilter == DEFAULT_TYPE_FILTER ? null : SelectedTypeFilter;
                List<Target> targets = loader.Load(ImportCSVFilePath, typeFilter);
                TSLogger.Info($"read {targets.Count} targets for import, filtered by '{typeFilter}'");

                if (targets.Count == 0) {
                    MyMessageBox.Show("No targets found for import.", "Oops");
                    return Task.FromResult(true);
                }

                if (MyMessageBox.Show($"Continue with import of {targets.Count} targets?  This cannot be undone.", "Import?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.No) {
                    TSLogger.Info("target import aborted");
                    return Task.FromResult(true);
                }

                // If we have a target template, grab EPs and override exposure order from that and clone for each target
                Target templateTarget = null;
                if (SelectedTargetId != -1) {
                    templateTarget = targetsDict.Where(d => d.Key.Id == SelectedTargetId).FirstOrDefault().Key;
                    TSLogger.Info($"applying exposure plans from target '{templateTarget.Name}' to imported targets");
                    foreach (Target target in targets) {
                        target.ExposurePlans = CloneTemplateExposurePlans(templateTarget.ExposurePlans);
                        target.OverrideExposureOrders = CloneOverrideExposureOrders(target.Id, templateTarget.OverrideExposureOrders);
                    }
                }

                if (SelectedProjectId == -1) {
                    Project project = managerVM.AddNewProject(profileItem);
                    managerVM.AddTargets(project, targets);
                } else {
                    foreach (TreeDataItem item in profileItem.Items) {
                        if (item.Type == TreeDataType.Project) {
                            Project project = item.Data as Project;
                            if (project != null && project.Id == SelectedProjectId) {
                                managerVM.AddTargets(project, targets, item);
                                break;
                            }
                        }
                    }
                }
            } catch (Exception e) {
                TSLogger.Error($"Failed to read CSV file for target import: {e.Message}\n{e.StackTrace}");
                MyMessageBox.Show($"Import file cannot be read:\n{e.Message}", "Oops");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        private List<string> GetTypeFilterChoices(string importFilePath) {
            try {
                if (importFilePath == null) {
                    return new List<string>() { DEFAULT_TYPE_FILTER };
                }

                CsvTargetLoader loader = new CsvTargetLoader();
                List<string> types = loader.GetUniqueTypes(importFilePath);
                return [DEFAULT_TYPE_FILTER, .. types.OrderBy(s => s).ToList()];
            } catch (Exception e) {
                TSLogger.Error($"Failed to read CSV file for target import: {e.Message}\n{e.StackTrace}");
                MyMessageBox.Show($"Import file cannot be read:\n{e.Message}", "Oops");
                return new List<string>() { DEFAULT_TYPE_FILTER };
            }
        }

        private List<ExposurePlan> CloneTemplateExposurePlans(List<ExposurePlan> exposurePlans) {
            List<ExposurePlan> list = new List<ExposurePlan>(exposurePlans.Count);
            if (exposurePlans == null || exposurePlans.Count == 0) {
                return list;
            }

            exposurePlans.ForEach(ep => list.Add(ep.GetPasteCopy(ep.ProfileId)));
            return list;
        }

        private List<OverrideExposureOrderItem> CloneOverrideExposureOrders(int targetId, List<OverrideExposureOrderItem> overrideExposureOrders) {
            List<OverrideExposureOrderItem> list = new List<OverrideExposureOrderItem>(overrideExposureOrders.Count);
            if (overrideExposureOrders?.Count == 0) {
                return list;
            }

            overrideExposureOrders.ForEach(oeo => list.Add(oeo.GetPasteCopy(targetId)));
            return list;
        }
    }
}