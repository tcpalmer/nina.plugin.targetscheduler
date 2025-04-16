using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning.Scoring {

    public class ScoringEngine : IScoringEngine {
        public IProfile ActiveProfile { get; set; }
        public ProfilePreference ProfilePreference { get; set; }
        public DateTime AtTime { get; set; }
        public ITarget PreviousPlanTarget { get; }
        public List<IScoringRule> Rules { get; set; }

        private Dictionary<string, double> ruleWeights;

        public Dictionary<string, double> RuleWeights {
            get { return ruleWeights; }
            set { ruleWeights = value; Rules = null; }
        }

        public ScoringEngine(IProfile activeProfile, ProfilePreference ProfilePreference, DateTime atTime, ITarget previousTarget) {
            this.ActiveProfile = activeProfile;
            this.ProfilePreference = ProfilePreference;
            this.PreviousPlanTarget = previousTarget;
            this.AtTime = atTime;
        }

        public double ScoreTarget(ITarget target) {
            if (Rules == null) {
                Rules = AddAllRules(RuleWeights);
            }

            target.ScoringResults = new ScoringResults();

            double totalScore = 0;
            foreach (ScoringRule rule in Rules) {
                double weight = RuleWeights[rule.Name] / ScoringRule.WEIGHT_SCALE;
                double score = rule.Score(this, target);
                totalScore += weight * score;
                target.ScoringResults.AddRuleResult(new RuleResult(rule, weight, score));
            }

            target.ScoringResults.TotalScore = totalScore;
            return totalScore;
        }

        public List<IScoringRule> AddAllRules(Dictionary<string, double> ruleWeights) {
            Dictionary<string, IScoringRule> allRules = ScoringRule.GetAllScoringRules();
            List<IScoringRule> activeRules = new List<IScoringRule>();

            foreach (KeyValuePair<string, IScoringRule> item in allRules) {
                if (ruleWeights[item.Value.Name] > 0) {
                    activeRules.Add(item.Value);
                }
            }

            return activeRules;
        }
    }
}