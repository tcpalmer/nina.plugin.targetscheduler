using NINA.Core.Model.Equipment;
using NINA.Equipment.Equipment.MyFilterWheel;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Shared.Utility;

namespace NINA.Plugin.TargetScheduler.SyncService.Sync {

    public class SyncFocuserConsumer : IFocuserConsumer, IFilterWheelConsumer {
        private FilterInfo? currentFilterInfo;

        public void AutoFocusRunStarting() {
            TSLogger.Info("SYNC server detected start of autofocus run, alerting clients to autofocus");
            /*
             * Can we just asynchroniously fire off the event to the client? Fire and forget? Best effort?
             * Or if the client is still running AF, would the server just time out trying to send the
             * next action (like an exposure) and that one is skipped?
             *
             * I think both client and server profiles should have to enable sync AF.
             * If client gets an AF action but doesn't have it enabled, display a warning pop.
             *
             * Another thought is to create a TSSyncAutoFocus sequence trigger. The triggering event is when
             * the client gets an AF action. It can crawl the sequence up and find the trigger and enable it.
             */

            if (currentFilterInfo == null) {
                TSLogger.Warning("SYNC server autofocus start detected but current filter info is not available");
                return;
            }

            TSLogger.Info($"SYNC server detected autofocus start, filter is {currentFilterInfo?.Name}");
        }

        public void UpdateDeviceInfo(FocuserInfo deviceInfo) {
        }

        public void UpdateDeviceInfo(FilterWheelInfo deviceInfo) {
            if (deviceInfo.Connected) {
                currentFilterInfo = deviceInfo.SelectedFilter;
            }
        }

        public void UpdateEndAutoFocusRun(AutoFocusInfo info) {
            TSLogger.Debug("SYNC server detected autofocus end");
            currentFilterInfo = null;
        }

        public void UpdateUserFocused(FocuserInfo info) {
        }

        public void Dispose() {
        }
    }
}