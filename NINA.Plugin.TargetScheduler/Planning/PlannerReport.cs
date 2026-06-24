using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Planning {

    public class PlannerReport {
        private readonly string _path;
        private readonly string _reportTimestamp;
        private readonly DateTime _reportTime;
        private readonly List<string> _sectionHtmls;

        private List<(IProject Project, ITarget Target)> _currentTargets;
        private Dictionary<ITarget, string> _currentFilterReasons;
        private HashSet<ITarget> _readyNowTargets;
        private DateTime _currentAtTime;
        private List<ITarget> _scoringCandidates;
        private ITarget _scoringSelected;

        private DateTime? _initialAtTime;

        private enum PlanResult { Target, Wait, Done }
        private PlanResult _planResult;
        private ITarget _resultTarget;
        private DateTime _resultWaitUntil;

        public PlannerReport() {
            DateTime now = DateTime.Now;
            _reportTimestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
            _reportTime = now;
            _sectionHtmls = new List<string>();

            string reportDir = Path.Combine(Common.PLUGIN_HOME, "Reports");
            if (!Directory.Exists(reportDir)) {
                Directory.CreateDirectory(reportDir);
            }
            _path = Path.Combine(reportDir, $"TS-Planning-Report-{now:yyyyMMdd-HHmmss}.html");
        }

        public void BeginPlan(List<IProject> projects, DateTime atTime) {
            _currentAtTime = atTime;
            _initialAtTime ??= atTime;
            _currentTargets = new List<(IProject, ITarget)>();
            _currentFilterReasons = new Dictionary<ITarget, string>(ReferenceEqualityComparer.Instance);
            _readyNowTargets = new HashSet<ITarget>(ReferenceEqualityComparer.Instance);
            _scoringCandidates = null;
            _scoringSelected = null;
            _planResult = PlanResult.Done;
            _resultTarget = null;
            _resultWaitUntil = default;

            if (projects != null) {
                foreach (var project in projects) {
                    foreach (var target in project.Targets) {
                        if (!target.Rejected) {
                            _currentTargets.Add((project, target));
                        }
                    }
                }
            }
        }

        public void TrackRejections() {
            foreach (var (_, target) in _currentTargets) {
                if (target.Rejected && !_currentFilterReasons.ContainsKey(target)) {
                    _currentFilterReasons[target] = target.RejectedReason;
                }
            }
        }

        public void SetReadyTargets(List<ITarget> readyTargets) {
            _readyNowTargets = new HashSet<ITarget>(readyTargets ?? Enumerable.Empty<ITarget>(), ReferenceEqualityComparer.Instance);
        }

        public void SetScoringCandidates(List<ITarget> candidates, ITarget selected) {
            _scoringCandidates = candidates;
            _scoringSelected = selected;
        }

        public void SetResultTarget(ITarget target) {
            _planResult = PlanResult.Target;
            _resultTarget = target;
        }

        public void SetResultWait(ITarget nextTarget, DateTime waitUntil) {
            _planResult = PlanResult.Wait;
            _resultTarget = nextTarget;
            _resultWaitUntil = waitUntil;
        }

        public void SetResultDone() {
            _planResult = PlanResult.Done;
        }

        public void Generate() {
            _sectionHtmls.Add(RenderSection());

            try {
                var sb = new StringBuilder();
                AppendPreamble(sb);

                foreach (var sectionHtml in _sectionHtmls) {
                    sb.Append(sectionHtml);
                }

                AppendEpilogue(sb);
                File.WriteAllText(_path, sb.ToString(), Encoding.UTF8);
                TSLogger.Debug($"planning report written: {_path}");
            } catch (Exception ex) {
                TSLogger.Error($"failed to generate planner report: {ex.Message}");
            }
        }

        private string RenderSection() {
            var sb = new StringBuilder();
            sb.AppendLine($"        <details>");
            sb.AppendLine($"        <summary>Plan Report for {HtmlEncode(_currentAtTime.ToString("yyyy-MM-dd"))} at {HtmlEncode(_currentAtTime.ToString("HH:mm:ss"))}</summary>");
            sb.AppendLine("        <h3>Initial Target Filtering</h3>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Project</th><th>Target</th><th>Filtered</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var (project, target) in _currentTargets.OrderBy(t => t.Project.Name).ThenBy(t => t.Target.Name)) {
                _currentFilterReasons.TryGetValue(target, out string reason);
                string filteredCell;
                if (reason != null) {
                    string cssClass = GetReasonCssClass(reason);
                    string displayText = GetReasonDisplayText(reason);
                    filteredCell = $"<td class=\"{cssClass}\">{HtmlEncode(displayText)}</td>";
                } else if (_readyNowTargets.Contains(target)) {
                    filteredCell = "<td class=\"candidate\">candidate now</td>";
                } else {
                    filteredCell = "<td class=\"candidate-later\">candidate later</td>";
                }
                sb.AppendLine($"                <tr><td>{HtmlEncode(project.Name)}</td><td>{HtmlEncode(target.Name)}</td>{filteredCell}</tr>");
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");

            if (_scoringCandidates != null) {
                sb.Append(RenderScoringTable());
            }

            sb.AppendLine("        </details>");
            sb.Append(RenderResult());
            sb.AppendLine("        <hr>");
            return sb.ToString();
        }

        private string RenderScoringTable() {
            var activeRuleNames = _scoringCandidates
                .Where(t => t.ScoringResults != null)
                .SelectMany(t => t.ScoringResults.Results)
                .Select(r => r.ScoringRule.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("        <h3>Target Scoring</h3>");
            sb.AppendLine("        <table class=\"score-table\">");
            sb.AppendLine("            <thead>");

            var headerRow = new StringBuilder("                <tr><th>Project</th><th>Target</th>");
            foreach (var ruleName in activeRuleNames) {
                headerRow.Append($"<th>{HtmlEncode(ruleName)}</th>");
            }
            headerRow.Append("<th>Total</th></tr>");
            sb.AppendLine(headerRow.ToString());
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var target in _scoringCandidates.OrderBy(t => t.Project.Name).ThenBy(t => t.Name)) {
                bool isWinner = ReferenceEquals(target, _scoringSelected);

                var ruleScores = new Dictionary<string, double>();
                if (target.ScoringResults != null) {
                    foreach (var ruleResult in target.ScoringResults.Results) {
                        ruleScores[ruleResult.ScoringRule.Name] = ruleResult.Weight * ruleResult.Score;
                    }
                }

                var row = new StringBuilder($"                <tr><td>{HtmlEncode(target.Project.Name)}</td><td>{HtmlEncode(target.Name)}</td>");
                foreach (var ruleName in activeRuleNames) {
                    row.Append(ruleScores.TryGetValue(ruleName, out double ruleScore)
                        ? $"<td>{ruleScore:F2}</td>"
                        : "<td>-</td>");
                }

                double total = target.ScoringResults?.TotalScore ?? 0;
                string totalClass = isWinner ? "candidate" : "filtered";
                row.Append($"<td class=\"{totalClass}\">{total:F2}</td></tr>");
                sb.AppendLine(row.ToString());
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
            return sb.ToString();
        }

        private string RenderResult() {
            var sb = new StringBuilder();
            switch (_planResult) {
                case PlanResult.Target:
                    sb.AppendLine("        <h3>Selected Target</h3>");
                    sb.AppendLine("        <table class=\"result-table\">");
                    sb.AppendLine("            <tbody>");
                    sb.AppendLine($"                <tr><td>Project</td><td>{HtmlEncode(_resultTarget.Project.Name)}</td></tr>");
                    sb.AppendLine($"                <tr><td>Target</td><td>{HtmlEncode(_resultTarget.Name)}</td></tr>");
                    if (_resultTarget.SelectedExposure != null) {
                        string filterName = HtmlEncode(_resultTarget.SelectedExposure.FilterName ?? string.Empty);
                        string expLen = _resultTarget.SelectedExposure.ExposureLength > 0
                            ? $"  {_resultTarget.SelectedExposure.ExposureLength}s"
                            : string.Empty;
                        sb.AppendLine($"                <tr><td>Exposure</td><td>{filterName}{expLen}</td></tr>");
                    }
                    sb.AppendLine($"                <tr><td>Start</td><td>{HtmlEncode(_currentAtTime.ToString("yyyy-MM-dd HH:mm:ss"))}</td></tr>");
                    sb.AppendLine($"                <tr><td>End</td><td>{HtmlEncode(_resultTarget.MinimumTimeSpanEnd.ToString("yyyy-MM-dd HH:mm:ss"))}</td></tr>");
                    sb.AppendLine("            </tbody>");
                    sb.AppendLine("        </table>");
                    break;
                case PlanResult.Wait:
                    TimeSpan wait = _resultWaitUntil - _currentAtTime;
                    string duration = $"{(int)wait.TotalHours}h {wait.Minutes:D2}m {wait.Seconds:D2}s";
                    sb.AppendLine("        <h3>Wait for Next Target</h3>");
                    sb.AppendLine("        <table class=\"result-table\">");
                    sb.AppendLine("            <tbody>");
                    sb.AppendLine($"                <tr><td>Next Target</td><td>{HtmlEncode(_resultTarget.Project.Name)} / {HtmlEncode(_resultTarget.Name)}</td></tr>");
                    sb.AppendLine($"                <tr><td>Wait Until</td><td>{HtmlEncode(_resultWaitUntil.ToString("yyyy-MM-dd HH:mm:ss"))} &nbsp;({HtmlEncode(duration)})</td></tr>");
                    sb.AppendLine("            </tbody>");
                    sb.AppendLine("        </table>");
                    break;
                case PlanResult.Done:
                    sb.AppendLine("        <h3>Result</h3>");
                    sb.AppendLine("        <p>No more targets, done for the night.</p>");
                    break;
            }
            return sb.ToString();
        }

        private static string GetReasonCssClass(string reason) {
            if (reason == Reasons.TargetNotYetVisible
                || reason == Reasons.TargetMoonAvoidance
                || reason == Reasons.TargetBeforeMeridianWindow
                || reason == Reasons.TargetMeridianFlipClipped
                || reason == Reasons.TargetMaxAltitude) {
                return "candidate-later";
            }
            return "filtered";
        }

        private static string GetReasonDisplayText(string reason) {
            if (reason == Reasons.TargetMoonAvoidance) return "moon avoidance";
            if (reason == Reasons.TargetMeridianWindowClipped) return "meridian window";
            if (reason == Reasons.TargetMeridianFlipClipped) return "meridian flip safety";
            if (reason == Reasons.TargetTwilight) return "twilight";
            if (reason == Reasons.TargetHumidity) return "humidity";
            return reason;
        }

        private void AppendPreamble(StringBuilder sb) {
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine($"    <title>Planning Report {_reportTimestamp}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body {");
            sb.AppendLine("            margin: 1cm;");
            sb.AppendLine("            font-family: system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;");
            sb.AppendLine("            font-size: small;");
            sb.AppendLine("            background-color: #1e1e1e;");
            sb.AppendLine("            color: #c7c6c3;");
            sb.AppendLine("        }");
            sb.AppendLine("        .container { max-width: 832px; margin: auto; }");
            sb.AppendLine("        .title { border: 1px solid #c7c6c3; padding: 10px 15px; line-height: 1.5em; }");
            sb.AppendLine("        .report { border: 1px solid #c7c6c3; border-top: none; padding: 15px; }");
            sb.AppendLine("        .header-table { width: auto; font-style: italic; }");
            sb.AppendLine("        .header-table td:first-child { color: #aaa; padding-right: 24px; white-space: nowrap; }");
            sb.AppendLine("        details { margin-top: 28px; }");
            sb.AppendLine("        details > summary { font-size: 1.25em; cursor: pointer; margin-bottom: 4px; user-select: none; }");
            sb.AppendLine("        h3 { font-size: 1.1em; margin-top: 28px; color: #aaa; }");
            sb.AppendLine("        hr { border: none; border-top: 2px solid #666; margin: 36px 0; }");
            sb.AppendLine("        table { border-collapse: collapse; width: 100%; margin-top: 8px; }");
            sb.AppendLine("        th { text-align: left; border-bottom: 1px solid #555; padding: 4px 10px; color: #aaa; font-weight: normal; }");
            sb.AppendLine("        td { padding: 3px 10px; }");
            sb.AppendLine("        tr:nth-of-type(even) { background-color: #282828; }");
            sb.AppendLine("        .filtered { color: #e07070; }");
        sb.AppendLine("        .candidate { color: #70c070; }");
        sb.AppendLine("        .candidate-later { color: #b8b820; }");
        sb.AppendLine("        .score-table td:nth-child(n+3), .score-table th:nth-child(n+3) { text-align: right; }");
            sb.AppendLine("        .result-table { width: auto; }");
            sb.AppendLine("        .result-table td:first-child { color: #aaa; padding-right: 24px; white-space: nowrap; }");
            sb.AppendLine("        .toggle-btn { background: none; border: 1px solid #888; color: #c7c6c3; padding: 4px 12px; cursor: pointer; font-size: small; margin-bottom: 14px; }");
            sb.AppendLine("        .toggle-btn:hover { border-color: #c7c6c3; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("    <script>");
            sb.AppendLine("        function toggleAll() {");
            sb.AppendLine("            var btn = document.getElementById('toggleBtn');");
            sb.AppendLine("            var expand = btn.dataset.state !== 'expanded';");
            sb.AppendLine("            document.querySelectorAll('details').forEach(function(d) { d.open = expand; });");
            sb.AppendLine("            btn.textContent = expand ? 'Collapse All' : 'Expand All';");
            sb.AppendLine("            btn.dataset.state = expand ? 'expanded' : 'collapsed';");
            sb.AppendLine("        }");
            sb.AppendLine("    </script>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <div class=\"title\">");
            sb.AppendLine("            <h1>Planning Report</h1>");
            sb.AppendLine("            <table class=\"header-table\">");
            sb.AppendLine("                <tbody>");
            sb.AppendLine($"                    <tr><td>Report Date</td><td>{HtmlEncode(_reportTime.ToString("MMM d, yyyy HH:mm"))}</td></tr>");
            sb.AppendLine($"                    <tr><td>Plan time</td><td>{HtmlEncode(_initialAtTime?.ToString("MMM d, yyyy HH:mm") ?? string.Empty)}</td></tr>");
            sb.AppendLine($"                    <tr><td>NINA</td><td>{HtmlEncode(CoreUtil.Version)}</td></tr>");
            sb.AppendLine($"                    <tr><td>Target Scheduler</td><td>{HtmlEncode(GetPluginVersion())}</td></tr>");
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class=\"report\">");
            sb.AppendLine("            <button id=\"toggleBtn\" class=\"toggle-btn\" data-state=\"collapsed\" onclick=\"toggleAll()\">Expand All</button>");
        }

        private static void AppendEpilogue(StringBuilder sb) {
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
        }

        private static string GetPluginVersion() {
            var fvi = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            return fvi.FileVersion ?? string.Empty;
        }

        private static string HtmlEncode(string text) {
            return System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
        }
    }
}
