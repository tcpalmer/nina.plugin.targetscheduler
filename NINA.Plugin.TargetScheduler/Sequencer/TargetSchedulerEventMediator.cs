using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using System;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public class TargetSchedulerEventMediator {

        private event EventHandler ContainerStartingEvent;

        private event EventHandler ContainerPausedEvent;

        private event EventHandler ContainerUnpausedEvent;

        private event EventHandler ContainerStoppingEvent;

        private event EventHandler<WaitStartingEventArgs> WaitStartingEvent;

        private event EventHandler WaitStoppingEvent;

        private event EventHandler<ExposureStartingEventArgs> ExposureStartingEvent;

        private event EventHandler ExposureStoppingEvent;

        private event EventHandler<TargetCompleteEventArgs> TargetCompleteEvent;

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

        public void InvokeContainerStarting(TargetSchedulerContainer container) {
            ContainerStartingEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeContainerPaused(TargetSchedulerContainer container) {
            ContainerPausedEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeContainerUnpaused(TargetSchedulerContainer container) {
            ContainerUnpausedEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeContainerStopping(TargetSchedulerContainer container) {
            ContainerStoppingEvent?.Invoke(container, EventArgs.Empty);
        }

        public void InvokeWaitStarting(DateTime waitUntil, ITarget nextTarget) {
            WaitStartingEvent?.Invoke(this, new WaitStartingEventArgs { WaitUntil = waitUntil, Target = nextTarget });
        }

        public void InvokeWaitStopping() {
            WaitStoppingEvent?.Invoke(this, EventArgs.Empty);
        }

        public void InvokeExposureStarting(ITarget target, IExposure exposure, bool isNewTarget) {
            ExposureStartingEvent?.Invoke(this, new ExposureStartingEventArgs { Target = target, Exposure = exposure, IsNewTarget = isNewTarget });
        }

        public void InvokeExposureStopping() {
            ExposureStoppingEvent?.Invoke(this, EventArgs.Empty);
        }

        public void InvokeTargetComplete(ITarget target) {
            TargetCompleteEvent?.Invoke(this, new TargetCompleteEventArgs { Target = target });
        }
    }

    public class WaitStartingEventArgs : EventArgs {
        public DateTime WaitUntil { get; set; }
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
}