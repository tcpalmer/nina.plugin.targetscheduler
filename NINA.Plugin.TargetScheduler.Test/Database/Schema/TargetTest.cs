using FluentAssertions;
using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Test.Util;
using NUnit.Framework;

namespace NINA.Plugin.TargetScheduler.Test.Database.Schema {

    [TestFixture]
    public class TargetTest {

        [Test]
        public void TestDefaults() {
            Target sut = new Target();
            TestUtils.ValidGuid(sut.Guid).Should().BeTrue();
            sut.active.Should().BeTrue();
            sut.RA.Should().Be(0);
            sut.Dec.Should().Be(0);
            sut.Epoch.Should().Be(Epoch.J2000);
            sut.Rotation.Should().Be(0);
            sut.ROI.Should().Be(100);
            sut.ExposurePlans.Count.Should().Be(0);
            sut.OverrideExposureOrders.Count.Should().Be(0);
        }

        [Test]
        public void TestGetPasteCopy() {
            Target target = new Target();
            target.Name = "MyTarget";
            Target sut = target.GetPasteCopy("abc123");
            TestUtils.ValidGuid(sut.Guid).Should().BeTrue();
            target.Guid.Equals(sut.Guid).Should().BeFalse();
            sut.Name.Should().Be("MyTarget (1)");
            sut.active.Should().BeTrue();
            sut.RA.Should().Be(0);
            sut.Dec.Should().Be(0);
            sut.Epoch.Should().Be(Epoch.J2000);
            sut.Rotation.Should().Be(0);
            sut.ROI.Should().Be(100);
            sut.ExposurePlans.Count.Should().Be(0);
            sut.OverrideExposureOrders.Count.Should().Be(0);
        }

        [Test]
        [TestCase(5, 10, 20, -10, 10, 20, 5.1722, -10.1722, "RA: 05:10:20; Dec: -10° 10' 20\"; Epoch: J2000")]
        [TestCase(5, 10, 20.9, -10, 10, 20.9, 5.1722, -10.1722, "RA: 05:10:21; Dec: -10° 10' 21\"; Epoch: J2000")]
        public void testCoordinatesGetSet(int raH, int raM, double raS,
                                          int decD, int decM, double decS,
                                          double expectedRA, double expectedDec, string expectedFmt) {
            Target sut = new Target();
            sut.Enabled.Should().Be(true);

            sut.RAHours = raH;
            sut.RAMinutes = raM;
            sut.RASeconds = raS;
            sut.DecDegrees = decD;
            sut.DecMinutes = decM;
            sut.DecSeconds = decS;

            sut.Coordinates.ToString().Should().Be(expectedFmt);
            sut.ra.Should().BeApproximately(expectedRA, 0.001);
            sut.RA.Should().BeApproximately(expectedRA, 0.001);
            sut.RASeconds.Should().Be(raS);
            sut.dec.Should().BeApproximately(expectedDec, 0.001);
            sut.Dec.Should().BeApproximately(expectedDec, 0.001);
            sut.DecSeconds.Should().Be(decS);
        }
    }
}