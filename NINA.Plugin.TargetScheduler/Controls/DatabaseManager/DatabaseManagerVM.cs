using LinqKit;
using NINA.Core.Model.Equipment;
using NINA.Core.MyMessageBox;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using RelayCommandParam = CommunityToolkit.Mvvm.Input.RelayCommand<object>;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class DatabaseManagerVM : BaseVM {
        private IApplicationMediator applicationMediator;
        private IFramingAssistantVM framingAssistantVM;
        private IDeepSkyObjectSearchVM deepSkyObjectSearchVM;
        private IPlanetariumFactory planetariumFactory;
        private SchedulerDatabaseInteraction database;

        private TreeDataItem selectedTreeDataItem;
        private TreeDataItem activeTreeDataItem;

        private TreeDisplayMode SelectedDisplayMode;
        private bool SelectedColorizeMode;

        public DatabaseManagerVM(IProfileService profileService,
            IApplicationMediator applicationMediator,
            IFramingAssistantVM framingAssistantVM,
            IDeepSkyObjectSearchVM deepSkyObjectSearchVM,
            IPlanetariumFactory planetariumFactory)
            : base(profileService) {
            this.applicationMediator = applicationMediator;
            this.framingAssistantVM = framingAssistantVM;
            this.deepSkyObjectSearchVM = deepSkyObjectSearchVM;
            this.planetariumFactory = planetariumFactory;

            database = new SchedulerDatabaseInteraction();

            SelectedItemChangedCommand = new RelayCommandParam(SelectedItemChanged);
            SelectedDisplayMode = TreeDisplayMode.DisplayAll;
            InitializeProjectsColorize();
        }

        public SchedulerDatabaseInteraction Database { get { return database; } }

        public int PanelHeight { get => 690; }

        public DatabaseTreeViewVM ProjectsTreeViewVM {
            get => new DatabaseTreeViewVM(this, profileService, "Projects", RootProjectsList, 350, true);
        }

        public DatabaseTreeViewVM ExposureTemplatesTreeViewVM {
            get => new DatabaseTreeViewVM(this, profileService, "Exposure Templates", RootExposureTemplateList, 255);
        }

        private Visibility showProfilePreferencesView = Visibility.Hidden;

        public Visibility ShowProfilePreferencesView {
            get => showProfilePreferencesView;
            set {
                showProfilePreferencesView = value;
                RaisePropertyChanged(nameof(ShowProfilePreferencesView));
            }
        }

        private ProfilePreferencesViewVM profilePreferencesViewVM;

        public ProfilePreferencesViewVM ProfilePreferencesViewVM {
            get => profilePreferencesViewVM;
            set {
                profilePreferencesViewVM = value;
                RaisePropertyChanged(nameof(ProfilePreferencesViewVM));
            }
        }

        private Visibility showProfileView = Visibility.Hidden;

        public Visibility ShowProfileView {
            get => showProfileView;
            set {
                showProfileView = value;
                RaisePropertyChanged(nameof(ShowProfileView));
            }
        }

        private ProfileViewVM profileViewVM;

        public ProfileViewVM ProfileViewVM {
            get => profileViewVM;
            set {
                profileViewVM = value;
                RaisePropertyChanged(nameof(ProfileViewVM));
            }
        }

        private Visibility showOrphanedProjectsView = Visibility.Hidden;

        public Visibility ShowOrphanedProjectsView {
            get => showOrphanedProjectsView;
            set {
                showOrphanedProjectsView = value;
                RaisePropertyChanged(nameof(ShowOrphanedProjectsView));
            }
        }

        private OrphanedProjectsViewVM orphanedProjectsViewVM;

        public OrphanedProjectsViewVM OrphanedProjectsViewVM {
            get => orphanedProjectsViewVM;
            set {
                orphanedProjectsViewVM = value;
                RaisePropertyChanged(nameof(OrphanedProjectsViewVM));
            }
        }

        private List<Project> orphanedProjects;

        public List<Project> OrphanedProjects {
            get => orphanedProjects;
            set {
                orphanedProjects = value;
                RaisePropertyChanged(nameof(OrphanedProjects));
            }
        }

        private Visibility showProjectView = Visibility.Hidden;

        public Visibility ShowProjectView {
            get => showProjectView;
            set {
                showProjectView = value;
                RaisePropertyChanged(nameof(ShowProjectView));
            }
        }

        private ProjectViewVM projectViewVM;

        public ProjectViewVM ProjectViewVM {
            get => projectViewVM;
            set {
                projectViewVM = value;
                RaisePropertyChanged(nameof(ProjectViewVM));
            }
        }

        private Visibility showTargetView = Visibility.Hidden;

        public Visibility ShowTargetView {
            get => showTargetView;
            set {
                showTargetView = value;
                RaisePropertyChanged(nameof(ShowTargetView));
            }
        }

        private TargetViewVM targetViewVM;

        public TargetViewVM TargetViewVM {
            get => targetViewVM;
            set {
                targetViewVM = value;
                RaisePropertyChanged(nameof(TargetViewVM));
            }
        }

        private Visibility showExposureTemplateProfileView = Visibility.Hidden;

        public Visibility ShowExposureTemplateProfileView {
            get => showExposureTemplateProfileView;
            set {
                showExposureTemplateProfileView = value;
                RaisePropertyChanged(nameof(ShowExposureTemplateProfileView));
            }
        }

        private ExposureTemplateProfileViewVM exposureTemplateProfileViewVM;

        public ExposureTemplateProfileViewVM ExposureTemplateProfileViewVM {
            get => exposureTemplateProfileViewVM;
            set {
                exposureTemplateProfileViewVM = value;
                RaisePropertyChanged(nameof(ExposureTemplateProfileViewVM));
            }
        }

        private Visibility showExposureTemplateView = Visibility.Hidden;

        public Visibility ShowExposureTemplateView {
            get => showExposureTemplateView;
            set {
                showExposureTemplateView = value;
                RaisePropertyChanged(nameof(ShowExposureTemplateView));
            }
        }

        private ExposureTemplateViewVM exposureTemplateViewVM;

        public ExposureTemplateViewVM ExposureTemplateViewVM {
            get => exposureTemplateViewVM;
            set {
                exposureTemplateViewVM = value;
                RaisePropertyChanged(nameof(ExposureTemplateViewVM));
            }
        }

        private Visibility showOrphanedExposureTemplatesView = Visibility.Hidden;

        public Visibility ShowOrphanedExposureTemplatesView {
            get => showOrphanedExposureTemplatesView;
            set {
                showOrphanedExposureTemplatesView = value;
                RaisePropertyChanged(nameof(ShowOrphanedExposureTemplatesView));
            }
        }

        private OrphanedExposureTemplatesViewVM orphanedExposureTemplatesViewVM;

        public OrphanedExposureTemplatesViewVM OrphanedExposureTemplatesViewVM {
            get => orphanedExposureTemplatesViewVM;
            set {
                orphanedExposureTemplatesViewVM = value;
                RaisePropertyChanged(nameof(OrphanedExposureTemplatesViewVM));
            }
        }

        private List<ExposureTemplate> orphanedExposureTemplates;

        public List<ExposureTemplate> OrphanedExposureTemplates {
            get => orphanedExposureTemplates;
            set {
                orphanedExposureTemplates = value;
                RaisePropertyChanged(nameof(OrphanedExposureTemplates));
            }
        }

        public ICommand SelectedItemChangedCommand { get; private set; }

        private void SelectedItemChanged(object obj) {
            TreeDataItem item = obj as TreeDataItem;
            if (item != null) {
                try {
                    DeselectOppositeTree(selectedTreeDataItem, item);
                    selectedTreeDataItem = item;

                    switch (item.Type) {
                        case TreeDataType.ProjectProfile:
                            activeTreeDataItem = item;
                            ProfileViewVM = new ProfileViewVM(this, profileService, item);
                            CollapseAllViews();
                            ShowProfileView = Visibility.Visible;
                            break;

                        case TreeDataType.OrphanedProjects:
                            activeTreeDataItem = item;
                            OrphanedProjectsViewVM = new OrphanedProjectsViewVM(this, profileService, item, OrphanedProjects);
                            CollapseAllViews();
                            ShowOrphanedProjectsView = Visibility.Visible;
                            break;

                        case TreeDataType.Project:
                            activeTreeDataItem = item;
                            Project project = (Project)item.Data;
                            ProjectViewVM = new ProjectViewVM(this, framingAssistantVM, profileService, project);
                            CollapseAllViews();
                            ShowProjectView = Visibility.Visible;
                            break;

                        case TreeDataType.Target:
                            activeTreeDataItem = item;
                            project = (Project)item.TreeParent.Data;
                            Target target = (Target)item.Data;
                            TargetViewVM = new TargetViewVM(this, profileService, applicationMediator, framingAssistantVM, deepSkyObjectSearchVM, planetariumFactory, target, project);
                            CollapseAllViews();
                            ShowTargetView = Visibility.Visible;
                            break;

                        case TreeDataType.ExposureTemplateProfile:
                            activeTreeDataItem = item;
                            ExposureTemplateProfileViewVM = new ExposureTemplateProfileViewVM(this, profileService, item);
                            CollapseAllViews();
                            ShowExposureTemplateProfileView = Visibility.Visible;
                            break;

                        case TreeDataType.OrphanedExposureTemplates:
                            activeTreeDataItem = item;
                            OrphanedExposureTemplatesViewVM = new OrphanedExposureTemplatesViewVM(this, profileService, item, OrphanedExposureTemplates);
                            CollapseAllViews();
                            ShowOrphanedExposureTemplatesView = Visibility.Visible;
                            break;

                        case TreeDataType.ExposureTemplate:
                            activeTreeDataItem = item;
                            ExposureTemplate exposureTemplate = (ExposureTemplate)item.Data;
                            ExposureTemplateViewVM = new ExposureTemplateViewVM(this, profileService, exposureTemplate);
                            CollapseAllViews();
                            ShowExposureTemplateView = Visibility.Visible;
                            break;

                        default:
                            activeTreeDataItem = null;
                            CollapseAllViews();
                            break;
                    }
                } catch (Exception e) {
                    CollapseAllViews();
                    TSLogger.Error($"Error while changing selected item in nav tree: {e.Message}");
                    MyMessageBox.Show("An error occured trying to select an item.  Is it possible you have another instance of NINA running that was locked the associated profile?", "Oops");
                }
            }
        }

        private void CollapseAllViews() {
            ShowProfilePreferencesView = Visibility.Collapsed;
            ShowProfileView = Visibility.Collapsed;
            ShowOrphanedProjectsView = Visibility.Collapsed;
            ShowOrphanedExposureTemplatesView = Visibility.Collapsed;
            ShowProjectView = Visibility.Collapsed;
            ShowTargetView = Visibility.Collapsed;
            ShowExposureTemplateProfileView = Visibility.Collapsed;
            ShowExposureTemplateView = Visibility.Collapsed;
        }

        private void DeselectOppositeTree(TreeDataItem existingItem, TreeDataItem newItem) {
            if (existingItem == null) {
                return;
            }

            TreeDataItem existingItemRoot = existingItem.GetRoot();
            TreeDataItem newItemRoot = newItem.GetRoot();
            if (existingItemRoot.Type == newItemRoot.Type) {
                return;
            }

            TreeDataItem.VisitAll(existingItemRoot, i => { i.IsSelected = false; });
        }

        public List<TreeDataItem> Refresh(List<TreeDataItem> rootList) {
            if (rootList == RootProjectsList) {
                RootProjectsList = LoadProjectsTree();
                TreeDataItem.VisitAll(RootProjectsList[0], i => { i.IsExpanded = false; });
                SelectedDisplayMode = TreeDisplayMode.DisplayAll;
                return RootProjectsList;
            }

            if (rootList == RootExposureTemplateList) {
                RootExposureTemplateList = LoadExposureTemplateTree();
                TreeDataItem.VisitAll(RootExposureTemplateList[0], i => { i.IsExpanded = false; });
                return RootExposureTemplateList;
            }

            TSLogger.Warning("failed to determine the root list for scheduler manager");
            return null;
        }

        private List<TreeDataItem> rootProjectsList;

        public List<TreeDataItem> RootProjectsList {
            get {
                if (rootProjectsList == null) {
                    rootProjectsList = LoadProjectsTree();
                }
                return rootProjectsList;
            }
            set {
                rootProjectsList = value;
                RaisePropertyChanged(nameof(RootProjectsList));
            }
        }

        private List<TreeDataItem> rootExposureTemplateList;

        public List<TreeDataItem> RootExposureTemplateList {
            get {
                if (rootExposureTemplateList == null) {
                    rootExposureTemplateList = LoadExposureTemplateTree();
                }
                return rootExposureTemplateList;
            }
            set {
                rootExposureTemplateList = value;
                RaisePropertyChanged(nameof(RootExposureTemplateList));
            }
        }

        private bool treeViewEabled = true;

        public bool TreeViewEabled {
            get => treeViewEabled;
            set {
                treeViewEabled = value;
                RaisePropertyChanged(nameof(TreeViewEabled));
            }
        }

        private List<TreeDataItem> LoadProjectsTree() {
            List<TreeDataItem> rootList = new List<TreeDataItem>();
            TreeDataItem profilesFolder = new TreeDataItem(TreeDataType.ProjectRoot, "Profiles", null);
            rootList.Add(profilesFolder);

            using (var context = database.GetContext()) {
                foreach (ProfileMeta profile in profileService.Profiles) {
                    TreeDataItem profileItem = new TreeDataItem(TreeDataType.ProjectProfile, profile.Name, profile, profilesFolder);
                    profilesFolder.Items.Add(profileItem);

                    List<Project> projects = context.GetAllProjects(profile.Id.ToString());
                    foreach (Project project in projects) {
                        TreeDataItem projectItem = new TreeDataItem(TreeDataType.Project, project.Name, project, profileItem);
                        profileItem.Items.Add(projectItem);

                        List<Target> targetList = project.Targets;
                        foreach (Target target in targetList) {
                            target.OverrideExposureOrders = context.GetOverrideExposureOrders(target.Id);
                            TreeDataItem targetItem = new TreeDataItem(TreeDataType.Target, target.Name, target, projectItem);
                            projectItem.Items.Add(targetItem);
                        }

                        projectItem.SortChildren();
                    }

                    profileItem.SortChildren();
                }

                // Handle 'orphaned' projects (associated profile has been deleted)
                List<Project> orphanedProjects = GetOrphanedProjects(profileService, context);
                if (orphanedProjects.Count > 0) {
                    OrphanedProjects = orphanedProjects;
                    TreeDataItem pseudoProfileItem = new TreeDataItem(TreeDataType.OrphanedProjects, "ORPHANED", null, profilesFolder);
                    profilesFolder.Items.Add(pseudoProfileItem);
                }

                profilesFolder.SortChildren();
            }

            return rootList;
        }

        private List<Project> GetOrphanedProjects(IProfileService profileService, ISchedulerDatabaseContext context) {
            List<string> currentProfileIds = new List<string>();
            profileService.Profiles.ForEach(p => currentProfileIds.Add(p.Id.ToString()));
            return context.GetOrphanedProjects(currentProfileIds);
        }

        private List<TreeDataItem> LoadExposureTemplateTree() {
            List<TreeDataItem> rootList = new List<TreeDataItem>();
            TreeDataItem profilesFolder = new TreeDataItem(TreeDataType.ExposureTemplateRoot, "Profiles", null);
            rootList.Add(profilesFolder);

            using (var context = database.GetContext()) {
                foreach (ProfileMeta profile in profileService.Profiles) {
                    TreeDataItem profileItem = new TreeDataItem(TreeDataType.ExposureTemplateProfile, profile.Name, profile, profilesFolder);
                    profilesFolder.Items.Add(profileItem);

                    List<ExposureTemplate> exposureTemplates = context.GetExposureTemplates(profile.Id.ToString());
                    foreach (ExposureTemplate exposureTemplate in exposureTemplates) {
                        TreeDataItem exposureTemplateItem = new TreeDataItem(TreeDataType.ExposureTemplate, exposureTemplate.Name, exposureTemplate, profileItem);
                        profileItem.Items.Add(exposureTemplateItem);
                    }

                    // We could sort ETs into filter -> filter wheel order
                }

                // Handle 'orphaned' exposure templates (associated profile has been deleted)
                List<ExposureTemplate> orphanedExposureTemplates = GetOrphanedExposureTemplates(profileService, context);
                if (orphanedExposureTemplates.Count > 0) {
                    OrphanedExposureTemplates = orphanedExposureTemplates;
                    TreeDataItem pseudoProfileItem = new TreeDataItem(TreeDataType.OrphanedExposureTemplates, "ORPHANED", null, profilesFolder);
                    profilesFolder.Items.Add(pseudoProfileItem);
                }

                profilesFolder.SortChildren();
            }

            return rootList;
        }

        private List<ExposureTemplate> GetOrphanedExposureTemplates(IProfileService profileService, ISchedulerDatabaseContext context) {
            List<string> currentProfileIds = new List<string>();
            profileService.Profiles.ForEach(p => currentProfileIds.Add(p.Id.ToString()));
            return context.GetOrphanedExposureTemplates(currentProfileIds);
        }

        public void SetEditMode(bool editMode) {
            TreeViewEabled = !editMode;
        }

        public void NavigateTo(TreeDataItem item) {
            TreeDataItem parent = item.TreeParent;
            while (parent != null) {
                parent.IsExpanded = true;
                parent = parent.TreeParent;
            }

            item.IsSelected = true;
        }

        public void ViewProfilePreferences(ProfileMeta profile) {
            ProfilePreferencesViewVM = new ProfilePreferencesViewVM(this, profileService, GetProfilePreference(profile.Id.ToString()), profile.Name);
            CollapseAllViews();
            ShowProfilePreferencesView = Visibility.Visible;
        }

        public ProfilePreference GetProfilePreference(string profileId) {
            using (var context = database.GetContext()) {
                return context.GetProfilePreference(profileId, true);
            }
        }

        public void SaveProfilePreference(ProfilePreference profilePreference) {
            using (var context = database.GetContext()) {
                if (context.SaveProfilePreference(profilePreference) == null) {
                    Notification.ShowError("Failed to save Scheduler Profile Preference (see log for details)");
                }
            }
        }

        public Project AddNewProject(TreeDataItem parentItem) {
            ProfileMeta profile = (ProfileMeta)parentItem.Data;
            Project project = new Project(profile.Id.ToString());
            project.Name = "<new project>";

            using (var context = database.GetContext()) {
                Project newProject = context.SaveProject(project);
                if (newProject != null) {
                    TreeDataItem projectItem = new TreeDataItem(TreeDataType.Project, project.Name, project, parentItem);
                    parentItem.Items.Add(projectItem);
                    projectItem.IsSelected = true;
                    parentItem.IsExpanded = true;
                    SetTreeColorizeMode(SelectedColorizeMode);
                    return newProject;
                } else {
                    Notification.ShowError("Failed to save new Scheduler Project (see log for details)");
                    return null;
                }
            }
        }

        public void SaveProject(Project project) {
            using (var context = database.GetContext()) {
                if (context.SaveProject(project) != null) {
                    activeTreeDataItem.Data = project;
                    TextBlock textBlock = activeTreeDataItem.Header as TextBlock;
                    if (textBlock.Text != project.Name) {
                        textBlock.Text = project.Name;
                        activeTreeDataItem.SortName = project.Name;
                    }

                    SetTreeColorizeMode(SelectedColorizeMode);
                    DitherManagerCache.Remove(project.Targets);
                } else {
                    Notification.ShowError("Failed to save Scheduler Project (see log for details)");
                }
            }
        }

        public void PasteProject(TreeDataItem parentItem) {
            ProfileMeta profile = (ProfileMeta)parentItem.Data;

            if (!Clipboard.HasType(TreeDataType.Project)) {
                TSLogger.Error($"expected clipboard to hold Project");
                return;
            }

            Project source = Clipboard.GetItem().Data as Project;
            using (var context = database.GetContext()) {
                Project newProject = context.PasteProject(profile.Id.ToString(), source);
                if (newProject != null) {
                    TreeDataItem newProjectItem = new TreeDataItem(TreeDataType.Project, newProject.Name, newProject, parentItem);
                    parentItem.Items.Add(newProjectItem);
                    newProjectItem.IsSelected = true;
                    parentItem.IsExpanded = true;

                    newProject.Targets.ForEach(target => {
                        TreeDataItem targetItem = new TreeDataItem(TreeDataType.Target, target.Name, target, newProjectItem);
                        newProjectItem.Items.Add(targetItem);
                    });

                    SetTreeColorizeMode(SelectedColorizeMode);
                } else {
                    Notification.ShowError("Failed to paste new Scheduler Project (see log for details)");
                }
            }
        }

        public void DeleteProject(Project project) {
            string profileId = profileService.ActiveProfile.Id.ToString();
            bool deleteAcquiredImagesWithTarget = GetProfilePreference(profileId).EnableDeleteAcquiredImagesWithTarget;

            using (var context = database.GetContext()) {
                if (context.DeleteProject(project, deleteAcquiredImagesWithTarget)) {
                    TreeDataItem parentItem = activeTreeDataItem.TreeParent;
                    parentItem.Items.Remove(activeTreeDataItem);
                    parentItem.IsSelected = true;
                } else {
                    Notification.ShowError("Failed to delete Scheduler Project (see log for details)");
                }
            }
        }

        public bool MoveOrphanedProject(Project project, string profileId) {
            using (var context = database.GetContext()) {
                Project newProject = context.MoveProject(project, profileId);
                if (newProject != null) {
                    TreeDataItem parentItem = GetProfileItem(RootProjectsList, profileId);
                    TreeDataItem newProjectItem = new TreeDataItem(TreeDataType.Project, newProject.Name, newProject, parentItem);
                    parentItem.Items.Add(newProjectItem);

                    newProject.Targets.ForEach(target => {
                        TreeDataItem targetItem = new TreeDataItem(TreeDataType.Target, target.Name, target, newProjectItem);
                        newProjectItem.Items.Add(targetItem);
                    });
                    return true;
                } else {
                    Notification.ShowError("Failed to move Scheduler orphaned Project (see log for details)");
                    return false;
                }
            }
        }

        public bool DeleteOrphanedProject(Project project) {
            string profileId = profileService.ActiveProfile.Id.ToString();
            bool deleteAcquiredImagesWithTarget = GetProfilePreference(profileId).EnableDeleteAcquiredImagesWithTarget;

            using (var context = database.GetContext()) {
                if (context.DeleteProject(project, deleteAcquiredImagesWithTarget)) {
                    return true;
                } else {
                    Notification.ShowError("Failed to delete Scheduler orphaned Project (see log for details)");
                    return false;
                }
            }
        }

        public void AddNewTarget(Project project) {
            Target target = new Target();
            target.Name = "<new target>";
            TreeDataItem parentItem = activeTreeDataItem;

            using (var context = database.GetContext()) {
                Target newTarget = context.AddNewTarget(project, target);
                if (newTarget != null) {
                    TreeDataItem targetItem = new TreeDataItem(TreeDataType.Target, target.Name, target, parentItem);
                    parentItem.Items.Add(targetItem);
                    targetItem.IsSelected = true;
                    parentItem.IsExpanded = true;
                    SetTreeColorizeMode(SelectedColorizeMode);
                } else {
                    Notification.ShowError("Failed to add new Scheduler Target (see log for details)");
                }
            }
        }

        public void AddTargets(Project project, List<Target> targets, TreeDataItem parentItem = null) {
            parentItem = parentItem == null ? activeTreeDataItem : parentItem;
            using (var context = database.GetContext()) {
                foreach (Target target in targets) {
                    Target newTarget = context.AddNewTarget(project, target);
                    if (newTarget != null) {
                        TreeDataItem targetItem = new TreeDataItem(TreeDataType.Target, target.Name, target, parentItem);
                        parentItem.Items.Add(targetItem);
                        parentItem.IsExpanded = true;
                    } else {
                        Notification.ShowError("Failed to add new Scheduler Target (see log for details)");
                    }
                }

                SetTreeColorizeMode(SelectedColorizeMode);
            }
        }

        public void SaveTarget(Target target, bool clearFilterCadenceItems = false) {
            using (var context = database.GetContext()) {
                if (context.SaveTarget(target, clearFilterCadenceItems) != null) {
                    activeTreeDataItem.Data = target;
                    TextBlock textBlock = activeTreeDataItem.Header as TextBlock;
                    if (textBlock.Text != target.Name) {
                        textBlock.Text = target.Name;
                        activeTreeDataItem.SortName = target.Name;
                    }

                    // Refresh the parent project
                    TreeDataItem parentItem = activeTreeDataItem.TreeParent;
                    parentItem.Data = context.GetProject(target.ProjectId);

                    SetTreeColorizeMode(SelectedColorizeMode);
                    DitherManagerCache.Remove(target);
                } else {
                    Notification.ShowError("Failed to save Scheduler Target (see log for details)");
                }
            }
        }

        public void PasteTarget(Project project) {
            if (!Clipboard.HasType(TreeDataType.Target)) {
                TSLogger.Error($"expected clipboard to hold Target");
                return;
            }

            Target source = Clipboard.GetItem().Data as Target;
            TreeDataItem parentItem = activeTreeDataItem;

            using (var context = database.GetContext()) {
                Target newTarget = context.PasteTarget(project, source);
                if (newTarget != null) {
                    TreeDataItem newTargetItem = new TreeDataItem(TreeDataType.Target, newTarget.Name, newTarget, parentItem);
                    parentItem.Items.Add(newTargetItem);
                    newTargetItem.IsSelected = true;
                    parentItem.IsExpanded = true;
                    SetTreeColorizeMode(SelectedColorizeMode);
                } else {
                    Notification.ShowError("Failed to paste new Scheduler Project (see log for details)");
                }
            }
        }

        public void MoveTarget(Project project) {
            if (!Clipboard.HasType(TreeDataType.Target)) {
                TSLogger.Error($"expected clipboard to hold Target");
                return;
            }

            TreeDataItem sourceItem = Clipboard.GetItem();
            Target source = sourceItem.Data as Target;

            if (source.ProjectId == project.Id) {
                MyMessageBox.Show("You cannot move a target to the same project.", "Oops");
                Clipboard.SetItem(sourceItem);
                return;
            }

            if (project.Targets.Where(t => t.Name == source.Name).Any()) {
                MyMessageBox.Show($"Destination project already has a target named '{source.Name}'.  You must rename one before moving.", "Oops");
                Clipboard.SetItem(sourceItem);
                return;
            }

            string message = $"Move target '{source.Name}' from project '{source.Project.Name}' to '{project.Name}'?  Be sure you are not actively imaging this target.";
            if (MyMessageBox.Show(message, "Move Target?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.No) {
                Clipboard.SetItem(sourceItem);
                return;
            }

            TreeDataItem parentItem = activeTreeDataItem;

            using (var context = database.GetContext()) {
                Target movedTarget = context.MoveTarget(project, source);
                if (movedTarget != null) {
                    TreeDataItem newTargetItem = new TreeDataItem(TreeDataType.Target, movedTarget.Name, movedTarget, parentItem);
                    parentItem.Items.Add(newTargetItem);
                    sourceItem.TreeParent.Items.Remove(sourceItem);
                    newTargetItem.IsSelected = true;
                    parentItem.IsExpanded = true;
                    SetTreeColorizeMode(SelectedColorizeMode);
                } else {
                    Notification.ShowError("Failed to move Target to Project (see log for details)");
                }
            }
        }

        public void GradeTarget(Target target) {
            List<AcquiredImage> acquiredImages = new List<AcquiredImage>();
            using (var context = database.GetContext()) {
                target.ExposurePlans.ForEach(ep => {
                    acquiredImages.AddRange(context.GetPendingAcquiredImagesForGrading(ep));
                });
            }

            ImageGradingController gradingController = ImageGradingController.Instance;
            ProfilePreference profilePrefs = GetProfilePreference(profileService.ActiveProfile.Id.ToString());
            ImageGraderPreferences graderPrefs = new ImageGraderPreferences(profileService.ActiveProfile, profilePrefs);
            CancellationTokenSource cancelSource = new CancellationTokenSource();

            TSLogger.Info($"queuing {acquiredImages.Count} images for manual grading");

            acquiredImages.OrderBy(ai => ai.AcquiredDate).ForEach(ai => {
                GradingWorkData workData = new GradingWorkData(target.Id, ai.ExposureId, ai.Id, -1, graderPrefs, true);
                gradingController.Enqueue(workData, cancelSource.Token);
            });
        }

        public void ResetProfile(TreeDataItem parentItem) {
            foreach (TreeDataItem projectItem in parentItem.Items) {
                ResetProjectTargets(projectItem);
            }
        }

        public void ResetProjectTargets(TreeDataItem projectItem = null) {
            TreeDataItem useItem = projectItem != null ? projectItem : activeTreeDataItem;
            foreach (TreeDataItem targetItem in useItem.Items) {
                ResetTarget((Target)targetItem.Data, targetItem);
            }
        }

        public Target ResetTarget(Target target, TreeDataItem targetItem = null) {
            using (var context = database.GetContext()) {
                Target updatedTarget = context.ResetExposurePlans(target);
                if (updatedTarget != null) {
                    if (targetItem != null) {
                        targetItem.Data = updatedTarget;
                    } else {
                        activeTreeDataItem.Data = updatedTarget;
                    }

                    int idx = target.Project.Targets.FindIndex(t => t.Id == target.Id);
                    if (idx != -1) {
                        target.Project.Targets[idx] = updatedTarget;
                    }

                    SetTreeColorizeMode(SelectedColorizeMode);
                    DitherManagerCache.Remove(target);
                } else {
                    Notification.ShowError("Failed to reset Target Exposure Plans (see log for details)");
                }

                return updatedTarget;
            }
        }

        public void DeleteTarget(Target target, bool deleteAcquiredImagesWithTarget) {
            using (var context = database.GetContext()) {
                if (deleteAcquiredImagesWithTarget) {
                    context.DeleteAcquiredImages(target.Id);
                }

                if (context.DeleteTarget(target)) {
                    TreeDataItem parentItem = activeTreeDataItem.TreeParent;
                    parentItem.Items.Remove(activeTreeDataItem);
                    parentItem.IsSelected = true;
                    SetTreeColorizeMode(SelectedColorizeMode);
                } else {
                    Notification.ShowError("Failed to delete Scheduler Target (see log for details)");
                }
            }
        }

        public Target ToggleExposurePlan(Target target, ExposurePlan exposurePlan) {
            using (var context = database.GetContext()) {
                Target updatedTarget = context.ToggleExposurePlan(target, exposurePlan);
                if (updatedTarget != null) {
                    activeTreeDataItem.Data = updatedTarget;
                    SetTreeColorizeMode(SelectedColorizeMode);
                    DitherManagerCache.Remove(target);
                } else {
                    Notification.ShowError("Failed to toggle on/off Scheduler Exposure Plan (see log for details)");
                }

                return updatedTarget;
            }
        }

        public Target DeleteExposurePlan(Target target, ExposurePlan exposurePlan) {
            using (var context = database.GetContext()) {
                Target updatedTarget = context.DeleteExposurePlan(target, exposurePlan);
                if (updatedTarget != null) {
                    activeTreeDataItem.Data = updatedTarget;
                    SetTreeColorizeMode(SelectedColorizeMode);
                    DitherManagerCache.Remove(target);
                } else {
                    Notification.ShowError("Failed to delete Scheduler Exposure Plan (see log for details)");
                }

                return updatedTarget;
            }
        }

        public Target DeleteAllExposurePlans(Target target) {
            using (var context = database.GetContext()) {
                Target updatedTarget = context.DeleteAllExposurePlans(target);
                if (updatedTarget != null) {
                    activeTreeDataItem.Data = updatedTarget;
                    SetTreeColorizeMode(SelectedColorizeMode);
                    DitherManagerCache.Remove(target);
                } else {
                    Notification.ShowError("Failed to delete all Scheduler Exposure Plans (see log for details)");
                }

                return updatedTarget;
            }
        }

        public Target ReloadTarget(Target reference) {
            using (var context = database.GetContext()) {
                Target reloadedTarget = context.GetTargetByProject(reference.ProjectId, reference.Id);
                if (reloadedTarget != null) {
                    activeTreeDataItem.Data = reloadedTarget;
                } else {
                    Notification.ShowError("Failed to reload target (see log for details)");
                }

                return reloadedTarget;
            }
        }

        public void AddNewExposureTemplate(TreeDataItem parentItem) {
            ProfileMeta profileMeta = (ProfileMeta)parentItem.Data;

            IProfile profile = ProfileLoader.GetProfile(profileService, profileMeta.Id.ToString());
            FilterInfo filterInfo = profile?.FilterWheelSettings?.FilterWheelFilters.FirstOrDefault();
            if (filterInfo == null) {
                TSLogger.Error("failed to get the first filter in profile's filter wheel");
                Notification.ShowError("Scheduler: failed to get the first filter in profile's filter wheel");
                return;
            }

            ExposureTemplate exposureTemplate = new ExposureTemplate(profileMeta.Id.ToString(), "<new template>", filterInfo.Name);

            using (var context = database.GetContext()) {
                ExposureTemplate newExposureTemplate = context.SaveExposureTemplate(exposureTemplate);
                if (newExposureTemplate != null) {
                    TreeDataItem exposureTemplateItem = new TreeDataItem(TreeDataType.ExposureTemplate, exposureTemplate.Name, exposureTemplate, parentItem);
                    parentItem.Items.Add(exposureTemplateItem);
                    exposureTemplateItem.IsSelected = true;
                    parentItem.IsExpanded = true;
                } else {
                    Notification.ShowError("Failed to save new Scheduler Exposure Template (see log for details)");
                }
            }
        }

        public void PasteExposureTemplate(TreeDataItem parentItem) {
            ProfileMeta profile = (ProfileMeta)parentItem.Data;

            if (!Clipboard.HasType(TreeDataType.ExposureTemplate)) {
                TSLogger.Error($"expected clipboard to hold Exposure Template");
                return;
            }

            ExposureTemplate source = Clipboard.GetItem().Data as ExposureTemplate;
            using (var context = database.GetContext()) {
                ExposureTemplate newExposureTemplate = context.PasteExposureTemplate(profile.Id.ToString(), source);
                if (newExposureTemplate != null) {
                    TreeDataItem newExposureTemplateItem = new TreeDataItem(TreeDataType.ExposureTemplate, newExposureTemplate.Name, newExposureTemplate, parentItem);
                    parentItem.Items.Add(newExposureTemplateItem);
                    newExposureTemplateItem.IsSelected = true;
                    parentItem.IsExpanded = true;
                } else {
                    Notification.ShowError("Failed to paste new Scheduler Exposure Template (see log for details)");
                }
            }
        }

        public void SaveExposureTemplate(ExposureTemplate exposureTemplate) {
            using (var context = database.GetContext()) {
                if (context.SaveExposureTemplate(exposureTemplate) != null) {
                    activeTreeDataItem.Data = exposureTemplate;
                    activeTreeDataItem.Header = exposureTemplate.Name;
                } else {
                    Notification.ShowError("Failed to save Scheduler Exposure Template (see log for details)");
                }
            }
        }

        public void DeleteExposureTemplate(ExposureTemplate exposureTemplate) {
            using (var context = database.GetContext()) {
                if (context.DeleteExposureTemplate(exposureTemplate)) {
                    TreeDataItem parentItem = activeTreeDataItem.TreeParent;
                    parentItem.Items.Remove(activeTreeDataItem);
                    parentItem.IsSelected = true;
                } else {
                    Notification.ShowError("Failed to delete Scheduler Exposure Template (see log for details)");
                }
            }
        }

        public bool MoveOrphanedExposureTemplate(ExposureTemplate exposureTemplate, string profileId) {
            using (var context = database.GetContext()) {
                ExposureTemplate newExposureTemplate = context.MoveExposureTemplate(exposureTemplate, profileId);
                if (newExposureTemplate != null) {
                    TreeDataItem parentItem = GetProfileItem(RootExposureTemplateList, profileId);
                    TreeDataItem newProjectItem = new TreeDataItem(TreeDataType.ExposureTemplate, newExposureTemplate.Name, newExposureTemplate, parentItem);
                    parentItem.Items.Add(newProjectItem);
                    return true;
                } else {
                    Notification.ShowError("Failed to move Scheduler orphaned Exposure Template (see log for details)");
                    return false;
                }
            }
        }

        public bool DeleteOrphanedExposureTemplate(ExposureTemplate exposureTemplate) {
            using (var context = database.GetContext()) {
                if (context.DeleteExposureTemplate(exposureTemplate)) {
                    return true;
                } else {
                    Notification.ShowError("Failed to delete Scheduler orphaned Exposure Template (see log for details)");
                    return false;
                }
            }
        }

        public int ExposureTemplateUsage(int exposureTemplateId) {
            using (var context = database.GetContext()) {
                return context.ExposurePlanSet.Where(ep => ep.ExposureTemplateId == exposureTemplateId).ToList().Count;
            }
        }

        public void CopyItem() {
            Clipboard.SetItem(activeTreeDataItem);
        }

        public List<ExposureTemplate> GetExposureTemplates(IProfile profile) {
            using (var context = database.GetContext()) {
                return context.GetExposureTemplates(profile.Id.ToString());
            }
        }

        public ExposureTemplate GetDefaultExposureTemplate(IProfile profile) {
            using (var context = database.GetContext()) {
                return context.GetExposureTemplates(profile.Id.ToString()).FirstOrDefault();
            }
        }

        private TreeDataItem GetProfileItem(List<TreeDataItem> rootList, string profileId) {
            foreach (TreeDataItem item in rootList[0].Items) {
                ProfileMeta profile = (ProfileMeta)item.Data;
                if (profile != null && profile.Id.ToString() == profileId) {
                    return item;
                }
            }

            TSLogger.Error($"Failed to find profile in nav tree: {profileId}");
            throw new Exception($"Failed to find profile in nav tree: {profileId}");
        }

        internal void SetTreeDisplayMode(TreeDisplayMode displayMode) {
            SelectedDisplayMode = displayMode;

            if (SelectedDisplayMode == TreeDisplayMode.DisplayAll) {
                TreeDataItem.VisitAll(RootProjectsList[0], item => { item.Visibility = Visibility.Visible; });
                return;
            }

            ExposureCompletionHelper helper = null;

            TreeDataItem.VisitAll(RootProjectsList[0], item => {
                switch (item.Type) {
                    case TreeDataType.Project:
                        Project project = item.Data as Project;
                        helper = GetExposureCompletionHelper(item.TreeParent.Data as ProfileMeta, project);
                        item.Visibility = ProjectActive(helper, project) ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case TreeDataType.Target:
                        Target target = item.Data as Target;
                        project = item.TreeParent.Data as Project;
                        item.Visibility = TargetActive(helper, project, target) ? Visibility.Visible : Visibility.Collapsed;
                        break;
                }
            });
        }

        private void InitializeProjectsColorize() {
            profileService.ActiveProfile.PropertyChanged += ActiveProfile_PropertyChanged;
            profileService.ActiveProfile.ColorSchemaSettings.PropertyChanged += ColorSchemaSettings_PropertyChanged;

            ColorSchemaPrimaryColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(profileService.ActiveProfile.ColorSchemaSettings.ColorSchema.PrimaryColor.ToString());
            ColorSchemaPrimaryColorBrush.Freeze();
            ActiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#008000"));
            ActiveBrush.Freeze();
            InactiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC143C"));
            InactiveBrush.Freeze();
        }

        private void ActiveProfile_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            ColorSchemaPrimaryColorBrush = new SolidColorBrush(profileService.ActiveProfile.ColorSchemaSettings.ColorSchema.PrimaryColor);
            ColorSchemaPrimaryColorBrush.Freeze();
            RaisePropertyChanged(nameof(ProjectsTreeViewVM));
        }

        private void ColorSchemaSettings_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            ColorSchemaPrimaryColorBrush = new SolidColorBrush(profileService.ActiveProfile.ColorSchemaSettings.ColorSchema.PrimaryColor);
            ColorSchemaPrimaryColorBrush.Freeze();
            RaisePropertyChanged(nameof(ProjectsTreeViewVM));
        }

        private Brush ColorSchemaPrimaryColorBrush;
        private Brush ActiveBrush;
        private Brush InactiveBrush;

        internal void SetTreeColorizeMode(bool colorize) {
            ExposureCompletionHelper helper = null;
            TextBlock textBlock = null;
            SelectedColorizeMode = colorize;

            using (var context = database.GetContext()) {
                TreeDataItem.VisitAll(RootProjectsList[0], item => {
                    switch (item.Type) {
                        case TreeDataType.Project:
                            Project project = item.Data as Project;
                            textBlock = item.Header as TextBlock;

                            if (colorize) {
                                project = context.GetProject(project.Id); // be sure it's up to date
                                helper = GetExposureCompletionHelper(item.TreeParent.Data as ProfileMeta, project);
                                textBlock.Foreground = ProjectActive(helper, project) ? ActiveBrush : InactiveBrush;
                            } else {
                                textBlock.Foreground = ColorSchemaPrimaryColorBrush;
                            }

                            break;

                        case TreeDataType.Target:
                            Target target = item.Data as Target;
                            textBlock = item.Header as TextBlock;

                            if (colorize) {
                                project = item.TreeParent.Data as Project;
                                textBlock.Foreground = TargetActive(helper, project, target) ? ActiveBrush : InactiveBrush;
                            } else {
                                textBlock.Foreground = ColorSchemaPrimaryColorBrush;
                            }

                            break;
                    }
                });
            }
        }

        private ExposureCompletionHelper GetExposureCompletionHelper(ProfileMeta profile, Project project) {
            ProfilePreference profilePreference = GetProfilePreference(profile.Id.ToString());
            return new ExposureCompletionHelper(project.EnableGrader, profilePreference.DelayGrading, profilePreference.ExposureThrottle);
        }

        private bool ProjectActive(ExposureCompletionHelper helper, Project project) {
            if (!project.ActiveNow || project.Targets == null || project.Targets.Count == 0) {
                return false;
            }

            foreach (Target target in project.Targets) {
                if (TargetActive(helper, project, target)) return true;
            }

            return false;
        }

        private bool TargetActive(ExposureCompletionHelper helper, Project project, Target target) {
            return project.ActiveNow
                && target.Enabled
                && target.ExposurePlans.Count > 0
                && helper.HasEnabledPlans(target)
                && helper.PercentComplete(target) < 100;
        }
    }

    public enum TreeDataType {
        ProjectRoot,
        ExposureTemplateRoot,
        ProjectProfile,
        OrphanedProjects,
        ExposureTemplateProfile,
        OrphanedExposureTemplates,
        Project,
        Target,
        ExposureTemplate
    }

    public class TreeDataItem : TreeViewItem, IComparable {
        public TreeDataType Type { get; }
        public TreeDataItem TreeParent { get; }
        public string SortName { get; set; }
        public object Data { get; set; }

        public TreeDataItem(TreeDataType type, string name, TreeDataItem parent) : this(type, name, null, parent) {
        }

        public TreeDataItem(TreeDataType type, string name, object data, TreeDataItem parent) {
            Type = type;
            TreeParent = parent;
            Data = data;
            SortName = name;

            if (type == TreeDataType.Project || type == TreeDataType.Target) {
                TextBlock textBlock = new TextBlock();
                textBlock.Text = name;
                Header = textBlock;
            } else {
                Header = name;
            }
        }

        public TreeDataItem GetRoot() {
            TreeDataItem item = this;
            while (item.TreeParent != null) {
                item = item.TreeParent;
            }

            return item;
        }

        public int CompareTo(object obj) {
            if (obj == null) {
                return 1;
            }

            TreeDataItem other = obj as TreeDataItem;
            return SortName.CompareTo(other.SortName);
        }

        public void SortChildren() {
            if (Items?.Count == 0) {
                return;
            }

            // This approach works to sort the tree initially.  However, it doesn't when trying to use it to resort
            // when a new item is added or item is renamed.  I think because rebuilding the list this way horks
            // the collection for subsequent view access.  I tried to go down the road of getting the ItemCollection
            // and playing nice with sorting via SortDescriptions but that didn't seem to work for TreeView.

            List<TreeDataItem> list = new List<TreeDataItem>(Items.Count);
            foreach (TreeDataItem item in Items) {
                list.Add(item);
            }

            list.Sort();
            Items.Clear();
            list.ForEach(i => Items.Add(i));
        }

        public static void VisitAll(TreeDataItem item, Action<TreeDataItem> action) {
            action(item);
            foreach (TreeDataItem child in item.Items) {
                VisitAll(child, action);
            }
        }
    }

    public class Clipboard {
        private static readonly Clipboard Instance = new Clipboard();
        private TreeDataItem item { get; set; }

        public static bool HasType(TreeDataType type) {
            return Instance.item?.Type == type;
        }

        public static void SetItem(TreeDataItem item) {
            Instance.item = item;
        }

        public static TreeDataItem GetItem() {
            TreeDataItem item = Instance.item;
            SetItem(null);
            return item;
        }

        public static void Clear() {
            SetItem(null);
        }

        private Clipboard() {
        }
    }

    public class ExposurePlansClipboard {
        private static readonly ExposurePlansClipboard Instance = new ExposurePlansClipboard();
        private ExposurePlansSpec item { get; set; }

        public static bool HasCopyItem() {
            return Instance.item != null;
        }

        public static void SetItem(List<ExposurePlan> exposurePlans, List<OverrideExposureOrderItem> overrideExposureOrders) {
            if (Common.IsNotEmpty(exposurePlans)) {
                Instance.item = new ExposurePlansSpec(exposurePlans, overrideExposureOrders);
            }
        }

        public static ExposurePlansSpec GetItem() {
            return Instance.item;
        }

        public static void Clear() {
            Instance.item = null;
        }

        private ExposurePlansClipboard() {
        }
    }

    public class ScoringRuleWeightsClipboard {
        private static readonly ScoringRuleWeightsClipboard Instance = new ScoringRuleWeightsClipboard();
        private List<RuleWeight> item { get; set; }

        public static bool HasCopyItem() {
            return Instance.item != null;
        }

        public static void SetItem(Project project) {
            Instance.item = new List<RuleWeight>(project.RuleWeights.Count);
            foreach (RuleWeight ruleWeight in project.RuleWeights) {
                Instance.item.Add(ruleWeight.GetPasteCopy());
            }
        }

        public static List<RuleWeight> GetItem() {
            return Instance.item;
        }

        public static void Clear() {
            Instance.item = null;
        }

        private ScoringRuleWeightsClipboard() {
        }
    }

    public class ExposurePlansSpec {
        public List<ExposurePlan> ExposurePlans { get; private set; }
        public List<OverrideExposureOrderItem> OverrideExposureOrders { get; private set; }

        public ExposurePlansSpec(List<ExposurePlan> exposurePlans, List<OverrideExposureOrderItem> overrideExposureOrders) {
            ExposurePlans = exposurePlans;
            OverrideExposureOrders = overrideExposureOrders;
        }
    }
}