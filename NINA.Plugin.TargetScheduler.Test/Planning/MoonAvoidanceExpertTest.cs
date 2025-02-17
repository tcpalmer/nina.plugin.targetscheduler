using FluentAssertions;
using Moq;
using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class MoonAvoidanceExpertTest {

        [Test]
        public void testClassicRelaxOff() {
            ITarget planTarget = GetPlanTarget();
            IExposure planExposure = GetPlanExposure(true, 120, 14, 0, 5, -15, false);
            DateTime atTime = DateTime.Now;

            // Full moon, sep angle too small
            MoonAvoidanceExpert sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 14, SeparationAngle = 20 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeTrue();

            // Full moon, sep angle big enough
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 14, SeparationAngle = 120 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeFalse();
            planExposure.MoonAvoidanceScore.Should().BeApproximately(.6646, 0.001);

            // Moon age=10, sep angle too small
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 10, SeparationAngle = 107 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeTrue();

            // Moon age=10, sep angle just big enough
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 10, SeparationAngle = 108 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeFalse();
            planExposure.MoonAvoidanceScore.Should().BeApproximately(.5974, 0.001);
        }

        [Test]
        public void testClassicNotRelaxZone() {
            ITarget planTarget = GetPlanTarget();
            IExposure planExposure = GetPlanExposure(true, 120, 14, 2, 5, -15, false);
            DateTime atTime = DateTime.Now;

            // Full moon, sep angle too small
            MoonAvoidanceExpert sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 14, SeparationAngle = 20 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeTrue();

            // Full moon, sep angle big enough
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 14, SeparationAngle = 120 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeFalse();
            planExposure.MoonAvoidanceScore.Should().BeApproximately(.6646, 0.001);

            // Moon age=10, sep angle too small
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 10, SeparationAngle = 107 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeTrue();

            // Moon age=10, sep angle big enough
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 10, SeparationAngle = 108 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeFalse();
            planExposure.MoonAvoidanceScore.Should().BeApproximately(.5974, 0.001);
        }

        [Test]
        public void testClassicRelaxZone() {
            ITarget planTarget = GetPlanTarget();
            IExposure planExposure = GetPlanExposure(true, 120, 14, 2, 5, -15, false);
            DateTime atTime = DateTime.Now;

            // With altitude 0, separation of 112 is now OK at full
            MoonAvoidanceExpert sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 0, MoonAge = 14, SeparationAngle = 112 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeFalse();

            // Less than min altitude, don't reject
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = -16, MoonAge = 14, SeparationAngle = 5 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeFalse();
            planExposure.MoonAvoidanceScore.Should().BeApproximately(.1973, 0.001);
        }

        [Test]
        public void testMoonDownEnabled() {
            ITarget planTarget = GetPlanTarget();
            IExposure planExposure = GetPlanExposure(true, 120, 14, 2, 5, -15, true);
            DateTime atTime = DateTime.Now;

            // With altitude 0, moon is above the max and should be rejected
            MoonAvoidanceExpert sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 6, MoonAge = 14, SeparationAngle = 112 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeTrue();

            // Less than max altitude, don't reject
            sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = -12, MoonAge = 14, SeparationAngle = 100 };
            sut.IsRejected(atTime, planTarget, planExposure).Should().BeFalse();
            planExposure.MoonAvoidanceScore.Should().Be(1);
        }

        [Test]
        public void testNotMocked() {
            MoonAvoidanceExpert sut = new MoonAvoidanceExpert(TestData.North_Mid_Lat);
            ITarget planTarget = GetPlanTarget();
            planTarget.Coordinates = TestData.M42;
            // Jan 17, 2024 8pm:
            //   moon is first quarter (age 6.85 at midpoint time), distance to M42 is ~62 degrees
            //   moon alt is ~53 degrees at highest point in plan (setting)
            planTarget.StartTime = new DateTime(2024, 1, 17, 20, 0, 0);
            planTarget.EndTime = new DateTime(2024, 1, 17, 20, 30, 0);
            planTarget.Project = PlanMocks.GetMockPlanProject("", ProjectState.Active).Object;

            IExposure planExposure = GetPlanExposure(true, 90, 12, 0, 5, -15, false);
            sut.IsRejected(planTarget.StartTime, planTarget, planExposure).Should().BeTrue();

            planExposure = GetPlanExposure(true, 90, 11, 0, 5, -15, false);
            sut.IsRejected(planTarget.StartTime, planTarget, planExposure).Should().BeFalse();
            planExposure.MoonAvoidanceScore.Should().BeApproximately(.3291, 0.001);
        }

        [Test]
        public void testAvoidanceScore() {
            DateTime atTime = DateTime.Now;

            MoonAvoidanceExpert sut = new MoonAvoidanceExpertMock(TestData.North_Mid_Lat) { Altitude = 20, MoonAge = 14, SeparationAngle = 20 };

            IExposure planExposure = GetPlanExposure(true, 180, 14, 0, 5, -15, false);

            sut.GetAvoidanceScore(true, planExposure, 180).Should().Be(MoonAvoidanceExpert.SCORE_OFF);
            sut.GetAvoidanceScore(false, planExposure, 0).Should().Be(0);
            sut.GetAvoidanceScore(false, planExposure, 180).Should().Be(1);
            sut.GetAvoidanceScore(false, planExposure, 170).Should().BeApproximately(0.944, 0.001);

            planExposure = GetPlanExposure(true, 180, 14, 0, 5, -15, true);
            sut.GetAvoidanceScore(false, planExposure, 120).Should().Be(1);
        }

        /*
        [Test]
        public void testAvoidanceScoreRange() {
            MoonAvoidanceExpert sut = new MoonAvoidanceExpert(TestData.North_Mid_Lat);
            ITarget planTarget = GetPlanTarget();
            planTarget.Coordinates = TestData.M42;
            planTarget.Project = PlanMocks.GetMockPlanProject("", ProjectState.Active).Object;
            IExposure planExposure = GetPlanExposure(true, 120, 10, 5, 5, -15, false);

            // Feb 17, 2025:
            //   moon rises just after 11pm
            planTarget.StartTime = new DateTime(2025, 2, 17, 19, 0, 0);
            planTarget.EndTime = planTarget.StartTime.AddHours(1);

            for (int i = 0; i < 30; i++) {
                sut.IsRejected(planTarget.StartTime, planTarget, planExposure).Should().BeFalse();
                TestContext.WriteLine($"score: {planExposure.MoonAvoidanceScore}, moon alt {sut.GetRelaxationMoonAltitude(planTarget.StartTime)} at {planTarget.StartTime}");
                planTarget.StartTime = planTarget.StartTime.AddMinutes(10);
                planTarget.EndTime = planTarget.StartTime.AddHours(1);
            }
        }*/

        private ITarget GetPlanTarget() {
            Mock<ITarget> pt = new Mock<ITarget>();
            pt.SetupAllProperties();
            pt.SetupProperty(m => m.Name, "T1");

            return pt.Object;
        }

        private IExposure GetPlanExposure(bool avoidanceEnabled, double separation, int width, double relaxScale, double relaxMaxAlt, double relaxMinAlt, bool moonDownEnabled) {
            Mock<IExposure> pe = new Mock<IExposure>();
            pe.SetupAllProperties();
            pe.SetupProperty(m => m.MoonAvoidanceEnabled, avoidanceEnabled);
            pe.SetupProperty(m => m.MoonAvoidanceSeparation, separation);
            pe.SetupProperty(m => m.MoonAvoidanceWidth, width);
            pe.SetupProperty(m => m.MoonRelaxScale, relaxScale);
            pe.SetupProperty(m => m.MoonRelaxMaxAltitude, relaxMaxAlt);
            pe.SetupProperty(m => m.MoonRelaxMinAltitude, relaxMinAlt);
            pe.SetupProperty(m => m.MoonDownEnabled, moonDownEnabled);
            pe.SetupProperty(m => m.FilterName, "FLT");
            return pe.Object;
        }
    }

    internal class MoonAvoidanceExpertMock : MoonAvoidanceExpert {
        public double Altitude { get; set; }
        public double MoonAge { get; set; }
        public double SeparationAngle { get; set; }

        public MoonAvoidanceExpertMock(ObserverInfo observerInfo) : base(observerInfo) {
            Altitude = 0;
            MoonAge = 0;
            SeparationAngle = 0;
        }

        public override double GetRelaxationMoonAltitude(DateTime evalTime) {
            return Altitude;
        }

        public override double GetMoonAge(DateTime atTime) {
            return MoonAge;
        }

        public override double GetMoonSeparationAngle(ObserverInfo location, DateTime atTime, Coordinates coordinates) {
            return SeparationAngle;
        }
    }
}