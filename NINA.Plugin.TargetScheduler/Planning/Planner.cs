using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning {

    public class Planner {
        private bool checkCondition = false;
        private DateTime atTime;
        private IProfile activeProfile;
        private ProfilePreference profilePreferences;
        private ObserverInfo observerInfo;
        private List<IProject> projects;

        public Planner(DateTime atTime, IProfile profile, ProfilePreference profilePreferences, bool checkCondition)
            : this(atTime, profile, profilePreferences, checkCondition, null) { }

        public Planner(DateTime atTime, IProfile profile, ProfilePreference profilePreferences, bool checkCondition, List<IProject> projects) {
            this.atTime = atTime;
            this.activeProfile = profile;
            this.profilePreferences = profilePreferences;
            this.checkCondition = checkCondition;
            this.projects = projects;
            this.observerInfo = new ObserverInfo {
                Latitude = activeProfile.AstrometrySettings.Latitude,
                Longitude = activeProfile.AstrometrySettings.Longitude,
                Elevation = activeProfile.AstrometrySettings.Elevation,
            };
        }

        public SchedulerPlan GetPlan(ITarget previousTarget) {
            string type = checkCondition ? "CONDITION" : "EXECUTE";
            string title = $"PLANNING ENGINE RUN ({type})";

            TSLogger.Info($"-- BEGIN {title} ---------------------------------------------------");
            TSLogger.Info($"getting current plan for {Utils.FormatDateTimeFull(atTime)}");

            using (MyStopWatch.Measure("Scheduler Plan Generation")) {
                try {
                    if (projects == null) {
                        projects = GetProjects();
                    }

                    // If the previous target still has time in it's initial minimum time span and is still acceptable, then stick with it
                    if (PreviousTargetCanContinue(previousTarget)) {
                        TSLogger.Info($"previous target still within min time span, continuing: {previousTarget.Project.Name}/{previousTarget.Name}: {previousTarget.MinimumTimeSpanEnd} < {atTime} ");
                        List<IInstruction> instructions = new InstructionGenerator().Generate(previousTarget, previousTarget);
                        return new SchedulerPlan(atTime, projects, previousTarget, instructions, !checkCondition);
                    }

                    // Filter all targets for suitability
                    projects = FilterForIncomplete(projects);
                    projects = FilterForVisibility(projects);
                    projects = FilterForMoonAvoidance(projects);
                    projects = FilterForTwilight(projects);

                    // See if one or more targets are ready to image now
                    List<ITarget> readyTargets = GetTargetsReadyNow(projects);
                    if (readyTargets.Count > 0) {
                        SelectTargetExposures(readyTargets, previousTarget);
                        ITarget selectedTarget = readyTargets.Count == 1
                            ? readyTargets[0]
                            : SelectTargetByScore(readyTargets, new ScoringEngine(activeProfile, profilePreferences, atTime, previousTarget));
                        List<IInstruction> instructions = new InstructionGenerator().Generate(selectedTarget, previousTarget);

                        // Target ready now
                        return new SchedulerPlan(atTime, projects, selectedTarget, instructions, !checkCondition);
                    } else {
                        ITarget nextTarget = GetNextPossibleTarget(projects);
                        if (nextTarget != null) {
                            // Wait for next possible target
                            return new SchedulerPlan(atTime, projects, nextTarget, !checkCondition);
                        } else {
                            // Otherwise done for the night
                            TSLogger.Info("Scheduler Planner: no target selected");
                            return null;
                        }
                    }
                } catch (Exception ex) {
                    if (ex is SequenceEntityFailedException) {
                        throw;
                    }

                    TSLogger.Error($"exception generating plan: {ex.StackTrace}");
                    throw new SequenceEntityFailedException($"Scheduler: exception generating plan: {ex.Message}", ex);
                } finally {
                    TSLogger.Info($"-- END {title} -----------------------------------------------------");
                }
            }
        }

        /// <summary>
        /// Determine if the previous target can continue.  The first time this target came through the planner,
        /// we save the future time at which it's minimum time window will expire.  We can then compare the current
        /// time to that to see if the target is still within that window.
        ///
        /// However, we also need to re-check completeness for all exposure plans as well as moon avoidance based on
        /// the now current time.  If all exposure plans are complete or all are now rejected for moon avoidance,
        /// we can't continue with this target.
        /// </summary>
        /// <param name="previousTarget"></param>
        /// <returns></returns>
        public bool PreviousTargetCanContinue(ITarget previousTarget) {
            if (previousTarget == null) { return false; }

            UpdateTargetExposurePlans(previousTarget);

            // TODO: NOT working for override exposure order:
            //   - if not all EPs are in the OEO, those will never get complete
            //   - even still, the

            // Recheck exposure completion
            if (previousTarget.ExposurePlans.Count == 0) {
                TSLogger.Info($"not continuing previous target {previousTarget.Name}: all exposure plans complete");
                return false;
            }

            // Recheck for moon avoidance
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences);
            targetExpert.MoonAvoidanceFilter(atTime, previousTarget, new MoonAvoidanceExpert(observerInfo));
            bool allRejected = true;
            previousTarget.ExposurePlans.ForEach(ep => { if (!ep.Rejected) { allRejected = false; } });
            if (allRejected) {
                TSLogger.Info($"not continuing previous target {previousTarget.Name}: all remaining exposure plans rejected for moon avoidance");
                return false;
            }

            // Determine the next exposure and be sure that it can fit in the remaining minimum time span
            IExposure nextExposure = previousTarget.ExposureSelector.Select(atTime, previousTarget.Project, previousTarget, previousTarget.SelectedExposure);
            if (atTime.AddSeconds(nextExposure.ExposureLength) > previousTarget.MinimumTimeSpanEnd) {
                TSLogger.Info($"not continuing previous target {previousTarget.Name}: minimum time window exceeded");
                return false;
            }

            previousTarget.SelectedExposure = nextExposure;
            TSLogger.Debug($"continuing previous target {previousTarget.Name}, next filter: {previousTarget.SelectedExposure.FilterName}");

            return true;
        }

        /// <summary>
        /// Review the project list and reject those projects that are already complete.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public List<IProject> FilterForIncomplete(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }

            foreach (IProject project in projects) {
                if (!ProjectIsInComplete(project)) {
                    SetRejected(project, Reasons.ProjectComplete);
                    foreach (ITarget target in project.Targets) {
                        SetRejected(target, Reasons.TargetComplete);
                    }
                }
            }

            return PropagateRejections(projects);
        }

        /// <summary>
        /// Review each project and the list of associated targets: reject those targets that are not visible.  If all targets
        /// for the project are rejected, mark the project rejected too.  A target is visible if it is above the horizon
        /// within the time window set by the most inclusive twilight over all incomplete exposure plans for that target, is clipped
        /// to any meridian window, is below the maximum altitude, and the remaining visible time is greater than the minimum imaging
        /// time preference for the project.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public List<IProject> FilterForVisibility(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences);

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }

                foreach (ITarget target in project.Targets) {
                    targetExpert.Visibility(atTime, target);
                }
            }

            return PropagateRejections(projects);
        }

        /// <summary>
        /// Review each project and the list of associated targets.  For each filter plan where moon avoidance is enabled,
        /// calculate the avoidance criteria and reject as needed.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public List<IProject> FilterForMoonAvoidance(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }
            IMoonAvoidanceExpert moonExpert = new MoonAvoidanceExpert(observerInfo);
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences);

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }

                foreach (ITarget target in project.Targets) {
                    targetExpert.MoonAvoidanceFilter(atTime, target, moonExpert);
                }
            }

            return PropagateRejections(projects);
        }

        /// <summary>
        /// Reject target exposures that are not suitable for the current level of twilight.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public List<IProject> FilterForTwilight(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(observerInfo, atTime);
            TwilightLevel? currentTwilightLevel = twilightCircumstances.GetCurrentTwilightLevel(atTime);
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences);

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }

                foreach (ITarget target in project.Targets) {
                    targetExpert.TwilightFilter(target, currentTwilightLevel);
                }
            }

            return PropagateRejections(projects);
        }

        /// <summary>
        /// Return the list of targets that are imagable now.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public List<ITarget> GetTargetsReadyNow(List<IProject> projects) {
            List<ITarget> targets = new List<ITarget>();

            if (NoProjects(projects)) { return targets; }
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences);

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }

                foreach (ITarget target in project.Targets) {
                    if (targetExpert.ReadyNow(atTime, target)) {
                        targets.Add(target);
                    }
                }
            }

            return targets;
        }

        /// <summary>
        /// Determine the target (if any) that could potentially be imaged at a later time.  This takes into account
        /// both the changing visibility of the target as well as well as other circumstances (like moon impact) that
        /// may reject the target or exposure plans.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ITarget GetNextPossibleTarget(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }

            List<ITarget> potentialTargets = new List<ITarget>();

            // Find all targets that could possibly be imaged later
            foreach (IProject project in projects) {
                foreach (ITarget target in project.Targets) {
                    if (target.Rejected
                        && target.RejectedReason != Reasons.TargetNotYetVisible
                        && target.RejectedReason != Reasons.TargetBeforeMeridianWindow
                        && target.RejectedReason != Reasons.TargetMaxAltitude) {
                        continue;
                    }

                    potentialTargets.Add(target);
                }
            }

            // Of the potential targets, find those that actually could be imaged given the actual circumstances
            // present when visible.  For example, moon conditions later in the night could either reject or allow
            // certain exposures - and perhaps not until later in the target's visibility time span.
            List<ITarget> imagableTargets = new List<ITarget>();
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences);
            IMoonAvoidanceExpert moonExpert = new MoonAvoidanceExpert(observerInfo);

            foreach (ITarget target in potentialTargets) {
                targetExpert.ClearRejections(target);
                targetExpert.CheckFuture(target, moonExpert);
                if (!target.Rejected) {
                    imagableTargets.Add(target);
                }
            }

            if (imagableTargets.Count == 0) {
                return null;
            }

            // Find the soonest of the imagable targets
            ITarget nextAvailableTarget = null;
            DateTime? nextAvailableTime = DateTime.MaxValue;
            foreach (ITarget target in imagableTargets) {
                if (target.StartTime < nextAvailableTime) {
                    nextAvailableTarget = target;
                    nextAvailableTime = target.StartTime;
                }
            }

            return nextAvailableTarget;
        }

        /// <summary>
        /// Select the best exposure plan now for each potential target.
        /// </summary>
        /// <param name="readyTargets"></param>
        /// <param name="previousTarget"></param>
        public void SelectTargetExposures(List<ITarget> readyTargets, ITarget previousTarget) {
            if (Common.IsEmpty(readyTargets)) {
                return;
            }

            IExposure previousExposure = previousTarget != null ? previousTarget.SelectedExposure : null;
            foreach (ITarget target in readyTargets) {
                target.SelectedExposure = target.ExposureSelector.Select(atTime, target.Project, target, previousExposure);
            }
        }

        /// <summary>
        /// Run the scoring engine, applying the weighted rules to determine the target with the highest score.
        /// </summary>
        /// <param name="readyTargets"></param>
        /// <param name="scoringEngine"></param>
        /// <returns></returns>
        public ITarget SelectTargetByScore(List<ITarget> readyTargets, IScoringEngine scoringEngine) {
            if (Common.IsEmpty(readyTargets)) {
                throw new ArgumentException("no ready targets in SelectTargetByScore");
            }

            if (readyTargets.Count == 1) { return readyTargets[0]; }

            ITarget highScoreTarget = null;
            double highScore = double.MinValue;

            foreach (ITarget target in readyTargets) {
                TSLogger.Trace($"running scoring engine for project/target {target.Project.Name}/{target.Name}");
                scoringEngine.RuleWeights = target.Project.RuleWeights;
                double score = scoringEngine.ScoreTarget(target);
                if (score > highScore) {
                    highScoreTarget = target;
                    highScore = score;
                }
            }

            // Mark losing targets rejected
            foreach (ITarget target in readyTargets) {
                if (target != highScoreTarget) {
                    target.Rejected = true;
                    target.RejectedReason = Reasons.TargetLowerScore;
                }
            }

            return highScoreTarget;
        }

        private List<IProject> GetProjects() {
            try {
                SchedulerDatabaseInteraction database = new SchedulerDatabaseInteraction();
                SchedulerPlanLoader loader = new SchedulerPlanLoader(activeProfile);
                return loader.LoadActiveProjects(database.GetContext());
            } catch (Exception ex) {
                TSLogger.Error($"exception reading database: {ex.StackTrace}");
                throw new SequenceEntityFailedException($"Scheduler: exception reading database: {ex.Message}", ex);
            }
        }

        private void UpdateTargetExposurePlans(ITarget previousTarget) {
            if (previousTarget == null) return;

            // If running in a real sequence, reload exposure plans to get latest from database
            if (!previousTarget.IsPreview) {
                previousTarget.AllExposurePlans = GetExposurePlans(previousTarget);
            }

            previousTarget.ExposurePlans.Clear();
            previousTarget.CompletedExposurePlans.Clear();

            previousTarget.AllExposurePlans.ForEach(ep => {
                if (ep.IsIncomplete()) {
                    previousTarget.ExposurePlans.Add(ep);
                } else {
                    SetRejected(ep, Reasons.FilterComplete);
                    previousTarget.CompletedExposurePlans.Add(ep);
                }
            });

            // If this target is using an override exposure order, we need to ensure that it covers all exposure plans.
            // Otherwise, they have to be marked rejected since the target would never reach completion just using
            // the OEO list (this is in the context of whether the current target can continue).
            OverrideOrderExposureSelector oeoExposureSelector = previousTarget.ExposureSelector as OverrideOrderExposureSelector;
            if (oeoExposureSelector != null) {
                for (int i = 0; i < previousTarget.AllExposurePlans.Count; i++) {
                    if (!oeoExposureSelector.ContainsExposurePlanIdx(i)) {
                        SetRejected(previousTarget.AllExposurePlans[i], Reasons.FilterComplete);
                    }
                }
            }
        }

        private List<IExposure> GetExposurePlans(ITarget target) {
            try {
                SchedulerDatabaseInteraction database = new SchedulerDatabaseInteraction();
                using (SchedulerDatabaseContext context = database.GetContext()) {
                    var eps = context.GetExposurePlans(target.DatabaseId);
                    List<IExposure> exposures = new List<IExposure>(eps.Count);
                    eps.ForEach(ep => {
                        exposures.Add(new PlanningExposure(target, ep, ep.ExposureTemplate));
                    });
                    return exposures;
                }
            } catch (Exception ex) {
                TSLogger.Error($"exception reloading target exposure plans {target.Name}: {ex.StackTrace}");
                throw new SequenceEntityFailedException($"Scheduler: exception reloading target exposure plans: {ex.Message}", ex);
            }
        }

        public bool HasActiveProjects(List<IProject> projects) {
            if (projects == null) {
                projects = GetProjects();
            }

            if (NoProjects(projects)) {
                return false;
            }

            foreach (IProject project in projects) {
                if (ProjectIsInComplete(project)) {
                    return true;
                }
            }

            return false;
        }

        private bool NoProjects(List<IProject> projects) {
            return projects == null || projects.Count == 0;
        }

        private void SetRejected(IProject project, string reason) {
            project.Rejected = true;
            project.RejectedReason = reason;
        }

        private void SetRejected(ITarget target, string reason) {
            target.Rejected = true;
            target.RejectedReason = reason;
        }

        private void SetRejected(IExposure exposure, string reason) {
            exposure.Rejected = true;
            exposure.RejectedReason = reason;
        }

        private List<IProject> PropagateRejections(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }
                bool projectRejected = true;

                foreach (ITarget target in project.Targets) {
                    if (target.Rejected) { continue; }
                    bool targetRejected = true;

                    bool allExposurePlansComplete = true;
                    foreach (IExposure exposure in target.ExposurePlans) {
                        if (!exposure.Rejected) {
                            targetRejected = false;
                            break;
                        }

                        if (exposure.Rejected && exposure.RejectedReason != Reasons.FilterComplete) {
                            allExposurePlansComplete = false;
                        }
                    }

                    if (targetRejected) {
                        SetRejected(target, allExposurePlansComplete ? Reasons.TargetComplete : Reasons.TargetAllExposurePlans);
                    }

                    if (!target.Rejected) {
                        projectRejected = false;
                    }
                }

                if (projectRejected) {
                    SetRejected(project, Reasons.ProjectAllTargets);
                }
            }

            return projects;
        }

        private bool ProjectIsInComplete(IProject project) {
            bool incomplete = false;

            foreach (ITarget target in project.Targets) {
                foreach (IExposure exposure in target.ExposurePlans) {
                    if (exposure.NeededExposures() > 0) {
                        incomplete = true;
                    } else {
                        SetRejected(exposure, Reasons.FilterComplete);
                    }
                }
            }

            return incomplete;
        }
    }

    public class Reasons {
        public const string ProjectComplete = "complete";
        public const string ProjectNoVisibleTargets = "no visible targets";
        public const string ProjectMoonAvoidance = "moon avoidance";
        public const string ProjectAllTargets = "all targets rejected";

        public const string TargetComplete = "complete";
        public const string TargetNeverRises = "never rises at location";
        public const string TargetNotVisible = "not visible";
        public const string TargetNotYetVisible = "not yet visible";
        public const string TargetMaxAltitude = "max altitude";
        public const string TargetMeridianWindowClipped = "clipped by meridian window";
        public const string TargetBeforeMeridianWindow = "before meridian window";
        public const string TargetMoonAvoidance = "moon avoidance";
        public const string TargetLowerScore = "lower score";
        public const string TargetAllExposurePlans = "all exposure plans rejected";

        public const string FilterComplete = "complete";
        public const string FilterMoonAvoidance = "moon avoidance";
        public const string FilterTwilight = "twilight";
        public const string FilterNoExposuresPlanned = "no exposures planned";

        private Reasons() {
        }
    }
}