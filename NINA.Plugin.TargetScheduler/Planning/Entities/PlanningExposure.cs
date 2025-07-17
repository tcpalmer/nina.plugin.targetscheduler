using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using System;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Planning.Entities {

    public class PlanningExposure : IExposure {
        public string PlanId { get; set; }
        public int DatabaseId { get; set; }
        public string FilterName { get; set; }
        public double ExposureLength { get; set; }
        public int? Gain { get; set; }
        public int? Offset { get; set; }
        public BinningMode BinningMode { get; set; }
        public int? ReadoutMode { get; set; }
        public int Desired { get; set; }
        public int Acquired { get; set; }
        public int Accepted { get; set; }
        public ITarget PlanTarget { get; set; }

        public TwilightLevel TwilightLevel { get; set; }
        public int MinutesOffset { get; set; }
        public int DitherEvery { get; set; }

        public bool MoonAvoidanceEnabled { get; set; }
        public double MoonAvoidanceSeparation { get; set; }
        public int MoonAvoidanceWidth { get; set; }
        public double MoonRelaxScale { get; set; }
        public double MoonRelaxMaxAltitude { get; set; }
        public double MoonRelaxMinAltitude { get; set; }
        public bool MoonDownEnabled { get; set; }
        public double MoonAvoidanceScore { get; set; }

        public double MaximumHumidity { get; set; }

        public bool PreDither { get; set; }

        public bool Rejected { get; set; }
        public string RejectedReason { get; set; }

        public PlanningExposure(ITarget planTarget, ExposurePlan exposurePlan, ExposureTemplate exposureTemplate) {
            this.PlanId = Guid.NewGuid().ToString();
            this.DatabaseId = exposurePlan.Id;
            this.FilterName = exposureTemplate.FilterName;
            this.ExposureLength = exposurePlan.Exposure != -1 ? exposurePlan.Exposure : exposureTemplate.DefaultExposure;
            this.Gain = GetNullableIntValue(exposureTemplate.Gain);
            this.Offset = GetNullableIntValue(exposureTemplate.Offset);
            this.BinningMode = exposureTemplate.BinningMode;
            this.ReadoutMode = GetNullableIntValue(exposureTemplate.ReadoutMode);
            this.Desired = exposurePlan.Desired;
            this.Acquired = exposurePlan.Acquired;
            this.Accepted = exposurePlan.Accepted;
            this.PlanTarget = planTarget;

            this.TwilightLevel = exposureTemplate.TwilightLevel;
            this.MinutesOffset = exposureTemplate.MinutesOffset;
            this.DitherEvery = exposureTemplate.DitherEvery;

            this.MoonAvoidanceEnabled = exposureTemplate.MoonAvoidanceEnabled;
            this.MoonAvoidanceSeparation = exposureTemplate.MoonAvoidanceSeparation;
            this.MoonAvoidanceWidth = exposureTemplate.MoonAvoidanceWidth;
            this.MoonRelaxScale = exposureTemplate.MoonRelaxScale;
            this.MoonRelaxMaxAltitude = exposureTemplate.MoonRelaxMaxAltitude;
            this.MoonRelaxMinAltitude = exposureTemplate.MoonRelaxMinAltitude;
            this.MoonDownEnabled = exposureTemplate.MoonDownEnabled;
            this.MoonAvoidanceScore = MoonAvoidanceExpert.SCORE_OFF;

            this.MaximumHumidity = exposureTemplate.MaximumHumidity;

            this.PreDither = false;
            this.Rejected = false;
        }

        public PlanningExposure() {
        } // testing only

        public int NeededExposures() {
            return PlanTarget.Project.ExposureCompletionHelper.RemainingExposures(this);
        }

        public bool IsIncomplete() {
            return PlanTarget.Project.ExposureCompletionHelper.IsIncomplete(this);
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Id: {PlanId}");
            sb.AppendLine($"FilterName: {FilterName}");
            sb.AppendLine($"ExposureLength: {ExposureLength}");
            sb.AppendLine($"Gain: {Gain}");
            sb.AppendLine($"Offset: {Offset}");
            sb.AppendLine($"Bin: {BinningMode}");
            sb.AppendLine($"ReadoutMode: {ReadoutMode}");
            sb.AppendLine($"Desired: {Desired}");
            sb.AppendLine($"Acquired: {Acquired}");
            sb.AppendLine($"Accepted: {Accepted}");
            sb.AppendLine($"Pre-dither: {PreDither}");
            sb.AppendLine($"Rejected: {Rejected}");
            sb.AppendLine($"RejectedReason: {RejectedReason}");
            return sb.ToString();
        }

        private int? GetNullableIntValue(int value) {
            if (value >= 0) {
                return value;
            }

            return null;
        }

        public override bool Equals(object obj) {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) {
                return false;
            }

            PlanningExposure e = (PlanningExposure)obj;
            return this.DatabaseId == e.DatabaseId && this.FilterName == e.FilterName;
        }

        public override int GetHashCode() {
            int hash = 17;
            hash = hash * 23 + this.DatabaseId;
            hash = hash * 23 + this.FilterName.GetHashCode();
            return hash;
        }
    }
}