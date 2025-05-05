using NINA.Plugin.Interfaces;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;

namespace NINA.Plugin.TargetScheduler.PubSub {

    public class TargetStartPublisher : TSPublisher {
        public const string TOPIC = "TargetScheduler-TargetStart";

        public TargetStartPublisher(IMessageBroker messageBroker) : base(messageBroker) {
        }

        public override string Topic => TOPIC;
        public override int Version => 2;

        public void Publish(SchedulerPlan plan) {
            Publish(GetMessage(plan));
        }

        private IMessage GetMessage(SchedulerPlan plan) {
            TSMessage message = new TSMessage(Topic, plan.PlanTarget.Name, MessageSender, MessageSenderId, Version);
            message.Expiration = plan.PlanTarget.EndTime;
            message.CustomHeaders.Add("ProjectName", plan.PlanTarget.Project.Name);
            message.CustomHeaders.Add("TargetName", plan.PlanTarget.Name);
            message.CustomHeaders.Add("Coordinates", plan.PlanTarget.Coordinates);
            message.CustomHeaders.Add("Rotation", plan.PlanTarget.Rotation);

            IExposure exp = plan.PlanTarget.SelectedExposure;
            message.CustomHeaders.Add("ExposureFilterName", exp.FilterName);
            message.CustomHeaders.Add("ExposureLength", exp.ExposureLength);
            message.CustomHeaders.Add("ExposureGain", exp.Gain.HasValue ? exp.Gain.ToString() : "(camera)");
            message.CustomHeaders.Add("ExposureOffset", exp.Offset.HasValue ? exp.Offset.ToString() : "(camera)");
            message.CustomHeaders.Add("ExposureBinning", exp.BinningMode.ToString());

            return message;
        }
    }
}