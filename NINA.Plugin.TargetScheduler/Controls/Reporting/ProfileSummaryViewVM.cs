using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.Reporting {

    public class ProfileSummaryViewVM : INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<TreeViewItem> ProjectItems { get; } = new ObservableCollection<TreeViewItem>();

        public ICommand ToggleExpandCollapseCommand { get; }

        private bool _allExpanded = false;
        public bool IsAllExpanded => _allExpanded;

        public ProfileSummaryViewVM() {
            ToggleExpandCollapseCommand = new RelayCommand(ToggleExpandCollapse);
        }

        private void ToggleExpandCollapse() {
            _allExpanded = !_allExpanded;
            foreach (TreeViewItem projectItem in ProjectItems) {
                projectItem.IsExpanded = _allExpanded;
                foreach (TreeViewItem targetItem in projectItem.Items) {
                    targetItem.IsExpanded = _allExpanded;
                }
            }
            RaisePropertyChanged(nameof(IsAllExpanded));
        }

        public static ProfileSummaryViewVM Build(ProfilePreference profilePreference, List<Project> projects) {
            var vm = new ProfileSummaryViewVM();
            vm.Populate(profilePreference, projects);
            return vm;
        }

        private void Populate(ProfilePreference profilePreference, List<Project> projects) {
            if (projects == null || projects.Count == 0) {
                ProjectItems.Add(new TreeViewItem { Header = "No projects found for this profile", IsEnabled = false });
                return;
            }

            var projectRows = new List<(Project project, ExposureCompletionHelper helper, double percent, bool provisional)>();
            foreach (var project in projects) {
                var helper = new ExposureCompletionHelper(
                    project.EnableGrader,
                    profilePreference.DelayGrading,
                    profilePreference.ExposureThrottle);

                var plans = (project.Targets ?? new List<Target>())
                    .SelectMany(t => t.ExposurePlans ?? new List<ExposurePlan>())
                    .ToList();

                double percent = WeightedPercent(plans, helper.PercentComplete);
                bool provisional = plans.Any(helper.IsProvisionalPercentComplete);
                projectRows.Add((project, helper, percent, provisional));
            }

            projectRows.Sort((a, b) => b.percent.CompareTo(a.percent));

            foreach (var pRow in projectRows) {
                string pSuffix = !pRow.project.EnableGrader ? ", grading disabled" : Label(pRow.provisional);
                var projectItem = new TreeViewItem {
                    Header = $"Project: {pRow.project.Name} ({pRow.percent:F2}%{pSuffix})",
                    IsExpanded = false,
                    FontWeight = FontWeights.SemiBold
                };

                var targetRows = new List<(Target target, double percent, bool provisional)>();
                foreach (var target in pRow.project.Targets ?? new List<Target>()) {
                    var plans = target.ExposurePlans ?? new List<ExposurePlan>();
                    double percent = WeightedPercent(plans, pRow.helper.PercentComplete);
                    bool provisional = plans.Any(pRow.helper.IsProvisionalPercentComplete);
                    targetRows.Add((target, percent, provisional));
                }

                targetRows.Sort((a, b) => b.percent.CompareTo(a.percent));

                foreach (var tRow in targetRows) {
                    var targetItem = new TreeViewItem {
                        Header = $"{tRow.target.Name} ({tRow.percent:F2}%{Label(tRow.provisional)})",
                        IsExpanded = false
                    };

                    var epRows = (tRow.target.ExposurePlans ?? new List<ExposurePlan>())
                        .Select(ep => new {
                            ep,
                            percent = pRow.helper.PercentComplete(ep),
                            provisional = pRow.helper.IsProvisionalPercentComplete(ep)
                        })
                        .OrderByDescending(x => x.percent)
                        .ToList();

                    foreach (var epRow in epRows) {
                        string templateName = epRow.ep.ExposureTemplate?.Name ?? "Unknown";
                        string filterName = epRow.ep.ExposureTemplate?.FilterName ?? "Unknown";
                        targetItem.Items.Add(new TreeViewItem {
                            Header = $"{templateName} / {filterName} ({epRow.percent:F2}%{Label(epRow.provisional)})"
                        });
                    }

                    projectItem.Items.Add(targetItem);
                }

                ProjectItems.Add(projectItem);
            }
        }

        private static double WeightedPercent(List<ExposurePlan> plans, Func<ExposurePlan, double> planPercent) {
            int totalDesired = plans.Sum(ep => ep.Desired);
            if (totalDesired == 0) return 0;
            return plans.Sum(ep => planPercent(ep) * ep.Desired) / totalDesired;
        }

        private static string Label(bool provisional) => provisional ? ", pre-grading" : "";
    }
}
