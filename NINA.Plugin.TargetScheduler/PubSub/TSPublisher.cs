using NINA.Core.Enum;
using NINA.Plugin.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace NINA.Plugin.TargetScheduler.PubSub {

    public abstract class TSPublisher {
        public const string SENDER = "Target Scheduler";

        private IMessageBroker MessageBroker;

        public TSPublisher(IMessageBroker messageBroker) {
            this.MessageBroker = messageBroker;
            MessageSenderId = Guid.Parse("B4541BA9-7B07-4D71-B8E1-6C73D4933EA0");
            MessageSender = SENDER;
        }

        public Guid MessageSenderId { get; }
        public string MessageSender { get; }

        public abstract string Topic { get; }
        public abstract int Version { get; }

        public async void Publish(IMessage message) {
            if (TSLogger.IsEnabled(LogLevelEnum.DEBUG)) {
                TSLogger.Debug($"publishing message to '{Topic}', message:\n{message}");
            } else {
                TSLogger.Info($"publishing message to '{Topic}'");
            }

            await MessageBroker.Publish(message);
        }
    }

    public class TSMessage : IMessage {
        public string Topic { get; }
        public object Content { get; }
        public string Sender { get; }
        public Guid SenderId { get; }
        public DateTimeOffset SentAt { get; }
        public Guid MessageId { get; }
        public DateTimeOffset? Expiration { get; set; }
        public int Version { get; }
        public Guid? CorrelationId { get; }
        public IDictionary<string, object> CustomHeaders { get; }

        public TSMessage(
            string topic,
            object content,
            string sender,
            Guid senderId,
            int version,
            DateTimeOffset? expiration = null,
            Guid? correlationId = null,
            IDictionary<string, object>? customHeaders = null) {
            Topic = topic;
            Content = content;
            Sender = sender;
            SenderId = senderId;
            SentAt = DateTimeOffset.UtcNow;
            MessageId = Guid.NewGuid();
            Version = version;
            Expiration = expiration;
            CorrelationId = correlationId;
            CustomHeaders = customHeaders ?? new Dictionary<string, object>();
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.Append($"    content: {this.Content}\n");
            sb.Append($"    id: {this.MessageId}\n");
            sb.Append($"    expires: {this.Expiration}\n");
            sb.Append($"    version: {this.Version}\n");
            sb.Append($"    correlation: {this.CorrelationId}\n");
            if (this.CustomHeaders != null && this.CustomHeaders.Count > 0) {
                sb.Append("    headers:\n");
                foreach (var header in this.CustomHeaders) {
                    sb.Append($"        {header.Key}: {header.Value}\n");
                }
            }

            return sb.ToString();
        }

        public static string LogReceived(IMessage message) {
            StringBuilder sb = new StringBuilder();
            sb.Append($"    topic: {message.Topic}\n");
            sb.Append($"    content: {message.Content}\n");
            sb.Append($"    sender: {message.Sender}\n");
            sb.Append($"    sentAt: {message.SentAt.ToLocalTime().ToString()}\n");
            sb.Append($"    id: {message.MessageId}\n");
            sb.Append($"    expires: {message.Expiration}\n");
            sb.Append($"    version: {message.Version}\n");
            sb.Append($"    correlation: {message.CorrelationId}\n");
            if (message.CustomHeaders != null && message.CustomHeaders.Count > 0) {
                sb.Append("    headers:\n");
                foreach (var header in message.CustomHeaders) {
                    sb.Append($"        {header.Key}: {header.Value}\n");
                }
            }

            return sb.ToString();
        }
    }
}