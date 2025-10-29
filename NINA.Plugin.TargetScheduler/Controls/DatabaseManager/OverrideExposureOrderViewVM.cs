using CommunityToolkit.Mvvm.Input;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class OverrideExposureOrderViewVM : BaseVM {
        private static object lockObj = new object();
        private TargetViewVM targetViewVM;

        public OverrideExposureOrderViewVM(TargetViewVM targetViewVM, IProfileService profileService) : base(profileService) {
            this.targetViewVM = targetViewVM;
            displayOverrideExposureOrder = SetOverrideItems(targetViewVM.TargetProxy.Target);

            MoveItemUpCommand = new RelayCommand<object>(MoveItemUp);
            MoveItemDownCommand = new RelayCommand<object>(MoveItemDown);
            CopyItemCommand = new RelayCommand<object>(CopyItem);
            DeleteItemCommand = new RelayCommand<object>(DeleteItem);
            InsertDitherCommand = new RelayCommand<object>(InsertDither);

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        public ICommand MoveItemUpCommand { get; private set; }
        public ICommand MoveItemDownCommand { get; private set; }
        public ICommand CopyItemCommand { get; private set; }
        public ICommand DeleteItemCommand { get; private set; }
        public ICommand InsertDitherCommand { get; private set; }

        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private ObservableCollection<OverrideItem> displayOverrideExposureOrder;

        public ObservableCollection<OverrideItem> DisplayOverrideExposureOrder {
            get => displayOverrideExposureOrder;
            set {
                displayOverrideExposureOrder = value;
                RaisePropertyChanged(nameof(DisplayOverrideExposureOrder));
            }
        }

        private void MoveItemUp(object obj) {
            OverrideItem item = obj as OverrideItem;
            lock (lockObj) {
                int idx = GetItemIndex(item);
                DisplayOverrideExposureOrder.RemoveAt(idx);

                if (idx == 0) {
                    DisplayOverrideExposureOrder.Add(item);
                } else {
                    DisplayOverrideExposureOrder.Insert(idx - 1, item);
                }
            }
        }

        private void MoveItemDown(object obj) {
            OverrideItem item = obj as OverrideItem;
            lock (lockObj) {
                int idx = GetItemIndex(item);

                if (idx == DisplayOverrideExposureOrder.Count - 1) {
                    DisplayOverrideExposureOrder.RemoveAt(idx);
                    DisplayOverrideExposureOrder.Insert(0, item);
                } else {
                    DisplayOverrideExposureOrder.RemoveAt(idx);
                    DisplayOverrideExposureOrder.Insert(idx + 1, item);
                }
            }
        }

        private void CopyItem(object obj) {
            OverrideItem item = obj as OverrideItem;
            lock (lockObj) {
                DisplayOverrideExposureOrder.Insert(GetItemIndex(item) + 1, item.Clone());
            }
        }

        private void DeleteItem(object obj) {
            OverrideItem item = obj as OverrideItem;
            lock (lockObj) {
                DisplayOverrideExposureOrder.RemoveAt(GetItemIndex(item));
            }
        }

        private void InsertDither(object obj) {
            if (obj != null) {
                OverrideItem current = obj as OverrideItem;
                lock (lockObj) {
                    DisplayOverrideExposureOrder.Insert(GetItemIndex(current) + 1, new OverrideItem());
                }
            } else {
                lock (lockObj) {
                    DisplayOverrideExposureOrder.Add(new OverrideItem());
                }
            }
        }

        private int GetItemIndex(OverrideItem find) {
            return DisplayOverrideExposureOrder.IndexOf(find);
        }

        private void Save() {
            List<OverrideExposureOrderItem> overrideExposureOrders = new List<OverrideExposureOrderItem>();
            if (DisplayOverrideExposureOrder?.Count > 0) {
                int targetId = targetViewVM.TargetProxy.Target.Id;
                int order = 1;
                foreach (var item in DisplayOverrideExposureOrder) {
                    if (item.IsDither) {
                        overrideExposureOrders.Add(new OverrideExposureOrderItem(targetId, order, OverrideExposureOrderAction.Dither));
                    } else {
                        overrideExposureOrders.Add(new OverrideExposureOrderItem(targetId, order, OverrideExposureOrderAction.Exposure, item.ExposurePlanIdx));
                    }

                    order++;
                }
            }

            targetViewVM.SaveOverrideExposureOrder(overrideExposureOrders);
            targetViewVM.ShowOverrideExposureOrderPopup = false;
        }

        private void Cancel() {
            targetViewVM.ShowOverrideExposureOrderPopup = false;
        }

        private ObservableCollection<OverrideItem> SetOverrideItems(Target target) {
            ObservableCollection<OverrideItem> items = new ObservableCollection<OverrideItem>();
            if (Common.IsNotEmpty(target.OverrideExposureOrders)) {
                foreach (var oeo in target.OverrideExposureOrders) {
                    if (oeo.Action == OverrideExposureOrderAction.Exposure) {
                        string name = target.ExposurePlans[oeo.ReferenceIdx].ExposureTemplate.Name;
                        items.Add(new OverrideItem(name, oeo.ReferenceIdx));
                    } else {
                        items.Add(new OverrideItem());
                    }
                }
            } else {
                // The default is just exposures in order
                int idx = 0;
                foreach (var ep in target.ExposurePlans) {
                    if (ep.IsEnabled) {
                        string name = ep.ExposureTemplate.Name;
                        items.Add(new OverrideItem(name, idx++));
                    }
                }
            }

            return items;
        }
    }

    public class OverrideItem {
        public string Name { get; private set; }
        public int ExposurePlanIdx { get; private set; }
        public bool IsDither { get; private set; }

        public OverrideItem() {
            Name = OverrideExposureOrderAction.Dither.ToString();
            ExposurePlanIdx = -1;
            IsDither = true;
        }

        public OverrideItem(string name, int exposurePlanIdx) {
            Name = name;
            ExposurePlanIdx = exposurePlanIdx;
            IsDither = false;
        }

        public OverrideItem Clone() {
            return new OverrideItem {
                Name = Name,
                ExposurePlanIdx = ExposurePlanIdx,
                IsDither = IsDither
            };
        }
    }
}