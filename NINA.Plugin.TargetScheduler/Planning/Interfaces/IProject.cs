using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning.Interfaces {

    public interface IProject {
        string PlanId { get; set; }
        int DatabaseId { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        ProjectState State { get; set; }
        ProjectPriority Priority { get; set; }
        DateTime CreateDate { get; set; }
        DateTime? ActiveDate { get; set; }
        DateTime? InactiveDate { get; set; }
        int SessionId { get; }

        int MinimumTime { get; set; }
        double MinimumAltitude { get; set; }
        double MaximumAltitude { get; set; }
        bool UseCustomHorizon { get; set; }
        double HorizonOffset { get; set; }
        int MeridianWindow { get; set; }
        int FilterSwitchFrequency { get; set; }
        int DitherEvery { get; set; }
        bool IsMosaic { get; set; }
        bool EnableGrader { get; set; }
        bool SmartExposureOrder { get; set; }
        int FlatsHandling { get; set; }
        Dictionary<string, double> RuleWeights { get; set; }

        ExposureCompletionHelper ExposureCompletionHelper { get; set; }
        List<ITarget> Targets { get; set; }
        HorizonDefinition HorizonDefinition { get; set; }
        bool Rejected { get; set; }
        string RejectedReason { get; set; }

        string ToString();
    }
}