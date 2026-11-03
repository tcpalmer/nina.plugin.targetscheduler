using FluentAssertions;
using Moq;
using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.Planning;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class DailyDetailsGeneratorTest {

        [Test]
        public void testVisibleWinterNights() {
            TargetPlanningParameters parameters = GetParameters(TestData.M42, new DateTime(2025, 1, 5), 3);
            List<DailyDetail> details = new DailyDetailsGenerator(parameters).Generate(CancellationToken.None);

            details.Count.Should().Be(3);
            for (int i = 0; i < details.Count; i++) {
                DailyDetail detail = details[i];
                detail.NightDate.Should().Be(new DateTime(2025, 1, 5).AddDays(i));
                detail.Accepted.Should().BeTrue();
                detail.HasWindow.Should().BeTrue();
                detail.ImagingMinutes.Should().BeGreaterThan(300);
                detail.TransitTime.Should().BeAfter(detail.StartImagingTime).And.BeBefore(detail.EndImagingTime);
                detail.StartLimitingFactor.Should().Be("twilight");
                detail.MoonAvoidanceSeparation.Should().BeNull();
            }
        }

        [Test]
        public void testNotVisibleSummer() {
            TargetPlanningParameters parameters = GetParameters(TestData.M42, new DateTime(2025, 6, 20), 1);
            DailyDetail detail = new DailyDetailsGenerator(parameters).Generate(CancellationToken.None)[0];

            detail.Accepted.Should().BeFalse();
            detail.HasWindow.Should().BeFalse();
            detail.ImagingMinutes.Should().Be(0);
            detail.RejectedReason.Should().Be(DailyDetailsGenerator.REJECT_NOT_VISIBLE);
        }

        [Test]
        public void testNotEnoughTime() {
            TargetPlanningParameters parameters = GetParameters(TestData.M42, new DateTime(2025, 1, 5), 1);
            parameters.MinimumTime = 720;
            DailyDetail detail = new DailyDetailsGenerator(parameters).Generate(CancellationToken.None)[0];

            detail.Accepted.Should().BeFalse();
            detail.HasWindow.Should().BeTrue();
            detail.ImagingMinutes.Should().BeLessThan(720);
            detail.RejectedReason.Should().Be(DailyDetailsGenerator.REJECT_MINIMUM_TIME);
        }

        [Test]
        public void testMeridianWindow() {
            TargetPlanningParameters parameters = GetParameters(TestData.M42, new DateTime(2025, 1, 5), 1);
            parameters.MeridianWindow = 60;
            DailyDetail detail = new DailyDetailsGenerator(parameters).Generate(CancellationToken.None)[0];

            detail.Accepted.Should().BeTrue();
            detail.ImagingMinutes.Should().BeInRange(118, 121);
            detail.StartLimitingFactor.Should().Be("meridian window");
            detail.EndLimitingFactor.Should().Be("meridian window");
        }

        [Test]
        public void testMoonAvoidance() {
            // Full moon 2025-01-13
            TargetPlanningParameters parameters = GetParameters(TestData.M42, new DateTime(2025, 1, 13), 1);
            parameters.MoonAvoidanceEnabled = true;
            parameters.MoonAvoidanceSeparation = 120;
            parameters.MoonAvoidanceWidth = 14;
            DailyDetail detail = new DailyDetailsGenerator(parameters).Generate(CancellationToken.None)[0];

            detail.Accepted.Should().BeFalse();
            detail.HasWindow.Should().BeTrue();
            detail.MoonRejected.Should().BeTrue();
            detail.RejectedReason.Should().Be(DailyDetailsGenerator.REJECT_MOON_AVOIDANCE);
            detail.MoonIllumination.Should().BeGreaterThan(0.95);
            detail.MoonAvoidanceSeparation.Should().BeGreaterThan(detail.MoonSeparation);
        }

        private TargetPlanningParameters GetParameters(Coordinates coordinates, DateTime startDate, int planDays) {
            return new TargetPlanningParameters {
                Profile = GetProfile(TestData.North_Mid_Lat),
                TargetName = "T1",
                Coordinates = coordinates,
                StartDate = startDate,
                PlanDays = planDays,
                MinimumTime = 30,
                TwilightLevel = TwilightLevel.Nighttime,
                MoonAvoidanceSeparation = 60,
                MoonAvoidanceWidth = 7,
                MoonRelaxMinAltitude = -15,
                MoonRelaxMaxAltitude = 5
            };
        }

        private IProfile GetProfile(ObserverInfo location) {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(location);
            profileMock.SetupProperty(m => m.ActiveProfile.MeridianFlipSettings.PauseTimeBeforeMeridian, 0);
            return profileMock.Object.ActiveProfile;
        }
    }
}
