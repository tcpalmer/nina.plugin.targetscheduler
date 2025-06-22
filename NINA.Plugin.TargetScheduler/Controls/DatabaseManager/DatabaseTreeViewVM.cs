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
            SwitchDisplayModeCommand = new RelayCommand(SwitchDisplayMode);
            SwitchColorizeModeCommand = new RelayCommand(SwitchColorizeMode);
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
        public ICommand SwitchDisplayModeCommand { get; private set; }
        public ICommand SwitchColorizeModeCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        private void ExpandAll() {
            TreeDataItem.VisitAll(RootList[0], i => { i.IsExpanded = true; });
        }

        private void CollapseAll() {
            TreeDataItem.VisitAll(RootList[0], i => { i.IsExpanded = false; });
        }

        private TreeDisplayMode displayMode = TreeDisplayMode.DisplayAll;

        public TreeDisplayMode DisplayMode {
            get => displayMode;
            private set {
                displayMode = value;
                ShowDisplayAll = value == TreeDisplayMode.DisplayAll;
            }
        }

        private bool showDisplayAll = true;

        public bool ShowDisplayAll {
            get => showDisplayAll;
            private set {
                showDisplayAll = value;
                RaisePropertyChanged(nameof(ShowDisplayAll));
            }
        }

        private bool colorizeProjectsTargets = false;

        public bool ColorizeProjectsTargets {
            get => colorizeProjectsTargets;
            private set {
                colorizeProjectsTargets = value;
                RaisePropertyChanged(nameof(ColorizeProjectsTargets));
            }
        }

        private void SwitchDisplayMode() {
            DisplayMode = DisplayMode == TreeDisplayMode.DisplayAll ? TreeDisplayMode.DisplayActiveOnly : TreeDisplayMode.DisplayAll;
            ParentVM.SetTreeDisplayMode(DisplayMode);
            ParentVM.SetTreeColorizeMode(ColorizeProjectsTargets);
        }

        private void SwitchColorizeMode() {
            ColorizeProjectsTargets = ColorizeProjectsTargets ? false : true;
            ParentVM.SetTreeColorizeMode(ColorizeProjectsTargets);
        }

        public void Refresh() {
            List<TreeDataItem> refreshed = ParentVM.Refresh(RootList);
            if (refreshed != null) {
                RootList = refreshed;
            }

            DisplayMode = TreeDisplayMode.DisplayAll;
            ColorizeProjectsTargets = false;
            Clipboard.Clear();
        }
    }

    public enum TreeDisplayMode { DisplayAll, DisplayActiveOnly }
}