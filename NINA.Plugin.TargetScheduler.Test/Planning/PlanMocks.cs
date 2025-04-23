using Moq;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    internal class PlanMocks {

        public static List<IProject> ProjectsList(params IProject[] array) {
            return array.ToList();
        }

        public static Mock<IProfileService> GetMockProfileService(ObserverInfo location) {
            Mock<IProfileService> profileMock = new Mock<IProfileService>();
            profileMock.SetupProperty(m => m.ActiveProfile.Id, new Guid("01234567-0000-0000-0000-000000000000"));
            profileMock.SetupProperty(m => m.ActiveProfile.AstrometrySettings.Latitude, location.Latitude);
            profileMock.SetupProperty(m => m.ActiveProfile.AstrometrySettings.Longitude, location.Longitude);
            profileMock.SetupProperty(m => m.ActiveProfile.AstrometrySettings.Elevation, location.Elevation);
            return profileMock;
        }

        public static Mock<IProject> GetMockPlanProject(string name, ProjectState state) {
            Mock<IProject> pp = new Mock<IProject>();
            pp.SetupAllProperties();
            pp.SetupProperty(m => m.Name, name);
            pp.SetupProperty(m => m.State, state);
            pp.SetupProperty(m => m.Rejected, false);
            pp.SetupProperty(m => m.HorizonDefinition, new HorizonDefinition(0));

            pp.SetupProperty(m => m.MinimumTime, 30);
            pp.SetupProperty(m => m.MinimumAltitude, 0);
            pp.SetupProperty(m => m.UseCustomHorizon, false);
            pp.SetupProperty(m => m.HorizonOffset, 0);
            pp.SetupProperty(m => m.MeridianWindow, 0);
            pp.SetupProperty(m => m.FilterSwitchFrequency, 0);
            pp.SetupProperty(m => m.DitherEvery, 0);
            pp.SetupProperty(m => m.EnableGrader, false);
            pp.SetupProperty(m => m.IsMosaic, false);
            pp.SetupProperty(m => m.ExposureCompletionHelper, new ExposureCompletionHelper(false, 0, 125));

            Dictionary<string, double> rw = new Dictionary<string, double>();
            Dictionary<string, IScoringRule> allRules = ScoringRule.GetAllScoringRules();
            foreach (KeyValuePair<string, IScoringRule> item in allRules) {
                rw[item.Key] = 1;
            }

            pp.SetupProperty(m => m.RuleWeights, rw);
            pp.SetupProperty(m => m.Targets, new List<ITarget>());
            return pp;
        }

        public static Mock<ITarget> GetMockPlanTarget(string name, Coordinates coordinates) {
            Mock<ITarget> pt = new Mock<ITarget>();
            pt.SetupAllProperties();
            pt.SetupProperty(m => m.Name, name);
            pt.SetupProperty(m => m.Coordinates, coordinates);
            pt.SetupProperty(m => m.AllExposurePlans, new List<IExposure>());
            pt.SetupProperty(m => m.ExposurePlans, new List<IExposure>());
            pt.SetupProperty(m => m.CompletedExposurePlans, new List<IExposure>());

            Mock<IProject> pp = new Mock<IProject>();
            pp.SetupAllProperties();
            pp.SetupProperty(m => m.ExposureCompletionHelper, new ExposureCompletionHelper(true, 0, 100));

            pt.SetupProperty(m => m.Project, pp.Object);

            return pt;
        }

        public static Mock<IExposure> GetMockPlanExposure(string filterName, int desired, int accepted, int databaseId = 0) {
            return GetMockPlanExposure(filterName, desired, accepted, 30, databaseId);
        }

        public static Mock<IExposure> GetMockPlanExposure(string filterName, int desired, int accepted, int exposureLength, int databaseId) {
            Mock<IExposure> pe = new Mock<IExposure>();
            pe.SetupAllProperties();
            pe.SetupProperty(m => m.DatabaseId, databaseId);
            pe.SetupProperty(m => m.FilterName, filterName);
            pe.SetupProperty(m => m.ExposureLength, exposureLength);
            pe.SetupProperty(m => m.Desired, desired);
            pe.SetupProperty(m => m.Acquired, 0);
            pe.SetupProperty(m => m.Accepted, accepted);
            pe.SetupProperty(m => m.DitherEvery, -1);
            pe.SetupProperty(m => m.MoonRelaxScale, 0);

            pe.Setup(m => m.NeededExposures()).Returns(accepted > desired ? 0 : desired - accepted);
            pe.Setup(m => m.IsIncomplete()).Returns(accepted < desired);
            return pe;
        }

        public static void AddMockPlanFilter(Mock<ITarget> pt, Mock<IExposure> pf) {
            pf.SetupProperty(m => m.PlanTarget, pt.Object);
            pt.Object.AllExposurePlans.Add(pf.Object);
            pt.Object.ExposurePlans.Add(pf.Object);
        }

        public static void AddMockPlanFilterToCompleted(Mock<ITarget> pt, Mock<IExposure> pf) {
            pf.SetupProperty(m => m.PlanTarget, pt.Object);
            pf.Object.Rejected = true;
            pf.Object.RejectedReason = Reasons.FilterComplete;
            pt.Object.CompletedExposurePlans.Add(pf.Object);
        }

        public static void AddMockPlanTarget(Mock<IProject> pp, Mock<ITarget> pt) {
            pt.SetupProperty(m => m.Project, pp.Object);
            pp.Object.Targets.Add(pt.Object);
        }

        public static Mock<IScoringEngine> GetMockScoringEnging() {
            Mock<IScoringEngine> mse = new Mock<IScoringEngine>();
            mse.SetupAllProperties();
            mse.SetupProperty(m => m.RuleWeights, new Dictionary<string, double>());
            return mse;
        }

        public static ImageSavedEventArgs GetImageSavedEventArgs(DateTime onDateTime, string filterName) {
            ImageMetaData metadata = new ImageMetaData();
            metadata.Image.ExposureStart = onDateTime;
            metadata.Image.Binning = "1x1";

            RMS rms = new RMS();
            rms.Total = 200;
            //rms.Scale = 1.5;
            rms.RA = 201;
            rms.Dec = 202;
            metadata.Image.RecordedRMS = rms;

            metadata.Focuser.Position = 300;
            metadata.Focuser.Temperature = 301;
            metadata.Rotator.Position = 302;
            metadata.Rotator.MechanicalPosition = 308;
            metadata.Telescope.SideOfPier = NINA.Core.Enum.PierSide.pierWest;
            metadata.Telescope.Airmass = 303;
            metadata.Camera.Temperature = 304;
            metadata.Camera.SetPoint = 305;
            metadata.Camera.Gain = 306;
            metadata.Camera.Offset = 307;

            ImageSavedEventArgs msg = new ImageSavedEventArgs();
            msg.MetaData = metadata;
            msg.PathToImage = new Uri("file://path/to/image.fits");
            msg.Duration = 60;
            msg.Filter = filterName;

            Mock<IImageStatistics> isMock = new Mock<IImageStatistics>();
            isMock.SetupAllProperties();

            Mock<IStarDetectionAnalysis> sdaMock = new Mock<IStarDetectionAnalysis>();
            sdaMock.SetupAllProperties();
            sdaMock.SetupProperty(m => m.DetectedStars, 100);
            sdaMock.SetupProperty(m => m.HFR, 101);
            sdaMock.SetupProperty(m => m.HFRStDev, 102);

            msg.Statistics = isMock.Object;
            msg.StarDetectionAnalysis = sdaMock.Object;

            return msg;
        }
    }
}