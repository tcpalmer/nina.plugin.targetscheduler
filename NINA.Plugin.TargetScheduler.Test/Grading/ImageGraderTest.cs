using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Test.Grading {

    [TestFixture]
    public class ImageGraderTest {

        [Test]
        public void testLegacyNoCriteriaEnabled() {
            IProfile profile = GraderExpertTest.GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GraderExpertTest.GetPreferences(profile, 0, 10, false, false, 0, false, 0, false, 0, false, 0, false, 0);
            ImageMetadata imageData = GraderExpertTest.GetMockImageData(0, 0, "L", 0, 0, 1.5, 0);

            List<AcquiredImage> pop = GraderExpertTest.GetTestImages(10, 101, "L", 60, 10, 20, "1x1", 100, 0, GradingStatus.Pending);

            Mock<ImageGrader> mock = new Mock<ImageGrader>() { CallBase = true };
            ExposurePlan exposurePlan = GetExposurePlan(201, "L", 10, 0);
            AcquiredImage ai = GetAcquiredImage(301, 101, "L");

            mock.Setup(m => m.GetExposurePlan(201)).Returns(exposurePlan);
            mock.Setup(m => m.GetTarget(101)).Returns(GetTarget(101, "T1", 100));
            mock.Setup(m => m.GetAllAcquired(exposurePlan)).Returns(pop);
            mock.Setup(m => m.GetCurrentAcquired(It.IsAny<int>())).Returns(ai);

            List<GradingResult> results = new List<GradingResult>();
            mock.Setup(m => m.UpdateDatabase(Capture.In(results), It.IsAny<ExposurePlan>(), It.IsAny<AcquiredImage>()));

            GradingWorkData workData = new GradingWorkData(101, 201, 301, 1, prefs);

            // No grading criteria enabled => accepted
            ImageGrader sut = mock.Object;
            sut.Grade(workData);
            results.Count.Should().Be(1);
            results[0].Should().Be(GradingResult.Accepted);
        }

        [Test]
        public void testLegacyNotEnoughToCompare() {
            IProfile profile = GraderExpertTest.GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GraderExpertTest.GetPreferences(profile, 0, 10, false, false, 0, true, 2, false, 0, false, 0, false, 0);
            ImageMetadata imageData = GraderExpertTest.GetMockImageData(0, 0, "L", 0, 0, 1.5, 0);

            List<AcquiredImage> pop = GraderExpertTest.GetTestImages(1, 101, "L", 60, 10, 20, "1x1", 100, 0, GradingStatus.Pending);

            Mock<ImageGrader> mock = new Mock<ImageGrader>() { CallBase = true };
            ExposurePlan exposurePlan = GetExposurePlan(201, "L", 10, 0);
            AcquiredImage ai = GetAcquiredImage(301, 101, "L");

            mock.Setup(m => m.GetExposurePlan(201)).Returns(exposurePlan);
            mock.Setup(m => m.GetTarget(101)).Returns(GetTarget(101, "T1", 100));
            mock.Setup(m => m.GetAllAcquired(exposurePlan)).Returns(pop);
            mock.Setup(m => m.GetCurrentAcquired(It.IsAny<int>())).Returns(ai);

            List<GradingResult> results = new List<GradingResult>();
            mock.Setup(m => m.UpdateDatabase(Capture.In(results), It.IsAny<ExposurePlan>(), It.IsAny<AcquiredImage>()));

            GradingWorkData workData = new GradingWorkData(101, 201, 301, 1, prefs);

            // Not enough to compare against => accepted
            ImageGrader sut = mock.Object;
            sut.Grade(workData);
            results.Count.Should().Be(1);
            results[0].Should().Be(GradingResult.Accepted);
        }

        [Test]
        public void testLegacyBadStars() {
            IProfile profile = GraderExpertTest.GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GraderExpertTest.GetPreferences(profile, 0, 10, false, false, 0, true, 2, false, 0, false, 0, false, 0);

            List<AcquiredImage> pop = GraderExpertTest.GetTestImages(10, 101, "L", 60, 10, 20, "1x1", 100, 0, GradingStatus.Pending);

            Mock<ImageGrader> mock = new Mock<ImageGrader>() { CallBase = true };
            ExposurePlan exposurePlan = GetExposurePlan(201, "L", 10, 0);
            ImageMetadata imageData = GraderExpertTest.GetMockImageData(0, 0, "L", 100, 0, 1.5, 0);
            AcquiredImage ai = GetAcquiredImage(301, 101, "L", 60, 10, 20, "1x1", 100, 0, GradingStatus.Pending, imageData);

            mock.Setup(m => m.GetExposurePlan(201)).Returns(exposurePlan);
            mock.Setup(m => m.GetTarget(101)).Returns(GetTarget(101, "T1", 100));
            mock.Setup(m => m.GetAllAcquired(exposurePlan)).Returns(pop);
            mock.Setup(m => m.GetCurrentAcquired(It.IsAny<int>())).Returns(ai);

            List<GradingResult> results = new List<GradingResult>();
            mock.Setup(m => m.UpdateDatabase(Capture.In(results), It.IsAny<ExposurePlan>(), It.IsAny<AcquiredImage>()));

            GradingWorkData workData = new GradingWorkData(101, 201, 301, 1, prefs);

            // Enough to compare against, bad star count (100) => rejected
            ImageGrader sut = mock.Object;
            sut.Grade(workData);
            results.Count.Should().Be(1);
            results[0].Should().Be(GradingResult.Rejected_Stars);
        }

        [Test]
        public void testDelayedNotTime() {
            IProfile profile = GraderExpertTest.GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GraderExpertTest.GetPreferences(profile, 60, 10, false, false, 0, true, 2, false, 0, false, 0, false, 0);
            ImageMetadata imageData = GraderExpertTest.GetMockImageData(0, 0, "L", 100, 0, 1.5, 0);

            List<AcquiredImage> pop = GraderExpertTest.GetTestImages(10, 101, "L", 60, 10, 20, "1x1", 100, 0, GradingStatus.Pending);

            Mock<ImageGrader> mock = new Mock<ImageGrader>() { CallBase = true };
            ExposurePlan exposurePlan = GetExposurePlan(201, "L", 20, 3);
            AcquiredImage ai = GetAcquiredImage(301, 101, "L");

            mock.Setup(m => m.GetExposurePlan(201)).Returns(exposurePlan);
            mock.Setup(m => m.GetTarget(101)).Returns(GetTarget(101, "T1", 100));
            mock.Setup(m => m.GetAllAcquired(exposurePlan)).Returns(pop);
            mock.Setup(m => m.GetCurrentAcquired(It.IsAny<int>())).Returns(ai);

            List<GradingResult> results = new List<GradingResult>();
            mock.Setup(m => m.UpdateDatabase(Capture.In(results), It.IsAny<ExposurePlan>(), It.IsAny<AcquiredImage>()));

            GradingWorkData workData = new GradingWorkData(101, 201, 301, 1, prefs);

            // Delayed enabled but not triggered yet so no calls to UpdateDatabase
            ImageGrader sut = mock.Object;
            sut.Grade(workData);
            results.Count.Should().Be(0);
        }

        [Test]
        public void testDelayedReady() {
            IProfile profile = GraderExpertTest.GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GraderExpertTest.GetPreferences(profile, 60, 10, false, false, 0, true, 2, false, 0, false, 0, false, 0);

            List<AcquiredImage> pop = GraderExpertTest.GetTestImages(10, 101, "L", 60, 10, 20, "1x1", 100, 0, GradingStatus.Pending);

            // two should fail with low stars
            ImageMetadata imageData = GraderExpertTest.GetMockImageData(0, 0, "L", 100, 0, 1.5, 0);
            pop[1].Metadata = imageData;
            pop[5].Metadata = imageData;

            Mock<ImageGrader> mock = new Mock<ImageGrader>() { CallBase = true };
            ExposurePlan exposurePlan = GetExposurePlan(201, "L", 14, 0);
            imageData = GraderExpertTest.GetMockImageData(0, 0, "L", 100, 0, 1.5, 0);
            AcquiredImage ai = GetAcquiredImage(301, 101, "L", 60, 10, 20, "1x1", 100, 0, GradingStatus.Pending, imageData);

            mock.Setup(m => m.GetExposurePlan(201)).Returns(exposurePlan);
            mock.Setup(m => m.GetTarget(101)).Returns(GetTarget(101, "T1", 100));

            mock.Setup(m => m.GetCurrentAcquired(301)).Returns(ai);
            mock.Setup(m => m.GetAllAcquired(exposurePlan)).Returns(pop);

            List<GradingResult> results = new List<GradingResult>();
            mock.Setup(m => m.UpdateDatabase(Capture.In(results), It.IsAny<ExposurePlan>(), It.IsAny<AcquiredImage>()));

            GradingWorkData workData = new GradingWorkData(101, 201, 301, 1, prefs);

            // Delayed enabled and triggered => all pending get graded with 2 rejected for stars (100)
            ImageGrader sut = mock.Object;
            sut.Grade(workData);
            results.Count.Should().Be(10);
            results.Where(r => r == GradingResult.Accepted).ToList().Count.Should().Be(8);
            results.Where(r => r == GradingResult.Rejected_Stars).ToList().Count.Should().Be(2);
        }

        [Test]
        public void testMoveRejected() {
            string tempDir = Path.Combine(Path.GetTempPath(), "imageGraderTest");
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
            Directory.CreateDirectory(tempDir);
            string srcDir = Path.Combine(tempDir, "srcDir");
            Directory.CreateDirectory(srcDir);
            string src = Path.Combine(srcDir, "image.fits");
            File.Create(src).Close();

            ImageGrader sut = new ImageGrader();
            sut.MoveRejected(src);
            string expectedPath = Path.Combine(srcDir, ImageGrader.REJECTED_SUBDIR, "image.fits");
            File.Exists(expectedPath).Should().BeTrue();
        }

        [Test]
        public void testGradingResultToStatus() {
            ImageGrader sut = new ImageGrader();
            sut.GradingResultToStatus(GradingResult.Accepted).Should().Be(GradingStatus.Accepted);
            sut.GradingResultToStatus(GradingResult.Rejected_RMS).Should().Be(GradingStatus.Rejected);
            sut.GradingResultToStatus(GradingResult.Rejected_Stars).Should().Be(GradingStatus.Rejected);
            sut.GradingResultToStatus(GradingResult.Rejected_HFR).Should().Be(GradingStatus.Rejected);
            sut.GradingResultToStatus(GradingResult.Rejected_FWHM).Should().Be(GradingStatus.Rejected);
            sut.GradingResultToStatus(GradingResult.Rejected_Eccentricity).Should().Be(GradingStatus.Rejected);
        }

        [Test]
        public void testGradingResultToReason() {
            ImageGrader sut = new ImageGrader();
            ImageGrader.GradingResultToReason(GradingResult.Accepted).Should().Be("");
            ImageGrader.GradingResultToReason(GradingResult.Rejected_RMS).Should().Be(ImageGrader.REJECT_RMS);
            ImageGrader.GradingResultToReason(GradingResult.Rejected_Stars).Should().Be(ImageGrader.REJECT_STARS);
            ImageGrader.GradingResultToReason(GradingResult.Rejected_HFR).Should().Be(ImageGrader.REJECT_HFR);
            ImageGrader.GradingResultToReason(GradingResult.Rejected_FWHM).Should().Be(ImageGrader.REJECT_FWHM);
            ImageGrader.GradingResultToReason(GradingResult.Rejected_Eccentricity).Should().Be(ImageGrader.REJECT_ECCENTRICITY);
        }

        private ExposurePlan GetExposurePlan(int id, string filterName, int desired, int accepted) {
            ExposureTemplate template = new ExposureTemplate { FilterName = filterName };
            return new ExposurePlan { Id = id, Desired = desired, Accepted = accepted, ExposureTemplate = template };
        }

        private Target GetTarget(int id, string name, double roi) {
            return new Target { Id = id, Name = name, ROI = roi };
        }

        private AcquiredImage GetAcquiredImage(int id, int targetId, string filterName,
            double duration = 60, int gain = 10, int offset = 20, string binning = "1x1", double roi = 100,
            double rotatorPosition = 0, GradingStatus status = GradingStatus.Pending, ImageMetadata metaData = null) {
            if (metaData == null) {
                metaData = new ImageMetadata {
                    ExposureDuration = duration,
                    DetectedStars = 500,
                    HFR = 1,
                    FWHM = 2,
                    Eccentricity = 3,
                    Gain = gain,
                    Offset = offset,
                    Binning = binning,
                    ROI = roi,
                    RotatorPosition = rotatorPosition
                };
            }

            return GraderExpertTest.GetAcquiredImage(id, targetId, filterName, duration, gain, offset, binning, roi, rotatorPosition, status, metaData);
        }

        private AcquiredImage GetAcquiredImage(int id, int targetId, string filterName, ImageMetadata imageData, GradingStatus status = GradingStatus.Pending) {
            return GraderExpertTest.GetAcquiredImage(id, targetId, filterName, imageData.ExposureDuration, imageData.Gain, imageData.Offset,
                imageData.Binning, imageData.ROI, imageData.RotatorPosition, status, imageData);
        }
    }
}