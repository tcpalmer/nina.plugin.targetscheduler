using FluentAssertions;
using Moq;
using NINA.Astrometry;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class PreviousTargetExpertTest {

        [SetUp]
        public void Setup() {
            TargetEditGuard.Instance.Clear();
        }

        [Test]
        public void testCanContinue() {
            ObserverInfo observerInfo = TestData.Pittsboro_NC;
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(observerInfo);
            IWeatherDataMediator weatherData = PlanMocks.GetWeatherDataMediator(false, 0);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2023, 12, 17, 20, 0, 0);

            PreviousTargetExpert sut = new PreviousTargetExpert(profile, GetPrefs(), false, observerInfo);

            sut.CanContinue(atTime, weatherData, null).Should().BeFalse();

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.SetupProperty(p => p.EnableGrader, true);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.EndTime, atTime.AddHours(12));
            var exposureSelector = new RepeatUntilDoneExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(p => p.IsPreview, true);
            pt.SetupProperty(p => p.MinimumTimeSpanEnd, atTime.AddMinutes(1));
            pt.SetupProperty(p => p.BonusTimeSpanEnd, atTime.AddMinutes(1));
            pt.SetupProperty(p => p.ExposureSelector, exposureSelector);

            IExposure exposure = new PlanningExposure();
            exposure.PlanTarget = pt.Object;
            exposure.ExposureLength = 180;
            exposure.Desired = 10;
            exposure.Accepted = 0;
            exposure.Acquired = 0;

            pt.Object.AllExposurePlans.Add(exposure);
            PlanMocks.AddMockPlanTarget(pp, pt);

            // Next exposure exceeds minimum time span
            ITarget target = pt.Object;
            sut.CanContinue(atTime, weatherData, target).Should().BeFalse();
            target.BonusTimeSpanEnd.Should().BeSameDateAs(target.MinimumTimeSpanEnd);

            // Next exposure will now fit in minimum time span
            exposure.ExposureLength = 30;
            target.SelectedExposure.Should().Be(null);
            sut.CanContinue(atTime, weatherData, target).Should().BeTrue();
            target.BonusTimeSpanEnd.Should().BeSameDateAs(target.MinimumTimeSpanEnd);
            target.SelectedExposure.Should().Be(exposure);

            // Exposure plan is now complete
            exposure.Acquired = 20;
            sut.CanContinue(atTime, weatherData, target).Should().BeFalse();
            target.BonusTimeSpanEnd.Should().BeSameDateAs(target.MinimumTimeSpanEnd);
            target.ExposurePlans.Count.Should().Be(0);
            target.CompletedExposurePlans.Count.Should().Be(1);
        }

        [Test]
        public void testPreviousTargetCanContinueMoon() {
            ObserverInfo observerInfo = TestData.Pittsboro_NC;
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(observerInfo);
            IWeatherDataMediator weatherData = PlanMocks.GetWeatherDataMediator(false, 0);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2025, 2, 16, 22, 30, 0);

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.SetupProperty(p => p.EnableGrader, true);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.EndTime, atTime.AddHours(12));
            var exposureSelector = new RepeatUntilDoneExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(p => p.IsPreview, true);
            pt.SetupProperty(p => p.MinimumTimeSpanEnd, atTime.AddHours(1));
            pt.SetupProperty(p => p.BonusTimeSpanEnd, atTime.AddHours(1));
            pt.SetupProperty(p => p.ExposureSelector, exposureSelector);

            IExposure exposure = new PlanningExposure();
            exposure.PlanTarget = pt.Object;
            exposure.ExposureLength = 180;
            exposure.Desired = 10;
            exposure.Accepted = 0;
            exposure.Acquired = 0;
            exposure.MoonAvoidanceEnabled = true;
            exposure.MoonRelaxMaxAltitude = 5;
            exposure.MoonDownEnabled = true;

            pt.Object.AllExposurePlans.Add(exposure);
            PlanMocks.AddMockPlanTarget(pp, pt);

            PreviousTargetExpert sut = new PreviousTargetExpert(profile, GetPrefs(), true, observerInfo);
            ITarget target = pt.Object;

            // At 22:30, the moon is below 5°
            sut.CanContinue(atTime, weatherData, target).Should().BeTrue();
            target.BonusTimeSpanEnd.Should().BeSameDateAs(target.MinimumTimeSpanEnd);

            // But by 22:40, it's above 5°
            sut.CanContinue(atTime.AddMinutes(10), weatherData, target).Should().BeFalse();
            target.BonusTimeSpanEnd.Should().BeSameDateAs(target.MinimumTimeSpanEnd);
            exposure.Rejected.Should().BeTrue();
            exposure.RejectedReason.Should().Be(Reasons.FilterMoonAvoidance);
        }

        [Test]
        public void testPreviousTargetCanContinueHumidity() {
            ObserverInfo observerInfo = TestData.Pittsboro_NC;
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(observerInfo);
            IWeatherDataMediator weatherData = PlanMocks.GetWeatherDataMediator(true, 10);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2025, 2, 16, 22, 30, 0);

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.SetupProperty(p => p.EnableGrader, true);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.EndTime, atTime.AddHours(12));
            var exposureSelector = new RepeatUntilDoneExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(p => p.IsPreview, true);
            pt.SetupProperty(p => p.MinimumTimeSpanEnd, atTime.AddHours(1));
            pt.SetupProperty(p => p.BonusTimeSpanEnd, atTime.AddHours(1));
            pt.SetupProperty(p => p.ExposureSelector, exposureSelector);

            IExposure exposure = new PlanningExposure();
            exposure.PlanTarget = pt.Object;
            exposure.ExposureLength = 180;
            exposure.Desired = 10;
            exposure.Accepted = 0;
            exposure.Acquired = 0;
            exposure.MoonAvoidanceEnabled = false;
            exposure.MaximumHumidity = 60;

            pt.Object.AllExposurePlans.Add(exposure);
            PlanMocks.AddMockPlanTarget(pp, pt);

            PreviousTargetExpert sut = new PreviousTargetExpert(profile, GetPrefs(), true, observerInfo);
            ITarget target = pt.Object;

            sut.CanContinue(atTime, weatherData, target).Should().BeTrue();

            weatherData = PlanMocks.GetWeatherDataMediator(true, 70);
            sut.CanContinue(atTime, weatherData, target).Should().BeFalse();
        }

        [Test]
        public void testPreviousTargetCanContinueOverrideExposureOrder() {
            ObserverInfo observerInfo = TestData.Pittsboro_NC;
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(observerInfo);
            IWeatherDataMediator weatherData = PlanMocks.GetWeatherDataMediator(false, 0);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2025, 2, 16, 22, 30, 0);

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.SetupProperty(p => p.EnableGrader, true);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.EndTime, atTime.AddHours(12));
            var exposureSelector = new RepeatUntilDoneExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(p => p.IsPreview, true);
            pt.SetupProperty(p => p.MinimumTimeSpanEnd, atTime.AddHours(1));
            pt.SetupProperty(p => p.BonusTimeSpanEnd, atTime.AddHours(1));

            Mock<IExposure> Lpf = PlanMocks.GetMockPlanExposure("L", 10, 0, 30, 1);
            Mock<IExposure> Rpf = PlanMocks.GetMockPlanExposure("R", 10, 0, 30, 2);
            Mock<IExposure> Gpf = PlanMocks.GetMockPlanExposure("G", 10, 0, 30, 3);
            Mock<IExposure> Bpf = PlanMocks.GetMockPlanExposure("B", 10, 0, 30, 4);

            PlanMocks.AddMockPlanFilter(pt, Lpf);
            PlanMocks.AddMockPlanFilter(pt, Rpf);
            PlanMocks.AddMockPlanFilter(pt, Gpf);
            PlanMocks.AddMockPlanFilter(pt, Bpf);

            List<OverrideExposureOrderItem> oeos = new List<OverrideExposureOrderItem>();
            oeos.Add(new OverrideExposureOrderItem(101, 1, OverrideExposureOrderAction.Exposure, 0));
            oeos.Add(new OverrideExposureOrderItem(101, 2, OverrideExposureOrderAction.Exposure, 0));
            oeos.Add(new OverrideExposureOrderItem(101, 3, OverrideExposureOrderAction.Dither, -1));
            oeos.Add(new OverrideExposureOrderItem(101, 4, OverrideExposureOrderAction.Exposure, 1));
            oeos.Add(new OverrideExposureOrderItem(101, 5, OverrideExposureOrderAction.Exposure, 1));
            oeos.Add(new OverrideExposureOrderItem(101, 6, OverrideExposureOrderAction.Dither, -1));

            Target dbTarget = new Target();
            dbTarget.OverrideExposureOrders = oeos;

            var selector = new OverrideOrderExposureSelector(pp.Object, pt.Object, dbTarget);
            pt.SetupProperty(p => p.ExposureSelector, selector);

            PreviousTargetExpert sut = new PreviousTargetExpert(profile, GetPrefs(), true, observerInfo);
            ITarget target = pt.Object;

            sut.CanContinue(atTime, weatherData, target).Should().BeTrue();
            target.BonusTimeSpanEnd.Should().BeSameDateAs(target.MinimumTimeSpanEnd);
            target.SelectedExposure.Should().Be(Lpf.Object);
        }

        [Test]
        public void testPreviousTargetCanContinuePlan() {
            ObserverInfo observerInfo = TestData.Pittsboro_NC;
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(observerInfo);
            IProfile profile = profileMock.Object.ActiveProfile;
            IWeatherDataMediator weatherData = PlanMocks.GetWeatherDataMediator(false, 0);
            DateTime atTime = new DateTime(2023, 12, 17, 20, 0, 0);

            Planner sut = new Planner(atTime, profile, GetPrefs(), weatherData, false, true, new List<IProject>());

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.SetupProperty(p => p.EnableGrader, true);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.EndTime, atTime.AddHours(12));
            var exposureSelector = new RepeatUntilDoneExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(p => p.IsPreview, true);
            pt.SetupProperty(p => p.MinimumTimeSpanEnd, atTime.AddMinutes(1));
            pt.SetupProperty(p => p.BonusTimeSpanEnd, atTime.AddMinutes(1));
            pt.SetupProperty(p => p.ExposureSelector, exposureSelector);

            IExposure exposure = new PlanningExposure();
            exposure.PlanTarget = pt.Object;
            exposure.FilterName = "f123";
            exposure.ExposureLength = 30;
            exposure.Desired = 10;
            exposure.Accepted = 0;
            exposure.Acquired = 0;

            pt.Object.AllExposurePlans.Add(exposure);
            PlanMocks.AddMockPlanTarget(pp, pt);

            ITarget target = pt.Object;
            var plan = sut.GetPlan(target);
            plan.PlanTarget.Should().Be(target);
            plan.IsWait.Should().BeFalse();
            plan.StartTime.Should().Be(atTime);
            plan.EndTime.Should().Be(atTime.AddSeconds(30));
            plan.PlanTarget.BonusTimeSpanEnd.Should().BeSameDateAs(target.MinimumTimeSpanEnd);
            plan.PlanTarget.SelectedExposure.FilterName.Should().Be("f123");
        }

        [Test]
        public void testPreviousTargetCanContinueBonusTime() {
            ObserverInfo observerInfo = TestData.Pittsboro_NC;
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(observerInfo);
            IWeatherDataMediator weatherData = PlanMocks.GetWeatherDataMediator(false, 0);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2025, 3, 12, 0, 53, 0);

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            var exposureSelector = new RepeatUntilDoneExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(p => p.IsPreview, true);
            pt.SetupProperty(p => p.EndTime, atTime.Date.AddMinutes(75));
            pt.SetupProperty(p => p.MinimumTimeSpanEnd, atTime.Date.AddMinutes(55));
            pt.SetupProperty(p => p.BonusTimeSpanEnd, atTime.Date.AddMinutes(55));
            pt.SetupProperty(p => p.ExposureSelector, exposureSelector);
            Mock<IExposure> pf = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            pf.SetupProperty(p => p.ExposureLength, 180);
            PlanMocks.AddMockPlanFilter(pt, pf);
            PlanMocks.AddMockPlanTarget(pp, pt);
            List<IProject> projects = PlanMocks.ProjectsList(pp.Object);

            PreviousTargetExpert sut = new PreviousTargetExpert(profile, GetPrefs(), true, observerInfo);

            // Should trigger the special case of allowing the target to continue past
            // regular minimum since it's nearing the end of visibility.
            ITarget target = projects[0].Targets[0];
            sut.CanContinue(atTime, weatherData, target).Should().BeTrue();
            target.BonusTimeSpanEnd.Should().BeSameDateAs(target.EndTime);
        }

        [Test]
        public void testPreviousTargetCanContinueTwilight() {
            ObserverInfo observerInfo = TestData.Pittsboro_NC;
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(observerInfo);
            IWeatherDataMediator weatherData = PlanMocks.GetWeatherDataMediator(false, 0);
            IProfile profile = profileMock.Object.ActiveProfile;
            DateTime atTime = new DateTime(2025, 2, 1, 19, 0, 0); // astro twilight

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.SetupProperty(p => p.EnableGrader, true);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("M42", TestData.M42);
            pt.SetupProperty(m => m.EndTime, atTime.AddHours(12));
            var exposureSelector = new RepeatUntilDoneExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(p => p.IsPreview, true);
            pt.SetupProperty(p => p.MinimumTimeSpanEnd, atTime.AddMinutes(30));
            pt.SetupProperty(p => p.BonusTimeSpanEnd, atTime.AddMinutes(30));
            pt.SetupProperty(p => p.ExposureSelector, exposureSelector);

            Mock<IExposure> HaExpMock = PlanMocks.GetMockPlanExposure("Ha", 1, 0, 30, 1);
            HaExpMock.SetupProperty(m => m.TwilightLevel, TwilightLevel.Astronomical);
            Mock<IExposure> LumExpMock = PlanMocks.GetMockPlanExposure("Lum", 2, 0, 30, 1);
            LumExpMock.SetupProperty(m => m.TwilightLevel, TwilightLevel.Nighttime);

            PlanMocks.AddMockPlanFilter(pt, HaExpMock);
            PlanMocks.AddMockPlanFilter(pt, LumExpMock);

            PreviousTargetExpert sut = new PreviousTargetExpert(profile, GetPrefs(), true, observerInfo);
            ITarget target = pt.Object;

            // In astro twilight -> Ha is OK
            sut.CanContinue(atTime, weatherData, target).Should().BeTrue();
            target.SelectedExposure.FilterName.Should().Be("Ha");

            // Ha now done but still in astro twilight -> Lum not OK
            target.AllExposurePlans.RemoveAt(0);
            target.ExposurePlans.RemoveAt(0);
            sut.CanContinue(atTime, weatherData, target).Should().BeFalse();

            // Advance time to nighttime -> Lum now OK
            LumExpMock.Object.Rejected = false;
            LumExpMock.Object.RejectedReason = null;
            sut.CanContinue(atTime.AddMinutes(20), weatherData, target).Should().BeTrue();
            target.SelectedExposure.FilterName.Should().Be("Lum");
        }

        private ProfilePreference GetPrefs(string profileId = "abcd-1234") {
            return new ProfilePreference(profileId);
        }
    }
}