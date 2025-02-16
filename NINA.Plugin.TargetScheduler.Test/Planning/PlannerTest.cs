using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class PlannerTest {

        [Test]
        [Ignore("implement PreviousTargetCanContinueTest")]
        public void PreviousTargetCanContinueTest() {
            // previous=null -> false
            // over minimum time -> false
            // moon comes into play -> false
            // all exposures complete -> false
            // OEO
        }

        [Test]
        public void testFilterForReadyComplete() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            Mock<IProject> pp2 = PlanMocks.GetMockPlanProject("pp2", ProjectState.Active);
            pt = PlanMocks.GetMockPlanTarget("M31", TestData.M31);
            pf = PlanMocks.GetMockPlanExposure("OIII", 10, 10);
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("SII", 10, 10);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp2, pt);

            Assert.That(new Planner(new DateTime(2023, 12, 17, 18, 0, 0), profile, GetPrefs(), false, false).FilterForIncomplete(null), Is.Null);

            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object, pp2.Object);
            projects = new Planner(new DateTime(2023, 12, 17, 18, 0, 0), profile, GetPrefs(), false, false).FilterForIncomplete(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(2);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeFalse();
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeFalse();
            IExposure pf1 = pt1.ExposurePlans[0];
            pf1.Rejected.Should().BeFalse();

            pp = projects[1];
            pp.Name.Should().Be("pp2");
            pp.Rejected.Should().BeTrue();
            pp.RejectedReason.Should().Be(Reasons.ProjectComplete);
            pt1 = pp.Targets[0];
            pf1 = pt1.ExposurePlans[0];
            pf1.Rejected.Should().BeTrue();
            pf1.RejectedReason.Should().Be(Reasons.FilterComplete);
            pf1 = pt1.ExposurePlans[1];
            pf1.Rejected.Should().BeTrue();
            pf1.RejectedReason.Should().Be(Reasons.FilterComplete);
        }

        [Test]
        public void testFilterForIncomplete() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            pt = PlanMocks.GetMockPlanTarget("M31", TestData.M31);
            pf = PlanMocks.GetMockPlanExposure("OIII", 10, 10);
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("SII", 10, 12);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);
            projects = new Planner(new DateTime(2023, 12, 17, 18, 0, 0), profile, GetPrefs(), false, false).FilterForIncomplete(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeFalse();

            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeFalse();
            IExposure pf1 = pt1.ExposurePlans[0];
            pf1.Rejected.Should().BeFalse();

            ITarget pt2 = pp.Targets[1];
            pt2.Rejected.Should().BeTrue();
            pt2.RejectedReason.Should().Be(Reasons.TargetComplete);

            IExposure pf2 = pt2.ExposurePlans[0];
            pf2.Rejected.Should().BeTrue();
            pf2.RejectedReason.Should().Be(Reasons.FilterComplete);
            IExposure pf3 = pt2.ExposurePlans[1];
            pf3.Rejected.Should().BeTrue();
            pf3.RejectedReason.Should().Be(Reasons.FilterComplete);
        }

        [Test]
        public void testFilterForIncompleteAllExposuresThrottled() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            ProfilePreference prefs = GetPrefs();

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);

            // Assert setup for throttling
            pp1.Object.EnableGrader.Should().BeFalse();
            prefs.ExposureThrottle.Should().Be(125);

            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            ExposurePlan ep = new ExposurePlan("abcd-1234") { Desired = 12, Acquired = 15 };
            ExposureTemplate et = new ExposureTemplate("abcd-1234", "R", "R");
            IExposure peRed = new PlanningExposure(pt.Object, ep, et);

            ep = new ExposurePlan("abcd-1234") { Desired = 12, Acquired = 15 };
            et = new ExposureTemplate("abcd-1234", "G", "G");
            IExposure peGreen = new PlanningExposure(pt.Object, ep, et);

            ep = new ExposurePlan("abcd-1234") { Desired = 12, Acquired = 10 };
            et = new ExposureTemplate("abcd-1234", "B", "B");
            IExposure peBlue = new PlanningExposure(pt.Object, ep, et);

            pt.Object.ExposurePlans = new List<IExposure>() { peRed, peGreen, peBlue };

            // Blue is not complete ...
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);
            projects = new Planner(new DateTime(2023, 12, 15, 18, 0, 0), profile, prefs, false, false).FilterForIncomplete(projects);
            projects.Count.Should().Be(1);
            projects[0].Rejected.Should().BeFalse();
            projects[0].Targets[0].Rejected.Should().BeFalse();

            ep = new ExposurePlan("abcd-1234") { Desired = 12, Acquired = 15 };
            et = new ExposureTemplate("abcd-1234", "B", "B");
            peBlue = new PlanningExposure(pt.Object, ep, et);

            pt.Object.ExposurePlans = new List<IExposure>() { peRed, peGreen, peBlue };

            // All are now complete due to throttle
            projects = new Planner(new DateTime(2023, 12, 15, 18, 0, 0), profile, prefs, false, false).FilterForIncomplete(projects);
            projects.Count.Should().Be(1);
            projects[0].Rejected.Should().BeTrue();
            projects[0].RejectedReason.Should().Be(Reasons.ProjectComplete);
            projects[0].Targets[0].Rejected.Should().BeTrue();
            projects[0].Targets[0].RejectedReason.Should().Be(Reasons.TargetComplete);
        }

        [Test]
        public void testTargetNoExposurePlans() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            pt = PlanMocks.GetMockPlanTarget("M31", TestData.M31);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);
            projects = new Planner(new DateTime(2023, 12, 17, 18, 0, 0), profile, GetPrefs(), false, false).FilterForIncomplete(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeFalse();

            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeFalse();
            IExposure pf1 = pt1.ExposurePlans[0];
            pf1.Rejected.Should().BeFalse();

            ITarget pt2 = pp.Targets[1];
            pt2.ExposurePlans.Count.Should().Be(0);
            pt2.Rejected.Should().BeTrue();
            pt2.RejectedReason.Should().Be(Reasons.TargetComplete);
        }

        [Test]
        public void testFilterForVisibilityNeverRises() {
            // Southern hemisphere location and IC1805
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.South_Mid_Lat);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            projects = new Planner(new DateTime(2023, 12, 17, 18, 0, 0), profile, GetPrefs(), false, false).FilterForVisibility(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeTrue();
            pp.RejectedReason.Should().Be(Reasons.ProjectAllTargets);
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeTrue();
            pt1.RejectedReason.Should().Be(Reasons.TargetNeverRises);
        }

        [Test]
        public void testFilterForVisibilityNotNow() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            projects = new Planner(new DateTime(2023, 6, 17, 18, 0, 0), profile, GetPrefs(), false, false).FilterForVisibility(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeTrue();
            pp.RejectedReason.Should().Be(Reasons.ProjectAllTargets);
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeTrue();
            pt1.RejectedReason.Should().Be(Reasons.TargetNotVisible);
        }

        [Test]
        public void testFilterForVisibilityVisible() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            DateTime atTime = new DateTime(2023, 12, 17, 19, 0, 0);
            projects = new Planner(atTime, profile, GetPrefs(), false, false).FilterForVisibility(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeFalse();
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeFalse();

            TimeSpan precision = TimeSpan.FromSeconds(1);
            pt1.StartTime.Should().BeCloseTo(new DateTime(2023, 12, 17, 18, 59, 54), precision);
            pt1.CulminationTime.Should().BeCloseTo(new DateTime(2023, 12, 18, 0, 5, 45), precision);
            pt1.EndTime.Should().BeCloseTo(new DateTime(2023, 12, 18, 5, 47, 59), precision);
        }

        [Test]
        public void testFilterForVisibilityInMeridianWindow() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(m => m.MeridianWindow, 30);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            projects = new Planner(new DateTime(2023, 12, 17, 23, 36, 0), profile, GetPrefs(), false, false).FilterForVisibility(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeFalse();
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeFalse();

            TimeSpan precision = TimeSpan.FromSeconds(1);
            pt1.StartTime.Should().BeCloseTo(new DateTime(2023, 12, 17, 23, 35, 54), precision);
            pt1.CulminationTime.Should().BeCloseTo(new DateTime(2023, 12, 18, 0, 5, 45), precision);
            pt1.EndTime.Should().BeCloseTo(new DateTime(2023, 12, 18, 0, 35, 45), precision);
        }

        [Test]
        public void testFilterForVisibilityWaitForMeridianWindow() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(m => m.MeridianWindow, 30);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            projects = new Planner(new DateTime(2023, 12, 17, 19, 0, 0), profile, GetPrefs(), false, false).FilterForVisibility(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeTrue();
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeTrue();
            pt1.RejectedReason.Should().Be(Reasons.TargetBeforeMeridianWindow);
        }

        [Test]
        public void testFilterForVisibilityMeridianWindowCircumpolar() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Sanikiluaq_NU);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(m => m.MeridianWindow, 30);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            projects = new Planner(new DateTime(2023, 12, 17, 20, 38, 0), profile, GetPrefs(), false, false).FilterForVisibility(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeFalse();
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeFalse();

            TimeSpan precision = TimeSpan.FromSeconds(1);
            pt1.StartTime.Should().BeCloseTo(new DateTime(2023, 12, 17, 20, 37, 55), precision);
            pt1.CulminationTime.Should().BeCloseTo(new DateTime(2023, 12, 17, 21, 7, 18), precision);
            pt1.EndTime.Should().BeCloseTo(new DateTime(2023, 12, 17, 21, 37, 18), precision);
        }

        [Test]
        public void testFilterForMaxAltitude() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(p => p.MaximumAltitude, 45);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            DateTime atTime = new DateTime(2024, 12, 18, 1, 0, 0);
            projects = new Planner(atTime, profile, GetPrefs(), false, false).FilterForVisibility(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeTrue();
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeTrue();
            pt1.RejectedReason.Should().Be(Reasons.TargetMaxAltitude);
        }

        [Test]
        public void testFilterForMoonAvoidance() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;

            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.StartTime, new DateTime(2023, 12, 25, 18, 9, 0));
            pt.SetupProperty(m => m.EndTime, new DateTime(2023, 12, 26, 5, 17, 0));

            Mock<IExposure> pe = PlanMocks.GetMockPlanExposure("L", 10, 0);
            pe.SetupProperty(f => f.MoonAvoidanceEnabled, true);
            pe.SetupProperty(f => f.MoonAvoidanceSeparation, 50);
            pe.SetupProperty(f => f.MoonAvoidanceWidth, 7);
            PlanMocks.AddMockPlanFilter(pt, pe);

            pe = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            pe.SetupProperty(f => f.MoonAvoidanceEnabled, true);
            pe.SetupProperty(f => f.MoonAvoidanceSeparation, 30);
            pe.SetupProperty(f => f.MoonAvoidanceWidth, 7);
            PlanMocks.AddMockPlanFilter(pt, pe);

            PlanMocks.AddMockPlanTarget(pp1, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);

            projects = new Planner(new DateTime(2023, 12, 25, 18, 0, 0), profile, GetPrefs(), false, false).FilterForMoonAvoidance(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);

            IProject pp = projects[0];
            pp.Name.Should().Be("pp1");
            pp.Rejected.Should().BeFalse();
            ITarget pt1 = pp.Targets[0];
            pt1.Rejected.Should().BeFalse();

            IExposure pe1 = pt1.ExposurePlans[0];
            pe1.Rejected.Should().BeTrue();
            pe1.RejectedReason.Should().Be(Reasons.FilterMoonAvoidance);

            pe1 = pt1.ExposurePlans[1];
            pe1.Rejected.Should().BeFalse();
            pe1.MoonAvoidanceScore.Should().BeApproximately(.1622, 0.001);
        }

        [Test]
        public void testFilterForTwilightCivil() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            List<IProject> projects = GetProjectForFilterTest();

            projects = new Planner(new DateTime(2024, 12, 1, 17, 0, 0), profile, GetPrefs(), false, false).FilterForTwilight(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);
            projects[0].Rejected.Should().BeTrue();
            projects[0].Targets[0].Rejected.Should().BeTrue();

            projects[0].Targets[0].ExposurePlans[0].Rejected.Should().BeTrue();
            projects[0].Targets[0].ExposurePlans[0].RejectedReason.Should().Be(Reasons.FilterTwilight);
            projects[0].Targets[0].ExposurePlans[1].Rejected.Should().BeTrue();
            projects[0].Targets[0].ExposurePlans[1].RejectedReason.Should().Be(Reasons.FilterTwilight);
            projects[0].Targets[0].ExposurePlans[2].Rejected.Should().BeTrue();
            projects[0].Targets[0].ExposurePlans[2].RejectedReason.Should().Be(Reasons.FilterTwilight);
        }

        [Test]
        public void testFilterForTwilightNautical() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            List<IProject> projects = GetProjectForFilterTest();

            projects = new Planner(new DateTime(2024, 12, 1, 18, 0, 0), profile, GetPrefs(), false, false).FilterForTwilight(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);
            projects[0].Rejected.Should().BeFalse();
            projects[0].Targets[0].Rejected.Should().BeFalse();

            projects[0].Targets[0].ExposurePlans[0].Rejected.Should().BeTrue();
            projects[0].Targets[0].ExposurePlans[0].RejectedReason.Should().Be(Reasons.FilterTwilight);
            projects[0].Targets[0].ExposurePlans[1].Rejected.Should().BeTrue();
            projects[0].Targets[0].ExposurePlans[1].RejectedReason.Should().Be(Reasons.FilterTwilight);
            projects[0].Targets[0].ExposurePlans[2].Rejected.Should().BeFalse();
        }

        [Test]
        public void testFilterForTwilightAstronomical() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            List<IProject> projects = GetProjectForFilterTest();

            projects = new Planner(new DateTime(2024, 12, 1, 18, 20, 0), profile, GetPrefs(), false, false).FilterForTwilight(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);
            projects[0].Rejected.Should().BeFalse();
            projects[0].Targets[0].Rejected.Should().BeFalse();

            projects[0].Targets[0].ExposurePlans[0].Rejected.Should().BeTrue();
            projects[0].Targets[0].ExposurePlans[0].RejectedReason.Should().Be(Reasons.FilterTwilight);
            projects[0].Targets[0].ExposurePlans[1].Rejected.Should().BeFalse();
            projects[0].Targets[0].ExposurePlans[2].Rejected.Should().BeFalse();
        }

        [Test]
        public void testFilterForTwilightNight() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            List<IProject> projects = GetProjectForFilterTest();

            projects = new Planner(new DateTime(2024, 12, 1, 19, 20, 0), profile, GetPrefs(), false, false).FilterForTwilight(projects);
            Assert.That(projects, Is.Not.Null);
            projects.Count.Should().Be(1);
            projects[0].Rejected.Should().BeFalse();
            projects[0].Targets[0].Rejected.Should().BeFalse();

            projects[0].Targets[0].ExposurePlans[0].Rejected.Should().BeFalse();
            projects[0].Targets[0].ExposurePlans[1].Rejected.Should().BeFalse();
            projects[0].Targets[0].ExposurePlans[2].Rejected.Should().BeFalse();
        }

        [Test]
        public void testTargetsReadyNow() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2023, 12, 25, 18, 0, 0);

            List<IProject> projects = new List<IProject>();
            List<ITarget> targets = new Planner(atTime, profile, GetPrefs(), false, false).GetTargetsReadyNow(projects);
            targets.Count.Should().Be(0);

            // 2 targets in future
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt1 = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt1.SetupProperty(t => t.StartTime, atTime.AddHours(1));
            Mock<IExposure> pf1 = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt1, pf1);
            PlanMocks.AddMockPlanTarget(pp1, pt1);

            Mock<ITarget> pt2 = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt2.SetupProperty(t => t.StartTime, atTime.AddHours(2));
            Mock<IExposure> pf2 = PlanMocks.GetMockPlanExposure("S2", 10, 0);
            PlanMocks.AddMockPlanFilter(pt2, pf2);
            PlanMocks.AddMockPlanTarget(pp1, pt2);

            projects = PlanMocks.ProjectsList(pp1.Object);

            targets = new Planner(atTime, profile, GetPrefs(), false, false).GetTargetsReadyNow(projects);
            targets.Count.Should().Be(0);

            // 2 targets ready now
            pt1.SetupProperty(t => t.StartTime, atTime.AddSeconds(10));
            pt2.SetupProperty(t => t.StartTime, atTime.AddSeconds(5));

            targets = new Planner(atTime, profile, GetPrefs(), false, false).GetTargetsReadyNow(projects);
            targets.Count.Should().Be(2);
        }

        [Test]
        public void testSelectTargetByScore() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2024, 12, 1, 20, 0, 0);

            List<ITarget> readyTargets = new List<ITarget>();
            Action score = () => new Planner(atTime, profile, GetPrefs(), false, false).SelectTargetByScore(readyTargets, null);
            score.Should().Throw<ArgumentException>().WithMessage("no ready targets in SelectTargetByScore");

            ITarget t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            readyTargets.Add(t1);
            ITarget selected = new Planner(atTime, profile, GetPrefs(), false, false).SelectTargetByScore(readyTargets, null);
            (t1 == selected).Should().BeTrue();

            ITarget t2 = PlanMocks.GetMockPlanTarget("T2", TestData.M31).Object;
            readyTargets.Add(t2);
            selected = new Planner(atTime, profile, GetPrefs(), false, false).SelectTargetByScore(readyTargets, GetTestScoringEngine());

            (t2 == selected).Should().BeTrue();
            t1.Rejected.Should().BeTrue();
            t1.RejectedReason.Should().Be(Reasons.TargetLowerScore);
            t2.Rejected.Should().BeFalse();
        }

        [Test]
        public void testGetNextPossibleTarget() {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2023, 12, 25, 18, 0, 0);

            // 2 targets in future
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt1 = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt1.SetupProperty(t => t.StartTime, atTime.AddHours(1));
            Mock<IExposure> pf1 = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            PlanMocks.AddMockPlanFilter(pt1, pf1);
            PlanMocks.AddMockPlanTarget(pp1, pt1);

            Mock<ITarget> pt2 = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt2.SetupProperty(t => t.StartTime, atTime.AddHours(2));
            Mock<IExposure> pf2 = PlanMocks.GetMockPlanExposure("S2", 10, 0);
            PlanMocks.AddMockPlanFilter(pt2, pf2);
            PlanMocks.AddMockPlanTarget(pp1, pt2);

            List<IProject> projects = PlanMocks.ProjectsList(pp1.Object);
            ITarget target = new Planner(atTime, profile, GetPrefs(), false, false).GetNextPossibleTarget(projects);
            target.Should().Be(pt1.Object);
            target.StartTime.Should().Be(atTime.AddHours(1));
        }

        private ProfilePreference GetPrefs(string profileId = "abcd-1234") {
            return new ProfilePreference(profileId);
        }

        private List<IProject> GetProjectForFilterTest() {
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.StartTime, new DateTime(2023, 12, 25, 18, 9, 0));
            pt.SetupProperty(m => m.EndTime, new DateTime(2023, 12, 26, 5, 17, 0));

            Mock<IExposure> pe = PlanMocks.GetMockPlanExposure("N", 10, 0);
            pe.SetupProperty(f => f.TwilightLevel, TwilightLevel.Nighttime);
            PlanMocks.AddMockPlanFilter(pt, pe);

            pe = PlanMocks.GetMockPlanExposure("A", 10, 0);
            pe.SetupProperty(f => f.TwilightLevel, TwilightLevel.Astronomical);
            PlanMocks.AddMockPlanFilter(pt, pe);

            pe = PlanMocks.GetMockPlanExposure("N", 10, 0);
            pe.SetupProperty(f => f.TwilightLevel, TwilightLevel.Nautical);
            PlanMocks.AddMockPlanFilter(pt, pe);

            PlanMocks.AddMockPlanTarget(pp1, pt);
            return PlanMocks.ProjectsList(pp1.Object);
        }

        private IScoringEngine GetTestScoringEngine() {
            Mock<IScoringEngine> mock = new Mock<IScoringEngine>();
            mock.SetupAllProperties();
            mock.Setup(p => p.ScoreTarget(It.IsAny<ITarget>())).Returns((ITarget t) => t.Name == "T1" ? .5 : .75);
            return mock.Object;
        }
    }
}