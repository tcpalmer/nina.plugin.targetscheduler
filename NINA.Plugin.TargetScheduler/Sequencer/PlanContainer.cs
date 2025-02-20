using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin.Interfaces;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.PubSub;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Camera;
using NINA.Sequencer.SequenceItem.FilterWheel;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Sequencer.SequenceItem.Platesolving;
using NINA.Sequencer.SequenceItem.Telescope;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public class PlanContainer : SequentialContainer, IDeepSkyObjectContainer {
        public static readonly string INSTRUCTION_CATEGORY = "Scheduler";

        private readonly TargetSchedulerContainer parentContainer;
        private readonly IProfileService profileService;
        private readonly IList<IDateTimeProvider> dateTimeProviders;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IRotatorMediator rotatorMediator;
        private readonly IGuiderMediator guiderMediator;
        private readonly ICameraMediator cameraMediator;
        private readonly IImagingMediator imagingMediator;
        private readonly IImageSaveMediator imageSaveMediator;
        private readonly IImageHistoryVM imageHistoryVM;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IDomeMediator domeMediator;
        private readonly IDomeFollower domeFollower;
        private readonly IPlateSolverFactory plateSolverFactory;
        private readonly IWindowServiceFactory windowServiceFactory;

        private readonly ProfilePreference profilePreferences;
        private readonly ITarget previousPlanTarget;
        private readonly SchedulerPlan plan;
        private readonly IProfile activeProfile;
        private SchedulerProgressVM schedulerProgress;

        private IImageSaveWatcher imageSaveWatcher;
        private bool synchronizationEnabled;
        private int syncActionTimeout;
        private int syncSolveRotateTimeout;

        private NewTargetStartPublisher newTargetStartPublisher;
        private TargetStartPublisher targetStartPublisher;

        public PlanContainer(
                TargetSchedulerContainer parentContainer,
                IProfileService profileService,
                IList<IDateTimeProvider> dateTimeProviders,
                ITelescopeMediator telescopeMediator,
                IRotatorMediator rotatorMediator,
                IGuiderMediator guiderMediator,
                ICameraMediator cameraMediator,
                IImagingMediator imagingMediator,
                IImageSaveMediator imageSaveMediator,
                IImageHistoryVM imageHistoryVM,
                IFilterWheelMediator filterWheelMediator,
                IDomeMediator domeMediator,
                IDomeFollower domeFollower,
                IPlateSolverFactory plateSolverFactory,
                IWindowServiceFactory windowServiceFactory,
                IMessageBroker messageBroker,
                IImageSaveWatcher imageSaveWatcher,
                ProfilePreference profilePreferences,
                bool synchronizationEnabled,
                ITarget previousPlanTarget,
                SchedulerPlan plan,
                SchedulerProgressVM schedulerProgress) {
            Name = nameof(PlanContainer);
            Description = "";
            Category = INSTRUCTION_CATEGORY;

            this.parentContainer = parentContainer;
            this.profileService = profileService;
            this.dateTimeProviders = dateTimeProviders;
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.guiderMediator = guiderMediator;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
            this.filterWheelMediator = filterWheelMediator;
            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.imageSaveWatcher = imageSaveWatcher;
            this.profilePreferences = profilePreferences;
            this.synchronizationEnabled = synchronizationEnabled;
            this.schedulerProgress = schedulerProgress;
            this.previousPlanTarget = previousPlanTarget;
            this.plan = plan;
            this.activeProfile = profileService.ActiveProfile;

            AttachNewParent(parentContainer);

            if (synchronizationEnabled) {
                SetSyncTimeouts();
            }

            newTargetStartPublisher = new NewTargetStartPublisher(messageBroker);
            targetStartPublisher = new TargetStartPublisher(messageBroker);

            // These have no impact on the container itself but are used to assign to each added instruction
            Attempts = 1;
            ErrorBehavior = InstructionErrorBehavior.ContinueOnError;
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            TSLogger.Debug("executing target container");

            try {
                AddEndTimeTrigger(plan.PlanTarget);
                AddInstructions(plan);
                EnsureUnparked(progress, token);

                targetStartPublisher.Publish(plan);

                base.Execute(progress, token).Wait();
            } catch (Exception ex) {
                throw;
            } finally {
                imageSaveWatcher.WaitForAllImagesSaved();
                parentContainer.ExecuteEventContainer(parentContainer.AfterEachExposureContainer, progress, token).Wait();

                foreach (var item in Items) {
                    item.AttachNewParent(null);
                }

                foreach (var trigger in Triggers) {
                    trigger.AttachNewParent(null);
                }

                Items.Clear();
                Triggers.Clear();
                TSLogger.Debug("done executing target container");
            }

            return Task.CompletedTask;
        }

        public override Task Interrupt() {
            TSLogger.Warning("PlanContainer: interrupt");
            return base.Interrupt();
        }

        private void AddEndTimeTrigger(ITarget planTarget) {
            TSLogger.Debug($"adding target end time trigger, run until: {Utils.FormatDateTimeFull(planTarget.EndTime)}");
            Add(new SchedulerTargetEndTimeTrigger(planTarget.EndTime));
        }

        private void AddInstructions(SchedulerPlan plan) {
            foreach (IInstruction instruction in plan.PlanInstructions) {
                if (instruction is PlanMessage) {
                    TSLogger.Debug($"exp plan msg: {((PlanMessage)instruction).msg}");
                    continue;
                }

                Add(new PlanSchedulerProgress(schedulerProgress, instruction));

                if (instruction is PlanSlew) {
                    if (profilePreferences.EnableSlewCenter) {
                        AddSlew((PlanSlew)instruction, plan.PlanTarget);
                    }
                    continue;
                }

                if (instruction is PlanSwitchFilter) {
                    AddSwitchFilter(instruction.exposure);
                    continue;
                }

                if (instruction is PlanSetReadoutMode) {
                    AddSetReadoutMode(instruction.exposure);
                    continue;
                }

                if (instruction is Planning.Entities.PlanTakeExposure) {
                    AddTakeExposure(plan.PlanTarget, instruction.exposure);
                    continue;
                }

                if (instruction is PlanDither) {
                    AddDither();
                    continue;
                }

                if (instruction is PlanPostExposure) {
                    // currently a no-op
                    continue;
                }

                if (instruction is PlanBeforeNewTargetContainer) {
                    newTargetStartPublisher.Publish(plan);
                    AddBeforeNewTargetInstructions();
                    continue;
                }

                TSLogger.Error($"unknown instruction type: {instruction.GetType().FullName}");
                throw new Exception($"unknown instruction type: {instruction.GetType().FullName}");
            }
        }

        private void AddSlew(PlanSlew instruction, ITarget target) {
            bool isPlateSolve = instruction.center;
            InputCoordinates slewCoordinates = new InputCoordinates(target.Coordinates);
            SequenceItem slewCenter;

            string with = isPlateSolve ? "with" : "without";
            TSLogger.Debug($"slew ({with} center): {Utils.FormatCoordinates(target.Coordinates)}");

            if (isPlateSolve) {
                if (rotatorMediator.GetInfo().Connected) {
                    if (synchronizationEnabled) {
                        slewCenter = new PlanCenterAndRotate(target, Target, syncActionTimeout, syncSolveRotateTimeout, profileService, telescopeMediator, imagingMediator, rotatorMediator, filterWheelMediator, guiderMediator, domeMediator, domeFollower, plateSolverFactory, windowServiceFactory);
                    } else {
                        slewCenter = new CenterAndRotate(profileService, telescopeMediator, imagingMediator, rotatorMediator, filterWheelMediator, guiderMediator, domeMediator, domeFollower, plateSolverFactory, windowServiceFactory);
                    }

                    slewCenter.Name = nameof(CenterAndRotate);
                    (slewCenter as Center).Coordinates = slewCoordinates;
                    (slewCenter as CenterAndRotate).PositionAngle = target.Rotation;
                } else {
                    slewCenter = new Center(profileService, telescopeMediator, imagingMediator, filterWheelMediator, guiderMediator, domeMediator, domeFollower, plateSolverFactory, windowServiceFactory);
                    slewCenter.Name = nameof(Center);
                    (slewCenter as Center).Coordinates = slewCoordinates;
                }
            } else {
                slewCenter = new SlewScopeToRaDec(telescopeMediator, guiderMediator);
                slewCenter.Name = nameof(SlewScopeToRaDec);
                (slewCenter as SlewScopeToRaDec).Coordinates = slewCoordinates;
            }

            SetItemDefaults(slewCenter, null);
            Add(slewCenter);
        }

        private void AddBeforeNewTargetInstructions() {
            int? numInstructions = parentContainer.BeforeTargetContainer.Items?.Count;
            TSLogger.Debug($"adding BeforeNewTarget container with {numInstructions} instruction(s)");
            parentContainer.BeforeTargetContainer.ResetAll();
            Add(parentContainer.BeforeTargetContainer);
        }

        private void AddSwitchFilter(IExposure exposure) {
            TSLogger.Debug($"adding switch filter: {exposure.FilterName}");

            SwitchFilter switchFilter = new SwitchFilter(profileService, filterWheelMediator);
            SetItemDefaults(switchFilter, nameof(SwitchFilter));

            switchFilter.Filter = Utils.LookupFilter(profileService.ActiveProfile, exposure.FilterName);
            Add(switchFilter);
        }

        private void AddSetReadoutMode(IExposure exposure) {
            short readoutMode = GetReadoutMode(exposure.ReadoutMode);
            TSLogger.Debug($"adding set readout mode: {readoutMode}");
            SetReadoutMode setReadoutMode = new SetReadoutMode(cameraMediator) { Mode = readoutMode };
            SetItemDefaults(setReadoutMode, nameof(SetReadoutMode));

            Add(setReadoutMode);
        }

        private void AddTakeExposure(ITarget target, IExposure exposure) {
            TSLogger.Debug($"adding take exposure: {exposure.FilterName} {exposure.ExposureLength}s");

            PlanTakeExposure takeExposure = new PlanTakeExposure(
                        parentContainer,
                        synchronizationEnabled,
                        syncActionTimeout,
                        profileService,
                        cameraMediator,
                        imagingMediator,
                        imageSaveMediator,
                        imageHistoryVM,
                        imageSaveWatcher,
                        target,
                        exposure);
            SetItemDefaults(takeExposure, nameof(TakeExposure));

            takeExposure.ExposureCount = GetExposureCount();
            takeExposure.ExposureTime = exposure.ExposureLength;
            takeExposure.Gain = GetGain(exposure.Gain);
            takeExposure.Offset = GetOffset(exposure.Offset);
            takeExposure.Binning = exposure.BinningMode;
            takeExposure.ROI = target.ROI;

            Add(takeExposure);
        }

        private void AddDither() {
            TSLogger.Debug("adding dither");
            Dither dither = new Dither(guiderMediator, profileService);
            Add(dither);
        }

        private void AddWait(DateTime waitForTime, ITarget target) {
            Add(new PlanWaitInstruction(guiderMediator, telescopeMediator, waitForTime));
        }

        private void EnsureUnparked(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (telescopeMediator.GetInfo().AtPark) {
                TSLogger.Debug("telescope is parked before potential target slew: unparking");
                try {
                    telescopeMediator.UnparkTelescope(progress, token).Wait();
                } catch (Exception ex) {
                    TSLogger.Error($"failed to unpark telescope: {ex.Message}");
                    throw new SequenceEntityFailedException("Failed to unpark telescope");
                }
            }
        }

        private void SetSyncTimeouts() {
            using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                ProfilePreference profilePreference = context.GetProfilePreference(profileService.ActiveProfile.Id.ToString());
                syncActionTimeout = profilePreference != null ? profilePreference.SyncActionTimeout : SyncManager.DEFAULT_SYNC_ACTION_TIMEOUT;
                syncSolveRotateTimeout = profilePreference != null ? profilePreference.SyncSolveRotateTimeout : SyncManager.DEFAULT_SYNC_SOLVEROTATE_TIMEOUT;
            }
        }

        private void SetItemDefaults(ISequenceItem item, string name) {
            if (name != null) {
                item.Name = name;
            }

            item.Category = INSTRUCTION_CATEGORY;
            item.Description = "";
            item.ErrorBehavior = this.ErrorBehavior;
            item.Attempts = this.Attempts;
        }

        private int GetExposureCount() {
            parentContainer.TotalExposureCount++;
            return parentContainer.TotalExposureCount;
        }

        private int GetGain(int? gain) {
            return gain.HasValue ? (int)gain : cameraMediator.GetInfo().DefaultGain;
        }

        private int GetOffset(int? offset) {
            return offset.HasValue ? (int)offset : cameraMediator.GetInfo().DefaultOffset;
        }

        private short GetReadoutMode(int? readoutMode) {
            return readoutMode.HasValue ? (short)readoutMode : cameraMediator.GetInfo().ReadoutMode;
        }

        public override object Clone() {
            throw new NotImplementedException();
        }

        // IDeepSkyObjectContainer behavior, defer to parent
        public InputTarget Target { get => parentContainer.Target; set { } }

        public NighttimeData NighttimeData => parentContainer.NighttimeData;
    }
}