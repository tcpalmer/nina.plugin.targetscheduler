using Newtonsoft.Json;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Database.Schema {

    public class AcquiredImage {
        [Key] public int Id { get; set; }
        public string guid { get; set; }
        public string profileId { get; set; }
        [Required] public int ProjectId { get; set; }
        [Required] public int TargetId { get; set; }
        public int ExposureId { get; set; }
        public long acquiredDate { get; set; }
        [Required] public string FilterName { get; set; }
        public int gradingStatus { get; set; }
        public string rejectreason { get; set; }
        internal string _metadata { get; set; }

        [NotMapped]
        public string Guid { get => guid; set { guid = value; } }

        [NotMapped]
        public string ProfileId {
            get { return profileId == null ? "" : profileId; }
            set { profileId = value; }
        }

        [NotMapped]
        public DateTime AcquiredDate {
            get { return Common.UnixSecondsToDateTime(acquiredDate); }
            set { acquiredDate = Common.DateTimeToUnixSeconds(value); }
        }

        [NotMapped]
        public GradingStatus GradingStatus {
            get { return (GradingStatus)gradingStatus; }
            set { gradingStatus = (int)value; }
        }

        [NotMapped]
        public bool Pending => GradingStatus == GradingStatus.Pending;

        [NotMapped]
        public bool Accepted => GradingStatus == GradingStatus.Accepted;

        [NotMapped]
        public bool Rejected => GradingStatus == GradingStatus.Rejected;

        [NotMapped]
        public string RejectReason {
            get { return rejectreason == null ? "" : rejectreason; }
            set { rejectreason = value; }
        }

        [NotMapped]
        public ImageMetadata Metadata {
            get {
                return _metadata != null ? JsonConvert.DeserializeObject<ImageMetadata>(_metadata) : null;
            }
            set {
                _metadata = JsonConvert.SerializeObject(value);
            }
        }

        public virtual List<ImageData> Images { get; set; }

        public AcquiredImage() {
        }

        public AcquiredImage(ImageMetadata imageMetadata) {
            this.Metadata = imageMetadata;
        }

        public AcquiredImage(string profileId, int projectId, int targetId, int exposureId, DateTime acquiredDate, string filterName, GradingStatus gradingStatus, string rejectReason, ImageMetadata imageMetadata) {
            this.Guid = System.Guid.NewGuid().ToString();
            this.ProfileId = profileId;
            this.ProjectId = projectId;
            this.TargetId = targetId;
            this.ExposureId = exposureId;
            this.AcquiredDate = acquiredDate;
            this.FilterName = filterName;
            this.GradingStatus = gradingStatus;
            this.RejectReason = rejectReason;
            this.Metadata = imageMetadata;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"ProfileId: {ProfileId}");
            sb.AppendLine($"AcquiredDate: {AcquiredDate}");
            sb.AppendLine($"FilterName: {FilterName}");
            sb.AppendLine($"Grading Status: {GradingStatus}");
            sb.AppendLine($"RejectReason: {RejectReason}");
            sb.AppendLine($"Metadata: {_metadata}");

            return sb.ToString();
        }
    }

    internal class AcquiredImageConfiguration : EntityTypeConfiguration<AcquiredImage> {

        public AcquiredImageConfiguration() {
            HasKey(x => new { x.Id });
            Property(x => x._metadata).HasColumnName("metadata");
            HasMany(x => x.Images)
                .WithRequired(i => i.AcquiredImage)
                .HasForeignKey(i => i.AcquiredImageId);
        }
    }
}