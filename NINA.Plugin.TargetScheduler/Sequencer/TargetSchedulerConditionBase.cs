using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;

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
            IProfile serverProfile = ProfileLoader.GetLastLoaded(serverProfileId);
            if (serverProfile != null) {
                TSLogger.Info($"sync client using server profile for TS condition: {serverProfileId}");
                return serverProfile;
            }

            TSLogger.Warning($"sync client server profile id={serverProfileId} not loaded, defaulting to sync client profile");
            return profileService.ActiveProfile;
        }

        protected ProfilePreference GetProfilePreferences() {
            SchedulerPlanLoader loader = new SchedulerPlanLoader(profileService.ActiveProfile);
            return loader.GetProfilePreferences(new SchedulerDatabaseInteraction().GetContext());
        }
    }
}