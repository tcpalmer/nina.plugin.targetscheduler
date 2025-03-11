using FluentAssertions;
using NINA.Astrometry;
using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Planning.Exposures {

    [TestFixture]
    public class ExposureCompletionHelperTest {

        [Test]
        [TestCase(0, 0, 0, true, 100, 0)]
        [TestCase(10, 0, 0, true, 100, 0)]
        [TestCase(10, 5, 0, true, 100, 50)]
        [TestCase(10, 10, 0, true, 100, 100)]
        [TestCase(10, 11, 0, true, 100, 100)]
        [TestCase(0, 0, 0, false, 100, 0)]
        [TestCase(10, 0, 0, false, 100, 0)]
        [TestCase(10, 10, 0, false, 100, 0)]
        [TestCase(10, 0, 10, false, 100, 100)]
        [TestCase(10, 0, 20, false, 100, 100)]
        [TestCase(10, 0, 2, false, 150, 13.33333)]
        [TestCase(10, 0, 10, false, 150, 66.66667)]
        [TestCase(10, 0, 15, false, 150, 100)]
        [TestCase(10, 0, 20, false, 150, 100)]
        public void TestPercentCompletePlan(int desired, int accepted, int acquired, bool gradingEnabled, double exposureThrottle, double expected) {
            ExposureCompletionHelper sut = new ExposureCompletionHelper(gradingEnabled, 0, exposureThrottle);
            TestPlan plan = new TestPlan(desired, accepted, acquired);
            sut.PercentComplete(plan).Should().BeApproximately(expected, 0.00001);
            sut.IsProvisionalPercentComplete(plan).Should().BeFalse();

            if (expected < 100) {
                sut.IsIncomplete(plan).Should().BeTrue();
                if (desired > 0) sut.RemainingExposures(plan).Should().BeGreaterThan(0);
            } else {
                sut.IsIncomplete(plan).Should().BeFalse();
                sut.RemainingExposures(plan).Should().Be(0);
            }
        }

        [Test]
        [TestCase(10, 0, 0, 80, 0)]
        [TestCase(10, 0, 4, 80, 40)]
        [TestCase(10, 0, 7, 80, 70)]
        [TestCase(10, 0, 12, 80, 0)]
        [TestCase(10, 0, 8, 80, 0)]
        [TestCase(10, 5, 8, 80, 50)]
        [TestCase(10, 12, 12, 80, 100)]
        public void TestPercentCompleteDelayed(int desired, int accepted, int acquired, double delayGrading, double expected) {
            ExposureCompletionHelper sut = new ExposureCompletionHelper(true, delayGrading, 100);
            TestPlan plan = new TestPlan(desired, accepted, acquired);
            sut.PercentComplete(plan).Should().BeApproximately(expected, 0.00001);
        }

        [Test]
        [TestCase(10, 0, 0, 80, true)]
        [TestCase(10, 0, 4, 80, true)]
        [TestCase(10, 0, 7, 80, true)]
        [TestCase(10, 0, 12, 80, false)]
        [TestCase(10, 0, 8, 80, false)]
        [TestCase(10, 5, 8, 80, false)]
        [TestCase(10, 12, 12, 80, false)]
        public void TestIsProvisionalPercentComplete(int desired, int accepted, int acquired, double delayGrading, bool expected) {
            ExposureCompletionHelper sut = new ExposureCompletionHelper(true, delayGrading, 100);
            TestPlan plan = new TestPlan(desired, accepted, acquired);
            sut.IsProvisionalPercentComplete(plan).Should().Be(expected);
        }

        [Test]
        [TestCase(0, 0, 0, true, 100, 0)]
        [TestCase(10, 5, 0, true, 100, 5)]
        [TestCase(10, 0, 0, true, 100, 10)]
        [TestCase(10, 12, 0, true, 100, 0)]
        [TestCase(0, 0, 0, false, 100, 0)]
        [TestCase(10, 0, 5, false, 100, 5)]
        [TestCase(10, 100, 5, false, 100, 5)]
        [TestCase(10, 0, 10, false, 100, 0)]
        [TestCase(10, 0, 0, false, 150, 15)]
        [TestCase(10, 0, 10, false, 150, 5)]
        public void TestRemainingExposures(int desired, int accepted, int acquired, bool gradingEnabled, double exposureThrottle, int expected) {
            ExposureCompletionHelper sut = new ExposureCompletionHelper(gradingEnabled, 0, exposureThrottle);
            TestPlan plan = new TestPlan(desired, accepted, acquired);
            sut.RemainingExposures(plan).Should().Be(expected);

            if (expected > 0) {
                sut.IsIncomplete(plan).Should().BeTrue();
            } else {
                if (desired > 0) sut.IsIncomplete(plan).Should().BeFalse();
            }

            // remaining exposures is uneffected by delayed grading
            sut = new ExposureCompletionHelper(gradingEnabled, 50, exposureThrottle);
            sut.RemainingExposures(plan).Should().Be(expected);
        }

        [Test]
        public void TestPercentCompleteTarget() {
            ExposureCompletionHelper sut = new ExposureCompletionHelper(true, 0, 100);
            Target target = new Target();

            sut.PercentComplete(target).Should().Be(0);

            target.ExposurePlans.Add(GetExposurePlan(0, 0, 0));
            target.ExposurePlans.Add(GetExposurePlan(0, 0, 0));
            sut.PercentComplete(target).Should().Be(0);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 10, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 20, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 20, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 5, 0));
            sut.PercentComplete(target).Should().Be(75);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 10, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 7, 0));
            sut.PercentComplete(target).Should().Be(85);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 20, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 7, 0));
            sut.PercentComplete(target).Should().Be(85);

            sut = new ExposureCompletionHelper(false, 0, 100);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            sut.PercentComplete(target).Should().Be(0);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 100, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 100, 0));
            sut.PercentComplete(target).Should().Be(0);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 10));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 20));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 20));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 5));
            sut.PercentComplete(target).Should().Be(75);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 20));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 10));
            sut.PercentComplete(target).Should().Be(100);

            sut = new ExposureCompletionHelper(false, 0, 150);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            sut.PercentComplete(target).Should().Be(0);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 100, 0));
            target.ExposurePlans.Add(GetExposurePlan(10, 100, 0));
            sut.PercentComplete(target).Should().Be(0);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 15));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 15));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 7));
            sut.PercentComplete(target).Should().BeApproximately(73.33333, 0.00001);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 15));
            target.ExposurePlans.Add(GetExposurePlan(10, 0, 15));
            sut.PercentComplete(target).Should().Be(100);
        }

        [Test]
        public void TestPercentCompletePlanTarget() {
            ExposureCompletionHelper sut = new ExposureCompletionHelper(true, 0, 100);
            ITarget target = new TestPlanTarget();

            sut.PercentComplete(target).Should().Be(0);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().Be(0);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 10, 100));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 100));
            sut.PercentComplete(target).Should().Be(50);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 10, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 10, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 5, 0));
            sut.PercentComplete(target).Should().Be(75);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 20, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 5, 0));
            sut.PercentComplete(target).Should().Be(75);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 10, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 10, 0));
            sut.PercentComplete(target).Should().Be(100);
            sut.IsIncomplete(target).Should().BeFalse();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 20, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 10, 0));
            sut.PercentComplete(target).Should().Be(100);
            sut.IsIncomplete(target).Should().BeFalse();

            sut = new ExposureCompletionHelper(false, 0, 100);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().Be(0);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 10));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 5));
            sut.PercentComplete(target).Should().Be(75);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 10));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 10));
            sut.PercentComplete(target).Should().Be(100);
            sut.IsIncomplete(target).Should().BeFalse();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 30));
            sut.PercentComplete(target).Should().Be(100);
            sut.IsIncomplete(target).Should().BeFalse();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 30));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().BeApproximately(66.66667, 0.00001);
            sut.IsIncomplete(target).Should().BeTrue();

            sut = new ExposureCompletionHelper(false, 0, 150);

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().Be(0);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 15));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().Be(50);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 15));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            sut.PercentComplete(target).Should().Be(100);
            sut.IsIncomplete(target).Should().BeFalse();

            target.ExposurePlans.Clear();
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 15));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().BeApproximately(66.66667, 0.00001);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.CompletedExposurePlans.Clear();
            target.CompletedExposurePlans.Add(new TestPlanExposure(10, 0, 15));
            target.CompletedExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            target.ExposurePlans.Add(new TestPlanExposure(10, 0, 0));
            sut.PercentComplete(target).Should().BeApproximately(66.66667, 0.00001);
            sut.IsIncomplete(target).Should().BeTrue();

            target.ExposurePlans.Clear();
            target.CompletedExposurePlans.Clear();
            target.CompletedExposurePlans.Add(new TestPlanExposure(10, 0, 15));
            target.CompletedExposurePlans.Add(new TestPlanExposure(10, 0, 20));
            sut.PercentComplete(target).Should().Be(100);
            sut.IsIncomplete(target).Should().BeFalse();
        }

        private ExposurePlan GetExposurePlan(int desired, int accepted, int acquired) {
            ExposurePlan ep = new ExposurePlan();
            ep.Desired = desired;
            ep.Accepted = accepted;
            ep.Acquired = acquired;
            return ep;
        }
    }

    internal class TestPlan : IExposureCounts {
        public int Desired { get; set; }
        public int Accepted { get; set; }
        public int Acquired { get; set; }

        public TestPlan(int desired, int accepted, int acquired) {
            Desired = desired;
            Accepted = accepted;
            Acquired = acquired;
        }
    }

    internal class TestPlanTarget : ITarget {
        public List<IExposure> AllExposurePlans { get; set; }
        public List<IExposure> ExposurePlans { get; set; }
        public List<IExposure> CompletedExposurePlans { get; set; }
        public List<IOverrideExposureOrderItem> OverrideExposureOrders { get; set; }
        public IExposureSelector ExposureSelector { get; set; }

        public TestPlanTarget() {
            ExposurePlans = new List<IExposure>();
            CompletedExposurePlans = new List<IExposure>();
        }

        public string PlanId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int DatabaseId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Coordinates Coordinates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Epoch Epoch { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double Rotation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double ROI { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsPreview { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IProject Project { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool Rejected { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string RejectedReason { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IExposure SelectedExposure { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ScoringResults ScoringResults { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime StartTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime EndTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime MinimumTimeSpanEnd { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime BonusTimeSpanEnd { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime CulminationTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeInterval MeridianWindow { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        string ITarget.PlanId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        int ITarget.DatabaseId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        string ITarget.Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        Coordinates ITarget.Coordinates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        Epoch ITarget.Epoch { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        double ITarget.Rotation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        double ITarget.ROI { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        IProject ITarget.Project { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        bool ITarget.Rejected { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        string ITarget.RejectedReason { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        IExposure ITarget.SelectedExposure { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        ScoringResults ITarget.ScoringResults { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        DateTime ITarget.StartTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        DateTime ITarget.EndTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        DateTime ITarget.CulminationTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        TimeInterval ITarget.MeridianWindow { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void SetCircumstances(bool isVisible, DateTime startTime, DateTime culminationTime, DateTime endTime) {
            throw new NotImplementedException();
        }

        void ITarget.SetCircumstances(bool isVisible, DateTime startTime, DateTime culminationTime, DateTime endTime) {
            throw new NotImplementedException();
        }

        string ITarget.ToString() {
            throw new NotImplementedException();
        }
    }

    internal class TestPlanExposure : IExposure {
        public int Desired { get; set; }
        public int Accepted { get; set; }
        public int Acquired { get; set; }

        public TestPlanExposure(int desired, int accepted, int acquired) {
            Desired = desired;
            Accepted = accepted;
            Acquired = acquired;
        }

        public string PlanId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int DatabaseId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FilterName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double ExposureLength { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int? Gain { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int? Offset { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public BinningMode BinningMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int? ReadoutMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ITarget PlanTarget { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TwilightLevel TwilightLevel { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool MoonAvoidanceEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonAvoidanceSeparation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int MoonAvoidanceWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonMaxAltitude { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonRelaxScale { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonRelaxMaxAltitude { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonRelaxMinAltitude { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool MoonDownEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MoonAvoidanceScore { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MaximumHumidity { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool PreDither { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool Rejected { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string RejectedReason { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int PlannedExposures { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsIncomplete() {
            throw new NotImplementedException();
        }

        public int NeededExposures() {
            throw new NotImplementedException();
        }
    }
}