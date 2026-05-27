using FluentAssertions;
using FluentAssertions.Extensions;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Planning;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Astrometry {

    [TestFixture]
    public class MaximumAltitudeClipperTest {

        [SetUp]
        public void SetUp() {
            TargetVisibilityCache.Clear();
        }

        [Test]
        public void testNoMaxCheck() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Upper_Lat, TestData.M42, dateTime, sunset, sunrise, 0, 60);
            MaximumAltitudeClipper sut = targetVisibility.MaxAltitudeClipper;
            TimeInterval clipped = sut.Clip(sunset, sunrise);
            clipped.StartTime.Should().Be(sunset);
            clipped.EndTime.Should().Be(sunrise);

            sut.NextSafeStart(sunset, sunrise).Should().Be(sunset);
        }

        [Test]
        public void testTargetAllBefore() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Upper_Lat, TestData.M42, dateTime, sunset, sunrise, 30, 60);
            MaximumAltitudeClipper sut = targetVisibility.MaxAltitudeClipper;
            DateTime start = new DateTime(2024, 12, 1, 20, 0, 0);
            DateTime end = new DateTime(2024, 12, 1, 22, 0, 0);
            TimeInterval clipped = sut.Clip(start, end);
            clipped.StartTime.Should().Be(start);
            clipped.EndTime.Should().Be(end);

            sut.NextSafeStart(start, end).Should().Be(start);
        }

        [Test]
        public void testTargetEndInSpan() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            DateTime maxStart = new DateTime(2024, 12, 1, 23, 0, 0);
            DateTime maxEnd = new DateTime(2024, 12, 2, 3, 13, 0);

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Upper_Lat, TestData.M42, dateTime, sunset, sunrise, 30, 60);
            MaximumAltitudeClipper sut = targetVisibility.MaxAltitudeClipper;
            DateTime start = new DateTime(2024, 12, 1, 20, 0, 0);
            DateTime end = new DateTime(2024, 12, 1, 23, 30, 0);
            TimeInterval clipped = sut.Clip(start, end);
            clipped.StartTime.Should().Be(start);
            clipped.EndTime.Should().Be(maxStart);

            sut.NextSafeStart(start, end).Should().Be(start);
        }

        [Test]
        public void testTargetAllInSpan() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            DateTime maxEnd = new DateTime(2024, 12, 2, 3, 13, 0);

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Upper_Lat, TestData.M42, dateTime, sunset, sunrise, 30, 60);
            MaximumAltitudeClipper sut = targetVisibility.MaxAltitudeClipper;
            DateTime start = new DateTime(2024, 12, 1, 23, 10, 0);
            DateTime end = new DateTime(2024, 12, 1, 23, 50, 0);
            TimeInterval clipped = sut.Clip(start, end);
            clipped.Should().BeNull();

            sut.NextSafeStart(start, end).Should().BeCloseTo(maxEnd, 1.Seconds());
        }

        [Test]
        public void testTargetStartInSpan() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            DateTime maxStart = new DateTime(2024, 12, 1, 23, 0, 0);
            DateTime maxEnd = new DateTime(2024, 12, 2, 3, 13, 0);

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Upper_Lat, TestData.M42, dateTime, sunset, sunrise, 30, 60);
            MaximumAltitudeClipper sut = targetVisibility.MaxAltitudeClipper;
            DateTime start = new DateTime(2024, 12, 1, 23, 10, 0);
            DateTime end = new DateTime(2024, 12, 2, 4, 0, 0);
            TimeInterval clipped = sut.Clip(start, end);
            clipped.StartTime.Should().Be(maxEnd);
            clipped.EndTime.Should().Be(end);

            sut.NextSafeStart(start, end).Should().Be(maxEnd);
        }

        [Test]
        public void testTargetAllAfter() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            // 2024-12-01 23:00:00 - 2024-12-02 03:13:00

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Upper_Lat, TestData.M42, dateTime, sunset, sunrise, 30, 60);
            MaximumAltitudeClipper sut = targetVisibility.MaxAltitudeClipper;
            DateTime start = new DateTime(2024, 12, 2, 4, 0, 0);
            DateTime end = new DateTime(2024, 12, 2, 5, 0, 0);
            TimeInterval clipped = sut.Clip(start, end);
            clipped.StartTime.Should().Be(start);
            clipped.EndTime.Should().Be(end);

            sut.NextSafeStart(start, end).Should().Be(start);
        }
    }
}