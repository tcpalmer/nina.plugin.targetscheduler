using FluentAssertions;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Test.Planning;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Astrometry {

    [TestFixture]
    public class TwilightCircumstancesTest {

        [Test]
        public void testNorthMid() {
            DateTime dateTime = new DateTime(2024, 12, 1, 12, 0, 0);
            DateTime date = dateTime.Date;

            var sut = new TwilightCircumstances(TestData.North_Mid_Lat, dateTime);

            Assertions.AssertTime(sut.CivilTwilightStart, date, 17, 4, 19);
            Assertions.AssertTime(sut.NauticalTwilightStart, date, 17, 33, 31);
            Assertions.AssertTime(sut.AstronomicalTwilightStart, date, 18, 4, 39);
            Assertions.AssertTime(sut.NighttimeStart, date, 18, 35, 10);
            date = date.AddDays(1);
            Assertions.AssertTime(sut.NighttimeEnd, date, 5, 35, 55);
            Assertions.AssertTime(sut.AstronomicalTwilightEnd, date, 6, 6, 23);
            Assertions.AssertTime(sut.NauticalTwilightEnd, date, 6, 37, 39);
            Assertions.AssertTime(sut.CivilTwilightEnd, date, 7, 6, 57);

            sut.HasCivilTwilight().Should().BeTrue();
            sut.HasNauticalTwilight().Should().BeTrue();
            sut.HasAstronomicalTwilight().Should().BeTrue();
            sut.HasNighttime().Should().BeTrue();

            sut.GetTwilightSpan(TwilightLevel.Civil).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Nautical).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Astronomical).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Nighttime).Should().NotBeNull();
        }

        [Test]
        public void testSouthMid() {
            DateTime dateTime = new DateTime(2024, 6, 1, 12, 0, 0);
            DateTime date = dateTime.Date;

            var sut = new TwilightCircumstances(TestData.South_Mid_Lat, dateTime);

            Assertions.AssertTime(sut.CivilTwilightStart, date, 18, 16, 1);
            Assertions.AssertTime(sut.NauticalTwilightStart, date, 18, 45, 14);
            Assertions.AssertTime(sut.AstronomicalTwilightStart, date, 19, 16, 40);
            Assertions.AssertTime(sut.NighttimeStart, date, 19, 47, 16);
            date = date.AddDays(1);
            Assertions.AssertTime(sut.NighttimeEnd, date, 6, 48, 58);
            Assertions.AssertTime(sut.AstronomicalTwilightEnd, date, 7, 19, 41);
            Assertions.AssertTime(sut.NauticalTwilightEnd, date, 7, 51, 2);
            Assertions.AssertTime(sut.CivilTwilightEnd, date, 8, 20, 4);

            sut.HasCivilTwilight().Should().BeTrue();
            sut.HasNauticalTwilight().Should().BeTrue();
            sut.HasAstronomicalTwilight().Should().BeTrue();
            sut.HasNighttime().Should().BeTrue();

            sut.GetTwilightSpan(TwilightLevel.Civil).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Nautical).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Astronomical).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Nighttime).Should().NotBeNull();
        }

        [Test]
        public void testAbovePolarCircleSummer() {
            DateTime dateTime = new DateTime(2024, 6, 21, 12, 0, 0);

            // Sun doesn't set on the summer solstice ...
            var sut = new TwilightCircumstances(TestData.North_Artic, dateTime);

            sut.CivilTwilightStart.Should().BeNull();
            sut.CivilTwilightEnd.Should().BeNull();
            sut.NauticalTwilightStart.Should().BeNull();
            sut.NauticalTwilightEnd.Should().BeNull();
            sut.AstronomicalTwilightStart.Should().BeNull();
            sut.AstronomicalTwilightEnd.Should().BeNull();
            sut.NighttimeStart.Should().BeNull();
            sut.NighttimeEnd.Should().BeNull();

            sut.HasCivilTwilight().Should().BeFalse();
            sut.HasNauticalTwilight().Should().BeFalse();
            sut.HasAstronomicalTwilight().Should().BeFalse();
            sut.HasNighttime().Should().BeFalse();

            sut.GetTwilightSpan(TwilightLevel.Civil).Should().BeNull();
            sut.GetTwilightSpan(TwilightLevel.Nautical).Should().BeNull();
            sut.GetTwilightSpan(TwilightLevel.Astronomical).Should().BeNull();
            sut.GetTwilightSpan(TwilightLevel.Nighttime).Should().BeNull();
        }

        [Test]
        public void testAbovePolarCircleWinter() {
            DateTime dateTime = new DateTime(2024, 12, 21, 12, 0, 0);
            DateTime date = dateTime.Date;

            var sut = new TwilightCircumstances(TestData.North_Artic, dateTime);

            Assertions.AssertTime(sut.CivilTwilightStart, date, 13, 4, 2);
            Assertions.AssertTime(sut.NauticalTwilightStart, date, 15, 15, 27);
            Assertions.AssertTime(sut.AstronomicalTwilightStart, date, 16, 36, 48);
            Assertions.AssertTime(sut.NighttimeStart, date, 17, 44, 53);
            date = date.AddDays(1);
            Assertions.AssertTime(sut.NighttimeEnd, date, 6, 52, 28);
            Assertions.AssertTime(sut.AstronomicalTwilightEnd, date, 8, 0, 15);
            Assertions.AssertTime(sut.NauticalTwilightEnd, date, 9, 22, 5);
            Assertions.AssertTime(sut.CivilTwilightEnd, date, 11, 33, 20);

            sut.HasCivilTwilight().Should().BeTrue();
            sut.HasNauticalTwilight().Should().BeTrue();
            sut.HasAstronomicalTwilight().Should().BeTrue();
            sut.HasNighttime().Should().BeTrue();

            sut.GetTwilightSpan(TwilightLevel.Civil).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Nautical).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Astronomical).Should().NotBeNull();
            sut.GetTwilightSpan(TwilightLevel.Nighttime).Should().NotBeNull();
        }

        [Test]
        public void testGetCurrentTwilightLevel() {
            DateTime dateTime = new DateTime(2024, 12, 1, 12, 0, 0);
            DateTime date = dateTime.Date;

            var sut = new TwilightCircumstances(TestData.North_Mid_Lat, dateTime);

            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 17, 4, 19)).Should().BeNull();
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 7, 6, 58)).Should().BeNull();

            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 17, 4, 20)).Should().Be(TwilightLevel.Civil);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 17, 33, 31)).Should().Be(TwilightLevel.Civil);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 7, 6, 57)).Should().Be(TwilightLevel.Civil);

            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 17, 33, 32)).Should().Be(TwilightLevel.Nautical);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 6, 37, 39)).Should().Be(TwilightLevel.Nautical);

            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 18, 4, 40)).Should().Be(TwilightLevel.Astronomical);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 6, 6, 23)).Should().Be(TwilightLevel.Astronomical);

            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 18, 35, 11)).Should().Be(TwilightLevel.Nighttime);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 5, 35, 55)).Should().Be(TwilightLevel.Nighttime);

            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 17, 15, 0)).Should().Be(TwilightLevel.Civil);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 18, 0, 0)).Should().Be(TwilightLevel.Nautical);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 1, 18, 15, 0)).Should().Be(TwilightLevel.Astronomical);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 0, 0, 0)).Should().Be(TwilightLevel.Nighttime);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 6, 0, 0)).Should().Be(TwilightLevel.Astronomical);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 6, 30, 0)).Should().Be(TwilightLevel.Nautical);
            sut.GetCurrentTwilightLevel(new DateTime(2024, 12, 2, 7, 0, 0)).Should().Be(TwilightLevel.Civil);
        }

        [Test]
        public void testCheckTwilightWithOffset() {
            DateTime dateTime = new DateTime(2025, 7, 9, 12, 0, 0);
            DateTime date = dateTime.Date;

            var sut = new TwilightCircumstances(TestData.North_Mid_Lat, dateTime);

            DateTime now = new DateTime(2025, 7, 9, 22, 0, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nighttime, -10).Should().BeFalse(); // at 10:00
            sut.CheckTwilightWithOffset(now.AddMinutes(10), TwilightLevel.Nighttime, -10).Should().BeTrue(); // at 10:10

            now = new DateTime(2025, 7, 10, 4, 40, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nighttime, -10).Should().BeFalse(); // at 4:40
            sut.CheckTwilightWithOffset(now.AddMinutes(-10), TwilightLevel.Nighttime, -10).Should().BeTrue(); // at 4:30

            now = new DateTime(2025, 7, 9, 21, 20, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Astronomical, -10).Should().BeFalse(); // at 9:20
            sut.CheckTwilightWithOffset(now.AddMinutes(10), TwilightLevel.Astronomical, -10).Should().BeTrue(); // at 9:30

            now = new DateTime(2025, 7, 10, 5, 20, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Astronomical, -10).Should().BeFalse(); // at 5:20
            sut.CheckTwilightWithOffset(now.AddMinutes(-10), TwilightLevel.Astronomical, -10).Should().BeTrue(); // at 5:10

            now = new DateTime(2025, 7, 9, 20, 50, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nautical, -10).Should().BeFalse(); // at 8:50
            sut.CheckTwilightWithOffset(now.AddMinutes(10), TwilightLevel.Nautical, -10).Should().BeTrue(); // at 9:00

            now = new DateTime(2025, 7, 10, 5, 50, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nautical, -10).Should().BeFalse(); // at 5:50
            sut.CheckTwilightWithOffset(now.AddMinutes(-10), TwilightLevel.Nautical, -10).Should().BeTrue(); // at 5:40

            now = new DateTime(2025, 7, 9, 20, 20, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Civil, -10).Should().BeFalse(); // at 8:20
            sut.CheckTwilightWithOffset(now.AddMinutes(10), TwilightLevel.Civil, -10).Should().BeTrue(); // at 8:30

            now = new DateTime(2025, 7, 10, 6, 21, 0);
            sut.CheckTwilightWithOffset(now, TwilightLevel.Civil, -10).Should().BeFalse(); // at 6:21
            sut.CheckTwilightWithOffset(now.AddMinutes(-10), TwilightLevel.Civil, -10).Should().BeTrue(); // at 6:11

            now = new DateTime(2025, 7, 9, 20, 32, 20); // civil start
            sut.CheckTwilightWithOffset(now, TwilightLevel.Civil, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Civil, -1).Should().BeTrue();

            now = new DateTime(2025, 7, 9, 21, 3, 13); // nautical start
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nautical, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nautical, -1).Should().BeTrue();

            now = new DateTime(2025, 7, 9, 21, 39, 27); // astro start
            sut.CheckTwilightWithOffset(now, TwilightLevel.Astronomical, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Astronomical, -1).Should().BeTrue();

            now = new DateTime(2025, 7, 9, 22, 18, 30); // night start
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nighttime, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nighttime, -1).Should().BeTrue();

            now = new DateTime(2025, 7, 10, 4, 23, 55); // night end
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nighttime, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nighttime, -1).Should().BeTrue();

            now = new DateTime(2025, 7, 10, 5, 3, 45); // astro end
            sut.CheckTwilightWithOffset(now, TwilightLevel.Astronomical, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Astronomical, -1).Should().BeTrue();

            now = new DateTime(2025, 7, 10, 5, 39, 58); // nautical end
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nautical, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Nautical, -1).Should().BeTrue();

            now = new DateTime(2025, 7, 10, 6, 10, 26); // civil end
            sut.CheckTwilightWithOffset(now, TwilightLevel.Civil, 1).Should().BeFalse();
            sut.CheckTwilightWithOffset(now, TwilightLevel.Civil, -1).Should().BeTrue();
        }
    }
}