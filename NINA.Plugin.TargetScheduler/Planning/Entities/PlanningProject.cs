using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Flats;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Planning.Entities {

    public class PlanningProject : IProject {
        public string PlanId { get; set; }
        public int DatabaseId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ProjectState State { get; set; }
        public ProjectPriority Priority { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ActiveDate { get; set; }
        public DateTime? InactiveDate { get; set; }
        public int SessionId { get; }

        public int MinimumTime { get; set; }
        public double MinimumAltitude { get; set; }
        public double MaximumAltitude { get; set; }
        public bool UseCustomHorizon { get; set; }
        public double HorizonOffset { get; set; }
        public int MeridianWindow { get; set; }
        public int FilterSwitchFrequency { get; set; }
        public int DitherEvery { get; set; }
        public bool SmartExposureOrder { get; set; }
        public bool EnableGrader { get; set; }
        public bool IsMosaic { get; set; }
        public int FlatsHandling { get; set; }
        public Dictionary<string, double> RuleWeights { get; set; }

        public ExposureCompletionHelper ExposureCompletionHelper { get; set; }
        public List<ITarget> Targets { get; set; }
        public HorizonDefinition HorizonDefinition { get; set; }
        public bool Rejected { get; set; }
        public string RejectedReason { get; set; }

        public PlanningProject(IProfile profile, Project project, ExposureCompletionHelper exposureCompletionHelper) {
            this.PlanId = Guid.NewGuid().ToString();
            this.DatabaseId = project.Id;
            this.Name = project.Name;
            this.Description = project.Description;
            this.State = project.State;
            this.Priority = project.Priority;
            this.CreateDate = project.CreateDate;
            this.ActiveDate = project.ActiveDate;
            this.InactiveDate = project.InactiveDate;
            this.SessionId = new FlatsExpert().GetCurrentSessionId(project, DateTime.Now);

            this.MinimumTime = project.MinimumTime;
            this.MinimumAltitude = project.MinimumAltitude;
            this.MaximumAltitude = project.MaximumAltitude;
            this.UseCustomHorizon = project.UseCustomHorizon;
            this.HorizonOffset = project.HorizonOffset;
            this.MeridianWindow = project.MeridianWindow;
            this.FilterSwitchFrequency = project.FilterSwitchFrequency;
            this.DitherEvery = project.DitherEvery;
            this.SmartExposureOrder = project.SmartExposureOrder;
            this.EnableGrader = project.EnableGrader;
            this.IsMosaic = project.IsMosaic;
            this.FlatsHandling = project.FlatsHandling;
            this.RuleWeights = GetRuleWeightsDictionary(project.RuleWeights);

            this.HorizonDefinition = DetermineHorizon(profile, project);
            this.Rejected = false;
            this.ExposureCompletionHelper = exposureCompletionHelper;

            Targets = new List<ITarget>();
            foreach (Target target in project.Targets) {
                if (target.Enabled) {
                    Targets.Add(new PlanningTarget(this, target));
                }
            }
        }

        private Dictionary<string, double> GetRuleWeightsDictionary(List<RuleWeight> ruleWeights) {
            Dictionary<string, double> dict = new Dictionary<string, double>(ruleWeights.Count);
            ruleWeights.ForEach((rw) => {
                dict.Add(rw.Name, rw.Weight);
            });

            return dict;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-- Project:");
            sb.AppendLine($"Name: {Name}");
            sb.AppendLine($"Description: {Description}");
            sb.AppendLine($"State: {State}");
            sb.AppendLine($"Priority: {Priority}");
            sb.AppendLine($"SessionId: {SessionId}");

            sb.AppendLine($"MinimumTime: {MinimumTime}");
            sb.AppendLine($"MinimumAltitude: {MinimumAltitude}");
            sb.AppendLine($"MaximumAltitude: {MaximumAltitude}");
            sb.AppendLine($"UseCustomHorizon: {UseCustomHorizon}");
            sb.AppendLine($"HorizonOffset: {HorizonOffset}");
            sb.AppendLine($"MeridianWindow: {MeridianWindow}");
            sb.AppendLine($"FilterSwitchFrequency: {FilterSwitchFrequency}");
            sb.AppendLine($"DitherEvery: {DitherEvery}");
            sb.AppendLine($"SmartExposureOrder: {SmartExposureOrder}");
            sb.AppendLine($"EnableGrader: {EnableGrader}");
            sb.AppendLine($"IsMosaic: {IsMosaic}");
            sb.AppendLine($"FlatsHandling: {FlatsHandling}");
            sb.AppendLine($"RuleWeights:");
            foreach (KeyValuePair<string, double> entry in RuleWeights) {
                sb.AppendLine($"  {entry.Key}: {entry.Value}");
            }

            sb.AppendLine($"Horizon: {HorizonDefinition}");
            sb.AppendLine($"Rejected: {Rejected}");
            sb.AppendLine($"RejectedReason: {RejectedReason}");

            sb.AppendLine("-- Targets:");
            foreach (PlanningTarget planTarget in Targets) {
                sb.AppendLine(planTarget.ToString());
            }

            return sb.ToString();
        }

        public static string ListToString(List<IProject> list) {
            if (list == null || list.Count == 0) {
                return "no projects";
            }

            StringBuilder sb = new StringBuilder();
            foreach (IProject planProject in list) {
                sb.AppendLine(planProject.ToString());
            }

            return sb.ToString();
        }

        private HorizonDefinition DetermineHorizon(IProfile profile, Project project) {
            if (project.UseCustomHorizon) {
                if (profile.AstrometrySettings.Horizon == null) {
                    TSLogger.Warning("project 'Use Custom Horizon' is enabled but no custom horizon was found in the profile, defaulting to Minimum Altitude");
                    return new HorizonDefinition(project.MinimumAltitude);
                }

                return new HorizonDefinition(profile.AstrometrySettings.Horizon, project.HorizonOffset, project.MinimumAltitude);
            }

            return new HorizonDefinition(project.MinimumAltitude);
        }
    }
}