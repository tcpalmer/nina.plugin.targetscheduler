using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.Collections.Generic;
using System.Windows.Input;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using RelayCommandParam = CommunityToolkit.Mvvm.Input.RelayCommand<object>;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class ExposureTemplateProfileViewVM : BaseVM {
        private DatabaseManagerVM managerVM;
        private ProfileMeta profile;
        private TreeDataItem parentItem;
        private List<ExposureTemplateRowView> exposureTemplates;

        public ProfileMeta Profile {
            get => profile;
            set {
                profile = value;
                RaisePropertyChanged(nameof(Profile));
            }
        }

        public List<ExposureTemplateRowView> ExposureTemplates {
            get => exposureTemplates;
            set {
                exposureTemplates = value;
                RaisePropertyChanged(nameof(ExposureTemplates));
            }
        }

        public bool PasteEnabled {
            get => Clipboard.HasType(TreeDataType.ExposureTemplate);
        }

        public ExposureTemplateProfileViewVM(DatabaseManagerVM managerVM, IProfileService profileService, TreeDataItem profileItem) : base(profileService) {
            this.managerVM = managerVM;
            Profile = (ProfileMeta)profileItem.Data;
            parentItem = profileItem;
            ExposureTemplates = InitExposureTemplates(profileItem);

            AddExposureTemplateCommand = new RelayCommand(AddExposureTemplate);
            PasteExposureTemplateCommand = new RelayCommand(PasteExposureTemplate);
            ViewExposureTemplateCommand = new RelayCommandParam(ViewExposureTemplate);
            CopyExposureTemplateCommand = new RelayCommandParam(CopyExposureTemplate);
        }

        private List<ExposureTemplateRowView> InitExposureTemplates(TreeDataItem profileItem) {
            List<ExposureTemplateRowView> exposureTemplates = new List<ExposureTemplateRowView>();
            foreach (TreeDataItem item in profileItem.Items) {
                exposureTemplates.Add(new ExposureTemplateRowView((ExposureTemplate)item.Data));
            }

            return exposureTemplates;
        }

        public ICommand AddExposureTemplateCommand { get; private set; }
        public ICommand PasteExposureTemplateCommand { get; private set; }
        public ICommand ViewExposureTemplateCommand { get; private set; }
        public ICommand CopyExposureTemplateCommand { get; private set; }

        private void AddExposureTemplate() {
            managerVM.AddNewExposureTemplate(parentItem);
        }

        private void PasteExposureTemplate() {
            managerVM.PasteExposureTemplate(parentItem);
        }

        private void ViewExposureTemplate(object obj) {
            ExposureTemplate exposureTemplate = (obj as ExposureTemplateRowView).ExposureTemplate;
            if (exposureTemplate != null) {
                TreeDataItem item = Find(exposureTemplate);
                if (item != null) {
                    managerVM.NavigateTo(item);
                }
            }
        }

        private void CopyExposureTemplate(object obj) {
            ExposureTemplate exposureTemplate = (obj as ExposureTemplateRowView).ExposureTemplate;
            if (exposureTemplate != null) {
                TreeDataItem item = Find(exposureTemplate);
                if (item != null) {
                    Clipboard.SetItem(item);
                    RaisePropertyChanged(nameof(PasteEnabled));
                }
            }
        }

        private TreeDataItem Find(ExposureTemplate exposureTemplate) {
            foreach (TreeDataItem item in parentItem.Items) {
                if (((ExposureTemplate)item.Data).Id == exposureTemplate.Id) {
                    return item;
                }
            }

            return null;
        }
    }

    public class ExposureTemplateRowView {
        public ExposureTemplate ExposureTemplate;

        public ExposureTemplateRowView(ExposureTemplate exposureTemplate) {
            this.ExposureTemplate = exposureTemplate;
        }

        public string Name => ExposureTemplate.Name;
        public string FilterName => ExposureTemplate.FilterName;
        public double DefaultExposure => ExposureTemplate.DefaultExposure;
        public int Gain => ExposureTemplate.Gain;
        public int Offset => ExposureTemplate.Offset;
        public BinningMode BinningMode => ExposureTemplate.BinningMode;
        public string MinutesOffset => ExposureTemplate.MinutesOffset > 0 ? ExposureTemplate.MinutesOffset.ToString() : "";
        public string DitherEvery => ExposureTemplate.DitherEvery != -1 ? ExposureTemplate.DitherEvery.ToString() : "";
        public bool MoonAvoidanceEnabled => ExposureTemplate.MoonAvoidanceEnabled;

        public string TwilightLevel {
            get {
                switch (ExposureTemplate.TwilightLevel) {
                    case Astrometry.TwilightLevel.Nighttime: return "Night";
                    case Astrometry.TwilightLevel.Astronomical: return "Astro";
                    default: return ExposureTemplate.TwilightLevel.ToString();
                }
            }
        }
    }
}