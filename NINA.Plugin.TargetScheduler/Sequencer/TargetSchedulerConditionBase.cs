using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using System;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public abstract class TargetSchedulerConditionBase : SequenceCondition {
        protected IProfileService profileService;
        protected IWeatherDataMediator weatherDataMediator;

        protected IProfile GetApplicableProfile() {
            if (!SyncManager.Instance.RunningClient) {
                return profileService.ActiveProfile;
            }

            // If running as a sync client, we need to use the server's profile for TS database queries used by the Planner
            string serverProfileId = SyncClient.Instance.ServerProfileId;

            try {
                ProfileMeta profileMeta = profileService.Profiles.Where(p => p.Id.ToString() == serverProfileId).FirstOrDefault();
                if (profileMeta != null) {
                    IProfile serverProfile = ProfileLoader.Load(profileService, profileMeta);
                    TSLogger.Info($"sync client using server profile for TS condition: {serverProfileId}");
                    return serverProfile;
                }

                TSLogger.Warning($"sync client could not load server profile id={serverProfileId}, defaulting to sync client profile");
                return profileService.ActiveProfile;
            } catch (Exception e) {
                TSLogger.Error($"sync client failed to load server profile id={serverProfileId}: {e.Message}");
                return profileService.ActiveProfile;
            }
        }

        protected ProfilePreference GetProfilePreferences() {
            SchedulerPlanLoader loader = new SchedulerPlanLoader(profileService.ActiveProfile);
            return loader.GetProfilePreferences(new SchedulerDatabaseInteraction().GetContext());
        }
    }
}