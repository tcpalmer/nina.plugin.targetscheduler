using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Flats;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
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

    [ExportMetadata("Name", "Target Scheduler Flats")]
    [ExportMetadata("Description", "Flats automation for Target Scheduler")]
    [ExportMetadata("Icon", "Scheduler.SchedulerSVG")]
    [ExportMetadata("Category", "Target Scheduler")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TargetSchedulerFlats : TargetSchedulerFlatsBase {

        [ImportingConstructor]
        public TargetSchedulerFlats(IProfileService profileService,
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

        public TargetSchedulerFlats(TargetSchedulerFlats cloneMe) : this(
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
            FlatCount = cloneMe.FlatCount;
        }

        public override object Clone() {
            return new TargetSchedulerFlats(this);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            try {
                InitSync();

                DisplayText = "Determining needed flats";
                List<LightSession> neededFlats = flatsExpert.GetNeededFlats(profileService.ActiveProfile, DateTime.Now);
                if (neededFlats.Count == 0) {
                    TSLogger.Info("TS Flats: no flats needed");
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

                imageSaveMediator.BeforeImageSaved += BeforeImageSaved;
                imageSaveMediator.BeforeFinalizeImageSaved += BeforeFinalizeImageSaved;
                imageSaveMediator.ImageSaved += ImageSaved;

                // Get the unique set of target ids over all neededFlats
                List<int> targetIds = new List<int>();
                foreach (LightSession neededFlat in neededFlats) {
                    if (!targetIds.Contains(neededFlat.TargetId)) {
                        targetIds.Add(neededFlat.TargetId);
                    }
                }

                List<FlatSpec> allTakenFlats = new List<FlatSpec>();

                foreach (int targetId in targetIds) {
                    List<LightSession> targetNeededFlats = neededFlats.Where(ls => ls.TargetId == targetId).ToList();
                    List<FlatSpec> targetTakenFlats = new List<FlatSpec>();

                    foreach (LightSession neededFlat in targetNeededFlats) {
                        bool success = true;

                        if (flatsExpert.IsRequiredFlat(AlwaysRepeatFlatSet, neededFlat, targetTakenFlats, allTakenFlats)) {
                            success = await TakeFlatSet(neededFlat, true, progress, token);
                            if (success) {
                                targetTakenFlats.Add(neededFlat.FlatSpec);
                                allTakenFlats.Add(neededFlat.FlatSpec);
                            }
                        } else {
                            TSLogger.Info($"TS Flats: flat already taken, skipping: {neededFlat}");
                        }

                        if (success) {
                            CompletedFlatSets++;
                            SaveFlatHistory(neededFlat);
                        }
                    }
                }

                await ToggleLight(false, progress, token);
            } catch (Exception ex) {
                DisplayText = "";

                if (Utils.IsCancelException(ex)) {
                    TSLogger.Warning("TS Flats: sequence was canceled/interrupted");
                    Status = SequenceEntityStatus.CREATED;
                    token.ThrowIfCancellationRequested();
                } else {
                    TSLogger.Error($"Exception taking flats: {ex.Message}:\n{ex.StackTrace}");
                }

                if (ex is SequenceEntityFailedException) {
                    throw;
                }

                throw new SequenceEntityFailedException($"exception taking flats: {ex.Message}", ex);
            } finally {
                DisplayText = "";
                TotalFlatSets = 0;
                CompletedFlatSets = 0;
                Iterations = 0;
                CompletedIterations = 0;

                imageSaveMediator.BeforeImageSaved -= BeforeImageSaved;
                imageSaveMediator.BeforeFinalizeImageSaved -= BeforeFinalizeImageSaved;
                imageSaveMediator.ImageSaved -= ImageSaved;
            }

            return;
        }
    }
}