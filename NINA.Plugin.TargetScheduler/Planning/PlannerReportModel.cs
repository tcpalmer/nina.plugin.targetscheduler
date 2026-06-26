using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning {

    /// <summary>
    /// Structured, render-agnostic representation of a planner report. Captured by <see cref="PlannerReport"/>
    /// during a (preview) planning run and consumed by both the HTML renderer and the in-UI WPF builder.
    /// </summary>
    public class PlannerReportModel {
        public DateTime ReportTime { get; set; }
        public DateTime? InitialAtTime { get; set; }
        public string NinaVersion { get; set; }
        public string PluginVersion { get; set; }
        public List<PlannerReportSection> Sections { get; } = new List<PlannerReportSection>();
    }

    public class PlannerReportSection {
        public DateTime PlanTime { get; set; }
        public List<FilterRow> FilterRows { get; set; } = new List<FilterRow>();
        public ScoringTable Scoring { get; set; }
        public ResultInfo Result { get; set; }
    }

    public enum FilterKind {
        Filtered,
        Candidate,
        CandidateLater
    }

    public class FilterRow {
        public string Project { get; set; }
        public string Target { get; set; }
        public string FilteredText { get; set; }
        public FilterKind Kind { get; set; }
    }

    public class ScoringTable {
        public List<string> RuleNames { get; set; } = new List<string>();
        public List<ScoringRow> Rows { get; set; } = new List<ScoringRow>();
    }

    public class ScoringRow {
        public string Project { get; set; }
        public string Target { get; set; }
        public Dictionary<string, double> RuleScores { get; set; } = new Dictionary<string, double>();
        public double Total { get; set; }
        public bool IsWinner { get; set; }
    }

    public enum ResultKind {
        Target,
        Wait,
        Done
    }

    public class ResultInfo {
        public ResultKind Kind { get; set; }

        // Target
        public string Project { get; set; }
        public string Target { get; set; }
        public string ExposureFilterName { get; set; }
        public int ExposureLength { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Wait
        public string NextProject { get; set; }
        public string NextTarget { get; set; }
        public DateTime WaitUntil { get; set; }
    }
}
