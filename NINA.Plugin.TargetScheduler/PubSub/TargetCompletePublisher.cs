using NINA.Plugin.Interfaces;
using NINA.Plugin.TargetScheduler.Planning;

namespace NINA.Plugin.TargetScheduler.PubSub {

    public class TargetCompletePublisher : TSPublisher {
        public const string TOPIC = "TargetScheduler-TargetComplete";

        public TargetCompletePublisher(IMessageBroker messageBroker) : base(messageBroker) {
        }

        public override string Topic => TOPIC;
        public override int Version => 1;

        public void Publish(SchedulerPlan plan) {
            Publish(GetMessage(plan));
        }

        private IMessage GetMessage(SchedulerPlan plan) {
            TSMessage message = new TSMessage(Topic, plan.PlanTarget.Name, MessageSender, MessageSenderId, Version);
            message.CustomHeaders.Add("ProjectName", plan.PlanTarget.Project.Name);
            message.CustomHeaders.Add("Coordinates", plan.PlanTarget.Coordinates);
            message.CustomHeaders.Add("Rotation", plan.PlanTarget.Rotation);
            return message;
        }
    }
}