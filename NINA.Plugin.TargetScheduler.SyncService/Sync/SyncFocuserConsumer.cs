using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Shared.Utility;

namespace NINA.Plugin.TargetScheduler.SyncService.Sync {

    public class SyncFocuserConsumer : IFocuserConsumer {
        private bool syncFocusEnabled = false;
        private IFocuserMediator focuserMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IProgress<ApplicationStatus> progress;
        private CancellationToken token;

        public SyncFocuserConsumer(IFocuserMediator focuserMediator, IFilterWheelMediator filterWheelMediator,
            IProgress<ApplicationStatus> progress, CancellationToken cancellationToken) {
            this.focuserMediator = focuserMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.progress = progress;
            this.token = cancellationToken;

            focuserMediator.RegisterConsumer(this);
        }

        public async void AutoFocusRunStarting() {
            if (!syncFocusEnabled) {
                TSLogger.Debug("SYNC server autofocus start detected but TS container is not running, skipping sync");
                return;
            }

            // Get the current filter to use on the client. Note that this is problematic for a sync client since it could
            // have different filter names than the server.
            FilterInfo? filterInfo = filterWheelMediator.GetInfo().SelectedFilter;
            if (filterInfo == null) {
                TSLogger.Warning("SYNC server autofocus start detected but current filter info is not available");
                return;
            }

            // Clients can begin ...
            string autoFocusId = Guid.NewGuid().ToString();
            TSLogger.Info($"SYNC server informing clients of need for autofocus, filter is {filterInfo?.Name}, id={autoFocusId}");
            progress?.Report(new ApplicationStatus() { Status = "Target Scheduler: waiting for sync clients to accept autofocus" });
            await SyncServer.Instance.SyncAutoFocus(autoFocusId, filterInfo?.Name, SyncManager.DEFAULT_SYNC_ACTION_TIMEOUT, token);
            progress?.Report(new ApplicationStatus() { Status = "" });

            // And wait for them to complete
            TSLogger.Info($"SYNC server waiting for clients to complete autofocus, id={autoFocusId}");
            progress?.Report(new ApplicationStatus() { Status = "Target Scheduler: waiting for sync clients to complete autofocus" });
            await SyncServer.Instance.WaitForClientAutoFocusCompletion(autoFocusId, SyncManager.DEFAULT_SYNC_AUTOFOCUS_TIMEOUT, token);
            progress?.Report(new ApplicationStatus() { Status = "" });

            syncFocusEnabled = false;
        }

        public void UpdateDeviceInfo(FocuserInfo deviceInfo) {
        }

        public void UpdateEndAutoFocusRun(AutoFocusInfo info) {
            TSLogger.Debug("SYNC server detected server autofocus end");
        }

        public void UpdateUserFocused(FocuserInfo info) {
        }

        public void ContainerStarting(IProgress<ApplicationStatus> progress, CancellationToken token) {
            syncFocusEnabled = true;
            this.progress = progress;
            this.token = token;
        }

        public void ContainerStopping() {
            syncFocusEnabled = false;
            focuserMediator.RemoveConsumer(this);
        }

        public void Dispose() {
            focuserMediator.RemoveConsumer(this);
        }
    }
}