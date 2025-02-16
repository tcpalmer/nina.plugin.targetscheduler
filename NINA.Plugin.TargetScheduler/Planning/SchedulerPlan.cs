using NINA.Core.Enum;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Planning {

    public class SchedulerPlan {
        public string PlanId { get; private set; }
        public DateTime PlanTime { get; private set; }

        public DateTime StartTime { get => PlanTime; }
        public DateTime EndTime { get; private set; }
        public TimeInterval TimeInterval { get; private set; }

        public ITarget PlanTarget { get; private set; }
        public List<IProject> Projects { get; private set; }

        public List<IInstruction> PlanInstructions { get; private set; }
        public DateTime? WaitForNextTargetTime { get; private set; }
        public bool IsWait { get => WaitForNextTargetTime.HasValue; }
        public bool IsEmulator { get; set; }
        public string DetailsLog { get; private set; }

        public SchedulerPlan(DateTime planTime, List<IProject> projects, ITarget target, List<IInstruction> planInstructions, bool logPlan) {
            this.PlanId = Guid.NewGuid().ToString();
            this.PlanTime = planTime;
            this.PlanTarget = target;
            this.Projects = projects;
            this.PlanInstructions = planInstructions;
            this.EndTime = planTime.AddSeconds(target.SelectedExposure.ExposureLength);
            this.TimeInterval = new TimeInterval(StartTime, EndTime);
            this.WaitForNextTargetTime = null;

            if (logPlan) {
                if (TSLogger.IsEnabled(LogLevelEnum.TRACE)) {
                    string log = LogPlanResultsTrace();
                    DetailsLog = DetailsLog + log;
                    TSLogger.Trace(log);
                } else if (TSLogger.IsEnabled(LogLevelEnum.DEBUG)) {
                    string log = LogPlanResultsDebug();
                    DetailsLog = DetailsLog + log;
                    TSLogger.Debug(log);
                } else {
                    string log = LogPlanResultsInfo();
                    DetailsLog = DetailsLog + log;
                    TSLogger.Info(log);
                }
            }
        }

        public SchedulerPlan(DateTime planTime, List<IProject> projects, ITarget nextTarget, bool logPlan) {
            this.PlanId = Guid.NewGuid().ToString();
            this.PlanTime = planTime;
            this.PlanTarget = nextTarget;
            this.EndTime = nextTarget.StartTime;
            this.Projects = projects;
            this.TimeInterval = new TimeInterval(StartTime, EndTime);
            this.WaitForNextTargetTime = nextTarget.StartTime;

            if (logPlan) {
                if (TSLogger.IsEnabled(LogLevelEnum.TRACE)) {
                    string log = LogPlanResultsTrace();
                    DetailsLog = DetailsLog + log;
                    TSLogger.Trace(log);
                } else if (TSLogger.IsEnabled(LogLevelEnum.DEBUG)) {
                    string log = LogPlanResultsDebug();
                    DetailsLog = DetailsLog + log;
                    TSLogger.Debug(log);
                } else {
                    string log = LogPlanResultsInfo();
                    DetailsLog = DetailsLog + log;
                    TSLogger.Info(log);
                }
            }
        }

        // Stub version used on sync clients to support immediate flats
        public SchedulerPlan(ITarget planTarget) {
            this.PlanId = Guid.NewGuid().ToString();
            this.PlanTarget = planTarget;
            this.PlanInstructions = new List<IInstruction>();
            this.WaitForNextTargetTime = null;
        }

        public void AddPlanInstruction(IInstruction planInstruction) {
            PlanInstructions.Add(planInstruction);
        }

        public string LogPlanResultsTrace() {
            StringBuilder sb = new StringBuilder(LogPlanResultsDebug());

            if (PlanInstructions != null) {
                sb.AppendLine($"\nPlanner Instructions:");
                foreach (IInstruction instruction in PlanInstructions) {
                    sb.AppendLine($"    {instruction}");
                }
            }

            bool haveScoringRuns = false;
            bool hasAllEPsRejected = false;

            if (Projects != null) {
                sb.AppendLine(String.Format("\n{0,-40} {1,-27} {2,6}   {3,19}", "TARGETS CONSIDERED", "REJECTED FOR", "SCORE", "POTENTIAL START"));
                foreach (IProject project in Projects) {
                    foreach (ITarget target in project.Targets) {
                        string score = "";
                        string startTime = GetStartTime(target);

                        if (target.ScoringResults != null && target.ScoringResults.Results.Count > 0) {
                            haveScoringRuns = true;
                            score = String.Format("{0:0.00}", target.ScoringResults.TotalScore * ScoringRule.WEIGHT_SCALE);
                        }

                        sb.AppendLine(String.Format("{0,-40} {1,-27} {2,6}   {3}", $"{project.Name}/{target.Name}", target.RejectedReason, score, startTime));
                        if (target.RejectedReason == Reasons.TargetAllExposurePlans) {
                            hasAllEPsRejected = true;
                        }
                    }
                }

                if (hasAllEPsRejected) {
                    sb.AppendLine("\n(Rejection for 'all exposure plans' is due to moon avoidance or all exposure plans complete.)");
                }

                if (haveScoringRuns) {
                    sb.AppendLine("\nSCORING RUNS");
                    foreach (IProject project in Projects) {
                        foreach (ITarget target in project.Targets) {
                            if (target.ScoringResults != null && target.ScoringResults.Results.Count > 0) {
                                sb.AppendLine($"\n{project.Name}/{target.Name}");
                                sb.AppendLine(String.Format("{0,-30} {1,-9} {2,11} {3,11}", "RULE", "RAW SCORE", "WEIGHT", "SCORE"));
                                foreach (RuleResult result in target.ScoringResults.Results) {
                                    sb.AppendLine(String.Format("{0,-30} {1,9:0.00} {2,10:0.00}%  {3,10:0.00}",
                                        result.ScoringRule.Name,
                                        result.Score * ScoringRule.WEIGHT_SCALE,
                                        result.Weight * ScoringRule.WEIGHT_SCALE,
                                        result.Score * result.Weight * ScoringRule.WEIGHT_SCALE));
                                }

                                sb.AppendLine("----------------------------------------------------------------");
                                sb.AppendLine(String.Format("{0,57} {1,6:0.00}", "TOTAL SCORE", target.ScoringResults.TotalScore * ScoringRule.WEIGHT_SCALE));
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }

        public string LogPlanResultsDebug() {
            StringBuilder sb = new StringBuilder();
            string type = IsWait ? "WAIT" : "TARGET";

            sb.AppendLine("\n" + String.Format("{0,-6}", type) + " ==========================================================================================");

            if (type == "WAIT") {
                sb.AppendLine($"Plan Start:      {DateFmt(PlanTime)}");
                sb.AppendLine($"Wait Until:      {DateFmt(WaitForNextTargetTime)}");
            }

            if (type == "TARGET") {
                sb.AppendLine($"Selected Target: {PlanTarget.Project.Name}/{PlanTarget.Name}");
                sb.AppendLine($"Selected Filter: {PlanTarget.SelectedExposure.FilterName}");
                sb.AppendLine($"Plan Start:      {DateFmt(PlanTime)}");
                sb.AppendLine($"Plan Stop:       {DateFmt(PlanTime.AddSeconds(TimeInterval.Duration))}");
                sb.AppendLine($"Plan Min Expire: {DateFmt(PlanTarget.MinimumTimeSpanEnd)}");
                sb.AppendLine($"Hard Stop:       {DateFmt(PlanTarget.EndTime)} (target sets)");
            }

            return sb.ToString();
        }

        public string LogPlanResultsInfo() {
            StringBuilder sb = new StringBuilder();
            string type = IsWait ? "WAIT" : "TARGET";

            if (type == "WAIT") {
                sb.Append($"WAIT from {DateFmt(PlanTime)} to {DateFmt(WaitForNextTargetTime)}");
            } else {
                sb.Append($"TARGET {PlanTarget.Project.Name}/{PlanTarget.Name}, filter {PlanTarget.SelectedExposure.FilterName} at {DateFmt(PlanTime)}");
            }

            return sb.ToString();
        }

        private string GetStartTime(ITarget target) {
            if (target.Rejected) {
                switch (target.RejectedReason) {
                    case Reasons.TargetNotYetVisible:
                    case Reasons.TargetBeforeMeridianWindow:
                    case Reasons.TargetLowerScore:
                        return DateFmt(target.StartTime);
                }
            }

            return "";
        }

        private string DateFmt(DateTime? dateTime) {
            if (dateTime == null || dateTime == DateTime.MinValue) {
                return "";
            }

            return ((DateTime)dateTime).ToString(Utils.DateFMT);
        }

        public string PlanSummary() {
            StringBuilder sb = new StringBuilder();
            if (IsWait) {
                sb.AppendLine($"Waiting until {Utils.FormatDateTimeFull(WaitForNextTargetTime)}");
            } else {
                sb.AppendLine($"Target:         {PlanTarget.Name} at {PlanTarget.Coordinates.RAString} {PlanTarget.Coordinates.DecString}");
                sb.AppendLine($"Imaging window: {TimeInterval}");
                sb.Append($"Instructions:   {PlanningInstruction.InstructionsSummary(PlanInstructions)}");
            }

            return sb.ToString();
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Id: {PlanId}");
            sb.Append("Target: ").AppendLine(PlanTarget != null ? PlanTarget.Name : null);
            sb.AppendLine($"Interval: {TimeInterval}");
            sb.AppendLine($"Wait: {WaitForNextTargetTime}");
            sb.Append("Selected Exp: ").AppendLine(PlanTarget != null ? PlanTarget.SelectedExposure?.FilterName : null);
            sb.AppendLine($"Instructions:\n");
            if (PlanInstructions != null) {
                foreach (IInstruction instruction in PlanInstructions) {
                    sb.AppendLine($"{instruction}");
                }
            }

            return sb.ToString();
        }
    }
}