namespace NINA.Plugin.TargetScheduler.Shared.Utility {

    public enum EventContainerType {
        BeforeWait,
        AfterWait,
        BeforeTarget,
        AfterEachExposure,
        AfterTarget,
        AfterEachTarget,
        AfterTargetComplete
    }

    public static class EventContainerHelper {

        public static EventContainerType Convert(string eventContainerType) {
            if (string.IsNullOrEmpty(eventContainerType)) {
                throw new ArgumentNullException(nameof(eventContainerType));
            }

            if (eventContainerType == EventContainerType.BeforeWait.ToString()) {
                return EventContainerType.BeforeWait;
            }

            if (eventContainerType == EventContainerType.AfterWait.ToString()) {
                return EventContainerType.AfterWait;
            }

            if (eventContainerType == EventContainerType.BeforeTarget.ToString()) {
                return EventContainerType.BeforeTarget;
            }

            if (eventContainerType == EventContainerType.AfterEachExposure.ToString()) {
                return EventContainerType.AfterEachExposure;
            }

            if (eventContainerType == EventContainerType.AfterTarget.ToString()) {
                return EventContainerType.AfterTarget;
            }

            if (eventContainerType == EventContainerType.AfterEachTarget.ToString()) {
                return EventContainerType.AfterEachTarget;
            }

            if (eventContainerType == EventContainerType.AfterTargetComplete.ToString()) {
                return EventContainerType.AfterTargetComplete;
            }

            throw new ArgumentException($"unknown event container type : {eventContainerType}");
        }
    }
}