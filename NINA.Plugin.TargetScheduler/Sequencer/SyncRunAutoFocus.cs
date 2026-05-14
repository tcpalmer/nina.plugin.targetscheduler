using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public class SyncRunAutoFocus : RunAutofocus {
        private bool enabled = false;
        private FilterInfo filter;
        private IFilterWheelMediator filterWheelMediator;

        public SyncRunAutoFocus(string filterName, IProfileService profileService, IImageHistoryVM history, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator, IFocuserMediator focuserMediator, IAutoFocusVMFactory autoFocusVMFactory)
            : base(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, autoFocusVMFactory) {
            this.filterWheelMediator = filterWheelMediator;

            if (!focuserMediator.GetInfo().Connected) { Warn("focuser"); return; }
            if (!filterWheelMediator.GetInfo().Connected) { Warn("filter wheel"); return; }
            if (!cameraMediator.GetInfo().Connected) { Warn("camera"); return; }

            filter = profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters?.FirstOrDefault(x => x.Name == filterName);
            if (filter == null) {
                Warn($"filter '{filterName}'");
                return;
            }

            Enabled = true;
        }

        public bool Enabled { get => enabled; private set => enabled = value; }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!Enabled) { return; }

            await filterWheelMediator.ChangeFilter(filter, token, progress);
            await base.Execute(progress, token);
        }

        private void Warn(string type) {
            string s = $"SYNC client: problem at execution time: {type} is not connected or available";
            TSLogger.Warning(s);
            Notification.ShowWarning(s);
        }
    }
}