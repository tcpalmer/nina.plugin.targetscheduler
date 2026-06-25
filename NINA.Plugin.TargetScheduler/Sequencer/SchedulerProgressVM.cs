using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Util;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public class SchedulerProgressVM : BaseINPC {
        public const string SlewLabel = "Slew";
        public const string BeforeTargetLabel = "BeforeTarget";
        public const string SwitchFilterLabel = "SwitchFilter";
        public const string TakeExposureLabel = "TakeExposure";
        public const string PausedLabel = "Paused";
        public const string DitherLabel = "Dither";

        public SchedulerProgressVM() {
        }

        private ProgressCollection progressItemList;

        public ProgressCollection ProgressItemList {
            get {
                if (progressItemList == null) {
                    // Create the collection and its default view on the UI thread so the view's
                    // thread-affinity is always bound to the dispatcher thread, regardless of which
                    // thread first touches this property (see issue #7).
                    RunOnUiThread(() => {
                        progressItemList = new ProgressCollection();
                        ItemsView = CollectionViewSource.GetDefaultView(progressItemList);
                        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
                    });
                }

                return progressItemList;
            }
            set {
                progressItemList = value;
            }
        }

        /// <summary>
        /// Run the action on the application dispatcher (UI) thread. Falls back to running inline
        /// when there is no application/dispatcher (e.g. headless test runs).
        /// </summary>
        private static void RunOnUiThread(Action action) {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null) {
                dispatcher.Invoke(action);
            } else {
                action();
            }
        }

        private ICollectionView itemsView;

        public ICollectionView ItemsView {
            get => itemsView;
            set {
                itemsView = value;
            }
        }

        public SchedulerProgressRow CurrentRow = null;
        public string CurrentGroup = null;

        private void EndCurrent() {
            if (CurrentRow != null) {
                CurrentRow.Finish();
            }
        }

        public void WaitStart(DateTime? waitUntil) {
            EndCurrent();
            CurrentGroup = $"Waiting : {DateTime.Now.ToString(Utils.DateFMT)} -> {waitUntil?.ToString(Utils.DateFMT)}";
        }

        public void TargetStart(string projectName, string targetName) {
            EndCurrent();
            CurrentGroup = $"{projectName} / {targetName} : {DateTime.Now.ToString(Utils.DateFMT)}";
        }

        public void Add(string name, string filter = "") {
            if (SameSwitchFilter(name, filter)) { return; }
            RunOnUiThread(() => {
                EndCurrent();
                CurrentRow = new SchedulerProgressRow(CurrentGroup, name, filter);
                ProgressItemList.Add(CurrentRow);

                RaisePropertyChanged(nameof(ItemsView));
            });
        }

        private bool SameSwitchFilter(string name, string filter) {
            if (name != SwitchFilterLabel) { return false; }

            foreach (var item in ProgressItemList.Reverse()) {
                if (!item.Group.Equals(CurrentGroup)) return false;
                if (item.ItemName == SwitchFilterLabel) {
                    return item.FilterName == filter;
                }
            }

            return false;
        }

        public void End() {
            RunOnUiThread(() => {
                EndCurrent();
                CurrentRow = null;
                CurrentGroup = null;
            });

            RaisePropertyChanged(nameof(ItemsView));
        }

        public void Reset() {
            RunOnUiThread(() => {
                ProgressItemList.Clear();
            });

            RaisePropertyChanged(nameof(ItemsView));
        }
    }

    public class ProgressCollection : AsyncObservableCollection<SchedulerProgressRow> { }

    public class SchedulerProgressRow : BaseINPC {

        public SchedulerProgressRow(string group, string itemName, string filterName) {
            this.Group = group;
            this.ItemName = itemName;
            this.FilterName = filterName;

            StartTime = DateTime.Now;
            IsComplete = false;
        }

        public void Finish() {
            EndTime = DateTime.Now;
            IsComplete = true;
            RaiseAllPropertiesChanged();
        }

        public string Group { get; private set; }
        public string ItemName { get; private set; }
        public string FilterName { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; set; }
        public bool IsComplete { get; set; }
        public GeometryGroup Complete { get => Application.Current?.TryFindResource("CheckedSVG") as GeometryGroup; }
    }
}