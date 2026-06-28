using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Planning {

    public class PlannerReport {
        private readonly PlannerReportModel _model;

        private List<(IProject Project, ITarget Target)> _currentTargets;
        private Dictionary<ITarget, string> _currentFilterReasons;
        private HashSet<ITarget> _readyNowTargets;
        private DateTime _currentAtTime;
        private List<ITarget> _scoringCandidates;
        private ITarget _scoringSelected;

        private PlanResult _planResult;
        private ITarget _resultTarget;
        private DateTime _resultWaitUntil;

        private enum PlanResult { Target, Wait, Done }

        public PlannerReport() {
            _model = new PlannerReportModel {
                ReportTime = DateTime.Now,
                NinaVersion = CoreUtil.Version,
                PluginVersion = GetPluginVersion()
            };
        }

        /// <summary>The structured report captured so far.</summary>
        public PlannerReportModel Model => _model;

        public void BeginPlan(List<IProject> projects, DateTime atTime) {
            _currentAtTime = atTime;
            _model.InitialAtTime ??= atTime;
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

        /// <summary>
        /// Records that the previous target continued imaging via the planner's "continue" shortcut, which
        /// returns without a full re-plan (and so without a new section). Extends the most recent target
        /// section's end time to cover this continuation exposure so the report's target window matches the
        /// actual imaging span shown by the Run output.
        /// </summary>
        public void Continue(ITarget target, DateTime atTime) {
            if (target == null || _model.Sections.Count == 0) {
                return;
            }

            ResultInfo last = _model.Sections[_model.Sections.Count - 1].Result;
            if (last != null && last.Kind == ResultKind.Target && last.TargetId == target.DatabaseId) {
                last.EndTime = atTime.AddSeconds(target.SelectedExposure?.ExposureLength ?? 0);
            }
        }

        /// <summary>
        /// Finalizes the data captured since the last <see cref="BeginPlan"/> into a report section
        /// and appends it to the model. Called once per plan result.
        /// </summary>
        public void Generate() {
            _model.Sections.Add(new PlannerReportSection {
                PlanTime = _currentAtTime,
                FilterRows = BuildFilterRows(),
                Scoring = BuildScoringTable(),
                Result = BuildResultInfo()
            });
        }

        private List<FilterRow> BuildFilterRows() {
            var rows = new List<FilterRow>();
            foreach (var (project, target) in _currentTargets.OrderBy(t => t.Project.Name).ThenBy(t => t.Target.Name)) {
                _currentFilterReasons.TryGetValue(target, out string reason);
                FilterKind kind;
                string text;
                if (reason != null) {
                    kind = GetReasonKind(reason);
                    text = GetReasonDisplayText(reason);
                } else if (_readyNowTargets.Contains(target)) {
                    kind = FilterKind.Candidate;
                    text = "candidate now";
                } else {
                    kind = FilterKind.CandidateLater;
                    text = "candidate later";
                }

                rows.Add(new FilterRow {
                    Project = project.Name,
                    Target = target.Name,
                    FilteredText = text,
                    Kind = kind
                });
            }

            return rows;
        }

        private ScoringTable BuildScoringTable() {
            if (_scoringCandidates == null) {
                return null;
            }

            var ruleNames = _scoringCandidates
                .Where(t => t.ScoringResults != null)
                .SelectMany(t => t.ScoringResults.Results)
                .Select(r => r.ScoringRule.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var table = new ScoringTable { RuleNames = ruleNames };

            foreach (var target in _scoringCandidates.OrderBy(t => t.Project.Name).ThenBy(t => t.Name)) {
                var row = new ScoringRow {
                    Project = target.Project.Name,
                    Target = target.Name,
                    Total = target.ScoringResults?.TotalScore ?? 0,
                    IsWinner = ReferenceEquals(target, _scoringSelected)
                };

                if (target.ScoringResults != null) {
                    foreach (var ruleResult in target.ScoringResults.Results) {
                        row.RuleScores[ruleResult.ScoringRule.Name] = ruleResult.Weight * ruleResult.Score;
                    }
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private ResultInfo BuildResultInfo() {
            switch (_planResult) {
                case PlanResult.Target:
                    return new ResultInfo {
                        Kind = ResultKind.Target,
                        TargetId = _resultTarget.DatabaseId,
                        Project = _resultTarget.Project.Name,
                        Target = _resultTarget.Name,
                        ExposureFilterName = _resultTarget.SelectedExposure?.FilterName ?? string.Empty,
                        ExposureLength = _resultTarget.SelectedExposure != null ? (int)_resultTarget.SelectedExposure.ExposureLength : 0,
                        StartTime = _currentAtTime,
                        EndTime = _currentAtTime.AddSeconds(_resultTarget.SelectedExposure?.ExposureLength ?? 0)
                    };
                case PlanResult.Wait:
                    return new ResultInfo {
                        Kind = ResultKind.Wait,
                        NextProject = _resultTarget.Project.Name,
                        NextTarget = _resultTarget.Name,
                        WaitUntil = _resultWaitUntil
                    };
                default:
                    return new ResultInfo { Kind = ResultKind.Done };
            }
        }

        private static FilterKind GetReasonKind(string reason) {
            if (reason == Reasons.TargetNotYetVisible
                || reason == Reasons.TargetMoonAvoidance
                || reason == Reasons.TargetBeforeMeridianWindow
                || reason == Reasons.TargetMeridianFlipClipped
                || reason == Reasons.TargetMaxAltitude) {
                return FilterKind.CandidateLater;
            }
            return FilterKind.Filtered;
        }

        private static string GetReasonDisplayText(string reason) {
            if (reason == Reasons.TargetMoonAvoidance) return "moon avoidance";
            if (reason == Reasons.TargetMeridianWindowClipped) return "meridian window";
            if (reason == Reasons.TargetMeridianFlipClipped) return "meridian flip safety";
            if (reason == Reasons.TargetTwilight) return "twilight";
            if (reason == Reasons.TargetHumidity) return "humidity";
            return reason;
        }

        private static string GetPluginVersion() {
            var fvi = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            return fvi.FileVersion ?? string.Empty;
        }

        // ---------------------------------------------------------------------------------------------
        // HTML rendering
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Renders the full standalone HTML report from the captured model.  When <paramref name="targetGroupSvgs"/>
        /// is supplied, the SVG strings are embedded at the top of each Target group in order (one per Target
        /// group); they align 1:1 with the Target groups produced by <see cref="PlannerReportModel.BuildGroups"/>.
        /// </summary>
        public string GenerateHtml(IReadOnlyList<string> targetGroupSvgs = null) {
            var sb = new StringBuilder();
            AppendPreamble(sb);

            int chartIndex = 0;
            foreach (var group in _model.BuildGroups()) {
                string svg = null;
                if (group.Kind == ResultKind.Target && targetGroupSvgs != null && chartIndex < targetGroupSvgs.Count) {
                    svg = targetGroupSvgs[chartIndex++];
                }

                AppendGroupHtml(sb, group, svg);
                sb.AppendLine("        <hr>");
            }

            AppendEpilogue(sb);
            return sb.ToString();
        }

        private void AppendGroupHtml(StringBuilder sb, PlannerReportGroup group, string svg) {
            // Wait and Done groups carry a single section and aren't aggregated; render it directly.
            if (group.Kind != ResultKind.Target) {
                AppendSectionHtml(sb, group.Sections[0]);
                return;
            }

            string window = $"start: {group.StartTime:yyyy-MM-dd HH:mm:ss}, end: {group.EndTime:yyyy-MM-dd HH:mm:ss}";
            sb.AppendLine("        <details class=\"target-group\">");
            sb.AppendLine($"        <summary>{HtmlEncode($"{group.Project} / {group.Target}")} &nbsp;&mdash;&nbsp; {HtmlEncode(window)}</summary>");

            if (!string.IsNullOrEmpty(svg)) {
                sb.AppendLine($"        <div class=\"altitude-chart\">{svg}</div>");
            }

            foreach (var section in group.Sections) {
                AppendSectionHtml(sb, section);
            }

            sb.AppendLine("        </details>");
        }

        private void AppendSectionHtml(StringBuilder sb, PlannerReportSection section) {
            sb.AppendLine($"        <details>");
            sb.AppendLine($"        <summary>Plan Report for {HtmlEncode(section.PlanTime.ToString("yyyy-MM-dd"))} at {HtmlEncode(section.PlanTime.ToString("HH:mm:ss"))}</summary>");
            sb.AppendLine("        <h3>Initial Target Filtering</h3>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Project</th><th>Target</th><th>Filtered</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var row in section.FilterRows) {
                string cssClass = GetReasonCssClass(row.Kind);
                string filteredCell = $"<td class=\"{cssClass}\">{HtmlEncode(row.FilteredText)}</td>";
                sb.AppendLine($"                <tr><td>{HtmlEncode(row.Project)}</td><td>{HtmlEncode(row.Target)}</td>{filteredCell}</tr>");
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");

            if (section.Scoring != null) {
                AppendScoringTableHtml(sb, section.Scoring);
            }

            sb.AppendLine("        </details>");
            AppendResultHtml(sb, section);
        }

        private void AppendScoringTableHtml(StringBuilder sb, ScoringTable scoring) {
            sb.AppendLine("        <h3>Target Scoring</h3>");
            sb.AppendLine("        <table class=\"score-table\">");
            sb.AppendLine("            <thead>");

            var headerRow = new StringBuilder("                <tr><th>Project</th><th>Target</th>");
            foreach (var ruleName in scoring.RuleNames) {
                headerRow.Append($"<th>{HtmlEncode(ruleName)}</th>");
            }
            headerRow.Append("<th>Total</th></tr>");
            sb.AppendLine(headerRow.ToString());
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var scoreRow in scoring.Rows) {
                var row = new StringBuilder($"                <tr><td>{HtmlEncode(scoreRow.Project)}</td><td>{HtmlEncode(scoreRow.Target)}</td>");
                foreach (var ruleName in scoring.RuleNames) {
                    row.Append(scoreRow.RuleScores.TryGetValue(ruleName, out double ruleScore)
                        ? $"<td>{ruleScore:F2}</td>"
                        : "<td>-</td>");
                }

                string totalClass = scoreRow.IsWinner ? "candidate" : "filtered";
                row.Append($"<td class=\"{totalClass}\">{scoreRow.Total:F2}</td></tr>");
                sb.AppendLine(row.ToString());
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
        }

        private void AppendResultHtml(StringBuilder sb, PlannerReportSection section) {
            ResultInfo result = section.Result;
            switch (result.Kind) {
                case ResultKind.Target:
                    sb.AppendLine("        <h3>Selected Target</h3>");
                    sb.AppendLine("        <table class=\"result-table\">");
                    sb.AppendLine("            <tbody>");
                    sb.AppendLine($"                <tr><td>Project</td><td>{HtmlEncode(result.Project)}</td></tr>");
                    sb.AppendLine($"                <tr><td>Target</td><td>{HtmlEncode(result.Target)}</td></tr>");
                    if (!string.IsNullOrEmpty(result.ExposureFilterName) || result.ExposureLength > 0) {
                        string expLen = result.ExposureLength > 0 ? $"  {result.ExposureLength}s" : string.Empty;
                        sb.AppendLine($"                <tr><td>Exposure</td><td>{HtmlEncode(result.ExposureFilterName)}{expLen}</td></tr>");
                    }
                    sb.AppendLine($"                <tr><td>Start</td><td>{HtmlEncode(result.StartTime.ToString("yyyy-MM-dd HH:mm:ss"))}</td></tr>");
                    sb.AppendLine($"                <tr><td>End</td><td>{HtmlEncode(result.EndTime.ToString("yyyy-MM-dd HH:mm:ss"))}</td></tr>");
                    sb.AppendLine("            </tbody>");
                    sb.AppendLine("        </table>");
                    break;
                case ResultKind.Wait:
                    string duration = FormatWaitDuration(result.WaitUntil - section.PlanTime);
                    sb.AppendLine("        <h3>Wait for Next Target</h3>");
                    sb.AppendLine("        <table class=\"result-table\">");
                    sb.AppendLine("            <tbody>");
                    sb.AppendLine($"                <tr><td>Next Target</td><td>{HtmlEncode(result.NextProject)} / {HtmlEncode(result.NextTarget)}</td></tr>");
                    sb.AppendLine($"                <tr><td>Wait Until</td><td>{HtmlEncode(result.WaitUntil.ToString("yyyy-MM-dd HH:mm:ss"))} &nbsp;({HtmlEncode(duration)})</td></tr>");
                    sb.AppendLine("            </tbody>");
                    sb.AppendLine("        </table>");
                    break;
                case ResultKind.Done:
                    sb.AppendLine("        <h3>Result</h3>");
                    sb.AppendLine("        <p>No more targets, done for the night.</p>");
                    break;
            }
        }

        internal static string FormatWaitDuration(TimeSpan wait) {
            return $"{(int)wait.TotalHours}h {wait.Minutes:D2}m {wait.Seconds:D2}s";
        }

        private static string GetReasonCssClass(FilterKind kind) {
            switch (kind) {
                case FilterKind.Candidate: return "candidate";
                case FilterKind.CandidateLater: return "candidate-later";
                default: return "filtered";
            }
        }

        private void AppendPreamble(StringBuilder sb) {
            string reportTimestamp = _model.ReportTime.ToString("yyyy-MM-dd HH:mm:ss");
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine($"    <title>Planning Report {reportTimestamp}</title>");
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
            sb.AppendLine("        details.target-group > summary { font-size: 1.45em; font-weight: 600; }");
            sb.AppendLine("        details.target-group { border-left: 2px solid #444; padding-left: 14px; }");
            sb.AppendLine("        details.target-group > details { margin-top: 18px; }");
            sb.AppendLine("        .altitude-chart { margin: 12px 0 4px; }");
            sb.AppendLine("        .altitude-chart svg { width: 100%; height: auto; max-width: 760px; }");
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
            sb.AppendLine($"                    <tr><td>Report Date</td><td>{HtmlEncode(_model.ReportTime.ToString("MMM d, yyyy HH:mm"))}</td></tr>");
            sb.AppendLine($"                    <tr><td>Plan time</td><td>{HtmlEncode(_model.InitialAtTime?.ToString("MMM d, yyyy HH:mm") ?? string.Empty)}</td></tr>");
            sb.AppendLine($"                    <tr><td>NINA</td><td>{HtmlEncode(_model.NinaVersion)}</td></tr>");
            sb.AppendLine($"                    <tr><td>Target Scheduler</td><td>{HtmlEncode(_model.PluginVersion)}</td></tr>");
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

        private static string HtmlEncode(string text) {
            return System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
        }
    }
}
