using Accord.Statistics.Kernels;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.Collections.Generic;
using System.Windows.Input;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class DatabaseTreeViewVM : BaseVM {
        public bool ShowActiveInactive { get; private set; }

        public DatabaseTreeViewVM(DatabaseManagerVM managerVM, IProfileService profileService, string name, List<TreeDataItem> rootList, int height, bool showActiveInactive = false) : base(profileService) {
            ParentVM = managerVM;
            RootList = rootList;
            Name = name;
            Height = height;
            ShowActiveInactive = showActiveInactive;

            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            ToggleDisplayModeCommand = new RelayCommand(ToggleDisplayMode);
            ToggleColorizeModeCommand = new RelayCommand(ToggleColorizeMode);
            RefreshCommand = new RelayCommand(Refresh);
        }

        public DatabaseManagerVM ParentVM { get; private set; }

        private List<TreeDataItem> rootList;

        public List<TreeDataItem> RootList {
            get => rootList;
            set {
                rootList = value;
                RaisePropertyChanged(nameof(RootList));
            }
        }

        public string Name { get; private set; }
        public int Height { get; private set; }

        public ICommand ExpandAllCommand { get; private set; }
        public ICommand CollapseAllCommand { get; private set; }
        public ICommand ToggleDisplayModeCommand { get; private set; }
        public ICommand ToggleColorizeModeCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        private void ExpandAll() {
            TreeDataItem.VisitAll(RootList[0], i => { i.IsExpanded = true; });
        }

        private void CollapseAll() {
            TreeDataItem.VisitAll(RootList[0], i => { i.IsExpanded = false; });
        }

        public bool EnableDisplayAll => ParentVM.SelectedDisplayMode == TreeDisplayMode.DisplayAll;

        private void ToggleDisplayMode() {
            TreeDisplayMode mode = ParentVM.SelectedDisplayMode == TreeDisplayMode.DisplayAll ? TreeDisplayMode.DisplayActiveOnly : TreeDisplayMode.DisplayAll;
            ParentVM.SetTreeDisplayMode(mode);
            ParentVM.SetTreeColorizeMode(ParentVM.SelectedColorizeMode);
        }

        public bool EnableColorize => ParentVM.SelectedColorizeMode;

        private void ToggleColorizeMode() {
            bool mode = ParentVM.SelectedColorizeMode ? false : true;
            ParentVM.SetTreeColorizeMode(mode);
        }

        public void Refresh() {
            List<TreeDataItem> refreshed = ParentVM.Refresh(RootList);
            if (refreshed != null) {
                RootList = refreshed;
            }

            ParentVM.SelectedDisplayMode = TreeDisplayMode.DisplayAll;
            ParentVM.SelectedColorizeMode = false;
            Clipboard.Clear();
        }
    }

    public enum TreeDisplayMode { DisplayAll, DisplayActiveOnly }
}