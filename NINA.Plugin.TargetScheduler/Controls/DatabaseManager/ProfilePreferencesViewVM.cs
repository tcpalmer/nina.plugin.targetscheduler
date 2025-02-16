using NINA.Plugin.TargetScheduler.Controls.Converters;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class ProfilePreferencesViewVM : BaseVM {
        private DatabaseManagerVM managerVM;
        private ProfilePreference profilePreference;

        public string ProfileName { get; private set; }

        private ProfilePreferenceProxy profilePreferenceProxy;

        public ProfilePreferenceProxy ProfilePreferenceProxy {
            get => profilePreferenceProxy;
            set {
                profilePreferenceProxy = value;
                RaisePropertyChanged(nameof(ProfilePreferenceProxy));
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

        private List<string> _delayGradingChoices;

        public List<string> DelayGradingChoices {
            get {
                return _delayGradingChoices;
            }
            set {
                _delayGradingChoices = value;
                RaisePropertyChanged(nameof(DelayGradingChoices));
            }
        }

        public ProfilePreferencesViewVM(DatabaseManagerVM managerVM, IProfileService profileService, ProfilePreference profilePreference, string profileName) : base(profileService) {
            this.managerVM = managerVM;
            this.profilePreference = profilePreference;

            ProfileName = profileName;
            ProfilePreferenceProxy = new ProfilePreferenceProxy(profilePreference);

            EditCommand = new RelayCommand(Edit);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);

            InitializeCombos();
        }

        private void InitializeCombos() {
            DelayGradingChoices = new List<string>();
            DelayGradingChoices.Add(AltitudeChoicesConverter.OFF);
            for (int i = 30; i <= 90; i += 10) {
                DelayGradingChoices.Add(i + "%");
            }
        }

        private void ProfilePreferenceProxy_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e?.PropertyName != nameof(ProfilePreferenceProxy.Proxy)) {
                ItemEdited = true;
            } else {
                RaisePropertyChanged(nameof(ProfilePreferenceProxy));
            }
        }

        public ICommand EditCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private void Edit() {
            ProfilePreferenceProxy.PropertyChanged += ProfilePreferenceProxy_PropertyChanged;
            managerVM.SetEditMode(true);
            ShowEditView = true;
            ItemEdited = false;
        }

        private void Save() {
            TSLogger.SetLogLevel(ProfilePreferenceProxy.ProfilePreference.LogLevel);
            managerVM.SaveProfilePreference(ProfilePreferenceProxy.ProfilePreference);
            ProfilePreferenceProxy.OnSave();
            ProfilePreferenceProxy.PropertyChanged -= ProfilePreferenceProxy_PropertyChanged;
            ShowEditView = false;
            ItemEdited = false;
            managerVM.SetEditMode(false);
        }

        private void Cancel() {
            ProfilePreferenceProxy.OnCancel();
            ProfilePreferenceProxy.PropertyChanged -= ProfilePreferenceProxy_PropertyChanged;
            ShowEditView = false;
            ItemEdited = false;
            managerVM.SetEditMode(false);
        }
    }
}