using NINA.Plugin.Interfaces;
using System;

namespace NINA.Plugin.TargetScheduler.PubSub {

    public class ContainerStoppedPublisher : TSPublisher {
        public const string TOPIC = "TargetScheduler-ContainerStopped";

        public ContainerStoppedPublisher(IMessageBroker messageBroker) : base(messageBroker) {
        }

        public override string Topic => TOPIC;
        public override int Version => 1;

        public void Publish() {
            Publish(GetMessage());
        }

        private IMessage GetMessage() {
            TSMessage message = new TSMessage(Topic, "Container Stopped", MessageSender, MessageSenderId, Version);
            message.CustomHeaders.Add("StoppedAt", DateTime.Now);
            return message;
        }
    }
}