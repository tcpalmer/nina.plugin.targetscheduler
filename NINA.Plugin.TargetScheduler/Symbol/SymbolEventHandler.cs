using NINA.Plugin.TargetScheduler.Sequencer;
using System;
using System.Reflection;

namespace NINA.Plugin.TargetScheduler.Symbol {

    public class SymbolEventHandler {
        private readonly SymbolPublisher _publisher;

        public SymbolEventHandler(TargetSchedulerEventMediator eventMediator, SymbolPublisher symbolPublisher) {
            _publisher = symbolPublisher;

            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_VERSION, GetVersion());
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, false);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_WAITING, false);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_PAUSED, false);

            eventMediator.ContainerStarting += EventMediator_ContainerStarting;
            eventMediator.ContainerStopping += EventMediator_ContainerStopping;
            eventMediator.WaitStarting += EventMediator_WaitStarting;
            eventMediator.WaitStopping += EventMediator_WaitStopping;
            eventMediator.ContainerPaused += EventMediator_ContainerPaused;
            eventMediator.ContainerUnpaused += EventMediator_ContainerUnpaused;
            eventMediator.TargetStarting += EventMediator_TargetStarting; ;
            eventMediator.TargetStopping += EventMediator_TargetStopping;
            eventMediator.ExposureStarting += EventMediator_ExposureStarting;
            eventMediator.ExposureStopping += EventMediator_ExposureStopping;
            eventMediator.TargetComplete += EventMediator_TargetComplete;
            eventMediator.SymbolReset += EventMediator_SymbolReset;
        }

        private void EventMediator_ContainerStarting(object sender, EventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, true);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_LAST_STARTED, DateTime.Now);
        }

        private void EventMediator_ContainerStopping(object sender, EventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, false);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_LAST_STOPPED, DateTime.Now);
        }

        private void EventMediator_WaitStarting(object sender, WaitStartingEventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_NEXT_TARGET_NAME, e.Target?.Name);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_NEXT_PROJECT_NAME, e.Target?.Project?.Name);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_NEXT_TARGET_START, e.WaitUntil);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_NAME, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_PROJECT_NAME, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_COORDINATES, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_ROTATION, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_STARTED, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_WAITING, true);
        }

        private void EventMediator_WaitStopping(object sender, EventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_WAITING, false);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_NEXT_TARGET_START, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_NEXT_TARGET_NAME, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_NEXT_PROJECT_NAME, null);
        }

        private void EventMediator_ContainerPaused(object sender, EventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_PAUSED, true);
        }

        private void EventMediator_ContainerUnpaused(object sender, EventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_PAUSED, false);
        }

        private void EventMediator_TargetStarting(object sender, TargetStartingEventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_NAME, e.Target?.Name);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_PROJECT_NAME, e.Target?.Project?.Name);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_COORDINATES, e.Target?.Coordinates);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_ROTATION, e.Target?.Rotation);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_STARTED, DateTime.Now);
        }

        private void EventMediator_TargetStopping(object sender, EventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_NAME, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_PROJECT_NAME, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_COORDINATES, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_ROTATION, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CURRENT_TARGET_STARTED, null);
        }

        private void EventMediator_ExposureStarting(object sender, ExposureStartingEventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_FILTER_NAME, e.Exposure?.FilterName);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_EXPOSURE_LENGTH, e.Exposure?.ExposureLength);
        }

        private void EventMediator_ExposureStopping(object sender, EventArgs e) {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_FILTER_NAME, null);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_EXPOSURE_LENGTH, null);
        }

        private void EventMediator_TargetComplete(object sender, TargetCompleteEventArgs e) {
            // no-op
        }

        private void EventMediator_SymbolReset(object sender, EventArgs e) {
            _publisher.Reset();
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, false);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_WAITING, false);
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_PAUSED, false);
        }

        private string GetVersion() {
            return Assembly.GetAssembly(typeof(TargetScheduler)).GetName().Version.ToString();
        }
    }
}