using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Planning {

    public class Planner {
        private bool checkCondition = false;
        private bool isPreview = false;
        private DateTime atTime;
        private IProfile activeProfile;
        private ProfilePreference profilePreferences;
        private IWeatherDataMediator weatherDataMediator;
        private ObserverInfo observerInfo;
        private PreviousTargetExpert previousTargetExpert;
        private List<IProject> projects;

        public Planner(DateTime atTime, IProfile profile, ProfilePreference profilePreferences, IWeatherDataMediator weatherDataMediator, bool checkCondition, bool isPreview)
            : this(atTime, profile, profilePreferences, weatherDataMediator, checkCondition, isPreview, null) {
        }

        public Planner(DateTime atTime, IProfile profile, ProfilePreference profilePreferences, IWeatherDataMediator weatherDataMediator, bool checkCondition, bool isPreview, List<IProject> projects) {
            this.atTime = atTime;
            this.activeProfile = profile;
            this.profilePreferences = profilePreferences;
            this.weatherDataMediator = weatherDataMediator;
            this.checkCondition = checkCondition;
            this.isPreview = isPreview;
            this.projects = projects;
            this.observerInfo = new ObserverInfo {
                Latitude = activeProfile.AstrometrySettings.Latitude,
                Longitude = activeProfile.AstrometrySettings.Longitude,
                Elevation = activeProfile.AstrometrySettings.Elevation,
            };

            previousTargetExpert = new PreviousTargetExpert(activeProfile, profilePreferences, isPreview, observerInfo);
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
                    if (previousTargetExpert.CanContinue(atTime, weatherDataMediator, previousTarget)) {
                        TSLogger.Info($"previous target still within permitted time span, continuing: {previousTarget.Project.Name}/{previousTarget.Name}: {previousTarget.BonusTimeSpanEnd} > {atTime} ");
                        List<IInstruction> instructions = new InstructionGenerator().Generate(previousTarget, previousTarget);
                        return new SchedulerPlan(atTime, projects, previousTarget, instructions, !checkCondition);
                    }

                    // Filter all targets for suitability
                    projects = FilterForIncomplete(projects);
                    projects = FilterForVisibility(projects);
                    projects = FilterForMoonAvoidance(projects);
                    projects = FilterForTwilight(projects);
                    projects = FilterForHumidity(projects, weatherDataMediator);

                    // See if one or more targets are ready to image now
                    List<ITarget> readyTargets = GetTargetsReadyNow(projects);
                    if (readyTargets.Count > 0) {
                        SelectTargetExposures(readyTargets);
                        ITarget selectedTarget = readyTargets.Count == 1
                            ? readyTargets[0]
                            : SelectTargetByScore(readyTargets, new ScoringEngine(activeProfile, profilePreferences, atTime, previousTarget));
                        List<IInstruction> instructions = new InstructionGenerator().Generate(selectedTarget, previousTarget);

                        HandleTargetSwitch(previousTarget, selectedTarget);
                        TargetEditGuard.Instance.Clear();

                        // Target ready now
                        return new SchedulerPlan(atTime, projects, selectedTarget, instructions, !checkCondition);
                    } else {
                        // If everything was rejected due to humidity, see if we're done (since it's very unlikely to improve)
                        if (!isPreview && projects?.Count > 0 && AllRejectedForHumidity(projects)) {
                            if (profilePreferences.EnableStopOnHumidity) {
                                TSLogger.Warning("all remaining targets were rejected for unacceptable humidity, ending for the night");
                                Notification.ShowWarning("Target Scheduler: all remaining targets were rejected for unacceptable humidity, ending for the night");
                                return null;
                            } else {
                                ITarget target = NextRejectedForHumidity(projects);
                                if (target != null) {
                                    TSLogger.Info("all remaining targets were rejected for unacceptable humidity, waiting 15 minutes and trying again");
                                    return new SchedulerPlan(atTime, projects, target, 15 * 60, !checkCondition);
                                }
                            }
                        }

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

                    TSLogger.Error($"exception generating plan: {ex.Message}\n{ex.StackTrace}");
                    throw new SequenceEntityFailedException($"Scheduler: exception generating plan: {ex.Message}", ex);
                } finally {
                    TSLogger.Info($"-- END {title} -----------------------------------------------------");
                }
            }
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
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);

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
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);

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
        public List<IProject> FilterForTwilight(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(observerInfo, atTime);
            TwilightLevel? currentTwilightLevel = twilightCircumstances.GetCurrentTwilightLevel(atTime);
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }

                foreach (ITarget target in project.Targets) {
                    targetExpert.TwilightFilter(target, atTime, twilightCircumstances, currentTwilightLevel);
                }
            }

            return PropagateRejections(projects);
        }

        /// <summary>
        /// Reject target exposures that are not suitable for the current level of humidity.
        /// </summary>
        /// <param name="projects"></param>
        /// <param name="weatherDataMediator"></param>
        /// <returns></returns>
        public List<IProject> FilterForHumidity(List<IProject> projects, IWeatherDataMediator weatherDataMediator) {
            if (NoProjects(projects)) { return null; }
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }

                foreach (ITarget target in project.Targets) {
                    targetExpert.HumidityFilter(target, weatherDataMediator);
                }
            }

            return PropagateRejections(projects);
        }

        /// <summary>
        /// Determine if all projects/targets were rejected for humidity.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public bool AllRejectedForHumidity(List<IProject> projects) {
            if (NoProjects(projects)) { throw new Exception("unexpected: no projects in AllRejectedForHumidity"); }

            foreach (IProject project in projects) {
                foreach (ITarget target in project.Targets) {
                    if (!target.Rejected) { return false; }
                    if (target.RejectedReason == Reasons.TargetComplete) { continue; }
                    if (target.RejectedReason != Reasons.TargetHumidity) { return false; }
                }
            }

            return true;
        }

        /// <summary>
        /// Assuming all targets were rejected for humidity, return the one that would be starting 'now', otherwise null if later.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public ITarget NextRejectedForHumidity(List<IProject> projects) {
            if (NoProjects(projects)) { throw new Exception("unexpected: no projects in NextRejectedForHumidity"); }

            List<ITarget> targets = new List<ITarget>();
            foreach (IProject project in projects) {
                foreach (ITarget target in project.Targets) {
                    if (target.Rejected && target.RejectedReason == Reasons.TargetHumidity) { targets.Add(target); }
                }
            }

            if (targets.Count == 0) {
                TSLogger.Warning("unexpected: no targets found in NextRejectedForHumidity");
                return null;
            }

            ITarget nextTarget = targets.OrderBy(t => t.StartTime).First();
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);
            return targetExpert.ReadyNow(atTime, nextTarget, false) ? nextTarget : null;
        }

        /// <summary>
        /// Return the list of targets that are imagable now.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public List<ITarget> GetTargetsReadyNow(List<IProject> projects) {
            List<ITarget> targets = new List<ITarget>();

            if (NoProjects(projects)) { return targets; }
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);

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
        public ITarget GetNextPossibleTarget(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }

            List<ITarget> potentialTargets = new List<ITarget>();

            // Find all targets that could possibly be imaged later
            foreach (IProject project in projects) {
                foreach (ITarget target in project.Targets) {
                    if (target.Rejected
                        && target.RejectedReason != Reasons.TargetNotYetVisible
                        && target.RejectedReason != Reasons.TargetBeforeMeridianWindow
                        && target.RejectedReason != Reasons.TargetMeridianFlipClipped
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
            TargetImagingExpert targetExpert = new TargetImagingExpert(activeProfile, profilePreferences, isPreview);
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
        public void SelectTargetExposures(List<ITarget> readyTargets) {
            if (Common.IsEmpty(readyTargets)) {
                return;
            }

            foreach (ITarget target in readyTargets) {
                target.SelectedExposure = target.ExposureSelector.Select(atTime, target.Project, target);
            }

            // Remove any target where we couldn't determine the selected exposure
            readyTargets.RemoveAll(t => t.SelectedExposure == null);
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

        private void HandleTargetSwitch(ITarget previousTarget, ITarget selectedTarget) {
            if (previousTarget == null) return;

            // If we're switching targets, we need to clear any cached dither manager for
            // the previous target so it starts fresh if selected in the future
            if (selectedTarget.DatabaseId != previousTarget.DatabaseId) {
                DitherManagerCache.Remove(previousTarget);
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

        public List<IProject> PropagateRejections(List<IProject> projects) {
            if (NoProjects(projects)) { return null; }

            foreach (IProject project in projects) {
                if (project.Rejected) { continue; }
                bool projectRejected = true;

                foreach (ITarget target in project.Targets) {
                    if (target.Rejected) { continue; }
                    if (target.ExposurePlans.All(ep => ep.Rejected)) { SetRejected(target, GetTargetExposureRejectReason(target)); }
                    if (!target.Rejected) { projectRejected = false; }
                }

                if (projectRejected) {
                    SetRejected(project, Reasons.ProjectAllTargets);
                }
            }

            return projects;
        }

        public string GetTargetExposureRejectReason(ITarget target) {
            if (target.ExposurePlans.All(ep => ep.RejectedReason == Reasons.FilterComplete)) { return Reasons.TargetComplete; }
            if (target.ExposurePlans.All(ep => ep.RejectedReason == Reasons.FilterTwilight)) { return Reasons.TargetTwilight; }
            if (target.ExposurePlans.All(ep => ep.RejectedReason == Reasons.FilterMoonAvoidance)) { return Reasons.TargetMoonAvoidance; }
            if (target.ExposurePlans.All(ep => ep.RejectedReason == Reasons.FilterHumidity)) { return Reasons.TargetHumidity; }
            return Reasons.TargetAllExposurePlans; // implicit assumption that all are indeed rejected
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
        public const string TargetMeridianFlipClipped = "clipped by meridian flip safety";
        public const string TargetTwilight = "all exposure plans rejected for twilight";
        public const string TargetMoonAvoidance = "all exposure plans rejected for moon avoidance";
        public const string TargetHumidity = "all exposure plans rejected for humidity";
        public const string TargetLowerScore = "lower score";
        public const string TargetAllExposurePlans = "all exposure plans rejected";

        public const string FilterComplete = "complete";
        public const string FilterMoonAvoidance = "moon avoidance";
        public const string FilterTwilight = "twilight";
        public const string FilterHumidity = "humidity";
        public const string FilterNoExposuresPlanned = "no exposures planned";

        private Reasons() {
        }
    }
}