using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Controls.Reporting {

    public class ProfileSummaryReport {

        public static string Generate(string profileId, SchedulerDatabaseInteraction database) {
            ProfilePreference profilePreference;
            List<Project> projects;

            using (var context = database.GetContext()) {
                profilePreference = context.GetProfilePreference(profileId, true);
                projects = context.GetAllProjectsReadOnly(profileId);
            }

            if (projects == null || projects.Count == 0) {
                return $"No projects found for profile {profileId}";
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

            var sb = new StringBuilder();

            bool firstProject = true;
            foreach (var pRow in projectRows) {
                if (!firstProject) sb.AppendLine();
                firstProject = false;

                string projectSuffix = !pRow.project.EnableGrader
                    ? ", grading disabled"
                    : Label(pRow.provisional);
                sb.AppendLine($"Project: {pRow.project.Name} ({pRow.percent:F2}%{projectSuffix})");

                var targetRows = new List<(Target target, double percent, bool provisional)>();
                foreach (var target in pRow.project.Targets ?? new List<Target>()) {
                    var plans = target.ExposurePlans ?? new List<ExposurePlan>();
                    double percent = WeightedPercent(plans, pRow.helper.PercentComplete);
                    bool provisional = plans.Any(pRow.helper.IsProvisionalPercentComplete);
                    targetRows.Add((target, percent, provisional));
                }

                targetRows.Sort((a, b) => b.percent.CompareTo(a.percent));

                foreach (var tRow in targetRows) {
                    sb.AppendLine();
                    sb.AppendLine($"  ► Target: {tRow.target.Name} ({tRow.percent:F2}%{Label(tRow.provisional)})");

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
                        sb.AppendLine($"      - Template: {templateName} (Filter: {filterName}) ({epRow.percent:F2}%{Label(epRow.provisional)})");
                    }
                }
            }

            TSLogger.Debug($"generated profile summary report for {}");
            return sb.ToString();
        }

        private static double WeightedPercent(List<ExposurePlan> plans, System.Func<ExposurePlan, double> planPercent) {
            int totalDesired = plans.Sum(ep => ep.Desired);
            if (totalDesired == 0) return 0;
            return plans.Sum(ep => planPercent(ep) * ep.Desired) / totalDesired;
        }

        private static string Label(bool provisional) => provisional ? " pre-grading" : "";
    }
}