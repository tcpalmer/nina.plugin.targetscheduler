using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;

namespace NINA.Plugin.TargetScheduler.Planning.Interfaces {

    public interface IExposure : IExposureCounts {
        string PlanId { get; set; }
        int DatabaseId { get; set; }
        bool IsEnabled { get; set; }
        string FilterName { get; set; }
        double ExposureLength { get; set; }
        int? Gain { get; set; }
        int? Offset { get; set; }
        BinningMode BinningMode { get; set; }
        int? ReadoutMode { get; set; }
        ITarget PlanTarget { get; set; }

        TwilightLevel TwilightLevel { get; set; }
        public int MinutesOffset { get; set; }
        public int DitherEvery { get; set; }
        bool MoonAvoidanceEnabled { get; set; }
        double MoonAvoidanceSeparation { get; set; }
        int MoonAvoidanceWidth { get; set; }
        double MoonRelaxScale { get; set; }
        double MoonRelaxMaxAltitude { get; set; }
        double MoonRelaxMinAltitude { get; set; }
        bool MoonDownEnabled { get; set; }
        double MoonAvoidanceScore { get; set; }
        double MaximumHumidity { get; set; }

        bool PreDither { get; set; }

        bool Rejected { get; set; }
        string RejectedReason { get; set; }

        int NeededExposures();

        bool IsIncomplete();

        string ToString();
    }
}