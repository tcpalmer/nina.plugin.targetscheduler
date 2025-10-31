using FluentAssertions;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Planning;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Astrometry {

    [TestFixture]
    public class TargetVisibilityTest {

        [SetUp]
        public void SetUp() {
            TargetVisibilityCache.Clear();
        }

        [Test]
        public void testBasic() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            TwilightCircumstances tc = new TwilightCircumstances(TestData.North_Mid_Lat, dateTime.Date);
            DateTime? sunset = tc.CivilTwilightStart;
            DateTime? sunrise = tc.CivilTwilightEnd;

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);

            sut.TargetName.Should().Be("T1");
            sut.TargetId.Should().Be(1);
            sut.ImagingDate.Should().Be(dateTime);
            sut.TransitTime.Should().BeCloseTo(new DateTime(2024, 12, 2, 1, 5, 8), TimeSpan.FromSeconds(1));
            sut.TargetPositions.Should().HaveCount(853);

            // Cached should give same result
            sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);
            sut.TargetName.Should().Be("T1");
            sut.TargetId.Should().Be(1);
            sut.ImagingDate.Should().Be(dateTime);
            sut.TransitTime.Should().BeCloseTo(new DateTime(2024, 12, 2, 1, 5, 8), TimeSpan.FromSeconds(1));
            sut.TargetPositions.Should().HaveCount(853);
        }

        [Test]
        public void testBadTime() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 20, 0, 0);
            DateTime sunrise = new DateTime(2024, 12, 1, 19, 0, 0);

            // sunset > sunrise
            var ex = Assert.Throws<ArgumentException>(() => new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60));
            Assert.That(ex.Message, Is.EqualTo("sunset is after sunrise"));

            // no sunset/sunrise
            ex = Assert.Throws<ArgumentException>(() => new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, null, null, 60));
            Assert.That(ex.Message, Is.EqualTo("no sunset/sunrise for this date/location"));

            // night < 300s
            sunset = new DateTime(2024, 12, 1, 20, 0, 0);
            sunrise = sunset.AddSeconds(299);
            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 180);
            sut.ImagingPossible.Should().BeFalse();

            // requested time < sunset
            sunrise = sunset.AddHours(10);
            sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 180);
            sut.ImagingPossible.Should().BeTrue();
        }

        [Test]
        public void testNextVisibleIntervalBadTime() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 18, 0, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 6, 0, 0);
            HorizonDefinition hd = new HorizonDefinition(0);
            TimeInterval imagingInterval = new TimeInterval(sunset, sunrise);

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 180);
            var viz = sut.NextVisibleInterval(imagingInterval.EndTime.AddSeconds(1), imagingInterval, hd);
            viz.IsVisible.Should().BeFalse();

            viz = sut.NextVisibleInterval(imagingInterval.StartTime.AddSeconds(-1), imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
        }

        [Test]
        public void testNextVisibleInterval1() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 18, 0, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 6, 0, 0);
            HorizonDefinition hd = new HorizonDefinition(0);
            TimeInterval imagingInterval = new TimeInterval(sunset, sunrise);

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);
            sut.ImagingPossible.Should().BeTrue();

            var viz = sut.NextVisibleInterval(sunset, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(new DateTime(2024, 12, 1, 19, 22, 0));
            viz.StopTime.Should().Be(sunrise);

            dateTime = new DateTime(2024, 6, 1, 13, 0, 0);
            sunset = new DateTime(2024, 6, 1, 21, 0, 0);
            sunrise = new DateTime(2024, 6, 2, 5, 0, 0);
            sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 180);
            sut.ImagingPossible.Should().BeFalse();
        }

        [Test]
        public void testNextVisibleInterval2() {
            DateTime dateTime = new DateTime(2024, 3, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 3, 1, 20, 0, 0);
            DateTime sunrise = new DateTime(2024, 3, 2, 6, 0, 0);
            HorizonDefinition hd = new HorizonDefinition(0);
            TimeInterval imagingInterval = new TimeInterval(sunset, sunrise);

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);
            sut.ImagingPossible.Should().BeTrue();

            DateTime belowStartTime = new DateTime(2024, 3, 2, 0, 55, 0);
            var viz = sut.NextVisibleInterval(sunset, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(sunset);
            viz.StopTime.Should().Be(belowStartTime);

            // Below horizon from atTime to end
            viz = sut.NextVisibleInterval(belowStartTime, imagingInterval, hd);
            viz.IsVisible.Should().BeFalse();
        }

        [Test]
        public void testNextVisibleInterval3() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 19, 0, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 6, 0, 0);
            HorizonDefinition hd = GetSpikedHorizon();
            TimeInterval imagingInterval = new TimeInterval(sunset, sunrise);

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);
            sut.ImagingPossible.Should().BeTrue();

            // Test with spiked horizon
            DateTime startTime = new DateTime(2024, 12, 1, 19, 21, 0);
            var viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime.AddMinutes(1));
            viz.StopTime.Should().Be(new DateTime(2024, 12, 1, 19, 52, 0));

            startTime = new DateTime(2024, 12, 1, 19, 22, 0);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime);
            viz.StopTime.Should().Be(new DateTime(2024, 12, 1, 19, 52, 0));

            startTime = new DateTime(2024, 12, 1, 19, 51, 0);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime);
            viz.StopTime.Should().Be(new DateTime(2024, 12, 1, 19, 52, 0));

            startTime = new DateTime(2024, 12, 2, 4, 59, 30);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime.AddSeconds(30));
            viz.StopTime.Should().Be(sunrise);

            startTime = new DateTime(2024, 12, 2, 5, 0, 0);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime);
            viz.StopTime.Should().Be(sunrise);
        }

        [Test]
        public void testNextVisibleInterval4() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 19, 0, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 6, 0, 0);
            HorizonDefinition hd = GetSpikedHorizon();

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 120);
            sut.ImagingPossible.Should().BeTrue();

            // With different imaging intervals
            DateTime startTime = new DateTime(2024, 12, 1, 19, 12, 0);
            DateTime endInterval = new DateTime(2024, 12, 1, 19, 46, 0);
            TimeInterval imagingInterval = new TimeInterval(sunset, endInterval);
            var viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime.AddMinutes(10));
            viz.StopTime.Should().Be(endInterval);

            startTime = new DateTime(2024, 12, 1, 19, 12, 0);
            endInterval = new DateTime(2024, 12, 1, 20, 0, 0);
            imagingInterval = new TimeInterval(sunset, endInterval);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime.AddMinutes(10));
            viz.StopTime.Should().Be(startTime.AddMinutes(40));

            startTime = new DateTime(2024, 12, 1, 20, 0, 0);
            endInterval = new DateTime(2024, 12, 1, 21, 0, 0);
            imagingInterval = new TimeInterval(sunset, endInterval);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeFalse();

            endInterval = new DateTime(2024, 12, 1, 23, 0, 0);
            imagingInterval = new TimeInterval(sunset, endInterval);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(new DateTime(2024, 12, 1, 22, 8, 0));
            viz.StopTime.Should().Be(endInterval);

            imagingInterval = new TimeInterval(sunset, sunrise);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(new DateTime(2024, 12, 1, 22, 8, 0));
            viz.StopTime.Should().Be(new DateTime(2024, 12, 1, 23, 34, 0));
        }

        [Test]
        public void testNextVisibleIntervalMinTime() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 19, 0, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 6, 0, 0);
            HorizonDefinition hd = GetSpikedHorizon();
            TimeInterval imagingInterval = new TimeInterval(sunset, sunrise);

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 60);
            sut.ImagingPossible.Should().BeTrue();

            // First interval is 29m
            DateTime startTime = sunset;
            var viz = sut.NextVisibleInterval(startTime, imagingInterval, hd, 20 * 60);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(startTime.AddMinutes(22));
            viz.StopTime.Should().Be(startTime.AddMinutes(52));

            // Asking for more gets the next interval
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd, 31 * 60);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(new DateTime(2024, 12, 1, 22, 8, 0));
            viz.StopTime.Should().Be(new DateTime(2024, 12, 1, 23, 34, 0));

            // In the next, 1h is doable
            startTime = new DateTime(2024, 12, 2, 0, 0, 0);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd, 60 * 60);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(new DateTime(2024, 12, 2, 0, 18, 0));
            viz.StopTime.Should().Be(new DateTime(2024, 12, 2, 1, 27, 0));

            // But 1.5h isn't available for the remainder of the night
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd, 90 * 60);
            viz.IsVisible.Should().BeFalse();

            // Find the last interval of 1h
            startTime = new DateTime(2024, 12, 2, 4, 0, 0);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd, 60 * 60);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(new DateTime(2024, 12, 2, 5, 0, 0));
            viz.StopTime.Should().Be(new DateTime(2024, 12, 2, 6, 0, 0));

            // We can get 20m at end
            startTime = new DateTime(2024, 12, 2, 5, 30, 0);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd, 20 * 60);
            viz.IsVisible.Should().BeTrue();
            viz.StartTime.Should().Be(new DateTime(2024, 12, 2, 5, 30, 0));
            viz.StopTime.Should().Be(new DateTime(2024, 12, 2, 6, 0, 0));

            // But not 35m
            startTime = new DateTime(2024, 12, 2, 5, 30, 0);
            viz = sut.NextVisibleInterval(startTime, imagingInterval, hd, 35 * 60);
            viz.IsVisible.Should().BeFalse();
        }

        [Test]
        public void testCircumpolarAntiMeridian() {
            DateTime dateTime = new DateTime(2024, 3, 1, 13, 0, 0);
            DateTime sunset = dateTime.AddHours(4);
            DateTime sunrise = sunset.AddHours(14);

            // altitude hits min and starts rising at anti-meridian crossing
            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.STAR_NORTH_CIRCP, dateTime, sunset, sunrise, 600);
            sut.TargetPositions.Should().HaveCount(86);
            sut.ImagingPossible.Should().BeTrue();
            sut.HasTransit().Should().BeFalse();

            DateTime atTime = dateTime.AddHours(12).AddSeconds(10);
            int pos = sut.FindInterval(atTime, 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(25.0982, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(10), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(25.0487, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(20), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(25.0164, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(30), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(25.0012, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(40), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(25.0032, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(50), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(25.0225, 0.001);
        }

        [Test]
        public void testCircumpolarMeridian() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = dateTime.AddHours(4);
            DateTime sunrise = sunset.AddHours(14);

            // altitude hits max and starts falling at meridian crossing
            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.STAR_NORTH_CIRCP, dateTime, sunset, sunrise, 600);
            sut.TargetPositions.Should().HaveCount(86);
            sut.ImagingPossible.Should().BeTrue();
            sut.TransitTime.Should().BeCloseTo(new DateTime(2024, 12, 1, 19, 33, 48), TimeSpan.FromSeconds(1));

            DateTime atTime = dateTime.AddHours(6).AddMinutes(20).AddSeconds(10);
            int pos = sut.FindInterval(atTime, 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(44.9876, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(10), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(45.0, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(20), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(44.9902, 0.001);
        }

        [Test]
        public void testCircumpolarSameAzimuth() {
            DateTime dateTime = new DateTime(2024, 2, 1, 13, 0, 0);
            DateTime sunset = dateTime.AddHours(4);
            DateTime sunrise = sunset.AddHours(14);

            // altitude continues falling while azimuth decreases, then increases - so two altitudes at same azimuth range
            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.STAR_NORTH_CIRCP, dateTime, sunset, sunrise, 600);
            sut.TargetPositions.Should().HaveCount(86);
            sut.ImagingPossible.Should().BeTrue();
            sut.HasTransit().Should().BeTrue();
            sut.TransitTime.Should().BeCloseTo(new DateTime(2024, 2, 1, 15, 32, 21), TimeSpan.FromSeconds(1));

            DateTime atTime = dateTime.AddHours(7).AddMinutes(50).AddSeconds(10);
            int pos = sut.FindInterval(atTime, 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(36.0796, 0.001);
            sut.TargetPositions[pos].Azimuth.Should().BeApproximately(347.7741, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(10), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(35.6444, 0.001);
            sut.TargetPositions[pos].Azimuth.Should().BeApproximately(347.7613, 0.001);

            pos = sut.FindInterval(atTime.AddMinutes(20), 0, 85);
            sut.TargetPositions[pos].Altitude.Should().BeApproximately(35.2092, 0.001);
            sut.TargetPositions[pos].Azimuth.Should().BeApproximately(347.7715, 0.001);
        }

        [Test]
        public void testGetAltitude() {
            DateTime dateTime = new DateTime(2024, 10, 15, 0, 0, 0);
            DateTime sunset = dateTime.AddHours(18);
            DateTime sunrise = sunset.AddHours(12);
            HorizonDefinition hd = new HorizonDefinition(0);
            TimeInterval imagingInterval = new TimeInterval(sunset, sunrise);

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M31, dateTime, sunset, sunrise, 60);

            DateTime atTime = dateTime.AddHours(23).AddSeconds(23);
            sut.GetAltitude(atTime).Should().BeApproximately(73.436, 0.001);

            atTime = sunset.AddSeconds(-1);
            sut.GetAltitude(atTime).Should().Be(double.MinValue);

            atTime = sunrise.AddSeconds(1);
            sut.GetAltitude(atTime).Should().Be(double.MinValue);
        }

        [Test]
        public void testIsApproximatelyNow() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 18, 0, 0);
            DateTime sunrise = sunset.AddHours(12);
            HorizonDefinition hd = new HorizonDefinition(0);
            TimeInterval imagingInterval = new TimeInterval(sunset, sunrise);

            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 10);

            DateTime atTime = new DateTime(2024, 12, 1, 19, 20, 50);
            var viz = sut.NextVisibleInterval(atTime, imagingInterval, hd);
            DateTime startTime = (DateTime)viz.StartTime;

            // should be 'approximately now' within plus/minus 2x sample interval (10)
            atTime = new DateTime(2024, 12, 1, 19, 20, 59);
            sut.IsApproximatelyNow(atTime, startTime).Should().BeFalse();

            for (int i = 0; i < 21; i++) {
                atTime = atTime.AddSeconds(1);
                sut.IsApproximatelyNow(atTime, (DateTime)viz.StartTime).Should().BeTrue();
            }

            atTime = atTime.AddSeconds(1);
            sut.IsApproximatelyNow(atTime, startTime).Should().BeFalse();
        }

        [Test]
        public void testTransitTime() {
            DateTime dateTime = new DateTime(2024, 2, 1, 13, 0, 0);
            DateTime sunset = dateTime.AddHours(5);
            DateTime sunrise = sunset.AddHours(14);

            // Outside of extended imaging time
            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.STAR_NORTH_CIRCP, dateTime, sunset, sunrise, 120);
            sut.TransitTime.Should().Be(TargetVisibility.TRANSIT_TIME_NA);

            dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            sunset = new DateTime(2024, 12, 1, 20, 0, 0);
            sunrise = new DateTime(2024, 12, 2, 6, 0, 0);

            // Never rises, imaging not possible
            sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.STAR_SOUTH_CIRCP, dateTime, sunset, sunrise, 120);
            sut.TransitTime.Should().Be(TargetVisibility.TRANSIT_TIME_NA);

            // Normal south transit
            sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 120);
            sut.TransitTime.Should().BeCloseTo(new DateTime(2024, 12, 2, 1, 5, 8), TimeSpan.FromSeconds(1));

            // Normal north transit
            // TODO:

            dateTime = new DateTime(2024, 8, 1, 13, 0, 0);
            sunset = new DateTime(2024, 8, 1, 20, 0, 0);
            sunrise = new DateTime(2024, 8, 2, 6, 0, 0);

            // North circumpolar
            sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.STAR_NORTH_CIRCP, dateTime, sunset, sunrise, 120);
            sut.TransitTime.Should().BeCloseTo(new DateTime(2024, 8, 2, 4, 31, 1), TimeSpan.FromSeconds(1));

            // South circumpolar
            sut = new TargetVisibility("T1", 1, TestData.South_Mid_Lat, TestData.STAR_SOUTH_CIRCP, dateTime, sunset, sunrise, 120);
            sut.TransitTime.Should().BeCloseTo(new DateTime(2024, 8, 2, 4, 35, 1), TimeSpan.FromSeconds(1));
        }

        [Test]
        public void testHasTransit() {
            DateTime dateTime = new DateTime(2024, 12, 1, 13, 0, 0);
            DateTime sunset = new DateTime(2024, 12, 1, 20, 0, 0);
            DateTime sunrise = new DateTime(2024, 12, 2, 6, 0, 0);
            TargetVisibility sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 180);
            sut.HasTransit().Should().BeTrue();

            dateTime = new DateTime(2024, 12, 1, 20, 0, 0);
            sunset = dateTime.AddMinutes(1);
            sunrise = sunset.AddMinutes(8);

            sut = new TargetVisibility("T1", 1, TestData.North_Mid_Lat, TestData.M42, dateTime, sunset, sunrise, 180);
            sut.HasTransit().Should().BeFalse();
        }

        public static HorizonDefinition GetSpikedHorizon() {
            // Pathological 'tree-gap' horizon: multiple spikes from 0 to 60
            SortedDictionary<double, double> altitudes = new SortedDictionary<double, double> {
                {0,0}, {10,0}, {20,0}, {30,60}, {40,60}, {50,0}, {60,0}, {70,60}, {80,60}, {90,0},
                {100,0},{110,60},{120,60},{130,0},{140,0},{150,60},{160,60},{170,0},{180,0},
                {190,60},{200,60},{210,0},{220,0},{230,60},{240,60},{250,0},{260,0},{270,60},
                {280,60},{290,0},{300,0},{310,60},{320,60},{330,0},{340,0},{350,0}
            };

            CustomHorizon ch = HorizonDefinitionTest.GetCustomHorizon(altitudes);
            return new HorizonDefinition(ch, 0);
        }
    }
}