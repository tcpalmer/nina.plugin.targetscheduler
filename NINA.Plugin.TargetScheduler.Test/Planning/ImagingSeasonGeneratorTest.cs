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
using System.Linq;
using System.Threading;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class ImagingSeasonGeneratorTest {

        [Test]
        public void testLongestCircularRun() {
            ImagingSeasonGenerator.LongestCircularRun(new[] { true, false, false, true, false }, false).Should().Be((1, 2));

            // Wraps around the end
            ImagingSeasonGenerator.LongestCircularRun(new[] { false, true, true, false, false }, false).Should().Be((3, 3));

            // None and all
            ImagingSeasonGenerator.LongestCircularRun(new[] { true, true, true }, false).Should().Be((0, 0));
            ImagingSeasonGenerator.LongestCircularRun(new[] { false, false, false }, false).length.Should().Be(3);
        }

        [Test]
        public void testSeasonContainingStartDate() {
            // M42 at mid-northern latitude: in season in early January, season runs roughly autumn to spring
            DateTime startDate = new DateTime(2025, 1, 5);
            List<DailyDetail> season = new ImagingSeasonGenerator(GetParameters(TestData.M42, startDate)).Generate(CancellationToken.None);

            season.Should().NotBeEmpty();
            season.First().Accepted.Should().BeTrue();
            season.Last().Accepted.Should().BeTrue();
            season.First().NightDate.Should().BeBefore(startDate).And.BeAfter(new DateTime(2024, 7, 1));
            season.Last().NightDate.Should().BeAfter(startDate).And.BeBefore(new DateTime(2025, 6, 1));

            // Consecutive nights
            for (int i = 1; i < season.Count; i++) {
                (season[i].NightDate - season[i - 1].NightDate).Days.Should().Be(1);
            }
        }

        [Test]
        public void testSeasonFollowingStartDate() {
            // M42 out of season in June: show the following season
            DateTime startDate = new DateTime(2025, 6, 15);
            List<DailyDetail> season = new ImagingSeasonGenerator(GetParameters(TestData.M42, startDate)).Generate(CancellationToken.None);

            season.Should().NotBeEmpty();
            season.First().NightDate.Should().BeAfter(startDate).And.BeBefore(new DateTime(2026, 1, 1));
            season.Last().NightDate.Should().BeAfter(new DateTime(2026, 1, 1)).And.BeBefore(new DateTime(2026, 6, 1));
        }

        [Test]
        public void testCircumpolar() {
            DateTime startDate = new DateTime(2025, 1, 1);
            List<DailyDetail> season = new ImagingSeasonGenerator(GetParameters(TestData.STAR_NORTH_CIRCP, startDate)).Generate(CancellationToken.None);

            season.Count.Should().BeGreaterThan(355);
            season.First().NightDate.Should().Be(startDate);
        }

        [Test]
        public void testNeverRises() {
            List<DailyDetail> season = new ImagingSeasonGenerator(GetParameters(TestData.STAR_SOUTH_CIRCP, new DateTime(2025, 1, 1))).Generate(CancellationToken.None);
            season.Should().BeEmpty();
        }

        private TargetPlanningParameters GetParameters(Coordinates coordinates, DateTime startDate) {
            return new TargetPlanningParameters {
                Profile = GetProfile(TestData.North_Mid_Lat),
                TargetName = "T1",
                Coordinates = coordinates,
                StartDate = startDate,
                PlanDays = 3,
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
