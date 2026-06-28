using NINA.Core.Model.Equipment;
using NINA.Core.MyMessageBox;
using NINA.Plugin.TargetScheduler.Controls.Converters;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class ExposureTemplateViewVM : BaseVM {
        private DatabaseManagerVM managerVM;
        private IProfile profile;

        public ExposureTemplateViewVM(DatabaseManagerVM managerVM, IProfileService profileService, ExposureTemplate exposureTemplate) : base(profileService) {
            this.managerVM = managerVM;
            this.profile = ProfileLoader.GetProfile(profileService, exposureTemplate.ProfileId);

            ExposureTemplateProxy = new ExposureTemplateProxy(exposureTemplate);

            EditCommand = new RelayCommand(Edit);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            CopyCommand = new RelayCommand(Copy);
            DeleteCommand = new RelayCommand(Delete);
            MoonAvoidanceHelperCommand = new RelayCommand(MoonAvoidanceHelper);

            InitializeCombos();
        }

        private void InitializeCombos() {
            FilterNameChoices = GetFilterNamesForProfile();

            BinningModeChoices = new List<BinningMode> {
                    new BinningMode(1,1),
                    new BinningMode(2,2),
                    new BinningMode(3,3),
                    new BinningMode(4,4),
            };

            RelaxScaleChoices = [RelaxScaleChoicesConverter.OFF];
            for (int d = 1; d <= 8; d++) {
                RelaxScaleChoices.Add(d.ToString());
            }

            MoonAvoidanceEnabledProxy = ExposureTemplateProxy.Proxy.MoonAvoidanceEnabled;
            MoonDownEnabledProxy = ExposureTemplateProxy.Proxy.MoonDownEnabled;
            RelaxScaleProxy = ExposureTemplateProxy.Proxy.MoonRelaxScale;
        }

        private List<string> GetFilterNamesForProfile() {
            var filterNames = new List<string>();

            foreach (FilterInfo filterInfo in profile?.FilterWheelSettings?.FilterWheelFilters) {
                filterNames.Add(filterInfo.Name);
            }

            return filterNames;
        }

        private List<string> _filterNameChoices;

        public List<string> FilterNameChoices {
            get => _filterNameChoices;
            set {
                _filterNameChoices = value;
                RaisePropertyChanged(nameof(FilterNameChoices));
            }
        }

        private List<BinningMode> _binningModeChoices;

        public List<BinningMode> BinningModeChoices {
            get => _binningModeChoices;
            set {
                _binningModeChoices = value;
                RaisePropertyChanged(nameof(BinningModeChoices));
            }
        }

        private bool _relaxEnabled;

        public bool RelaxEnabled {
            get => _relaxEnabled;
            set {
                _relaxEnabled = value;
                RaisePropertyChanged(nameof(RelaxEnabled));
            }
        }

        private List<string> _relaxScaleChoices;

        public List<string> RelaxScaleChoices {
            get {
                return _relaxScaleChoices;
            }
            set {
                _relaxScaleChoices = value;
                RaisePropertyChanged(nameof(RelaxScaleChoices));
            }
        }

        // These proxies let us manage enable/disable of avoidance relaxed fields

        private bool _moonAvoidanceEnabledProxy;

        public bool MoonAvoidanceEnabledProxy {
            get {
                return _moonAvoidanceEnabledProxy;
            }
            set {
                _moonAvoidanceEnabledProxy = value;
                ExposureTemplateProxy.Proxy.MoonAvoidanceEnabled = _moonAvoidanceEnabledProxy;
                RaisePropertyChanged(nameof(MoonAvoidanceEnabledProxy));
                SetRelaxEnabled();
            }
        }

        private bool _moonDownEnabledProxy;

        public bool MoonDownEnabledProxy {
            get {
                return _moonDownEnabledProxy;
            }
            set {
                _moonDownEnabledProxy = value;
                ExposureTemplateProxy.Proxy.MoonDownEnabled = _moonDownEnabledProxy;
                RaisePropertyChanged(nameof(MoonDownEnabledProxy));
            }
        }

        private double _relaxScaleProxy;

        public double RelaxScaleProxy {
            get {
                return _relaxScaleProxy;
            }
            set {
                _relaxScaleProxy = value;
                ExposureTemplateProxy.Proxy.MoonRelaxScale = _relaxScaleProxy;
                RaisePropertyChanged(nameof(RelaxScaleProxy));
                SetRelaxEnabled();
            }
        }

        private void SetRelaxEnabled() {
            RelaxEnabled = ExposureTemplateProxy.Proxy.MoonAvoidanceEnabled && RelaxScaleProxy > 0;
        }

        private ExposureTemplateProxy exposureTemplateProxy;

        public ExposureTemplateProxy ExposureTemplateProxy {
            get => exposureTemplateProxy;
            set {
                exposureTemplateProxy = value;
                RaisePropertyChanged(nameof(ExposureTemplateProxy));
            }
        }

        private void ExposureTemplateProxy_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e?.PropertyName != nameof(TargetProxy.Proxy)) {
                ItemEdited = true;
            } else {
                RaisePropertyChanged(nameof(ExposureTemplateProxy));
            }
        }

        private bool showEditView = false;

        public bool ShowEditView {
            get => showEditView;
            set {
                showEditView = value;
                RaisePropertyChanged(nameof(ShowEditView));
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

        public ICommand EditCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand MoonAvoidanceHelperCommand { get; private set; }

        private void MoonAvoidanceHelper() {
            ExposureTemplate et = ExposureTemplateProxy.Proxy;
            MoonAvoidanceHelperVM helperVM = new MoonAvoidanceHelperVM(
                et.MoonAvoidanceEnabled, et.MoonAvoidanceSeparation, et.MoonAvoidanceWidth,
                et.MoonRelaxScale, et.MoonRelaxMinAltitude, et.MoonRelaxMaxAltitude, et.MoonDownEnabled);
            MoonAvoidanceHelperWindow window = new MoonAvoidanceHelperWindow {
                DataContext = helperVM,
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true) {
                MoonAvoidanceEnabledProxy = helperVM.ClassicEnabled;
                et.MoonAvoidanceSeparation = helperVM.ClassicSeparation;
                et.MoonAvoidanceWidth = helperVM.ClassicWidth;
                RelaxScaleProxy = helperVM.RelaxScale;
                et.MoonRelaxMinAltitude = helperVM.RelaxMinAltitude;
                et.MoonRelaxMaxAltitude = helperVM.RelaxMaxAltitude;
                MoonDownEnabledProxy = helperVM.MoonDownEnabled;
            }
        }

        private void Edit() {
            ExposureTemplateProxy.PropertyChanged += ExposureTemplateProxy_PropertyChanged;
            managerVM.SetEditMode(true);
            ShowEditView = true;
            ItemEdited = false;
        }

        private void Save() {
            managerVM.SaveExposureTemplate(ExposureTemplateProxy.Proxy);
            ExposureTemplateProxy.OnSave();
            ExposureTemplateProxy.PropertyChanged -= ExposureTemplateProxy_PropertyChanged;
            ShowEditView = false;
            ItemEdited = false;
            managerVM.SetEditMode(false);
        }

        private void Cancel() {
            ExposureTemplateProxy.OnCancel();
            ExposureTemplateProxy.PropertyChanged -= ExposureTemplateProxy_PropertyChanged;
            ShowEditView = false;
            ItemEdited = false;
            managerVM.SetEditMode(false);
        }

        private void Copy() {
            managerVM.CopyItem();
        }

        private void Delete() {
            int count = managerVM.ExposureTemplateUsage(ExposureTemplateProxy.ExposureTemplate.Id);
            string msg = count > 0
                ? $"Are you sure?  '{ExposureTemplateProxy.ExposureTemplate.Name}' is in use by {count} exposure plan(s).  If those exposure plans are still active, deleting may cause problems later.  This cannot be undone."
                : $"Delete exposure template '{ExposureTemplateProxy.ExposureTemplate.Name}'?  This cannot be undone.";

            if (MyMessageBox.Show(msg, "Delete Exposure Template?", MessageBoxButton.YesNo, MessageBoxResult.No) == MessageBoxResult.Yes) {
                managerVM.DeleteExposureTemplate(ExposureTemplateProxy.Proxy);
            }
        }
    }
}