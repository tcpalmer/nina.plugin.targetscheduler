using NINA.Profile.Interfaces;
using System;

namespace NINA.Plugin.TargetScheduler.Planning {

    /// <summary>
    /// If a profile specifies a pause before meridian flip, they are asserting that the rig cannot track
    /// past the meridian safely.  In this case, we want to clip a target's visibility such that the time
    /// interval from the pause (target transit - pause minutes) to past the meridian to the time at which it
    /// would be safe to flip (target transit + minutes after) is disallowed - the no flip, unsafe zone.
    ///
    /// We also expand the no flip zone on either side by 30s for extra safety.
    ///
    /// NINA would stop tracking anyway at the pause time so even without this extra clipping, the rig should
    /// be safe.  However, by preemptively clipping out this interval, we let the planner take advantage of
    /// this extra implicit visibility information.
    /// </summary>
    public class MeridianFlipClipper {

        public MeridianFlipClipper() {
        }

        public TimeInterval Clip(IProfile profile, DateTime targetStartTime, DateTime targetTransitTime, DateTime targetEndTime) {
            double pauseMinutes = profile.MeridianFlipSettings.PauseTimeBeforeMeridian;
            if (pauseMinutes == 0) {
                return new TimeInterval(targetStartTime, targetEndTime);
            }

            DateTime startTime = targetStartTime;
            DateTime transitTime = targetTransitTime;
            DateTime endTime = targetEndTime;

            DateTime pauseTime = targetTransitTime.AddMinutes(-pauseMinutes).AddSeconds(-30);
            double safeAfterMinutes = profile.MeridianFlipSettings.MinutesAfterMeridian;
            DateTime safeAfterTime = targetTransitTime.AddMinutes(safeAfterMinutes).AddSeconds(30); ;

            // The pause time (P) and the safe after time (A) define an unsafe 'no flip' interval around the target transit (T).
            // There are six cases of the timing of target start (S) and end (E) with respect to the no flip zone (P===T===A).
            // Note that it doesn't make any difference when S and/or E occurs in relation to T, only P and A matter.
            // Case 1: S------E------P======T======A -> no flip zone after span (no change)
            // Case 2: P======T======A------S------E -> no flip zone before span (no change)
            // Case 3: S------P======T======A------E -> start before and end after no flip zone (clip E to P)
            // Case 4: S------P======T===E===A------ -> start before and end in no flip zone (clip E to P)
            // Case 5: -------P===S===T=====A------E -> start in no flip zone, end after (clip S to A)
            // Case 6: -------P===S===T===E===A----- -> start and end in no flip zone (reject)

            // Case 3: S------P======T======A------E -> start before and end after no flip zone (clip E to P)
            if (startTime < pauseTime && endTime > safeAfterTime) {
                return new TimeInterval(startTime, pauseTime);
            }

            // Case 4: S------P======T===E===A------ -> start before and end in no flip zone (clip E to P)
            if (startTime < pauseTime && endTime > pauseTime && endTime < safeAfterTime) {
                return new TimeInterval(startTime, pauseTime);
            }

            // Case 5: -------P===S===T=====A------E -> start in no flip zone, end after (clip S to A)
            if (pauseTime < startTime && startTime < safeAfterTime && safeAfterTime < endTime) {
                return new TimeInterval(safeAfterTime, endTime);
            }

            // Case 6: -------P===S===T===E===A----- -> start and end in no flip zone (reject)
            if (pauseTime < startTime && startTime < safeAfterTime && pauseTime < endTime && endTime < safeAfterTime) {
                return null;
            }

            // Cases 1 and 2 (the 'no change' cases) are handled implicitly
            return new TimeInterval(startTime, endTime);
        }
    }
}