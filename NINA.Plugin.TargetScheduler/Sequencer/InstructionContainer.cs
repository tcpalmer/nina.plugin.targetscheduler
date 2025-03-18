using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    [ExportMetadata("Name", "SchedulerInstructionContainer")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public class InstructionContainer : SequenceContainer, ISequenceContainer, IValidatable {
        private IProfileService profileService;
        private Object lockObj = new Object();

        private int syncActionTimeout;
        private int syncEventContainerTimeout;

        [JsonProperty]
        public EventContainerType EventContainerType { get; set; }

        /* Note that the container parent isn't set in the importing constructor which is the one
         * called when a sequence loads.  The important parent (i.e. either TS Container or
         * TS Sync Container) is reset just prior to execution via ResetParent().  This ensures
         * that member instructions can locate the DSO Target if needed.
         */

        [ImportingConstructor]
        public InstructionContainer() : base(new InstructionContainerStrategy()) { }

        public InstructionContainer(EventContainerType containerType, ISequenceContainer parent) : base(new InstructionContainerStrategy()) {
            EventContainerType = containerType;
            Name = containerType.ToString();
            AttachNewParent(parent);
        }

        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context) {
            EventContainerType = EventContainerHelper.Convert(Name);
        }

        public void Initialize(IProfileService profileService) {
            this.profileService = profileService;
            Initialize();
        }

        public override void Initialize() {
            foreach (ISequenceItem item in Items) {
                item.Initialize();
            }

            base.Initialize();
            SetSyncTimeouts();
        }

        /// <summary>
        /// Force the parent of an event container to be the TS (or TS Sync) Container.  This is needed since a
        /// sequence loaded from a file doesn't seem to handle this as one would expect.
        /// </summary>
        /// <param name="parent"></param>
        public void ResetParent(ISequenceContainer parent) {
            AttachNewParent(parent);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!SyncManager.Instance.IsRunning) {
                if (Items.Count > 0) {
                    TSLogger.Info($"Event container {Name}: starting execution");
                    await base.Execute(progress, token);
                    TSLogger.Info($"Event container {Name}: finished execution");
                }

                return;
            }

            if (IsSyncServer()) {
                // Inform clients they can execute
                string eventContainerId = Guid.NewGuid().ToString();
                TSLogger.Info($"SYNC server waiting for clients to start {EventContainerType}");
                progress?.Report(new ApplicationStatus() { Status = $"Target Scheduler: waiting for sync clients to run {EventContainerType}" });
                await SyncServer.Instance.SyncEventContainer(eventContainerId, EventContainerType, syncActionTimeout, token);
                progress?.Report(new ApplicationStatus() { Status = "" });

                // Server can proceed with event container
                if (Items.Count > 0) {
                    TSLogger.Info($"Event container {Name}: starting execution on server");
                    await base.Execute(progress, token);
                    TSLogger.Info($"Event container {Name}: finished execution on server");
                }

                // Wait for clients to complete the event container
                TSLogger.Info($"SYNC server waiting for clients to complete {EventContainerType}");
                progress?.Report(new ApplicationStatus() { Status = $"Target Scheduler: waiting for sync clients to complete {EventContainerType}" });
                await SyncServer.Instance.WaitForClientEventContainerCompletion(EventContainerType, eventContainerId, syncEventContainerTimeout, token);
                progress?.Report(new ApplicationStatus() { Status = "" });
            }

            if (IsSyncClient()) {
                if (Items.Count > 0) {
                    TSLogger.Info($"Event container {Name}: starting execution on client");
                    await base.Execute(progress, token);
                    TSLogger.Info($"Event container {Name}: finished execution on client");
                }
            }

            return;
        }

        public override object Clone() {
            InstructionContainer ic = new InstructionContainer(EventContainerType, Parent);
            ic.Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem));
            foreach (var item in ic.Items) {
                item.AttachNewParent(ic);
            }

            AttachNewParent(Parent);
            return ic;
        }

        public new void MoveUp(ISequenceItem item) {
            lock (lockObj) {
                var index = Items.IndexOf(item);
                if (index == 0) {
                    return;
                } else {
                    base.MoveUp(item);
                }
            }
        }

        private void SetSyncTimeouts() {
            using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                ProfilePreference profilePreference = context.GetProfilePreference(profileService.ActiveProfile.Id.ToString());
                syncActionTimeout = profilePreference != null ? profilePreference.SyncActionTimeout : SyncManager.DEFAULT_SYNC_ACTION_TIMEOUT;
                syncEventContainerTimeout = profilePreference != null ? profilePreference.SyncEventContainerTimeout : SyncManager.DEFAULT_SYNC_ACTION_TIMEOUT;
            }
        }

        private bool IsSyncServer() {
            return SyncManager.Instance.RunningServer;
        }

        private bool IsSyncClient() {
            return SyncManager.Instance.RunningClient;
        }
    }

    /// <summary>
    /// This is only used so that we can recognize calls to TS Condition that originate from operation of TS.  See
    /// TargetSchedulerCondition which skips its Check if called from some TS action.
    /// </summary>
    internal class InstructionContainerStrategy : IExecutionStrategy {
        private SequentialStrategy sequentialStrategy;

        public InstructionContainerStrategy() {
            sequentialStrategy = new SequentialStrategy();
        }

        public object Clone() {
            return sequentialStrategy.Clone();
        }

        public Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            return sequentialStrategy.Execute(context, progress, token);
        }
    }
}