using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning.Interfaces {

    public interface ITarget {
        string PlanId { get; set; }
        int DatabaseId { get; set; }
        string Name { get; set; }
        Coordinates Coordinates { get; set; }
        Epoch Epoch { get; set; }
        double Rotation { get; set; }
        double ROI { get; set; }
        bool IsPreview { get; set; }
        List<IExposure> AllExposurePlans { get; set; }
        List<IExposure> ExposurePlans { get; set; }
        List<IExposure> CompletedExposurePlans { get; set; }
        IExposureSelector ExposureSelector { get; set; }
        IProject Project { get; set; }

        bool Rejected { get; set; }
        string RejectedReason { get; set; }
        IExposure SelectedExposure { get; set; }
        ScoringResults ScoringResults { get; set; }
        DateTime StartTime { get; set; }
        DateTime EndTime { get; set; }
        DateTime MinimumTimeSpanEnd { get; set; }
        DateTime BonusTimeSpanEnd { get; set; }
        DateTime CulminationTime { get; set; }
        TimeInterval MeridianWindow { get; set; }

        void SetCircumstances(bool isVisible, DateTime startTime, DateTime culminationTime, DateTime endTime);

        string ToString();
    }
}