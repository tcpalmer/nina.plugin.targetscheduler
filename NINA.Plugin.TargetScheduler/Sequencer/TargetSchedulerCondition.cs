using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Flats;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    [ExportMetadata("Name", "Target Scheduler Condition")]
    [ExportMetadata("Description", "Loop condition for Target Scheduler")]
    [ExportMetadata("Icon", "Scheduler.SchedulerSVG")]
    [ExportMetadata("Category", "Target Scheduler")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TargetSchedulerCondition : TargetSchedulerConditionBase {
        private const string TARGETS_REMAIN = "While Targets Remain Tonight";
        private const string ACTIVE_PROJECTS_REMAIN = "While Active Projects Remain";
        private const string FLATS_NEEDED = "While Flats Needed";

        [ImportingConstructor]
        public TargetSchedulerCondition(IProfileService profileService) {
            this.profileService = profileService;

            Modes = new List<string>() { TARGETS_REMAIN, ACTIVE_PROJECTS_REMAIN, FLATS_NEEDED };
            SelectedMode = TARGETS_REMAIN;
        }

        public List<string> Modes { get; }
        private string selectedMode;

        [JsonProperty]
        public string SelectedMode {
            get => selectedMode;
            set {
                selectedMode = value;
                RaisePropertyChanged();
            }
        }

        private TargetSchedulerCondition(TargetSchedulerCondition cloneMe, IProfileService profileService) : this(profileService) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new TargetSchedulerCondition(this, profileService) {
                SelectedMode = this.SelectedMode
            };
        }

        private bool checkIsActive = true;
        public bool CheckIsActive { get => checkIsActive; set => checkIsActive = value; }

        // If this condition instance ever returned false (completed) then continue to do so until block finished or reset
        private bool conditionWasCompleted = false;

        public bool ConditionWasCompleted { get => conditionWasCompleted; set => conditionWasCompleted = value; }

        /// <summary>
        /// If the main TS container has ended normally (no more targets) then it will call this to force
        /// a full recheck even if the container this condition belongs too hasn't finished an iteration.
        /// </summary>
        public void EnableCheckIsActive() {
            TSLogger.Info("TargetSchedulerCondition informed of need to execute check on next attempt");
            CheckIsActive = true;
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
            TSLogger.Info($"TargetSchedulerCondition Check: CheckIsActive={CheckIsActive}, ConditionWasCompleted={ConditionWasCompleted}");

            if (ConditionWasCompleted) {
                TSLogger.Info($"TargetSchedulerCondition already completed");
                return false;
            }

            if (!CheckIsActive) {
                return true;
            }

            TSLogger.Info($"TargetSchedulerCondition starting check: {SelectedMode}");
            CheckIsActive = false;
            bool canContinue = false;

            switch (SelectedMode) {
                case TARGETS_REMAIN: canContinue = HasRemainingTargets(); break;
                case ACTIVE_PROJECTS_REMAIN: canContinue = HasActiveProjects(); break;
                case FLATS_NEEDED: canContinue = NeedsFlats(); break;
            }

            if (!canContinue) {
                ConditionWasCompleted = true;
            }

            return canContinue;
        }

        public override void SequenceBlockFinished() {
            TSLogger.Info($"TargetSchedulerCondition: SequenceBlockFinished");
            CheckIsActive = true;
            ConditionWasCompleted = false;
        }

        public override void ResetProgress() {
            TSLogger.Info($"TargetSchedulerCondition: ResetProgress");
            Status = SequenceEntityStatus.CREATED;
            CheckIsActive = true;
            ConditionWasCompleted = false;
            base.ResetProgress();
        }

        public override void Initialize() {
            TSLogger.Info($"TargetSchedulerCondition: Initialize");
            ConditionWasCompleted = false;
        }

        public override void SequenceBlockInitialize() {
            TSLogger.Info($"TargetSchedulerCondition: SequenceBlockInitialize");
        }

        public override void SequenceBlockStarted() {
            TSLogger.Info($"TargetSchedulerCondition: SequenceBlockStarted");
        }

        public override void SequenceBlockTeardown() {
            TSLogger.Info($"TargetSchedulerCondition: SequenceBlockTeardown");
        }

        public override void Teardown() {
            TSLogger.Info($"TargetSchedulerCondition: Teardown");
        }

        private bool HasRemainingTargets() {
            try {
                Planner planner = new Planner(DateTime.Now, GetApplicableProfile(), GetProfilePreferences(), true, false);
                bool result = planner.GetPlan(null) != null;
                TSLogger.Info($"TargetSchedulerCondition check for remaining targets, continue={result}");
                return result;
            } catch (Exception ex) {
                TSLogger.Error($"TargetSchedulerCondition exception determining remaining targets: {ex.StackTrace}");
                throw new SequenceEntityFailedException($"TargetSchedulerCondition: exception determining remaining targets: {ex.Message}", ex);
            }
        }

        private bool HasActiveProjects() {
            try {
                Planner planner = new Planner(DateTime.Now, GetApplicableProfile(), GetProfilePreferences(), true, false);
                bool result = planner.HasActiveProjects(null);
                TSLogger.Info($"TargetSchedulerCondition check for active projects, continue={result}");
                return result;
            } catch (Exception ex) {
                TSLogger.Error($"TargetSchedulerCondition exception determining active projects: {ex.StackTrace}");
                throw new SequenceEntityFailedException($"TargetSchedulerCondition: exception determining active projects: {ex.Message}", ex);
            }
        }

        private bool NeedsFlats() {
            try {
                FlatsExpert flatsExpert = new FlatsExpert() { VerboseLogging = false };
                bool result = flatsExpert.GetNeededFlats(profileService.ActiveProfile, DateTime.Now).Count > 0;
                TSLogger.Info($"TargetSchedulerCondition check for needed flats, continue={result}");
                return result;
            } catch (Exception ex) {
                TSLogger.Error($"TargetSchedulerCondition exception determining needed flats: {ex.StackTrace}");
                throw new SequenceEntityFailedException($"TargetSchedulerCondition: exception determining needed flats: {ex.Message}", ex);
            }
        }

        public override string ToString() {
            return $"Condition: {nameof(TargetSchedulerCondition)} mode={SelectedMode}";
        }
    }
}