using FluentAssertions;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NUnit.Framework;

namespace NINA.Plugin.TargetScheduler.Test.Database.Schema {

    [TestFixture]
    public class ExposureTemplateTest {

        [Test]
        public void TestDefaults() {
            var sut = new ExposureTemplate("123", "Lum", "L");
            sut.Name.Should().Be("Lum");
            sut.FilterName.Should().Be("L");

            sut.Gain.Should().Be(-1);
            sut.Offset.Should().Be(-1);
            sut.BinningMode.X.Should().Be(1);
            sut.BinningMode.Y.Should().Be(1);
            sut.ReadoutMode.Should().Be(-1);

            sut.TwilightLevel.Should().Be(TwilightLevel.Nighttime);
            sut.MoonAvoidanceEnabled.Should().BeFalse();
            sut.MoonAvoidanceSeparation.Should().BeApproximately(60, 0.0001);
            sut.MoonAvoidanceWidth.Should().Be(7);
            sut.DitherEvery.Should().Be(-1);
            sut.MaximumHumidity.Should().BeApproximately(0, 00001);
        }

        [Test]
        public void TestExposureTemplatesOrder() {
            Assert.That(TwilightLevel.Nighttime, Is.LessThan(TwilightLevel.Astronomical));
            Assert.That(TwilightLevel.Astronomical, Is.LessThan(TwilightLevel.Nautical));
            Assert.That(TwilightLevel.Nautical, Is.LessThan(TwilightLevel.Civil));
        }

        [Test]
        public void TestExposureTemplateTwilightChecks() {
            var sut = new ExposureTemplate("123", "L", "L");

            sut.TwilightLevel = TwilightLevel.Nighttime;
            sut.IsTwilightNightOnly().Should().BeTrue();
            sut.IsTwilightAstronomical().Should().BeFalse();
            sut.IsTwilightNautical().Should().BeFalse();
            sut.IsTwilightCivil().Should().BeFalse();

            sut.TwilightLevel = TwilightLevel.Astronomical;
            sut.IsTwilightNightOnly().Should().BeFalse();
            sut.IsTwilightAstronomical().Should().BeTrue();
            sut.IsTwilightNautical().Should().BeFalse();
            sut.IsTwilightCivil().Should().BeFalse();

            sut.TwilightLevel = TwilightLevel.Nautical;
            sut.IsTwilightNightOnly().Should().BeFalse();
            sut.IsTwilightAstronomical().Should().BeFalse();
            sut.IsTwilightNautical().Should().BeTrue();
            sut.IsTwilightCivil().Should().BeFalse();

            sut.TwilightLevel = TwilightLevel.Civil;
            sut.IsTwilightNightOnly().Should().BeFalse();
            sut.IsTwilightAstronomical().Should().BeFalse();
            sut.IsTwilightNautical().Should().BeFalse();
            sut.IsTwilightCivil().Should().BeTrue();
        }
    }
}