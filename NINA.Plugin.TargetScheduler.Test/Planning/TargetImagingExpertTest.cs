using FluentAssertions;
using FluentAssertions.Extensions;
using Moq;
using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class TargetImagingExpertTest {

        [Test]
        public void testVisibilityVisibleNow() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 23, 0, 0);
            DateTime sunset = atTime.AddHours(-4);
            DateTime sunrise = atTime.AddHours(7);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = DateTime.MinValue;
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestData.North_Mid_Lat, atTime, sunset, sunrise, 60);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestData.North_Mid_Lat, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeTrue();
            t1.Rejected.Should().BeFalse();
            t1.StartTime.Should().Be(atTime);
        }

        [Test]
        public void testVisibilityVisibleFuture() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2024, 10, 1, 20, 0, 0);
            DateTime sunset = atTime.AddHours(-2);
            DateTime sunrise = atTime.AddHours(9);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = DateTime.MinValue;
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestData.North_Mid_Lat, atTime, sunset, sunrise, 60);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestData.North_Mid_Lat, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeFalse();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetNotYetVisible);
            t1.StartTime.Should().Be(new DateTime(2024, 10, 2, 0, 21, 0));
        }

        [Test]
        public void testVisibilityMeridianWindow() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2024, 12, 1, 20, 0, 0);
            DateTime sunset = atTime.AddHours(-2);
            DateTime sunrise = atTime.AddHours(10);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 10;
            p1.MeridianWindow = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = DateTime.MinValue;
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestData.North_Mid_Lat, atTime, sunset, sunrise, 60);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestData.North_Mid_Lat, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeFalse();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetBeforeMeridianWindow);
            t1.StartTime.Should().BeCloseTo(new DateTime(2024, 12, 2, 0, 35, 8), 2.Seconds());
        }

        [Test]
        public void testVisibilityMeridianFlipPause() {
            IProfile profile = GetProfileService(10, 10);
            DateTime atTime = new DateTime(2025, 1, 1, 23, 0, 0);
            DateTime sunset = atTime.AddHours(-4);
            DateTime sunrise = atTime.AddHours(7);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = DateTime.MinValue;
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestData.North_Mid_Lat, atTime, sunset, sunrise, 60);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestData.North_Mid_Lat, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeFalse();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetMeridianFlipClipped);
            t1.StartTime.Should().BeCloseTo(new DateTime(2025, 1, 1, 23, 17, 22), 1.Seconds()); // start shifted until after MF unsafe zone
        }

        [Test]
        public void testVisibilityMeridianFlipPauseMinimum() {
            IProfile profile = GetProfileService(15, 15);
            DateTime atTime = new DateTime(2025, 5, 20, 1, 8, 0);
            DateTime sunset = atTime.AddHours(-6);
            DateTime sunrise = atTime.AddHours(6);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 20;

            Coordinates RhoP4 = new Coordinates(AstroUtil.HMSToDegrees("16:21:17"), AstroUtil.DMSToDegrees("-26:23:12"), Epoch.J2000, Coordinates.RAType.Degrees);
            ObserverInfo TestLoc = new ObserverInfo();
            TestLoc.Latitude = 31.54;
            TestLoc.Longitude = -79;
            TestLoc.Elevation = 0;

            ITarget t1 = PlanMocks.GetMockPlanTarget("RhoP4", RhoP4).Object;
            t1.StartTime = DateTime.MinValue;
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestLoc, atTime, sunset, sunrise);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestLoc, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeTrue(); // starting at 1:08, we can fit in 20m minimum
            t1.Rejected.Should().BeFalse();

            atTime = atTime.AddMinutes(10);
            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeFalse(); // but 10m later, we can't fit 20m before pause
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetMeridianFlipClipped);

            // But it is available when it's safe after the MF
            t1.StartTime.Should().BeCloseTo(new DateTime(2025, 5, 20, 2, 0, 30), TimeSpan.FromSeconds(1));
        }

        [Test]
        public void testVisibilityNeverRises() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);
            DateTime sunset = atTime.AddHours(-1);
            DateTime sunrise = atTime.AddHours(12);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.STAR_SOUTH_CIRCP).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            // location is way north, target is way south
            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestData.Sanikiluaq_NU, atTime, sunset, sunrise, 60);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestData.Sanikiluaq_NU, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeFalse();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetNeverRises);
        }

        [Test]
        public void testVisibilityNoNight() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 6, 21, 23, 0, 0);
            DateTime sunset = atTime.AddHours(-1);
            DateTime sunrise = atTime.AddHours(5);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            // no night on summer solstice this way north
            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestData.Sanikiluaq_NU, atTime, sunset, sunrise, 60);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestData.Sanikiluaq_NU, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeFalse();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetAllExposurePlans);
        }

        [Test]
        public void testVisibilityNotVisibleOnDate() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 6, 21, 22, 0, 0);
            DateTime sunset = atTime.AddHours(-1);
            DateTime sunrise = atTime.AddHours(7);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nautical;
            t1.ExposurePlans.Add(e1);

            // no night on summer solstice this way north
            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);
            TargetVisibility viz = new TargetVisibility(t1, TestData.North_Mid_Lat, atTime, sunset, sunrise, 60);
            TwilightCircumstances twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(TestData.North_Mid_Lat, atTime);

            sut.Visibility(atTime, t1, twilightCircumstances, viz).Should().BeFalse();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetNotVisible);
        }

        [Test]
        public void testTwilightFilter() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.TwilightLevel = TwilightLevel.Nighttime;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            sut.TwilightFilter(t1, TwilightLevel.Astronomical);
            e1.Rejected.Should().BeTrue();
            e1.RejectedReason.Should().Be(Reasons.FilterTwilight);
            sut.ClearRejections(t1);

            sut.TwilightFilter(t1, TwilightLevel.Nighttime);
            e1.Rejected.Should().BeFalse();
            sut.ClearRejections(t1);

            e1.Rejected = true;
            e1.RejectedReason = "other";
            sut.TwilightFilter(t1, TwilightLevel.Nighttime);
            e1.Rejected.Should().BeTrue();
            e1.RejectedReason.Should().Be("other");
            sut.ClearRejections(t1);

            sut.TwilightFilter(t1, null);
            e1.Rejected.Should().BeTrue();
            e1.RejectedReason.Should().Be(Reasons.FilterTwilight);
            sut.ClearRejections(t1);

            e1.TwilightLevel = TwilightLevel.Nautical;
            sut.TwilightFilter(t1, TwilightLevel.Nautical);
            e1.Rejected.Should().BeFalse();
            sut.TwilightFilter(t1, TwilightLevel.Astronomical);
            e1.Rejected.Should().BeFalse();
            sut.TwilightFilter(t1, TwilightLevel.Nighttime);
            e1.Rejected.Should().BeFalse();
            sut.TwilightFilter(t1, TwilightLevel.Civil);
            e1.Rejected.Should().BeTrue();
            e1.RejectedReason.Should().Be(Reasons.FilterTwilight);
        }

        [Test]
        public void testMoonAvoidanceFilter() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            // Not rejected
            sut.MoonAvoidanceFilter(atTime, t1, GetMoonAvoidanceExpert("L"));
            e1.Rejected.Should().BeFalse();
            sut.ClearRejections(t1);

            // Rejected
            sut.MoonAvoidanceFilter(atTime, t1, GetMoonAvoidanceExpert("X"));
            e1.Rejected.Should().BeTrue();
            e1.RejectedReason.Should().Be(Reasons.FilterMoonAvoidance);
            sut.ClearRejections(t1);

            // Multiple exposures
            IExposure e2 = PlanMocks.GetMockPlanExposure("R", 10, 0).Object;
            t1.ExposurePlans.Add(e2);
            sut.MoonAvoidanceFilter(atTime, t1, GetMoonAvoidanceExpert("L"));
            e1.Rejected.Should().BeFalse();
            e2.Rejected.Should().BeTrue();
            e2.RejectedReason.Should().Be(Reasons.FilterMoonAvoidance);
        }

        [Test]
        public void testReadyNow() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            sut.ReadyNow(atTime, t1).Should().BeFalse();
            sut.ReadyNow(atTime.AddHours(1), t1).Should().BeTrue();
        }

        [Test]
        public void testCheckFutureAtStart() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            // Visible at target start and exposures good
            IMoonAvoidanceExpert moonExpert = GetMoonAvoidanceExpert("L");
            sut.CheckFuture(t1, moonExpert);
            t1.Rejected.Should().BeFalse();
        }

        [Test]
        public void testCheckFutureMaxAltitude() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2024, 10, 15, 23, 0, 0);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MaximumAltitude = 70;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            t1.StartTime = atTime;
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            t1.ExposurePlans.Add(e1);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            // Above max altitude at start but descending and available later
            sut.CheckFuture(t1, GetMoonAvoidanceExpert("L"));
            t1.Rejected.Should().BeFalse();
            t1.StartTime.Should().BeCloseTo(new DateTime(2024, 10, 16, 1, 57, 50), 1.Seconds());
        }

        [Test]
        public void testCheckFutureMoonAllNight() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            t1.ExposurePlans.Add(e1);
            IExposure e2 = PlanMocks.GetMockPlanExposure("R", 10, 0).Object;
            t1.ExposurePlans.Add(e2);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            // Visible at target start but exposures are rejected for moon - for the rest of the night
            IMoonAvoidanceExpert moonExpert = GetMoonAvoidanceExpert(null);
            sut.CheckFuture(t1, moonExpert);
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetNotVisible);
        }

        [Test]
        public void testCheckFutureMoon() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);
            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            t1.ExposurePlans.Add(e1);
            IExposure e2 = PlanMocks.GetMockPlanExposure("R", 10, 0).Object;
            t1.ExposurePlans.Add(e2);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            // Visible at target start but exposures are rejected for moon until about 10pm
            DateTime moonAcceptTime = atTime.AddHours(2);
            IMoonAvoidanceExpert moonExpert = GetMoonAvoidanceExpert(null, moonAcceptTime);
            sut.CheckFuture(t1, moonExpert);
            t1.Rejected.Should().BeFalse();
            t1.StartTime.Should().BeCloseTo(moonAcceptTime, 2.Minutes());
        }

        [Test]
        public void testCheckFutureVizSpan() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2025, 1, 1, 20, 0, 0);

            IProject p1 = PlanMocks.GetMockPlanProject("P1", ProjectState.Active).Object;
            p1.MinimumTime = 30;
            p1.MinimumAltitude = 30;
            p1.HorizonDefinition = TargetVisibilityTest.GetSpikedHorizon();

            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42).Object;
            t1.StartTime = atTime.AddHours(1);
            t1.Project = p1;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            t1.ExposurePlans.Add(e1);
            IExposure e2 = PlanMocks.GetMockPlanExposure("R", 10, 0).Object;
            t1.ExposurePlans.Add(e2);

            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            // With the spiked horizon, visibility will be interrupted and have to jump spans.
            // Moon will reject until 00:05:00.  Visibility will resume at 00:04:24am
            DateTime moonAcceptTime = new DateTime(2025, 1, 2, 0, 5, 0);
            IMoonAvoidanceExpert moonExpert = GetMoonAvoidanceExpert(null, moonAcceptTime);
            sut.CheckFuture(t1, moonExpert);
            t1.Rejected.Should().BeFalse();
            t1.StartTime.Should().BeCloseTo(moonAcceptTime, 2.Minutes());
        }

        [Test]
        public void testVisibleLater() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2024, 12, 1, 20, 0, 0);
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            sut.VisibleLater(t1).Should().BeFalse();

            t1.Rejected = true;
            t1.RejectedReason = Reasons.TargetComplete;
            sut.VisibleLater(t1).Should().BeFalse();

            t1.RejectedReason = Reasons.TargetNotYetVisible;
            sut.VisibleLater(t1).Should().BeTrue();

            t1.RejectedReason = Reasons.TargetBeforeMeridianWindow;
            sut.VisibleLater(t1).Should().BeTrue();
        }

        [Test]
        public void testCheckMaximumAltitude() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2024, 10, 15, 23, 0, 0);
            DateTime sunset = atTime.AddHours(-5);
            DateTime sunrise = atTime.AddHours(7);
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            TargetVisibility viz = new TargetVisibility(t1, TestData.North_Mid_Lat, atTime, sunset, sunrise, 60);
            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            t1.Rejected = false;
            t1.RejectedReason = null;

            t1.Project.MaximumAltitude = 0; // max check not enabled
            sut.CheckMaximumAltitude(atTime, t1, viz).Should().BeTrue();
            t1.Rejected.Should().BeFalse();
            t1.RejectedReason.Should().BeNull();

            t1.Project.MaximumAltitude = 70; // max check enabled and above
            sut.CheckMaximumAltitude(atTime, t1, viz).Should().BeFalse();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetMaxAltitude);

            t1.Rejected = false;
            t1.RejectedReason = null;

            t1.Project.MaximumAltitude = 75; // max check enabled but below
            sut.CheckMaximumAltitude(atTime, t1, viz).Should().BeTrue();
            t1.Rejected.Should().BeFalse();
            t1.RejectedReason.Should().BeNull();
        }

        [Test]
        public void testAllExposurePlansRejected() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2024, 12, 1, 20, 0, 0);
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            sut.AllExposurePlansRejected(t1).Should().BeTrue();

            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.Rejected = false;
            IExposure e2 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e2.Rejected = false;
            t1.ExposurePlans.Add(e1);
            t1.ExposurePlans.Add(e2);

            sut.AllExposurePlansRejected(t1).Should().BeFalse();
            e1.Rejected = true;
            sut.AllExposurePlansRejected(t1).Should().BeFalse();
            e2.Rejected = true;
            sut.AllExposurePlansRejected(t1).Should().BeTrue();
        }

        [Test]
        public void testClearRejections() {
            IProfile profile = GetProfileService();
            DateTime atTime = new DateTime(2024, 12, 1, 20, 0, 0);
            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            TargetImagingExpert sut = new TargetImagingExpert(profile, GetPrefs(), false);

            t1.Rejected = true;
            sut.ClearRejections(t1);
            t1.Rejected.Should().BeFalse();

            t1.Rejected = true;
            IExposure e1 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e1.Rejected = true;
            IExposure e2 = PlanMocks.GetMockPlanExposure("L", 10, 0).Object;
            e2.Rejected = true;
            t1.ExposurePlans.Add(e1);
            t1.ExposurePlans.Add(e2);

            sut.ClearRejections(t1);
            t1.Rejected.Should().BeFalse();
            t1.ExposurePlans[0].Rejected.Should().BeFalse();
            t1.ExposurePlans[1].Rejected.Should().BeFalse();
        }

        private IProfile GetProfileService(double pauseMinutes = 0, double minutesAfter = 0) {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            profileMock.SetupProperty(m => m.ActiveProfile.MeridianFlipSettings.PauseTimeBeforeMeridian, pauseMinutes);
            profileMock.SetupProperty(m => m.ActiveProfile.MeridianFlipSettings.MinutesAfterMeridian, minutesAfter);
            return profileMock.Object.ActiveProfile;
        }

        private ProfilePreference GetPrefs(string profileId = "abcd-1234") {
            return new ProfilePreference(profileId);
        }

        private IMoonAvoidanceExpert GetMoonAvoidanceExpert(string acceptedFilterName, DateTime? rejectUntil = null) {
            Mock<IMoonAvoidanceExpert> mock = new Mock<IMoonAvoidanceExpert>();

            // reject until atTime
            if (rejectUntil != null) {
                mock.Setup(x => x.IsRejected(It.IsAny<DateTime>(), It.IsAny<ITarget>(), It.IsAny<IExposure>()))
                    .Returns((DateTime d, ITarget t, IExposure e) => d < rejectUntil);
                return mock.Object;
            }

            if (acceptedFilterName != null) {
                // reject if filterName != acceptedFilterName
                mock.Setup(x => x.IsRejected(It.IsAny<DateTime>(), It.IsAny<ITarget>(), It.IsAny<IExposure>()))
                    .Returns((DateTime d, ITarget t, IExposure e) => e.FilterName != acceptedFilterName);
            } else {
                // reject all
                mock.Setup(x => x.IsRejected(It.IsAny<DateTime>(), It.IsAny<ITarget>(), It.IsAny<IExposure>()))
                    .Returns(true);
            }

            return mock.Object;
        }
    }
}