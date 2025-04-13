using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Database.Schema {

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OverrideExposureOrderAction {
        Exposure = 0, Dither = 1,
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class OverrideExposureOrderItem : INotifyPropertyChanged {
        [JsonProperty][Key] public int Id { get; set; }

        [JsonProperty][Required] public int TargetId { get; set; }
        [Required] public int order { get; set; }
        [Required] public int action { get; set; }
        public int referenceIdx { get; set; }

        public OverrideExposureOrderItem() {
        }

        public OverrideExposureOrderItem(int targetId, int order, int action, int referenceIdx) {
            this.TargetId = targetId;
            this.order = order;
            this.action = action;
            this.referenceIdx = referenceIdx;
        }

        public OverrideExposureOrderItem(int targetId, int order, int action)
            : this(targetId, order, action, -1) { }

        public OverrideExposureOrderItem(int targetId, int order, OverrideExposureOrderAction action, int referenceIdx)
            : this(targetId, order, (int)action, referenceIdx) { }

        public OverrideExposureOrderItem(int targetId, int order, OverrideExposureOrderAction action)
            : this(targetId, order, (int)action, -1) { }

        [NotMapped]
        [JsonProperty]
        public int Order {
            get => order;
            set {
                order = value;
                RaisePropertyChanged(nameof(Order));
            }
        }

        [NotMapped]
        [JsonProperty]
        public OverrideExposureOrderAction Action {
            get { return (OverrideExposureOrderAction)action; }
            set {
                action = (int)value;
                RaisePropertyChanged(nameof(Action));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int ReferenceIdx {
            get => referenceIdx;
            set {
                referenceIdx = value;
                RaisePropertyChanged(nameof(ReferenceIdx));
            }
        }

        public OverrideExposureOrderItem GetPasteCopy(int targetId) {
            OverrideExposureOrderItem copy = new OverrideExposureOrderItem();
            copy.TargetId = targetId;
            copy.order = order;
            copy.action = action;
            copy.referenceIdx = referenceIdx;
            return copy;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"TargetId: {TargetId}");
            sb.AppendLine($"Order: {Order}");
            sb.AppendLine($"Action: {Action}");
            sb.AppendLine($"RefIdx: {ReferenceIdx}");

            return sb.ToString();
        }

        public override bool Equals(object obj) {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) {
                return false;
            }

            OverrideExposureOrderItem other = obj as OverrideExposureOrderItem;
            return TargetId == other.TargetId &&
                Order == other.Order &&
                Action == other.Action &&
                ReferenceIdx == other.ReferenceIdx;
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }
}