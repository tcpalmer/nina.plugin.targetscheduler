using NINA.Astrometry;
using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning {

    /// <summary>
    /// PlannerEmulator isolates the NINA Scheduler sequence instructions from the Planner, allowing comprehensive
    /// testing of the sequencer operation without having to have an working database or running planner that relies
    /// on the current time, nighttime circumstances, etc.
    /// </summary>
    public class PlannerEmulator {
        private static int CallNumber = 0;

        private DateTime atTime;
        private IProfile activeProfile;

        public PlannerEmulator(DateTime atTime, IProfile activeProfile) {
            this.atTime = atTime;
            this.activeProfile = activeProfile;
        }

        public SchedulerPlan GetPlan(ITarget previousTarget) {
            CallNumber++;
            TSLogger.Info($"PlannerEmulator.GetPlan: {CallNumber}");
            SchedulerPlan plan;

            switch (CallNumber) {
                case 1: plan = WaitForTime(DateTime.Now.AddSeconds(5)); break;
                case 2: plan = Plan1(CallNumber); break;
                case 3: plan = Plan1(CallNumber); break;
                case 4: plan = Plan1(CallNumber); break;
                case 5: plan = Plan2(CallNumber); break;
                case 6: plan = Plan1(CallNumber); break;
                case 7: plan = WaitForTime(DateTime.Now.AddSeconds(5)); break;
                case 8: plan = Plan2(CallNumber); break;
                default:
                    CallNumber = 0;
                    return null;
            }

            plan.IsEmulator = true;
            return plan;
        }

        private SchedulerPlan Plan1(int callNumber) {
            DateTime endTime = atTime.AddMinutes(5);
            TimeInterval timeInterval = new TimeInterval(atTime, endTime);

            EmulatedProject project = new EmulatedProject("P1");

            ITarget target = GetBasePlanTarget("T1", project, Cp5n5);
            target.EndTime = endTime;
            target.DatabaseId = 1;

            IExposure lum = GetExposurePlan("Lum", 10, null, null, 3, 1);
            IExposure red = GetExposurePlan("Red", 4, null, null, 3, 2);
            IExposure grn = GetExposurePlan("Green", 4, null, null, 3, 3);
            IExposure blu = GetExposurePlan("Blue", 4, null, null, 3, 4);
            target.ExposurePlans.Add(lum);
            target.ExposurePlans.Add(red);
            target.ExposurePlans.Add(grn);
            target.ExposurePlans.Add(blu);

            List<IInstruction> instructions = new List<IInstruction>();
            instructions.Add(new PlanMessage("planner emulator: Plan1"));
            if (callNumber == 2 || callNumber == 6) instructions.Add(new PlanSlew(target, false));
            instructions.Add(new PlanSwitchFilter(lum));
            instructions.Add(new PlanTakeExposure(lum));

            target.SelectedExposure = lum;

            return new SchedulerPlan(atTime, null, target, instructions, false);
        }

        private SchedulerPlan Plan2(int callNumber) {
            DateTime endTime = atTime.AddMinutes(5);
            TimeInterval timeInterval = new TimeInterval(atTime, endTime);

            EmulatedProject project = new EmulatedProject("P1");

            ITarget target = GetBasePlanTarget("T2", project, Cp5n5);
            target.EndTime = endTime;
            target.DatabaseId = 1;

            IExposure lum = GetExposurePlan("Lum", 10, null, null, 3, 1);
            IExposure red = GetExposurePlan("Red", 4, null, null, 3, 2);
            IExposure grn = GetExposurePlan("Green", 4, null, null, 3, 3);
            IExposure blu = GetExposurePlan("Blue", 4, null, null, 3, 4);
            target.ExposurePlans.Add(lum);
            target.ExposurePlans.Add(red);
            target.ExposurePlans.Add(grn);
            target.ExposurePlans.Add(blu);

            List<IInstruction> instructions = new List<IInstruction>();
            instructions.Add(new PlanMessage("planner emulator: Plan2"));
            instructions.Add(new PlanSlew(target, false));
            instructions.Add(new PlanDither());
            instructions.Add(new PlanSwitchFilter(red));
            instructions.Add(new PlanSetReadoutMode(red));
            instructions.Add(new PlanTakeExposure(red));

            target.SelectedExposure = lum;

            return new SchedulerPlan(atTime, null, target, instructions, false);
        }

        private SchedulerPlan WaitForTime(DateTime waitFor) {
            return new SchedulerPlan(atTime, null, new EmulatedTarget { StartTime = waitFor }, true);
        }

        private ITarget GetBasePlanTarget(string name, IProject project, Coordinates coordinates) {
            ITarget target = new EmulatedTarget();
            target.Project = project;
            target.Name = name;
            target.Coordinates = coordinates;
            target.Rotation = 16;
            return target;
        }

        private IExposure GetExposurePlan(string name, int length, int? gain, int? offset, int desired, int databaseId) {
            EmulatedExposure exposure = new EmulatedExposure();
            exposure.DatabaseId = databaseId;
            exposure.FilterName = name;
            exposure.ExposureLength = length;
            exposure.Gain = gain;
            exposure.Offset = offset;
            exposure.ReadoutMode = 0;
            exposure.BinningMode = new BinningMode(1, 1);
            exposure.Desired = desired;
            return exposure;
        }

        public static readonly Coordinates Cp5n5 = new Coordinates(AstroUtil.HMSToDegrees("5:0:0"), AstroUtil.DMSToDegrees("-5:0:0"), Epoch.J2000, Coordinates.RAType.Degrees);
        public static readonly Coordinates Cp1525 = new Coordinates(AstroUtil.HMSToDegrees("15:0:0"), AstroUtil.DMSToDegrees("25:0:0"), Epoch.J2000, Coordinates.RAType.Degrees);
    }

    internal class EmulatedProject : IProject {
        public string PlanId { get; set; }
        public int DatabaseId { get; set; }
        public string Name { get; set; }
        public DateTime CreateDate { get; set; }
        public int SessionId { get; set; }
        public bool UseCustomHorizon { get; set; }
        public double MinimumAltitude { get; set; }
        public double MaximumAltitude { get; set; }
        public int DitherEvery { get; set; }
        public bool EnableGrader { get; set; }
        public bool IsMosaic { get; set; }
        public int FlatsHandling { get; set; }

        public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ProjectState State { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ProjectPriority Priority { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime? ActiveDate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime? InactiveDate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int MinimumTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double HorizonOffset { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int MeridianWindow { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int FilterSwitchFrequency { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool SmartExposureOrder { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Dictionary<string, double> RuleWeights { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ExposureCompletionHelper ExposureCompletionHelper { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public List<ITarget> Targets { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public HorizonDefinition HorizonDefinition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool Rejected { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string RejectedReason { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public EmulatedProject(string name) {
            PlanId = Guid.NewGuid().ToString();
            Name = name;
            SessionId = 1;
        }
    }

    internal class EmulatedTarget : PlanningTarget {

        public EmulatedTarget() : base() {
            this.PlanId = Guid.NewGuid().ToString();
            this.ExposurePlans = new List<IExposure>();
        }
    }

    internal class EmulatedExposure : IExposure {
        public string PlanId { get; set; }
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
        public int DatabaseId { get; set; }

        public bool Rejected { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string RejectedReason { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int PlannedExposures { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TwilightLevel TwilightLevel { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool MoonAvoidanceEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonAvoidanceSeparation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int MoonAvoidanceWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonMaxAltitude { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonRelaxScale { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonRelaxMaxAltitude { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonRelaxMinAltitude { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool MoonDownEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MaximumHumidity { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonAvoidanceScore { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool PreDither { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public EmulatedExposure() {
            this.PlanId = Guid.NewGuid().ToString();
        }

        public bool IsIncomplete() {
            throw new NotImplementedException();
        }

        public int NeededExposures() {
            throw new NotImplementedException();
        }
    }
}