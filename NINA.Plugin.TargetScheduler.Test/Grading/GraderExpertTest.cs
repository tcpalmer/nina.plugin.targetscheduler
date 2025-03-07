using FluentAssertions;
using Moq;
using NINA.Core.Model;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Test.Grading {

    [TestFixture]
    public class GraderExpertTest {
        public static readonly Guid DefaultProfileId = new Guid("01234567-0000-0000-0000-000000000000");

        [Test]
        public void testNoGradingMetricsEnabled() {
            var prefs = GetPreferences(GetMockProfile(0, 0), 0, 0, false, false, 0, false, 0, false, 0, false, 0, false, 0);
            GraderExpert sut = new GraderExpert(prefs, null);
            sut.NoGradingMetricsEnabled.Should().BeTrue();

            prefs = GetPreferences(GetMockProfile(0, 0), 0, 0, false, false, 0, true, 0, false, 0, false, 0, false, 0);
            sut = new GraderExpert(prefs, null);
            sut.NoGradingMetricsEnabled.Should().BeFalse();
        }

        [Test]
        public void testEnableGradeRMS() {
            var prefs = GetPreferences(GetMockProfile(0, 0), 0, 0, false, true, 0, false, 0, false, 0, false, 0, false, 0);
            GraderExpert sut = new GraderExpert(prefs, null);
            sut.EnableGradeRMS.Should().BeTrue();
            prefs = GetPreferences(GetMockProfile(0, 0), 0, 0, false, false, 0, false, 0, false, 0, false, 0, false, 0);
            sut = new GraderExpert(prefs, null);
            sut.EnableGradeRMS.Should().BeFalse();
        }

        [Test]
        public void testGradeRMS() {
            IProfile profile = GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GetPreferences(profile, 0, 0, false, false, 1, false, 0, false, 0, false, 0, false, 0);
            GraderExpert sut = new GraderExpert(prefs, GetMockImageData(0.6, 1, "L", 0, 0));

            sut.GradeRMS().Should().BeTrue(); // not enabled

            prefs = GetPreferences(profile, 0, 0, false, true, 1, false, 0, false, 0, false, 0, false, 0);
            sut = new GraderExpert(prefs, GetMockImageData(0.6, 1, "L", 0, 0));
            sut.GradeRMS().Should().BeTrue();

            sut = new GraderExpert(prefs, GetMockImageData(1, 1, "L", 0, 0));
            sut.GradeRMS().Should().BeFalse();

            prefs = GetPreferences(profile, 0, 0, false, true, 1.35, false, 0, false, 0, false, 0, false, 0);
            sut = new GraderExpert(prefs, GetMockImageData(0.6, 1, "L", 0, 0, 0, 0, 60, 10, 20, "2x2"));
            sut.GradeRMS().Should().BeTrue();

            prefs = GetPreferences(profile, 0, 0, false, true, 1.34, false, 0, false, 0, false, 0, false, 0);
            sut = new GraderExpert(prefs, GetMockImageData(0.6, 1, "L", 0, 0, 0, 0, 60, 10, 20, "2x2"));
            sut.GradeRMS().Should().BeFalse();
        }

        [Test]
        public void testGradeStars() {
            IProfile profile = GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GetPreferences(profile, 0, 10, false, false, 0, false, 2, false, 0, false, 0, false, 0);
            List<AcquiredImage> pop = GetTestImages(10, 1, "L", 60);

            GraderExpert sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 500, 0));
            sut.GradeStars(pop).Should().BeTrue(); // not enabled

            prefs = GetPreferences(profile, 0, 10, false, false, 0, true, 2, false, 0, false, 0, false, 0);
            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 500, 0));
            sut.GradeStars(pop).Should().BeTrue(); // enabled, within variance

            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 400, 0));
            sut.GradeStars(pop).Should().BeFalse(); // enabled, outside variance

            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 600, 0));
            sut.GradeStars(pop).Should().BeFalse(); // enabled, outside variance, don't accept improvements

            prefs = GetPreferences(profile, 0, 10, true, false, 0, true, 2, false, 0, false, 0, false, 0);
            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 600, 0));
            sut.GradeStars(pop).Should().BeTrue(); // enabled, outside variance, accept improvements
        }

        [Test]
        public void testGradeHFR() {
            IProfile profile = GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GetPreferences(profile, 0, 10, false, false, 0, false, 0, false, 0, false, 0, false, 0);
            List<AcquiredImage> pop = GetTestImages(10, 1, "L", 60);

            GraderExpert sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 0, 1.5));
            sut.GradeHFR(pop).Should().BeTrue(); // not enabled

            prefs = GetPreferences(profile, 0, 10, false, false, 0, false, 0, true, 2, false, 0, false, 0);
            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 0, 1.5));
            sut.GradeHFR(pop).Should().BeTrue(); // enabled, within variance

            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 0, 3));
            sut.GradeHFR(pop).Should().BeFalse(); // enabled, outside variance

            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 0, 0.2));
            sut.GradeHFR(pop).Should().BeFalse(); // enabled, outside variance, don't accept improvements

            prefs = GetPreferences(profile, 0, 10, true, false, 0, false, 0, true, 2, false, 0, false, 0);
            sut = new GraderExpert(prefs, GetMockImageData(0, 0, "L", 0, 0.2));
            sut.GradeHFR(pop).Should().BeTrue(); // enabled, outside variance, accept improvements
        }

        [Test]
        public void testGradeFWHM() {
            IProfile profile = GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GetPreferences(profile, 0, 10, false, false, 0, false, 0, false, 0, false, 0, false, 0);
            ImageMetadata imageData = GetMockImageData(0, 0, "L", 0, 0, 1.5, 0);
            List<AcquiredImage> pop = GetTestImages(10, 1, "L", 60);

            GraderExpert sut = new GraderExpert(prefs, imageData);
            sut.GradeFWHM(pop).Should().BeTrue(); // not enabled

            prefs = GetPreferences(profile, 0, 10, false, false, 0, false, 0, false, 0, true, 2, false, 0);

            imageData.FWHM = 2.234;
            sut = new GraderExpert(prefs, imageData);
            sut.GradeFWHM(pop).Should().BeTrue(); // enabled, within variance

            imageData.FWHM = 6;
            sut = new GraderExpert(prefs, imageData);
            sut.GradeFWHM(pop).Should().BeFalse(); // enabled, outside variance

            imageData.FWHM = 0.1;
            sut = new GraderExpert(prefs, imageData);
            sut.GradeFWHM(pop).Should().BeFalse(); // enabled, outside variance, don't accept improvements

            prefs = GetPreferences(profile, 0, 10, true, false, 0, false, 0, false, 0, true, 2, false, 0);
            sut = new GraderExpert(prefs, imageData);
            sut.GradeFWHM(pop).Should().BeTrue(); // enabled, outside variance, accept improvements
        }

        [Test]
        public void testGradeEccentricity() {
            IProfile profile = GetMockProfile(3.8, 700);
            ImageGraderPreferences prefs = GetPreferences(profile, 0, 10, false, false, 0, false, 0, false, 0, false, 0, false, 0);
            ImageMetadata imageData = GetMockImageData(0, 0, "L", 0, 0, 1.5, 0);
            List<AcquiredImage> pop = GetTestImages(10, 1, "L", 60);

            GraderExpert sut = new GraderExpert(prefs, imageData);
            sut.GradeEccentricity(pop).Should().BeTrue(); // not enabled

            prefs = GetPreferences(profile, 0, 10, false, false, 0, false, 0, false, 0, false, 0, true, 2);

            imageData.Eccentricity = 3.456;
            sut = new GraderExpert(prefs, imageData);
            sut.GradeEccentricity(pop).Should().BeTrue(); // enabled, within variance

            imageData.Eccentricity = 10.456;
            sut = new GraderExpert(prefs, imageData);
            sut.GradeEccentricity(pop).Should().BeFalse(); // enabled, outside variance

            imageData.Eccentricity = 1;
            sut = new GraderExpert(prefs, imageData);
            sut.GradeEccentricity(pop).Should().BeFalse(); // enabled, outside variance, don't accept improvements

            prefs = GetPreferences(profile, 0, 10, true, false, 0, false, 0, false, 0, false, 0, true, 2);
            sut = new GraderExpert(prefs, imageData);
            sut.GradeEccentricity(pop).Should().BeTrue(); // enabled, outside variance, accept improvements
        }

        [Test]
        public void testSampleStandardDeviation() {
            var prefs = GetPreferences(GetMockProfile(0, 0), 0, 0, false, false, 0, false, 0, false, 0, false, 0, false, 0);
            GraderExpert sut = new GraderExpert(prefs, null);

            Action act = () => sut.SampleStandardDeviation(null);
            act.Should().Throw<Exception>().Where(e => e.Message == "must have >= 3 samples");

            double[] samples = new double[] { 483, 500 };
            act = () => sut.SampleStandardDeviation(samples.ToList());
            act.Should().Throw<Exception>().Where(e => e.Message == "must have >= 3 samples");

            samples = new double[] { 483, 500, 545 };
            (double mean, double stddev) = sut.SampleStandardDeviation(samples.ToList());
            mean.Should().BeApproximately(509.3333, 0.001);
            stddev.Should().BeApproximately(32.0364, 0.001);
        }

        [Test]
        public void testAutoAcceptLevelHFR() {
            Mock<IImageGraderPreferences> mock = new Mock<IImageGraderPreferences>();
            mock.SetupAllProperties();
            mock.SetupProperty(m => m.EnableGradeHFR, true);
            mock.SetupProperty(m => m.AutoAcceptLevelHFR, 4);
            List<AcquiredImage> pop = GetTestImages(10, 1, "L", 60);

            // auto enabled and permissive
            GraderExpert sut = new GraderExpert(mock.Object, GetMockImageData(0, 0, "L", 0, 3.99));
            sut.GradeHFR(pop).Should().BeTrue();

            // auto disabled
            mock.SetupProperty(m => m.AutoAcceptLevelHFR, 0);
            sut = new GraderExpert(mock.Object, GetMockImageData(0, 0, "L", 0, 3.99));
            sut.GradeHFR(pop).Should().BeFalse();

            // auto enabled but too tight
            mock.SetupProperty(m => m.AutoAcceptLevelHFR, 1);
            sut = new GraderExpert(mock.Object, GetMockImageData(0, 0, "L", 0, 3.99));
            sut.GradeHFR(pop).Should().BeFalse();
        }

        [Test]
        public void testAutoAcceptLevelFWHM() {
            Mock<IImageGraderPreferences> mockPrefs = new Mock<IImageGraderPreferences>();
            mockPrefs.SetupAllProperties();
            mockPrefs.SetupProperty(m => m.EnableGradeFWHM, true);
            mockPrefs.SetupProperty(m => m.AutoAcceptLevelFWHM, 4);
            List<AcquiredImage> pop = GetTestImages(10, 1, "L", 60);
            ImageMetadata imageData = GetMockImageData(0, 0, "L", 0, 0, 1.5, 0);

            // auto enabled and permissive
            imageData.FWHM = 3.99;
            GraderExpert sut = new GraderExpert(mockPrefs.Object, imageData);
            sut.GradeFWHM(pop).Should().BeTrue();

            // auto disabled
            mockPrefs.SetupProperty(m => m.AutoAcceptLevelFWHM, 0);
            imageData.FWHM = 3.99;
            sut = new GraderExpert(mockPrefs.Object, imageData);
            sut.GradeFWHM(pop).Should().BeFalse();

            // auto enabled but too tight
            mockPrefs.SetupProperty(m => m.AutoAcceptLevelFWHM, 3.9);
            sut = new GraderExpert(mockPrefs.Object, imageData);
            sut.GradeFWHM(pop).Should().BeFalse();
        }

        [Test]
        public void testAutoAcceptLevelEccentricity() {
            Mock<IImageGraderPreferences> mockPrefs = new Mock<IImageGraderPreferences>();
            mockPrefs.SetupAllProperties();
            mockPrefs.SetupProperty(m => m.EnableGradeEccentricity, true);
            mockPrefs.SetupProperty(m => m.AutoAcceptLevelEccentricity, 4);
            List<AcquiredImage> pop = GetTestImages(10, 1, "L", 60);
            ImageMetadata imageData = GetMockImageData(0, 0, "L", 0, 0, 1.5, 0);

            // auto enabled and permissive
            imageData.Eccentricity = 3.99;
            GraderExpert sut = new GraderExpert(mockPrefs.Object, imageData);
            sut.GradeEccentricity(pop).Should().BeTrue();

            // auto disabled
            mockPrefs.SetupProperty(m => m.AutoAcceptLevelEccentricity, 0);
            sut = new GraderExpert(mockPrefs.Object, imageData);
            sut.GradeEccentricity(pop).Should().BeFalse();

            // auto enabled but too tight
            mockPrefs.SetupProperty(m => m.AutoAcceptLevelEccentricity, 3.9);
            sut = new GraderExpert(mockPrefs.Object, imageData);
            sut.GradeEccentricity(pop).Should().BeFalse();
        }

        public static IProfile GetMockProfile(double pixelSize, double focalLength) {
            Mock<IProfileService> mock = new Mock<IProfileService>();
            mock.SetupProperty(m => m.ActiveProfile.Id, DefaultProfileId);
            mock.SetupProperty(m => m.ActiveProfile.CameraSettings.PixelSize, pixelSize);
            mock.SetupProperty(m => m.ActiveProfile.TelescopeSettings.FocalLength, focalLength);
            return mock.Object.ActiveProfile;
        }

        public static ImageGraderPreferences GetPreferences(IProfile profile, double DelayGrading, int SampleSize, bool AcceptImprovement,
                                                      bool GradeRMS, double RMSPixelThreshold,
                                                      bool GradeDetectedStars, double DetectedStarsSigmaFactor,
                                                      bool GradeHFR, double HFRSigmaFactor,
                                                      bool GradeFWHM, double FWHMSigmaFactor,
                                                      bool GradeEccentricity, double EccentricitySigmaFactor) {
            return new ImageGraderPreferences(profile, DelayGrading, SampleSize, AcceptImprovement,
                                              GradeRMS, RMSPixelThreshold,
                                              GradeDetectedStars, DetectedStarsSigmaFactor,
                                              GradeHFR, HFRSigmaFactor,
                                              GradeFWHM, FWHMSigmaFactor, GradeEccentricity, EccentricitySigmaFactor);
        }

        public static ImageMetadata GetMockImageData(double rmsTotal, double rmsScale, string filter, int detectedStars, double HFR, double FWHM = Double.NaN, double eccentricity = Double.NaN, double duration = 60,
                                               int gain = 10, int offset = 20, string binning = "1x1", double rotation = 0) {
            ImageMetadata imageMetadata = new ImageMetadata {
                ExposureDuration = duration,
                Gain = gain,
                Offset = offset,
                Binning = binning,
                RotatorPosition = rotation,
                ROI = 100,
                GuidingRMSScale = rmsScale,
                GuidingRMS = rmsTotal,
                DetectedStars = detectedStars,
                HFR = HFR,
                FWHM = FWHM,
                Eccentricity = eccentricity,
            };

            return imageMetadata;
        }

        public static ImageSavedEventArgs GetMockImageSavedEventArgs(double rmsTotal, double rmsScale, string filter, int detectedStars, double HFR, double FWHM = Double.NaN, double eccentricity = Double.NaN, double duration = 60,
                                               int gain = 10, int offset = 20, string binning = "1x1", double rotation = 0) {
            ImageSavedEventArgs msg = new ImageSavedEventArgs();
            ImageMetaData metadata = new ImageMetaData();
            ImageParameter imageParameter = new ImageParameter();
            CameraParameter cameraParameter = new CameraParameter();

            msg.PathToImage = new Uri("C:\\image.fits");

            RMS rms = new RMS { Total = rmsTotal };
            rms.SetScale(rmsScale);

            imageParameter.RecordedRMS = rms;
            imageParameter.Binning = binning;
            imageParameter.ImageType = "LIGHT";
            metadata.Rotator.Position = rotation;
            metadata.Image = imageParameter;
            metadata.Image.Id = 0;

            cameraParameter.Gain = gain;
            cameraParameter.Offset = offset;
            metadata.Camera = cameraParameter;

            msg.Duration = duration;
            msg.MetaData = metadata;
            msg.Filter = filter;

            Mock<IImageStatistics> statMock = new Mock<IImageStatistics>();
            msg.Statistics = statMock.Object;

            Mock<IStarDetectionAnalysis> sdMock = new Mock<IStarDetectionAnalysis>();
            sdMock.SetupProperty(m => m.DetectedStars, detectedStars);
            sdMock.SetupProperty(m => m.HFR, HFR);
            msg.StarDetectionAnalysis = sdMock.Object;

            return msg;
        }

        public static List<AcquiredImage> GetTestImages(int count, int targetId, string filterName,
            double duration = 60, int gain = 10, int offset = 20, string binning = "1x1", double roi = 100,
            double rotatorPosition = 0, GradingStatus status = GradingStatus.Accepted) {
            DateTime dateTime = DateTime.Now.Date;
            List<AcquiredImage> images = new List<AcquiredImage>();
            int id = 501;

            for (int i = 0; i < count; i++) {
                dateTime = dateTime.AddMinutes(5);
                ImageMetadata metaData = new ImageMetadata {
                    ExposureDuration = duration,
                    DetectedStars = 500 + i,
                    HFR = 1 + (double)i / 10,
                    FWHM = 2 + (double)i / 10,
                    Eccentricity = 3 + (double)i / 10,
                    Gain = gain,
                    Offset = offset,
                    Binning = binning,
                    ROI = roi,
                    RotatorPosition = rotatorPosition
                };

                images.Add(new AcquiredImage(DefaultProfileId.ToString(), 0, targetId, 0, dateTime, filterName, status, "", metaData) { Id = id++ });
            }

            return images.OrderByDescending(i => i.AcquiredDate).ToList();
        }

        public static AcquiredImage GetAcquiredImage(int id, int targetId, string filterName,
            double duration = 60, int gain = 10, int offset = 20, string binning = "1x1", double roi = 100,
            double rotatorPosition = 0, GradingStatus status = GradingStatus.Accepted, ImageMetadata metaData = null) {
            return new AcquiredImage(DefaultProfileId.ToString(), 0, targetId, 0, DateTime.Now.Date, filterName, status, "", metaData) { Id = id };
        }
    }
}