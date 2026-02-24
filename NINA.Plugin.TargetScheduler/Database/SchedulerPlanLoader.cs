using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Database {

    public class SchedulerPlanLoader {
        private IProfile activeProfile;
        private string profileId;

        public SchedulerPlanLoader(IProfile activeProfile) {
            this.activeProfile = activeProfile;
            profileId = activeProfile.Id.ToString();
        }

        public ProfilePreference GetProfilePreferences() {
            return GetProfilePreferences(new SchedulerDatabaseInteraction().GetContext());
        }

        public ProfilePreference GetProfilePreferences(ISchedulerDatabaseContext context) {
            using (context) {
                return context.GetProfilePreference(profileId, true);
            }
        }

        public List<IProject> LoadActiveProjects(ISchedulerDatabaseContext context) {
            List<Project> projects = null;

            using (context) {
                try {
                    projects = context.GetActiveProjects(profileId);
                } catch (Exception ex) {
                    throw; // let the caller decide how to handle
                }

                if (projects == null || projects.Count == 0) {
                    TSLogger.Warning("no projects are active at planning time");
                    return null;
                }

                bool haveActiveTargets = false;
                ProfilePreference profilePreference = GetProfilePreferences();

                foreach (Project project in projects) {
                    ExposureCompletionHelper helper = new ExposureCompletionHelper(project.EnableGrader, profilePreference.DelayGrading, profilePreference.ExposureThrottle);
                    foreach (Target target in project.Targets) {
                        if (target.Enabled) {
                            target.ExposurePlans.RemoveAll(ep => !ep.IsEnabled);
                            foreach (ExposurePlan plan in target.ExposurePlans) {
                                if (helper.RemainingExposures(plan) > 0) {
                                    haveActiveTargets = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!haveActiveTargets) {
                    TSLogger.Warning("no targets with exposure plans are active for active projects at planning time");
                    return null;
                }

                List<IProject> planProjects = new List<IProject>();
                foreach (Project project in projects) {
                    ExposureCompletionHelper helper = new ExposureCompletionHelper(project.EnableGrader, profilePreference.DelayGrading, profilePreference.ExposureThrottle);
                    PlanningProject planProject = new PlanningProject(activeProfile, project, helper);
                    planProjects.Add(planProject);
                }

                return planProjects;
            }
        }
    }
}