using NINA.Astrometry;
using NINA.Core.MyMessageBox;
using NINA.Plugin.TargetScheduler.Controls.Converters;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class ProjectViewVM : BaseVM {
        private DatabaseManagerVM managerVM;
        private IFramingAssistantVM framingAssistantVM;
        private ProjectProxy projectProxy;
        private ExposureCompletionHelper exposureCompletionHelper;

        public ProjectProxy ProjectProxy {
            get => projectProxy;
            set {
                projectProxy = value;
                RaisePropertyChanged(nameof(ProjectProxy));
            }
        }

        public ProjectViewVM(DatabaseManagerVM managerVM, IFramingAssistantVM framingAssistantVM, IProfileService profileService, Project project) : base(profileService) {
            this.managerVM = managerVM;
            this.framingAssistantVM = framingAssistantVM;
            exposureCompletionHelper = GetExposureCompletionHelper(project);

            project.RuleWeights.Sort();
            ProjectProxy = new ProjectProxy(project);
            ProjectActive = ActiveNowWithActiveTargets(ProjectProxy.Project);

            InitializeRuleWeights(ProjectProxy.Proxy);
            InitializeCombos();

            EditCommand = new RelayCommand(Edit);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            CopyCommand = new RelayCommand(Copy);
            DeleteCommand = new RelayCommand(Delete);
            AddTargetCommand = new RelayCommand(AddTarget);
            ResetTargetsCommand = new RelayCommand(ResetTargets);
            PasteTargetCommand = new RelayCommand(PasteTarget);
            MoveTargetCommand = new RelayCommand(MoveTarget);
            ImportMosaicPanelsCommand = new RelayCommand(ImportMosaicPanels);
            CopyScoringRuleWeightsCommand = new RelayCommand(CopyScoringRuleWeights);
            PasteScoringRuleWeightsCommand = new RelayCommand(PasteScoringRuleWeights);
            ResetScoringRuleWeightsCommand = new RelayCommand(ResetScoringRuleWeights);
        }

        private ExposureCompletionHelper GetExposureCompletionHelper(Project project) {
            ProfilePreference profilePreference = managerVM.Database.GetContext().GetProfilePreference(project.ProfileId, true);
            return new ExposureCompletionHelper(project.EnableGrader, profilePreference.DelayGrading, profilePreference.ExposureThrottle);
        }

        private bool ActiveNowWithActiveTargets(Project project) {
            if (!project.ActiveNow || project.Targets == null || project.Targets.Count == 0) {
                return false;
            }

            foreach (Target target in project.Targets) {
                if (target.Enabled && exposureCompletionHelper.PercentComplete(target) < 100) {
                    return true;
                }
            }

            return false;
        }

        private void InitializeRuleWeights(Project project) {
            List<RuleWeight> ruleWeights = new List<RuleWeight>();

            project.RuleWeights.ForEach((rw) => {
                rw.PropertyChanged -= ProjectProxy_PropertyChanged;
                rw.PropertyChanged += ProjectProxy_PropertyChanged;
                ruleWeights.Add(rw);
            });

            RuleWeights = ruleWeights;
        }

        private void ProjectProxy_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e?.PropertyName != nameof(ProjectProxy.Proxy)) {
                ItemEdited = true;
            } else {
                ProjectActive = ActiveNowWithActiveTargets(ProjectProxy.Project);
                RaisePropertyChanged(nameof(ProjectProxy));
            }
        }

        private bool projectActive;

        public bool ProjectActive {
            get {
                return projectActive;
            }
            set {
                projectActive = value;
                RaisePropertyChanged(nameof(ProjectActive));
            }
        }

        private void InitializeCombos() {
            MinimumTimeChoices = new List<string>();
            MinimumTimeChoices.Add(Utils.MtoHM(5));
            MinimumTimeChoices.Add(Utils.MtoHM(10));
            MinimumTimeChoices.Add(Utils.MtoHM(20));
            for (int i = 30; i <= 240; i += 30) {
                MinimumTimeChoices.Add(Utils.MtoHM(i));
            }

            MinimumAltitudeChoices = new List<string>();
            MinimumAltitudeChoices.Add(AltitudeChoicesConverter.OFF);
            for (int i = 5; i <= 60; i += 5) {
                MinimumAltitudeChoices.Add(i + "°");
            }

            MaximumAltitudeChoices = new List<string>();
            MaximumAltitudeChoices.Add(AltitudeChoicesConverter.OFF);
            for (int i = 50; i <= 85; i += 5) {
                MaximumAltitudeChoices.Add(i + "°");
            }

            FlatsHandlingChoices = new List<string> {
                "Off",
                "1","2","3","5","7","10","14",
                "Target Completion",
                "Use With Immediate"
            };
        }

        private List<RuleWeight> ruleWeights = new List<RuleWeight>();

        public List<RuleWeight> RuleWeights {
            get => ruleWeights;
            set {
                ruleWeights = value;
                RaisePropertyChanged(nameof(RuleWeights));
            }
        }

        private List<string> _minimumTimeChoices;

        public List<string> MinimumTimeChoices {
            get => _minimumTimeChoices;
            set {
                _minimumTimeChoices = value;
                RaisePropertyChanged(nameof(MinimumTimeChoices));
            }
        }

        private List<string> _minimumAltitudeChoices;

        public List<string> MinimumAltitudeChoices {
            get {
                return _minimumAltitudeChoices;
            }
            set {
                _minimumAltitudeChoices = value;
                RaisePropertyChanged(nameof(MinimumAltitudeChoices));
            }
        }

        private List<string> _maximumAltitudeChoices;

        public List<string> MaximumAltitudeChoices {
            get {
                return _maximumAltitudeChoices;
            }
            set {
                _maximumAltitudeChoices = value;
                RaisePropertyChanged(nameof(MaximumAltitudeChoices));
            }
        }

        private List<string> _flatsHandlingChoices;

        public List<string> FlatsHandlingChoices {
            get {
                return _flatsHandlingChoices;
            }
            set {
                _flatsHandlingChoices = value;
                RaisePropertyChanged(nameof(FlatsHandlingChoices));
            }
        }

        private bool showEditView = false;

        public bool ShowEditView {
            get => showEditView;
            set {
                showEditView = value;
                RaisePropertyChanged(nameof(ShowEditView));
                RaisePropertyChanged(nameof(PasteScoringRuleWeightsEnabled));
            }
        }

        private bool itemEdited = false;

        public bool ItemEdited {
            get => itemEdited;
            set {
                itemEdited = value;
                RaisePropertyChanged(nameof(ItemEdited));
            }
        }

        public bool PasteTargetEnabled { get => Clipboard.HasType(TreeDataType.Target); }
        public bool PasteScoringRuleWeightsEnabled { get => ScoringRuleWeightsClipboard.HasCopyItem() && !ShowEditView; }

        public bool MosaicPanelsAvailable {
            get => FramingAssistantPanelsDefined() > 1;
        }

        public ICommand EditCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand AddTargetCommand { get; private set; }
        public ICommand ResetTargetsCommand { get; private set; }
        public ICommand PasteTargetCommand { get; private set; }
        public ICommand MoveTargetCommand { get; private set; }
        public ICommand ImportMosaicPanelsCommand { get; private set; }
        public ICommand CopyScoringRuleWeightsCommand { get; private set; }
        public ICommand PasteScoringRuleWeightsCommand { get; private set; }
        public ICommand ResetScoringRuleWeightsCommand { get; private set; }

        private void Edit() {
            ProjectProxy.PropertyChanged += ProjectProxy_PropertyChanged;
            managerVM.SetEditMode(true);
            ShowEditView = true;
            ItemEdited = false;
        }

        private void Save() {
            // Prevent save if minimum time setting is such that it would never allow a meridian window to work properly
            if (ProjectProxy.Proxy.MeridianWindow > 0 && ProjectProxy.Proxy.MinimumTime > (ProjectProxy.Proxy.MeridianWindow * 2)) {
                string message = $"Minimum Time must be less than twice the Meridian Window or the project will never be selected for imaging.";
                MyMessageBox.Show(message, "Oops");
                return;
            }

            ProjectProxy.Proxy.RuleWeights = RuleWeights;
            managerVM.SaveProject(ProjectProxy.Proxy);
            ProjectProxy.OnSave();
            InitializeRuleWeights(ProjectProxy.Proxy);
            ProjectProxy.PropertyChanged -= ProjectProxy_PropertyChanged;
            ShowEditView = false;
            ItemEdited = false;
            managerVM.SetEditMode(false);
        }

        private void Cancel() {
            ProjectProxy.OnCancel();
            ProjectProxy.PropertyChanged -= ProjectProxy_PropertyChanged;
            InitializeRuleWeights(ProjectProxy.Proxy);
            ShowEditView = false;
            ItemEdited = false;
            managerVM.SetEditMode(false);
        }

        private void Copy() {
            managerVM.CopyItem();
        }

        private void Delete() {
            string message = $"Delete project '{ProjectProxy.Project.Name}' and any associated targets?  This cannot be undone.";
            if (MyMessageBox.Show(message, "Delete Project?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                managerVM.DeleteProject(ProjectProxy.Proxy);
            }
        }

        private void AddTarget() {
            managerVM.AddNewTarget(ProjectProxy.Proxy);
        }

        private void ResetTargets() {
            string message = $"Reset target completion (accepted and acquired counts) on all targets under '{ProjectProxy.Project.Name}'?  This cannot be undone.";
            if (MyMessageBox.Show(message, "Reset Target Completion?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                managerVM.ResetProjectTargets();
            }
        }

        private void PasteTarget() {
            managerVM.PasteTarget(ProjectProxy.Proxy);
            ProjectActive = ActiveNowWithActiveTargets(ProjectProxy.Project);
        }

        private void MoveTarget() {
            managerVM.MoveTarget(ProjectProxy.Proxy);
            ProjectActive = ActiveNowWithActiveTargets(ProjectProxy.Project);
        }

        private void ImportMosaicPanels() {
            int panels = FramingAssistantPanelsDefined();
            if (panels == 1) {
                MyMessageBox.Show("The Framing Assistant only defines one panel at the moment.", "Oops");
                return;
            }

            string message = $"Add {panels} mosaic panels as new targets to project '{ProjectProxy.Project.Name}'?";
            if (MyMessageBox.Show(message, "Add Targets?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                List<Target> targets = new List<Target>();
                foreach (FramingRectangle rect in framingAssistantVM.CameraRectangles) {
                    TSLogger.Debug($"Add mosaic panel as target: {rect.Name} {rect.Coordinates.RAString} {rect.Coordinates.DecString} rot={rect.DSOPositionAngle}");
                    Target target = new Target();
                    target.Name = rect.Name;
                    target.ra = rect.Coordinates.RA;
                    target.dec = rect.Coordinates.Dec;
                    target.Rotation = rect.DSOPositionAngle;
                    targets.Add(target);
                }

                managerVM.AddTargets(ProjectProxy.Project, targets);
                ProjectProxy.Proxy.IsMosaic = true;
                Save();
            }
        }

        private void CopyScoringRuleWeights() {
            ScoringRuleWeightsClipboard.SetItem(projectProxy.Project);
            RaisePropertyChanged(nameof(PasteScoringRuleWeightsEnabled));
        }

        private void PasteScoringRuleWeights() {
            List<RuleWeight> weights = ScoringRuleWeightsClipboard.GetItem();
            foreach (RuleWeight weight in ProjectProxy.Proxy.RuleWeights) {
                RuleWeight newRuleWeight = weights.Where(rw => rw.Name == weight.Name).FirstOrDefault();
                weight.Weight = newRuleWeight.Weight;
            }

            managerVM.SaveProject(ProjectProxy.Proxy);
            ProjectProxy.OnSave();
            InitializeRuleWeights(ProjectProxy.Proxy);
        }

        private void ResetScoringRuleWeights() {
            Dictionary<string, IScoringRule> rules = ScoringRule.GetAllScoringRules();
            List<RuleWeight> weights = new List<RuleWeight>(rules.Count);
            foreach (var rule in rules) {
                weights.Add(new RuleWeight { Name = rule.Value.Name, Weight = rule.Value.DefaultWeight });
            }

            foreach (RuleWeight weight in ProjectProxy.Proxy.RuleWeights) {
                RuleWeight newRuleWeight = weights.Where(rw => rw.Name == weight.Name).FirstOrDefault();
                weight.Weight = newRuleWeight.Weight;
            }

            managerVM.SaveProject(ProjectProxy.Proxy);
            ProjectProxy.OnSave();
            InitializeRuleWeights(ProjectProxy.Proxy);
        }

        private int FramingAssistantPanelsDefined() {
            return framingAssistantVM.VerticalPanels * framingAssistantVM.HorizontalPanels;
        }
    }
}