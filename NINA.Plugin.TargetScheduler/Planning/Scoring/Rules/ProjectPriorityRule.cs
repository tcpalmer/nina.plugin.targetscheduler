using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;

namespace NINA.Plugin.TargetScheduler.Planning.Scoring.Rules {

    public class ProjectPriorityRule : ScoringRule {
        public const string RULE_NAME = "Project Priority";
        public const double DEFAULT_WEIGHT = .5 * WEIGHT_SCALE;

        public override string Name { get { return RULE_NAME; } }
        public override double DefaultWeight { get { return DEFAULT_WEIGHT; } }

        /// <summary>
        /// Score the potential target based on assigned priority.
        /// </summary>
        /// <param name="scoringEngine"></param>
        /// <param name="potentialTarget"></param>
        /// <returns></returns>
        public override double Score(IScoringEngine scoringEngine, ITarget potentialTarget) {
            if (potentialTarget.Priority != TargetPriority.Default) {
                switch (potentialTarget.Priority) {
                    case TargetPriority.Low: return 0;
                    case TargetPriority.Normal: return 0.5;
                    case TargetPriority.High: return 1.0;
                    default: return 0;
                }
            }

            switch (potentialTarget.Project.Priority) {
                case ProjectPriority.Low: return 0;
                case ProjectPriority.Normal: return 0.5;
                case ProjectPriority.High: return 1.0;
                default: return 0;
            }
        }
    }
}