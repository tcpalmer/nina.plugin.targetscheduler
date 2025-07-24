using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Database.Schema {

    [JsonObject(MemberSerialization.OptIn)]
    public class ExposurePlan : IExposureCounts, INotifyPropertyChanged {
        [JsonProperty][Key] public int Id { get; set; }
        [Required] public string profileId { get; set; }
        [NotMapped] private int exposureTemplateId;
        [Required] public double exposure { get; set; }

        public int desired { get; set; }
        public int acquired { get; set; }
        public int accepted { get; set; }

        public bool enabled { get; set; }

        [ForeignKey("ExposureTemplate")]
        [JsonProperty]
        public int ExposureTemplateId {
            get { return exposureTemplateId; }
            set {
                exposureTemplateId = value;
                RaisePropertyChanged(nameof(ExposureTemplateId));
            }
        }

        public virtual ExposureTemplate ExposureTemplate { get; set; }

        [ForeignKey("Target")]
        [JsonProperty]
        public int TargetId { get; set; }

        public virtual Target Target { get; set; }

        [NotMapped]
        [JsonProperty]
        public string ProfileId {
            get { return profileId; }
            set {
                profileId = value;
                RaisePropertyChanged(nameof(ProfileId));
            }
        }

        [NotMapped]
        [JsonProperty]
        public double Exposure {
            get { return exposure; }
            set {
                exposure = value;
                RaisePropertyChanged(nameof(Exposure));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int Desired {
            get { return desired; }
            set {
                desired = value;
                RaisePropertyChanged(nameof(Desired));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int Acquired {
            get { return acquired; }
            set {
                acquired = value;
                RaisePropertyChanged(nameof(Acquired));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int Accepted {
            get { return accepted; }
            set {
                accepted = value;
                RaisePropertyChanged(nameof(Accepted));
            }
        }

        [NotMapped]
        [JsonProperty]
        public bool IsEnabled {
            get { return enabled; }
            set {
                enabled = value;
                RaisePropertyChanged(nameof(IsEnabled));
            }
        }

        [NotMapped]
        public double PercentComplete { get; set; }

        public ExposurePlan() {
        }

        public ExposurePlan(string profileId) {
            ProfileId = profileId;
            Exposure = -1;
            Desired = 1;
            Acquired = 0;
            Accepted = 0;
            IsEnabled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ExposurePlan GetPasteCopy(string newProfileId, bool moveOp = false) {
            ExposurePlan exposurePlan = new ExposurePlan();

            exposurePlan.profileId = newProfileId;
            exposurePlan.ExposureTemplateId = this.ExposureTemplateId;
            exposurePlan.exposure = exposure;
            exposurePlan.desired = desired;
            exposurePlan.acquired = moveOp ? acquired : 0;
            exposurePlan.accepted = moveOp ? accepted : 0;
            exposurePlan.enabled = enabled;

            return exposurePlan;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"ProfileId: {ProfileId}");
            sb.AppendLine($"TargetId: {TargetId}");
            sb.AppendLine($"ExposureTemplate: {ExposureTemplate}");
            sb.AppendLine($"Exposure: {Exposure}");
            sb.AppendLine($"Desired: {Desired}");
            sb.AppendLine($"Acquired: {Acquired}");
            sb.AppendLine($"Accepted: {Accepted}");
            sb.AppendLine($"Enabled: {IsEnabled}");

            return sb.ToString();
        }
    }

    public interface IExposureCounts {
        int Desired { get; set; }
        int Accepted { get; set; }
        int Acquired { get; set; }
    }
}