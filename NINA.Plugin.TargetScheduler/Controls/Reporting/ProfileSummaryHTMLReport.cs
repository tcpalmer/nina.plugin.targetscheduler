using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Controls.Reporting {

    public static class ProfileSummaryHTMLReport {

        public static string GetHTMLReportPath(string profileName) {
            string safe = SanitizeFileName(profileName);
            return Path.Combine(Path.GetTempPath(), $"TS_ProfileSummary_{safe}.html");
        }

        public static string Generate(string profileId, string profileName, SchedulerDatabaseInteraction database) {
            ProfilePreference profilePreference;
            List<Project> projects;

            using (var context = database.GetContext()) {
                profilePreference = context.GetProfilePreference(profileId, true);
                projects = context.GetAllProjectsReadOnly(profileId);
            }

            var sb = new StringBuilder();
            AppendPreamble(sb, profileName);

            if (projects == null || projects.Count == 0) {
                sb.AppendLine("        <p>No projects found for this profile.</p>");
            } else {
                AppendProjects(sb, projects, profilePreference);
            }

            AppendEpilogue(sb);

            string path = GetHTMLReportPath(profileName);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            TSLogger.Debug($"profile summary HTML report written: {path}");
            return path;
        }

        private static void AppendPreamble(StringBuilder sb, string profileName) {
            string version = GetPluginVersion();
            string reportDate = DateTime.Now.ToString("MMM d, yyyy HH:mm");
            string encodedName = HtmlEncode(profileName);

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine($"    <title>Profile Summary: {encodedName}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body {");
            sb.AppendLine("            margin: 1cm;");
            sb.AppendLine("            font-family: system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;");
            sb.AppendLine("            font-size: small;");
            sb.AppendLine("            background-color: #1e1e1e;");
            sb.AppendLine("            color: #c7c6c3;");
            sb.AppendLine("        }");
            sb.AppendLine("        .container { width: 950px; margin: auto; }");
            sb.AppendLine("        .title {");
            sb.AppendLine("            border: 1px solid #c7c6c3;");
            sb.AppendLine("            padding: 10px 15px;");
            sb.AppendLine("            line-height: 1.5em;");
            sb.AppendLine("        }");
            sb.AppendLine("        .report {");
            sb.AppendLine("            border: 1px solid #c7c6c3;");
            sb.AppendLine("            border-top: none;");
            sb.AppendLine("            padding: 15px;");
            sb.AppendLine("        }");
            sb.AppendLine("        .project-block { margin-bottom: 24px; }");
            sb.AppendLine("        .project-header {");
            sb.AppendLine("            background-color: #2a2a2a;");
            sb.AppendLine("            border: 1px solid #555;");
            sb.AppendLine("            padding: 7px 12px;");
            sb.AppendLine("            font-size: 1.05em;");
            sb.AppendLine("            font-weight: bold;");
            sb.AppendLine("        }");
            sb.AppendLine("        .target-block { margin-left: 20px; margin-top: 8px; }");
            sb.AppendLine("        .target-header {");
            sb.AppendLine("            padding: 5px 8px;");
            sb.AppendLine("            font-weight: bold;");
            sb.AppendLine("            background-color: #252525;");
            sb.AppendLine("        }");
            sb.AppendLine("        .ep-table {");
            sb.AppendLine("            width: calc(100% - 30px);");
            sb.AppendLine("            margin-left: 30px;");
            sb.AppendLine("            border-collapse: collapse;");
            sb.AppendLine("            margin-top: 4px;");
            sb.AppendLine("        }");
            sb.AppendLine("        .ep-table th {");
            sb.AppendLine("            text-align: left;");
            sb.AppendLine("            border-bottom: 1px solid #555;");
            sb.AppendLine("            padding: 4px 10px;");
            sb.AppendLine("            color: #aaa;");
            sb.AppendLine("            font-weight: normal;");
            sb.AppendLine("        }");
            sb.AppendLine("        .ep-table td { padding: 3px 10px; }");
            sb.AppendLine("        .ep-table tr:nth-of-type(even) { background-color: #323232; }");
            sb.AppendLine("        .pct { color: #8fc8e8; }");
            sb.AppendLine("        .pregrad { color: #e8c864; font-style: italic; }");
            sb.AppendLine("        .grad-disabled { color: #888; font-style: italic; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"container\">");
            sb.AppendLine("    <div class=\"title\">");
            sb.AppendLine($"        <h1>Profile Summary: {encodedName}</h1>");
            sb.AppendLine($"        <div><i>Report date: {reportDate}</i></div>");
            sb.AppendLine($"        <div><i>N.I.N.A. Target Scheduler plugin {version}</i></div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"report\">");
        }

        private static void AppendProjects(StringBuilder sb, List<Project> projects, ProfilePreference profilePreference) {
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
                AppendProject(sb, pRow);
            }
        }

        private static void AppendProject(StringBuilder sb, (Project project, ExposureCompletionHelper helper, double percent, bool provisional) pRow) {
            string pctHtml = $"<span class=\"pct\">{pRow.percent:F2}%</span>";
            string suffixHtml;
            if (!pRow.project.EnableGrader) {
                suffixHtml = " <span class=\"grad-disabled\">, grading disabled</span>";
            } else if (pRow.provisional) {
                suffixHtml = " <span class=\"pregrad\">pre-grading</span>";
            } else {
                suffixHtml = "";
            }

            sb.AppendLine("        <div class=\"project-block\">");
            sb.AppendLine($"            <div class=\"project-header\">{HtmlEncode(pRow.project.Name)} ({pctHtml}{suffixHtml})</div>");

            var targetRows = new List<(Target target, double percent, bool provisional)>();
            foreach (var target in pRow.project.Targets ?? new List<Target>()) {
                var plans = target.ExposurePlans ?? new List<ExposurePlan>();
                double percent = WeightedPercent(plans, pRow.helper.PercentComplete);
                bool provisional = plans.Any(pRow.helper.IsProvisionalPercentComplete);
                targetRows.Add((target, percent, provisional));
            }

            targetRows.Sort((a, b) => b.percent.CompareTo(a.percent));

            foreach (var tRow in targetRows) {
                AppendTarget(sb, tRow, pRow.helper);
            }

            sb.AppendLine("        </div>");
        }

        private static void AppendTarget(StringBuilder sb, (Target target, double percent, bool provisional) tRow, ExposureCompletionHelper helper) {
            string pctHtml = $"<span class=\"pct\">{tRow.percent:F2}%</span>";
            string suffixHtml = tRow.provisional ? " <span class=\"pregrad\">pre-grading</span>" : "";

            sb.AppendLine("            <div class=\"target-block\">");
            sb.AppendLine($"                <div class=\"target-header\">&#9658; {HtmlEncode(tRow.target.Name)} ({pctHtml}{suffixHtml})</div>");

            var epRows = (tRow.target.ExposurePlans ?? new List<ExposurePlan>())
                .Select(ep => new {
                    ep,
                    percent = helper.PercentComplete(ep),
                    provisional = helper.IsProvisionalPercentComplete(ep)
                })
                .OrderByDescending(x => x.percent)
                .ToList();

            if (epRows.Count > 0) {
                sb.AppendLine("                <table class=\"ep-table\">");
                sb.AppendLine("                    <thead><tr><th>Template</th><th>Filter</th><th>Complete</th></tr></thead>");
                sb.AppendLine("                    <tbody>");
                foreach (var epRow in epRows) {
                    string templateName = HtmlEncode(epRow.ep.ExposureTemplate?.Name ?? "Unknown");
                    string filterName = HtmlEncode(epRow.ep.ExposureTemplate?.FilterName ?? "Unknown");
                    string epPctHtml = $"<span class=\"pct\">{epRow.percent:F2}%</span>";
                    string epSuffixHtml = epRow.provisional ? " <span class=\"pregrad\">pre-grading</span>" : "";
                    sb.AppendLine($"                    <tr><td>{templateName}</td><td>{filterName}</td><td>{epPctHtml}{epSuffixHtml}</td></tr>");
                }
                sb.AppendLine("                    </tbody>");
                sb.AppendLine("                </table>");
            }

            sb.AppendLine("            </div>");
        }

        private static void AppendEpilogue(StringBuilder sb) {
            sb.AppendLine("    </div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
        }

        private static double WeightedPercent(List<ExposurePlan> plans, Func<ExposurePlan, double> planPercent) {
            int totalDesired = plans.Sum(ep => ep.Desired);
            if (totalDesired == 0) return 0;
            return plans.Sum(ep => planPercent(ep) * ep.Desired) / totalDesired;
        }

        private static string GetPluginVersion() {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        private static string HtmlEncode(string text) {
            return System.Net.WebUtility.HtmlEncode(text ?? "");
        }

        private static string SanitizeFileName(string name) {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { ' ' };
            var chars = (name ?? "profile").Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }
    }
}
