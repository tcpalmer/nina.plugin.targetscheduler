using FluentAssertions;
using NINA.Plugin.TargetScheduler.Astrometry;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Astrometry {

    [TestFixture]
    public class MaximumAltitudeClipperTest {

        [Test]
        public void testNoMaxCheck() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);

            var sut = new MaximumAltitudeClipper(targetVisibility, TestData.North_Mid_Lat, TestData.M42, sunset, sunrise, 0);
            sut.IsClipped.Should().BeFalse();
            sut.ClipInterval.Should().BeNull();
        }

        [Test]
        public void testBasic() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 17, 30, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 7, 0, 0);

            TargetVisibility targetVisibility = new TargetVisibility("M42", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);

            var sut = new MaximumAltitudeClipper(targetVisibility, TestData.North_Mid_Lat, TestData.M42, sunset, sunrise, 30);
            sut.IsClipped.Should().BeTrue();
            sut.ClipInterval.Should().NotBeNull();
        }
    }
}