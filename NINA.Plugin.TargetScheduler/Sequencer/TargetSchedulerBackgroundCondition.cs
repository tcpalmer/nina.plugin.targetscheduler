using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    [ExportMetadata("Name", "Target Scheduler Background Condition")]
    [ExportMetadata("Description", "Loop condition for Target Scheduler")]
    [ExportMetadata("Icon", "Scheduler.SchedulerSVG")]
    [ExportMetadata("Category", "Target Scheduler")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TargetSchedulerBackgroundCondition : TargetSchedulerConditionBase {
        private DateTime LastPlanEndTime;

        [ImportingConstructor]
        public TargetSchedulerBackgroundCondition(IProfileService profileService) {
            this.profileService = profileService;
            LastPlanEndTime = DateTime.Now;
            ConditionWatchdog = new ConditionWatchdog(InterruptWhenNoTargetsRemain, TimeSpan.FromSeconds(60));
        }

        private TargetSchedulerBackgroundCondition(TargetSchedulerBackgroundCondition cloneMe, IProfileService profileService) : this(profileService) {
            CopyMetaData(cloneMe);
        }

        private async Task InterruptWhenNoTargetsRemain() {
            if (!HasRemainingTargets()) {
                if (Parent != null) {
                    if (ItemUtility.IsInRootContainer(Parent) && this.Parent.Status == SequenceEntityStatus.RUNNING && this.Status != SequenceEntityStatus.DISABLED) {
                        TSLogger.Info("TargetSchedulerBackgroundCondition no more targets tonight - interrupting current instruction set");
                        Status = SequenceEntityStatus.FINISHED;
                        await Parent.Interrupt();
                    }
                }
            }
        }

        private bool HasRemainingTargets() {
            if (DateTime.Now < LastPlanEndTime) {
                TSLogger.Info($"TargetSchedulerBackgroundCondition check for remaining targets: skipping until: {Utils.FormatDateTimeFull(LastPlanEndTime)}");
                return true;
            }

            try {
                TSLogger.Info("TargetSchedulerBackgroundCondition: running planner");
                Planner planner = new Planner(DateTime.Now, GetApplicableProfile(), GetProfilePreferences(), true, false);
                SchedulerPlan plan = planner.GetPlan(null);

                if (plan == null) {
                    TSLogger.Info("TargetSchedulerBackgroundCondition check for remaining targets: no more targets");
                    return false;
                }

                if (plan.WaitForNextTargetTime != null) {
                    LastPlanEndTime = (DateTime)plan.WaitForNextTargetTime;
                    TSLogger.Info($"TargetSchedulerBackgroundCondition check for remaining targets: wait time end: {Utils.FormatDateTimeFull(LastPlanEndTime)}");
                    return true;
                }

                LastPlanEndTime = plan.PlanTarget.EndTime;
                TSLogger.Info($"TargetSchedulerBackgroundCondition check for remaining targets: target time end: {plan.PlanTarget.Name} / {Utils.FormatDateTimeFull(LastPlanEndTime)}");
                return true;
            } catch (Exception ex) {
                TSLogger.Error($"TargetSchedulerBackgroundCondition exception determining remaining targets: {ex.StackTrace}");
                throw new SequenceEntityFailedException($"TargetSchedulerBackgroundCondition: exception determining remaining targets: {ex.Message}", ex);
            }
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
            return true;
        }

        public override object Clone() {
            return new TargetSchedulerBackgroundCondition(this, profileService);
        }

        public override void AfterParentChanged() {
            if (Parent == null) {
                SequenceBlockTeardown();
            } else {
                if (Parent.Status == SequenceEntityStatus.RUNNING) {
                    SequenceBlockInitialize();
                }
            }
        }

        public override void SequenceBlockInitialize() {
            ConditionWatchdog?.Start();
        }

        public override void SequenceBlockTeardown() {
            try { ConditionWatchdog?.Cancel(); } catch { }
        }

        public override string ToString() {
            return $"Condition: {nameof(TargetSchedulerBackgroundCondition)}";
        }
    }
}