using FluentAssertions;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Test.Util;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Database.Schema {

    [TestFixture]
    public class AcquiredImageTest {

        [Test]
        public void TestAcquiredImage() {
            DateTime now = new DateTime(2024, 11, 11, 1, 2, 3);
            AcquiredImage sut = new AcquiredImage("abc123", 1, 2, 3, now, "Ha", GradingStatus.Accepted, "foo", GetIM());
            TestUtils.ValidGuid(sut.Guid).Should().BeTrue();
            sut.ProfileId.Should().Be("abc123");
            sut.ProjectId.Should().Be(1);
            sut.TargetId.Should().Be(2);
            sut.ExposureId.Should().Be(3);
            sut.AcquiredDate.Should().Be(now);
            sut.FilterName.Should().Be("Ha");
            sut.GradingStatus.Should().Be(GradingStatus.Accepted);
            sut.Accepted.Should().BeTrue();
            sut.Rejected.Should().BeFalse();
            sut.Pending.Should().BeFalse();
            sut.RejectReason.Should().Be("foo");
            sut.Metadata.Should().NotBeNull();
            sut.Metadata.FileName.Should().Be("C:\\foo\\bar");
        }

        private ImageMetadata GetIM() {
            return new ImageMetadata {
                FileName = @"C:\foo\bar",
                SessionId = 1,
                FilterName = "filter",
                ExposureStartTime = DateTime.Now,
                ExposureDuration = 180,
                Gain = 10,
                Offset = 12,
                Binning = "1x1",
                ReadoutMode = 42,
                ROI = 100,
                DetectedStars = 123,
                HFR = 1,
                HFRStDev = 2,
                FWHM = 3,
                Eccentricity = 4,
                ADUStDev = 5,
                ADUMean = 6,
                ADUMedian = 7,
                ADUMax = 8,
                ADUMin = 9,
                GuidingRMSScale = -1,
                GuidingRMS = -2,
                GuidingRMSArcSec = -3,
                GuidingRMSRA = -4,
                GuidingRMSRAArcSec = -5,
                GuidingRMSDEC = -6,
                GuidingRMSDECArcSec = -7,
                FocuserPosition = -8,
                FocuserTemp = -9,
                RotatorPosition = -10,
                RotatorMechanicalPosition = -11,
                PierSide = "west",
                CameraTemp = -12,
                CameraTargetTemp = -13,
                Airmass = -14,
            };
        }
    }
}