using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.MyMessageBox;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using RelayCommandParam = CommunityToolkit.Mvvm.Input.RelayCommand<object>;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class TargetViewVM : BaseVM {
        private IApplicationMediator applicationMediator;
        private IFramingAssistantVM framingAssistantVM;

        private DatabaseManagerVM managerVM;
        private Project project;
        private IProfile profile;
        private string profileId;
        private ExposureCompletionHelper exposureCompletionHelper;
        public List<ExposureTemplate> exposureTemplates;

        public TargetViewVM(DatabaseManagerVM managerVM,
            IProfileService profileService,
            IApplicationMediator applicationMediator,
            IFramingAssistantVM framingAssistantVM,
            IDeepSkyObjectSearchVM deepSkyObjectSearchVM,
            IPlanetariumFactory planetariumFactory,
            Target target,
            Project project) : base(profileService) {
            this.applicationMediator = applicationMediator;
            this.framingAssistantVM = framingAssistantVM;
            this.managerVM = managerVM;
            this.project = project;
            exposureCompletionHelper = GetExposureCompletionHelper(project);

            profileId = project.ProfileId;
            TargetProxy = new TargetProxy(target);
            TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);

            profile = ProfileLoader.GetProfile(profileService, profileId);
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            InitializeExposurePlans(TargetProxy.Proxy);
            InitializeExposureTemplateList(profile);
            SetExposureOrderDisplay();

            EditCommand = new RelayCommand(Edit);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            CopyCommand = new RelayCommand(Copy);
            DeleteCommand = new RelayCommand(Delete);
            ResetTargetCommand = new RelayCommand(ResetTarget);
            GradeCommand = new RelayCommand(Grade);
            RefreshCommand = new RelayCommand(Refresh);

            ShowTargetImportViewCommand = new RelayCommand(ShowTargetImportViewCmd);
            AddExposurePlanCommand = new RelayCommand(AddExposurePlan);
            CopyExposurePlansCommand = new RelayCommand(CopyExposurePlans);
            PasteExposurePlansCommand = new RelayCommand(PasteExposurePlans);
            DeleteExposurePlansCommand = new RelayCommand(DeleteAllExposurePlans);
            DeleteExposurePlanCommand = new RelayCommandParam((obj) => DeleteExposurePlan(obj), (obj) => true);
            ToggleExposurePlanCommand = new RelayCommandParam((obj) => ToggleExposurePlan(obj), (obj) => true);
            OverrideExposureOrderCommand = new RelayCommand(DisplayOverrideExposureOrder);
            CancelOverrideExposureOrderCommand = new RelayCommand(CancelOverrideExposureOrder);
            SendCoordinatesToFramingAssistantCommand = new AsyncRelayCommand(SendCoordinatesToFramingAssistant);

            TargetImportVM = new TargetImportVM(deepSkyObjectSearchVM, framingAssistantVM, planetariumFactory);
            TargetImportVM.PropertyChanged += ImportTarget_PropertyChanged;
        }

        private ExposureCompletionHelper GetExposureCompletionHelper(Project project) {
            ProfilePreference profilePreference = managerVM.Database.GetContext().GetProfilePreference(project.ProfileId, true);
            return new ExposureCompletionHelper(project.EnableGrader, profilePreference.DelayGrading, profilePreference.ExposureThrottle);
        }

        private bool ActiveWithActiveExposurePlans(Target target) {
            return target.Project.ActiveNow && target.Enabled && target.ExposurePlans.Count > 0 && exposureCompletionHelper.PercentComplete(target) < 100;
        }

        private TargetProxy targetProxy;

        public TargetProxy TargetProxy {
            get => targetProxy;
            set {
                targetProxy = value;
                RaisePropertyChanged(nameof(TargetProxy));
            }
        }

        private void TargetProxy_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e?.PropertyName != nameof(TargetProxy.Proxy)) {
                ItemEdited = true;
            } else {
                TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);
                RaisePropertyChanged(nameof(TargetProxy));
            }
        }

        private bool targetActive;

        public bool TargetActive {
            get {
                return targetActive;
            }
            set {
                targetActive = value;
                RaisePropertyChanged(nameof(TargetActive));
            }
        }

        public DeepSkyObject TargetDSO {
            get {
                Target target = TargetProxy.Target;
                DeepSkyObject dso = new DeepSkyObject(string.Empty, target.Coordinates, string.Empty, profileService.ActiveProfile.AstrometrySettings.Horizon);
                dso.Name = target.Name;
                dso.RotationPositionAngle = target.Rotation;
                return dso;
            }
        }

        private void InitializeExposurePlans(Target target) {
            List<ExposurePlan> exposurePlans = new List<ExposurePlan>();

            target.ExposurePlans.ForEach((plan) => {
                plan.PercentComplete = exposureCompletionHelper.PercentComplete(plan);
                plan.PropertyChanged -= TargetProxy_PropertyChanged;
                plan.PropertyChanged += TargetProxy_PropertyChanged;
                exposurePlans.Add(plan);
            });

            ExposurePlans = exposurePlans;

            List<ExposurePlanVM> vmList = new List<ExposurePlanVM>(ExposurePlans.Count);
            ExposurePlans.ForEach((plan) => { vmList.Add(new ExposurePlanVM(exposureCompletionHelper, plan)); });
            ExposurePlanVMList = vmList;
        }

        private List<ExposurePlan> exposurePlans = new List<ExposurePlan>();

        public List<ExposurePlan> ExposurePlans {
            get => exposurePlans;
            set {
                exposurePlans = value;
                RaisePropertyChanged(nameof(ExposurePlans));
                SetExposureOrderDisplay();
            }
        }

        private List<ExposurePlanVM> exposurePlanVMList = new List<ExposurePlanVM>();

        public List<ExposurePlanVM> ExposurePlanVMList {
            get => exposurePlanVMList;
            set {
                exposurePlanVMList = value;
                RaisePropertyChanged(nameof(ExposurePlanVMList));
            }
        }

        private void ProfileService_ProfileChanged(object sender, System.EventArgs e) {
            InitializeExposureTemplateList(profile);
        }

        private void InitializeExposureTemplateList(IProfile profile) {
            exposureTemplates = managerVM.GetExposureTemplates(profile);
            ExposureTemplateChoices = new AsyncObservableCollection<KeyValuePair<int, string>>();
            exposureTemplates.ForEach(et => {
                ExposureTemplateChoices.Add(new KeyValuePair<int, string>(et.Id, et.Name));
            });

            RaisePropertyChanged(nameof(ExposureTemplateChoices));
        }

        private AsyncObservableCollection<KeyValuePair<int, string>> exposureTemplateChoices;

        public AsyncObservableCollection<KeyValuePair<int, string>> ExposureTemplateChoices {
            get {
                return exposureTemplateChoices;
            }
            set {
                exposureTemplateChoices = value;
            }
        }

        private bool showEditView = false;

        public bool ShowEditView {
            get => showEditView;
            set {
                showEditView = value;
                RaisePropertyChanged(nameof(ShowEditView));
                RaisePropertyChanged(nameof(ExposurePlansCopyEnabled));
                RaisePropertyChanged(nameof(ExposurePlansPasteEnabled));
                RaisePropertyChanged(nameof(ExposurePlansDeleteEnabled));
            }
        }

        private bool showTargetImportView = false;

        public bool ShowTargetImportView {
            get => showTargetImportView;
            set {
                showTargetImportView = value;
                RaisePropertyChanged(nameof(ShowTargetImportView));
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

        public bool ExposurePlansCopyEnabled {
            get => !ShowEditView && Common.IsNotEmpty(TargetProxy.Original.ExposurePlans);
        }

        public bool ExposurePlansPasteEnabled {
            get => !ShowEditView && ExposurePlansClipboard.HasCopyItem();
        }

        public bool ExposurePlansDeleteEnabled {
            get => !ShowEditView && Common.IsNotEmpty(TargetProxy.Original.ExposurePlans);
        }

        private TargetImportVM targetImportVM;
        public TargetImportVM TargetImportVM { get => targetImportVM; set => targetImportVM = value; }

        private void ImportTarget_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (!ShowEditView) {
                return;
            }

            if (TargetImportVM.Target.Name != null) {
                TargetProxy.Proxy.Name = TargetImportVM.Target.Name;
            }

            TargetProxy.Proxy.Coordinates = TargetImportVM.Target.Coordinates;
            TargetProxy.Proxy.Rotation = TargetImportVM.Target.Rotation;
            RaisePropertyChanged(nameof(TargetProxy.Proxy));
        }

        public ICommand EditCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand ResetTargetCommand { get; private set; }
        public ICommand GradeCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public ICommand SendCoordinatesToFramingAssistantCommand { get; private set; }
        public ICommand ShowTargetImportViewCommand { get; private set; }

        public ICommand AddExposurePlanCommand { get; private set; }
        public ICommand CopyExposurePlansCommand { get; private set; }
        public ICommand PasteExposurePlansCommand { get; private set; }
        public ICommand DeleteExposurePlansCommand { get; private set; }
        public ICommand DeleteExposurePlanCommand { get; private set; }
        public ICommand ToggleExposurePlanCommand { get; private set; }

        public ICommand OverrideExposureOrderCommand { get; private set; }
        public ICommand CancelOverrideExposureOrderCommand { get; private set; }

        private void Edit() {
            Refresh(); // force a refresh since display could be out of date
            TargetProxy.PropertyChanged += TargetProxy_PropertyChanged;
            managerVM.SetEditMode(true);
            ShowEditView = true;
            ItemEdited = false;
        }

        private void ShowTargetImportViewCmd() {
            ShowTargetImportView = !ShowTargetImportView;
        }

        private void Save() {
            TargetProxy.Proxy.ExposurePlans = ExposurePlans;

            // If exposure plans have been added or removed, we have to clear override exposure order and filter cadence
            if (TargetProxy.Proxy.ExposurePlans.Count != TargetProxy.Original.ExposurePlans.Count) {
                TargetProxy.Proxy.OverrideExposureOrders = new List<OverrideExposureOrderItem>();
                TargetProxy.Proxy.FilterCadences = new List<FilterCadenceItem>();
            }

            managerVM.SaveTarget(TargetProxy.Proxy);
            TargetProxy.OnSave();
            InitializeExposurePlans(TargetProxy.Proxy);
            TargetProxy.PropertyChanged -= TargetProxy_PropertyChanged;
            ShowEditView = false;
            ItemEdited = false;
            ShowTargetImportView = false;
            SetExposureOrderDisplay();

            managerVM.SetEditMode(false);
        }

        private void Cancel() {
            TargetProxy.OnCancel();
            TargetProxy.PropertyChanged -= TargetProxy_PropertyChanged;
            InitializeExposurePlans(TargetProxy.Proxy);
            ShowEditView = false;
            ItemEdited = false;
            ShowTargetImportView = false;
            managerVM.SetEditMode(false);
        }

        private void Copy() {
            managerVM.CopyItem();
        }

        private void Delete() {
            bool deleteAcquiredImagesWithTarget = managerVM.GetProfilePreference(profileId).EnableDeleteAcquiredImagesWithTarget;
            string message = deleteAcquiredImagesWithTarget
                ? $"Delete target '{TargetProxy.Target.Name}' and all associated acquired image records?  This cannot be undone."
                : $"Delete target '{TargetProxy.Target.Name}'?  This cannot be undone.";
            if (MyMessageBox.Show(message, "Delete Target?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                managerVM.DeleteTarget(TargetProxy.Proxy, deleteAcquiredImagesWithTarget);
            }
        }

        private void ResetTarget() {
            string message = $"Reset target completion (accepted and acquired counts) on all Exposure Plans for '{TargetProxy.Proxy.Name}'?  This cannot be undone.";
            if (MyMessageBox.Show(message, "Reset Target Completion?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                Target updatedTarget = managerVM.ResetTarget(TargetProxy.Original);
                if (updatedTarget != null) {
                    TargetProxy = new TargetProxy(updatedTarget);
                    InitializeExposurePlans(TargetProxy.Proxy);
                    TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);
                }
            }
        }

        private void Grade() {
            if (!project.EnableGrader) {
                MyMessageBox.Show("You cannot grade since the project has grading disabled.", "Oops");
                return;
            }

            string message = $"Trigger grading now on all Exposure Plans for '{TargetProxy.Proxy.Name}'?  This cannot be undone.";
            if (MyMessageBox.Show(message, "Trigger Grading?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                MyMessageBox.Show("Grading will be run in the background.  You can refresh exposure plans shortly to see the results.", "Grade");
                managerVM.GradeTarget(TargetProxy.Proxy);
            }
        }

        private void Refresh() {
            Target target = managerVM.ReloadTarget(TargetProxy.Proxy);
            if (target != null) {
                TargetProxy = new TargetProxy(target);
                TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);
                InitializeExposurePlans(TargetProxy.Proxy);
            }
        }

        private ExposureTemplate GetDefaultExposureTemplate() {
            ExposureTemplate exposureTemplate = managerVM.GetDefaultExposureTemplate(profile);
            if (exposureTemplate == null) {
                MyMessageBox.Show("Can't find a default Exposure Template.  You must create some Exposure Templates for this profile before creating an Exposure Plan.", "Oops");
                return null;
            }

            return exposureTemplate;
        }

        private void AddExposurePlan() {
            ExposureTemplate exposureTemplate = GetDefaultExposureTemplate();
            if (exposureTemplate == null) {
                return;
            }

            Target proxy = TargetProxy.Proxy;
            ExposurePlan exposurePlan = new ExposurePlan(profile.Id.ToString());
            exposurePlan.ExposureTemplate = exposureTemplate;
            exposurePlan.ExposureTemplateId = exposureTemplate.Id;
            exposurePlan.TargetId = proxy.Id;

            proxy.ExposurePlans.Add(exposurePlan);
            InitializeExposurePlans(proxy);
            ItemEdited = true;
        }

        private void CopyExposurePlans() {
            if (Common.IsNotEmpty(ExposurePlans)) {
                List<ExposurePlan> exposurePlans = new List<ExposurePlan>(ExposurePlans.Count);
                ExposurePlans.ForEach(ep => exposurePlans.Add(ep));
                List<OverrideExposureOrderItem> overrideExposureOrders = new List<OverrideExposureOrderItem>();
                TargetProxy.Target.OverrideExposureOrders.ForEach(oeo => overrideExposureOrders.Add(oeo));
                ExposurePlansClipboard.SetItem(exposurePlans, overrideExposureOrders);
                RaisePropertyChanged(nameof(ExposurePlansPasteEnabled));
            } else {
                ExposurePlansClipboard.Clear();
                RaisePropertyChanged(nameof(ExposurePlansPasteEnabled));
            }
        }

        private void PasteExposurePlans() {
            ExposurePlansSpec source = ExposurePlansClipboard.GetItem();
            List<ExposurePlan> srcExposurePlans = source.ExposurePlans;

            if (srcExposurePlans?.Count == 0) {
                return;
            }

            List<OverrideExposureOrderItem> srcOverrideExposureOrders = new List<OverrideExposureOrderItem>();
            source.OverrideExposureOrders.ForEach(oeo => srcOverrideExposureOrders.Add(oeo.GetPasteCopy(TargetProxy.Proxy.Id)));

            ExposureTemplate exposureTemplate = null;
            if (srcExposurePlans[0].ExposureTemplate.ProfileId != profileId) {
                MyMessageBox.Show("The copied Exposure Plans reference Exposure Templates from a different profile.  They will be defaulted to the default (first) Exposure Template for this profile.");
                exposureTemplate = GetDefaultExposureTemplate();
                if (exposureTemplate == null) {
                    return;
                }

                srcOverrideExposureOrders.Clear();
            }

            // Only paste override order if existing target has no exposure plans now
            if (ExposurePlans.Count == 0 || Common.IsNotEmpty(srcOverrideExposureOrders)) {
                TargetProxy.Proxy.OverrideExposureOrders = srcOverrideExposureOrders;
            }

            foreach (ExposurePlan copy in srcExposurePlans) {
                ExposurePlan ep = copy.GetPasteCopy(profileId);
                ep.TargetId = TargetProxy.Original.Id;

                if (exposureTemplate != null) {
                    ep.ExposureTemplateId = exposureTemplate.Id;
                    ep.ExposureTemplate = exposureTemplate;
                }

                ExposurePlans.Add(ep);
            }

            TargetProxy.Proxy.ExposurePlans = ExposurePlans;

            managerVM.SaveTarget(TargetProxy.Proxy);
            TargetProxy.OnSave();
            InitializeExposurePlans(TargetProxy.Proxy);
            RaisePropertyChanged(nameof(ExposurePlans));
            RaisePropertyChanged(nameof(ExposurePlansCopyEnabled));
            RaisePropertyChanged(nameof(ExposurePlansDeleteEnabled));

            TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);
        }

        private void DeleteAllExposurePlans() {
            if (Common.IsNotEmpty(TargetProxy.Original.ExposurePlans)) {
                string message = "Delete all exposure plans for this target?  This cannot be undone.";
                if (MyMessageBox.Show(message, "Delete all Exposure Plans?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                    Target updatedTarget = managerVM.DeleteAllExposurePlans(TargetProxy.Original);
                    if (updatedTarget != null) {
                        TargetProxy = new TargetProxy(updatedTarget);
                        InitializeExposurePlans(TargetProxy.Proxy);
                        RaisePropertyChanged(nameof(ExposurePlansCopyEnabled));
                        RaisePropertyChanged(nameof(ExposurePlansDeleteEnabled));
                        TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);
                        SetExposureOrderDisplay();
                    }
                }
            }
        }

        private void ToggleExposurePlan(object obj) {
            ExposurePlanVM item = obj as ExposurePlanVM;
            ExposurePlan exposurePlan = TargetProxy.Original.ExposurePlans.Where(ep => ep.Id == item?.ExposurePlan.Id).FirstOrDefault();
            if (exposurePlan != null) {
                Target updatedTarget = managerVM.ToggleExposurePlan(TargetProxy.Original, exposurePlan);
                if (updatedTarget != null) {
                    TargetProxy = new TargetProxy(updatedTarget);
                    // InitializeExposurePlans(TargetProxy.Proxy);
                    TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);
                    SetExposureOrderDisplay();
                }
            } else {
                TSLogger.Error($"failed to find original exposure plan: {item?.ExposurePlan.Id}");
            }
        }

        private void DeleteExposurePlan(object obj) {
            ExposurePlanVM item = obj as ExposurePlanVM;
            ExposurePlan exposurePlan = TargetProxy.Original.ExposurePlans.Where(ep => ep.Id == item?.ExposurePlan.Id).FirstOrDefault();
            if (exposurePlan != null) {
                string message = $"Delete exposure plan using template '{exposurePlan.ExposureTemplate?.Name}'?  This cannot be undone.";
                if (MyMessageBox.Show(message, "Delete Exposure Plan?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                    Target updatedTarget = managerVM.DeleteExposurePlan(TargetProxy.Original, exposurePlan);
                    if (updatedTarget != null) {
                        TargetProxy = new TargetProxy(updatedTarget);
                        InitializeExposurePlans(TargetProxy.Proxy);
                        RaisePropertyChanged(nameof(ExposurePlansDeleteEnabled));
                        TargetActive = ActiveWithActiveExposurePlans(TargetProxy.Target);
                        SetExposureOrderDisplay();
                    }
                }
            } else {
                TSLogger.Error($"failed to find original exposure plan: {item?.ExposurePlan.Id}");
            }
        }

        private string exposureOrderDisplay;

        public string ExposureOrderDisplay {
            get {
                return exposureOrderDisplay;
            }
            set {
                exposureOrderDisplay = value;
                RaisePropertyChanged(nameof(ExposureOrderDisplay));
            }
        }

        private bool showOverrideExposureOrderPopup = false;

        public bool ShowOverrideExposureOrderPopup {
            get => showOverrideExposureOrderPopup;
            set {
                showOverrideExposureOrderPopup = value;
                RaisePropertyChanged(nameof(ShowOverrideExposureOrderPopup));
            }
        }

        private OverrideExposureOrderViewVM overrideExposureOrderVM;

        public OverrideExposureOrderViewVM OverrideExposureOrderVM {
            get => overrideExposureOrderVM;
            set {
                overrideExposureOrderVM = value;
                RaisePropertyChanged(nameof(OverrideExposureOrderVM));
            }
        }

        public bool HaveFSFExposureOrder { get => !HaveOverrideExposureOrder && !HaveSmartExposureOrder; private set { } }
        public bool HaveOverrideExposureOrder { get => Common.IsNotEmpty(TargetProxy.Original.OverrideExposureOrders); private set { } }
        public bool HaveSmartExposureOrder { get => TargetProxy.Target.Project.SmartExposureOrder; private set { } }

        private void SetExposureOrderDisplay() {
            ExposureOrderDisplay = HaveSmartExposureOrder
                ? string.Empty : HaveOverrideExposureOrder
                    ? GetOverrideExposureOrder()
                    : GetDefaultExposureOrder();

            RaisePropertyChanged(nameof(ExposureOrderDisplay));
            RaisePropertyChanged(nameof(HaveFSFExposureOrder));
            RaisePropertyChanged(nameof(HaveOverrideExposureOrder));
            RaisePropertyChanged(nameof(HaveSmartExposureOrder));
        }

        private string GetDefaultExposureOrder() {
            StringBuilder sb = new StringBuilder();
            List<string> exposureInstructions = new List<string>();

            int filterSwitchFrequency = project.FilterSwitchFrequency;
            int ditherEvery = project.DitherEvery;

            foreach (ExposurePlan plan in ExposurePlans.Where(ep => ep.IsEnabled)) {
                if (filterSwitchFrequency == 0) {
                    sb.Append(plan.ExposureTemplate.Name).Append("..., ");
                } else {
                    for (int i = 0; i < filterSwitchFrequency; i++) {
                        sb.Append(plan.ExposureTemplate.Name).Append(", ");
                        exposureInstructions.Add(plan.ExposureTemplate.Name);
                    }
                }
            }

            if (filterSwitchFrequency == 0 || ditherEvery == 0) {
                return sb.ToString().TrimEnd().TrimEnd(new Char[] { ',' });
            }

            List<string> dithered = new DitherInjector(exposureInstructions, ditherEvery).ExposureOrderInject();
            StringBuilder sb2 = new StringBuilder();
            foreach (string item in dithered) {
                sb2.Append(item).Append(", ");
            }

            return sb2.ToString().TrimEnd().TrimEnd(new Char[] { ',' });
        }

        private string GetOverrideExposureOrder() {
            if (Common.IsNotEmpty(TargetProxy.Proxy.OverrideExposureOrders)) {
                StringBuilder sb = new StringBuilder();
                TargetProxy.Proxy.OverrideExposureOrders.ForEach((oeo) => {
                    if (oeo.Action == OverrideExposureOrderAction.Dither) {
                        sb.Append("Dither, ");
                    } else {
                        string name = ExposurePlans[oeo.ReferenceIdx].ExposureTemplate.Name;
                        sb.Append(name).Append(", ");
                    }
                });

                return sb.ToString().TrimEnd().TrimEnd(new Char[] { ',' });
            } else {
                return "";
            }
        }

        private void DisplayOverrideExposureOrder() {
            OverrideExposureOrderVM = new OverrideExposureOrderViewVM(this, profileService);
            ShowOverrideExposureOrderPopup = true;
        }

        private void CancelOverrideExposureOrder() {
            string message = $"Clear override exposure order?  This cannot be undone.";
            if (MyMessageBox.Show(message, "Clear?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                TargetProxy.Proxy.OverrideExposureOrders = new List<OverrideExposureOrderItem>();
                managerVM.SaveTarget(TargetProxy.Proxy, true);
                TargetProxy.OnSave();
                SetExposureOrderDisplay();
            }
        }

        public void SaveOverrideExposureOrder(List<OverrideExposureOrderItem> overrideExposureOrders) {
            TargetProxy.Target.OverrideExposureOrders = overrideExposureOrders;
            managerVM.SaveTarget(TargetProxy.Proxy, true);
            TargetProxy.OnSave();
            SetExposureOrderDisplay();
        }

        private async Task<bool> SendCoordinatesToFramingAssistant() {
            applicationMediator.ChangeTab(ApplicationTab.FRAMINGASSISTANT);
            // Note that IFramingAssistantVM doesn't expose any properties to set the rotation, although they are on the impl
            return await framingAssistantVM.SetCoordinates(TargetDSO);
        }
    }

    public class ExposurePlanVM : BaseINPC {
        public ExposurePlan ExposurePlan { get; private set; }
        public bool IsProvisional { get; private set; }
        public string PercentComplete { get; private set; }
        public string ProvisionalPercentComplete { get; private set; }

        public ExposurePlanVM(ExposureCompletionHelper helper, ExposurePlan exposurePlan) {
            ExposurePlan = exposurePlan;
            string pc = string.Format("{0:0}%", helper.PercentComplete(exposurePlan));
            if (helper.IsProvisionalPercentComplete(exposurePlan)) {
                IsProvisional = true;
                PercentComplete = "-";
                ProvisionalPercentComplete = pc;
            } else {
                IsProvisional = false;
                PercentComplete = pc;
                ProvisionalPercentComplete = "-";
            }
        }

        public bool IsEnabled {
            get => ExposurePlan.IsEnabled;
            set {
                ExposurePlan.IsEnabled = value;
                RaiseAllPropertiesChanged();
            }
        }

        public ExposureTemplate ExposureTemplate { get => ExposurePlan.ExposureTemplate; set => ExposurePlan.ExposureTemplate = value; }
        public int ExposureTemplateId { get => ExposurePlan.ExposureTemplateId; set => ExposurePlan.ExposureTemplateId = value; }
        public double Exposure { get => ExposurePlan.Exposure; set => ExposurePlan.Exposure = value; }
        public int Desired { get => ExposurePlan.Desired; set => ExposurePlan.Desired = value; }
        public int Accepted { get => ExposurePlan.Accepted; set => ExposurePlan.Accepted = value; }
        public int Acquired { get => ExposurePlan.Acquired; set => ExposurePlan.Acquired = value; }
    }
}