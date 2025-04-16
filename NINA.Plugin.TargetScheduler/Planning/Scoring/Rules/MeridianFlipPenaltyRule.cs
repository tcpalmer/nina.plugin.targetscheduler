using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using System;

namespace NINA.Plugin.TargetScheduler.Planning.Scoring.Rules {

    public class MeridianFlipPenaltyRule : ScoringRule {
        public const string RULE_NAME = "Meridian Flip Penalty";
        public const double DEFAULT_WEIGHT = 0 * WEIGHT_SCALE;

        public override string Name { get { return RULE_NAME; } }
        public override double DefaultWeight { get { return DEFAULT_WEIGHT; } }

        /// <summary>
        /// Score the potential target on whether it will require a MF during this minimum time span.
        /// </summary>
        /// <param name="scoringEngine"></param>
        /// <param name="potentialTarget"></param>
        /// <returns></returns>
        public override double Score(IScoringEngine scoringEngine, ITarget potentialTarget) {
            double minutesAfterMeridian = scoringEngine.ActiveProfile.MeridianFlipSettings.MinutesAfterMeridian;
            DateTime transitTime = potentialTarget.CulminationTime;
            int minimumTime = potentialTarget.Project.MinimumTime;

            TSLogger.Debug($"MF penalty rule: target: {potentialTarget.Project.Name}/{potentialTarget.Name}, min time: {minimumTime}, transit: {Utils.FormatDateTimeFull(transitTime)}, minutes after transit: {minutesAfterMeridian}");

            if (transitTime == DateTime.MinValue) {
                return 1; // target transit time is not valid but if we're scoring it, assume ok
            }

            DateTime flipTime = transitTime.AddMinutes(minutesAfterMeridian);
            if (flipTime < scoringEngine.AtTime) {
                return 1; // target is already safely west of the meridian, ok to slew
            }

            if (scoringEngine.AtTime.AddMinutes(minimumTime) < flipTime) {
                return 1; // we can fit in the minimum time span before a flip is needed
            }

            // Otherwise we'd need a flip during this minimum time span
            return 0;
        }
    }
}