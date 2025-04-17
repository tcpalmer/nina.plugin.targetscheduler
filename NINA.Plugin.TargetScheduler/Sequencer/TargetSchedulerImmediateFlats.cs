using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Flats;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    [ExportMetadata("Name", "Target Scheduler Immediate Flats")]
    [ExportMetadata("Description", "Flats automation for Target Scheduler")]
    [ExportMetadata("Icon", "Scheduler.SchedulerSVG")]
    [ExportMetadata("Category", "Target Scheduler")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TargetSchedulerImmediateFlats : TargetSchedulerFlatsBase {

        [ImportingConstructor]
        public TargetSchedulerImmediateFlats(IProfileService profileService,
                                    ICameraMediator cameraMediator,
                                    IImagingMediator imagingMediator,
                                    IImageSaveMediator imageSaveMediator,
                                    IImageHistoryVM imageHistoryVM,
                                    IFilterWheelMediator filterWheelMediator,
                                    IRotatorMediator rotatorMediator,
                                    IFlatDeviceMediator flatDeviceMediator) :
            base(profileService,
                 cameraMediator,
                 imagingMediator,
                 imageSaveMediator,
                 imageHistoryVM,
                 filterWheelMediator,
                 rotatorMediator,
                 flatDeviceMediator) { }

        public TargetSchedulerImmediateFlats(TargetSchedulerImmediateFlats cloneMe) : this(
            cloneMe.profileService,
            cloneMe.cameraMediator,
            cloneMe.imagingMediator,
            cloneMe.imageSaveMediator,
            cloneMe.imageHistoryVM,
            cloneMe.filterWheelMediator,
            cloneMe.rotatorMediator,
            cloneMe.flatDeviceMediator) {
            CopyMetaData(cloneMe);
            AlwaysRepeatFlatSet = cloneMe.AlwaysRepeatFlatSet;
        }

        public override object Clone() {
            return new TargetSchedulerImmediateFlats(this);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            try {
                DisplayText = "Determining needed flats";
                List<LightSession> neededFlats = GetNeededFlats();
                if (neededFlats == null) {
                    DisplayText = "";
                    return;
                }

                TotalFlatSets = neededFlats.Count;
                CompletedFlatSets = 0;

                LogTrainedFlatDetails();

                // Prep the flat device
                DisplayText = "Preparing flat device";
                await CloseCover(progress, token);
                await ToggleLight(true, progress, token);

                List<FlatSpec> takenFlats = new List<FlatSpec>();
                foreach (LightSession neededFlat in neededFlats) {
                    bool success = true;
                    if (!takenFlats.Contains(neededFlat.FlatSpec)) {
                        success = await TakeFlatSet(neededFlat, false, progress, token);
                        if (success) {
                            takenFlats.Add(neededFlat.FlatSpec);
                        }
                    } else {
                        TSLogger.Info($"TS Immediate Flats: flat already taken, skipping: {neededFlat}");
                    }

                    if (success) {
                        CompletedFlatSets++;
                        SaveFlatHistory(neededFlat);
                    }
                }

                DisplayText = "";
                Iterations = 0;
                CompletedIterations = 0;

                await ToggleLight(false, progress, token);
                await OpenCover(progress, token);
            } catch (Exception ex) {
                DisplayText = "";

                if (Utils.IsCancelException(ex)) {
                    TSLogger.Warning("TS Immediate Flats: sequence was canceled/interrupted");
                    Status = SequenceEntityStatus.CREATED;
                    token.ThrowIfCancellationRequested();
                } else {
                    TSLogger.Error($"Exception taking immediate flats: {ex.Message}:\n{ex.StackTrace}");
                }

                if (ex is SequenceEntityFailedException) {
                    throw;
                }

                throw new SequenceEntityFailedException($"exception taking immediate flats: {ex.Message}", ex);
            } finally {
                DisplayText = "";
                TotalFlatSets = 0;
                CompletedFlatSets = 0;
                Iterations = 0;
                CompletedIterations = 0;
            }

            return;
        }

        private List<LightSession> GetNeededFlats() {
            PlanExecutionHistory planExecutionHistory = GetPlanExecutionHistory();
            if (planExecutionHistory == null) {
                TSLogger.Warning("TS Immediate Flats: failed to find plan execution history on parent container, aborting flats");
                return null;
            }

            (ITarget target, List<Planning.Entities.PlanTakeExposure> exposureInstructions) = planExecutionHistory.GetImmediateTargetExposures();

            if (exposureInstructions.Count == 0) {
                TSLogger.Error("TS Immediate Flats: failed to find previous target exposure instructions, aborting flats");
                return null;
            }

            List<FlatSpec> flatSpecs = new List<FlatSpec>();

            // For each take exposure instruction, add the associated flat spec to the list if not already present
            exposureInstructions.ForEach(takeExposure => {
                IExposure exp = takeExposure.exposure;
                FlatSpec flatSpec = new FlatSpec(target.DatabaseId, exp.FilterName,
                                                 GetGain(exp.Gain),
                                                 GetOffset(exp.Offset),
                                                 exp.BinningMode,
                                                 GetReadoutMode(exp.ReadoutMode),
                                                 GetCurrentRotation(),
                                                 target.ROI);

                if (!flatSpecs.Contains(flatSpec)) {
                    flatSpecs.Add(flatSpec);
                }
            });

            FlatsExpert flatsExpert = new FlatsExpert();
            List<LightSession> neededFlats = new List<LightSession>();

            DateTime lightSessionDate = flatsExpert.GetLightSessionDate(DateTime.Now);
            Target databaseTarget = flatsExpert.GetTarget(target.Project.DatabaseId, target.DatabaseId);
            int sessionId = flatsExpert.GetCurrentSessionId(databaseTarget?.Project, DateTime.Now);

            foreach (FlatSpec flatSpec in flatSpecs) {
                neededFlats.Add(new LightSession(target.DatabaseId, lightSessionDate, sessionId, flatSpec));
            }

            flatsExpert.LogLightSessions($"raw immediate needed flats (repeat = {AlwaysRepeatFlatSet})", neededFlats);

            // If always repeat is false, then remove where we've already taken a flat during this same light session
            if (!AlwaysRepeatFlatSet && neededFlats.Count > 0) {
                List<FlatHistory> takenFlats;
                using (var context = database.GetContext()) {
                    takenFlats = context.GetFlatsHistory(lightSessionDate, profileService.ActiveProfile.Id.ToString())
                       .Where(fh => fh.TargetId == target.DatabaseId)
                       .ToList();
                }

                if (takenFlats.Count > 0) {
                    neededFlats = flatsExpert.CullByFlatsHistory(databaseTarget, neededFlats, takenFlats);
                }
            }

            if (neededFlats.Count == 0) {
                TSLogger.Info("TS Immediate Flats: no flats needed");
                return null;
            }

            TSLogger.Info($"TS Immediate Flats: need {neededFlats.Count} flat sets for target: {target.Name}");
            flatsExpert.LogLightSessions("needed immediate flats", neededFlats);

            return neededFlats;
        }

        private PlanExecutionHistory GetPlanExecutionHistory() {
            // Find parent TargetSchedulerContainer which should have the plan history we need
            ISequenceContainer parent = Parent;

            while (parent != null) {
                if (parent is TargetSchedulerContainer) {
                    return (parent as TargetSchedulerContainer).PlanExecutionHistory;
                }

                parent = parent.Parent;
            }

            // If that failed, try to find TargetSchedulerSyncContainer; first find the root
            ISequenceContainer sequenceContainer = Parent;
            parent = Parent;

            while (parent != null) {
                if (parent is SequenceRootContainer) {
                    sequenceContainer = parent as SequenceRootContainer;
                    break;
                }

                parent = parent.Parent;
            }

            // Then recurse over all
            return CheckContainer(sequenceContainer);
        }

        private PlanExecutionHistory CheckContainer(ISequenceContainer container) {
            if (container == null) { return null; }
            PlanExecutionHistory planHistory = null;
            foreach (var item in container.Items) {
                if (item is TargetSchedulerSyncContainer) {
                    return (item as TargetSchedulerSyncContainer).PlanExecutionHistory;
                } else if (item is ISequenceContainer) {
                    planHistory = CheckContainer((ISequenceContainer)item);
                    if (planHistory != null) { return planHistory; }
                }
            }

            return null;
        }

        private int GetGain(int? gain) {
            return (int)(gain == null ? cameraMediator.GetInfo().DefaultGain : gain);
        }

        private int GetOffset(int? offset) {
            return (int)((int)(offset == null ? cameraMediator.GetInfo().DefaultOffset : offset));
        }

        private int GetReadoutMode(int? readoutMode) {
            return (int)((int)(readoutMode == null ? cameraMediator.GetInfo().ReadoutMode : readoutMode));
        }
    }
}