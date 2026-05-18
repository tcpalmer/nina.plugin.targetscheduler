using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public class TargetSchedulerEventMediator {

        private event EventHandler ContainerStartingEvent;

        private event EventHandler ContainerPausedEvent;

        private event EventHandler ContainerUnpausedEvent;

        private event EventHandler ContainerStoppingEvent;

        private event EventHandler<WaitStartingEventArgs> WaitStartingEvent;

        private event EventHandler WaitStoppingEvent;

        private event EventHandler<TargetStartingEventArgs> TargetStartingEvent;

        private event EventHandler TargetStoppingingEvent;

        private event EventHandler<ExposureStartingEventArgs> ExposureStartingEvent;

        private event EventHandler ExposureStoppingEvent;

        private event EventHandler<TargetCompleteEventArgs> TargetCompleteEvent;

        private event EventHandler<APIStartingEventArgs> APIStartingEvent;

        private event EventHandler APIStoppingEvent;

        private event EventHandler SymbolResetEvent;

        public TargetSchedulerEventMediator() {
        }

        public event EventHandler ContainerStarting {
            add { ContainerStartingEvent += value; }
            remove { ContainerStartingEvent -= value; }
        }

        public event EventHandler ContainerPaused {
            add { ContainerPausedEvent += value; }
            remove { ContainerPausedEvent -= value; }
        }

        public event EventHandler ContainerUnpaused {
            add { ContainerUnpausedEvent += value; }
            remove { ContainerUnpausedEvent -= value; }
        }

        public event EventHandler ContainerStopping {
            add { ContainerStoppingEvent += value; }
            remove { ContainerStoppingEvent -= value; }
        }

        public event EventHandler<WaitStartingEventArgs> WaitStarting {
            add { WaitStartingEvent += value; }
            remove { WaitStartingEvent -= value; }
        }

        public event EventHandler WaitStopping {
            add { WaitStoppingEvent += value; }
            remove { WaitStoppingEvent -= value; }
        }

        public event EventHandler<TargetStartingEventArgs> TargetStarting {
            add { TargetStartingEvent += value; }
            remove { TargetStartingEvent -= value; }
        }

        public event EventHandler TargetStopping {
            add { TargetStoppingingEvent += value; }
            remove { TargetStoppingingEvent -= value; }
        }

        public event EventHandler<ExposureStartingEventArgs> ExposureStarting {
            add { ExposureStartingEvent += value; }
            remove { ExposureStartingEvent -= value; }
        }

        public event EventHandler ExposureStopping {
            add { ExposureStoppingEvent += value; }
            remove { ExposureStoppingEvent -= value; }
        }

        public event EventHandler<TargetCompleteEventArgs> TargetComplete {
            add { TargetCompleteEvent += value; }
            remove { TargetCompleteEvent -= value; }
        }

        public event EventHandler<APIStartingEventArgs> APIStarting {
            add { APIStartingEvent += value; }
            remove { APIStartingEvent -= value; }
        }

        public event EventHandler APIStopping {
            add { APIStoppingEvent += value; }
            remove { APIStoppingEvent -= value; }
        }

        public event EventHandler SymbolReset {
            add { SymbolResetEvent += value; }
            remove { SymbolResetEvent -= value; }
        }

        public void InvokeContainerStarting(TargetSchedulerContainer container) {
            TSLogger.Trace("invoking event: ContainerStarting");
            ContainerStartingEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeContainerPaused(TargetSchedulerContainer container) {
            TSLogger.Trace("invoking event: ContainerPaused");
            ContainerPausedEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeContainerUnpaused(TargetSchedulerContainer container) {
            TSLogger.Trace("invoking event: ContainerUnpaused");
            ContainerUnpausedEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeContainerStopping(TargetSchedulerContainer container) {
            TSLogger.Trace("invoking event: ContainerStopping");
            ContainerStoppingEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeWaitStarting(DateTime waitUntil, ITarget nextTarget) {
            TSLogger.Trace("invoking event: WaitStarting");
            WaitStartingEvent?.Invoke(this, new WaitStartingEventArgs { WaitUntil = waitUntil, Target = nextTarget });
        }

        public void InvokeWaitStopping() {
            TSLogger.Trace("invoking event: WaitStopping");
            WaitStoppingEvent?.Invoke(this, EventArgs.Empty);
        }

        public void InvokeTargetStarting(ITarget target) {
            TSLogger.Trace($"invoking event: TargetStarting ({target?.Name})");
            TargetStartingEvent?.Invoke(this, new TargetStartingEventArgs { Target = target });
        }

        public void InvokeTargetStopping() {
            TSLogger.Trace("invoking event: TargetStopping");
            TargetStoppingingEvent?.Invoke(this, EventArgs.Empty);
        }

        public void InvokeExposureStarting(ITarget target, IExposure exposure, bool isNewTarget) {
            TSLogger.Trace("invoking event: ExposureStarting");
            ExposureStartingEvent?.Invoke(this, new ExposureStartingEventArgs { Target = target, Exposure = exposure, IsNewTarget = isNewTarget });
        }

        public void InvokeExposureStopping() {
            TSLogger.Trace("invoking event: ExposureStopping");
            ExposureStoppingEvent?.Invoke(this, EventArgs.Empty);
        }

        public void InvokeTargetComplete(ITarget target) {
            TSLogger.Trace($"invoking event: TargetComplete ({target?.Name})");
            TargetCompleteEvent?.Invoke(this, new TargetCompleteEventArgs { Target = target });
        }

        public void InvokeAPIStarting(string url) {
            TSLogger.Trace("invoking event: APIStarting");
            APIStartingEvent?.Invoke(this, new APIStartingEventArgs { URL = url });
        }

        public void InvokeAPIStopping() {
            TSLogger.Trace("invoking event: APIStopping");
            APIStoppingEvent?.Invoke(this, EventArgs.Empty);
        }

        internal void InvokeSymbolReset() {
            TSLogger.Trace("invoking event: SymbolReset");
            SymbolResetEvent?.Invoke(this, EventArgs.Empty);
        }
    }

    public class WaitStartingEventArgs : EventArgs {
        public DateTime WaitUntil { get; set; }
        public ITarget Target { get; set; }
    }

    public class TargetStartingEventArgs : EventArgs {
        public ITarget Target { get; set; }
    }

    public class ExposureStartingEventArgs : EventArgs {
        public ITarget Target { get; set; }
        public IExposure Exposure { get; set; }
        public bool IsNewTarget { get; set; }
    }

    public class TargetCompleteEventArgs : EventArgs {
        public ITarget Target { get; set; }
    }

    public class APIStartingEventArgs : EventArgs {
        public string URL { get; set; }
    }
}