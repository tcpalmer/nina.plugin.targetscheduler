using NINA.Plugin.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using System;

namespace NINA.Plugin.TargetScheduler.PubSub {

    public class WaitStartPublisher : TSPublisher {

        public WaitStartPublisher(IMessageBroker messageBroker) : base(messageBroker) {
        }

        public override string Topic => "TargetScheduler-WaitStart";
        public override int Version => 2;

        public void Publish(ITarget nextTarget, DateTime waitUntil) {
            Publish(GetMessage(nextTarget, waitUntil));
        }

        private IMessage GetMessage(ITarget nextTarget, DateTime waitUntil) {
            TSMessage message = new TSMessage(Topic, waitUntil, MessageSender, MessageSenderId, Version);
            message.Expiration = waitUntil;
            message.CustomHeaders.Add("SecondsUntilNextTarget", (int)(waitUntil - DateTime.Now).TotalSeconds);
            message.CustomHeaders.Add("ProjectName", nextTarget.Project.Name);
            message.CustomHeaders.Add("TargetName", nextTarget.Name);
            message.CustomHeaders.Add("Coordinates", nextTarget.Coordinates);
            message.CustomHeaders.Add("Rotation", nextTarget.Rotation);
            return message;
        }
    }
}