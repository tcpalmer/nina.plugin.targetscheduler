using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    [ExportMetadata("Name", "Target Scheduler Sync Auto Focus")]
    [ExportMetadata("Description", "Performs a synchronized autofocus operation on a sync client")]
    [ExportMetadata("Icon", "Scheduler.SchedulerSVG")]
    [ExportMetadata("Category", "Target Scheduler")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TargetSchedulerSyncAutoFocus : SequenceTrigger, IValidatable {
        private readonly IProfileService profileService;
        private readonly IImageHistoryVM history;
        private readonly ICameraMediator cameraMediator;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IAutoFocusVMFactory autoFocusVMFactory;
        private readonly ISafetyMonitorMediator safetyMonitorMediator;

        [ImportingConstructor]
        public TargetSchedulerSyncAutoFocus(
            IProfileService profileService,
            IImageHistoryVM imageHistoryVM,
            ICameraMediator cameraMediator,
            IFilterWheelMediator filterWheelMediator,
            IFocuserMediator focuserMediator,
            IAutoFocusVMFactory autoFocusVMFactory,
            ISafetyMonitorMediator safetyMonitorMediator) : base() {
            this.profileService = profileService;
            this.history = imageHistoryVM;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.focuserMediator = focuserMediator;
            this.autoFocusVMFactory = autoFocusVMFactory;
            this.safetyMonitorMediator = safetyMonitorMediator;
            TriggerRunner.Add(new RunAutofocus(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, autoFocusVMFactory));
        }

        public TargetSchedulerSyncAutoFocus(TargetSchedulerSyncAutoFocus cloneMe) : this(
            cloneMe.profileService,
            cloneMe.history,
            cloneMe.cameraMediator,
            cloneMe.filterWheelMediator,
            cloneMe.focuserMediator,
            cloneMe.autoFocusVMFactory,
            cloneMe.safetyMonitorMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new TargetSchedulerSyncAutoFocus(this) {
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            TSLogger.Info("SYNC client: starting autofocus");
            RunAutoFocus = false;
            await TriggerRunner.Run(progress, token);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (safetyMonitorMediator.GetInfo() is { Connected: true, IsSafe: false }) { return false; }
            return RunAutoFocus;
        }

        public void TriggerAutoFocus() {
            RunAutoFocus = true;
        }

        private bool runAutoFocus = false;
        public bool RunAutoFocus { get => runAutoFocus; set => runAutoFocus = value; }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = ImmutableList.CreateRange(value);
                RaisePropertyChanged();
            }
        }

        public bool Validate() {
            var i = new List<string>();
            var cameraInfo = cameraMediator.GetInfo();
            var focuserInfo = focuserMediator.GetInfo();
            var fwInfo = filterWheelMediator.GetInfo();

            if (!cameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            }
            if (!focuserInfo.Connected) {
                i.Add(Loc.Instance["LblFocuserNotConnected"]);
            }
            if (!fwInfo.Connected) {
                i.Add(Loc.Instance["LblFilterWheelNotConnected"]);
            }

            if (!SyncManager.Instance.RunningClient || SyncManager.Instance.IsServer) {
                i.Add("Sync client is not running");
            }

            // TODO: validate that the client is enabled for sync AF

            Issues = i;
            return i.Count == 0;
        }
    }
}