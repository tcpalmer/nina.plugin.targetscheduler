using Newtonsoft.Json;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Database.ExportImport {

    [TestFixture]
    public class AcquiredImageDeserializeTest {

        [Test]
        public void testBasic() {
            //string json = File.ReadAllText("C:\\Users\\Tom\\AppData\\Local\\NINA\\SchedulerPlugin\\Temp\\ai.json");
            //TestContext.WriteLine(json);

            // AcquiredImage(string profileId, int projectId, int targetId, int exposureId, DateTime acquiredDate, string filterName, GradingStatus gradingStatus, string rejectReason, ImageMetadata imageMetadata) {
            AcquiredImage acquiredImage = new AcquiredImage("abcd-1234", 1, 2, 3, DateTime.Now, "filter", GradingStatus.Pending, "<not>", GetIM());
            TestContext.WriteLine($"AI obj:\n{acquiredImage}");

            string json = JsonConvert.SerializeObject(acquiredImage);
            TestContext.WriteLine(json);

            var ai = JsonConvert.DeserializeObject<AcquiredImage>(json);
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