using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Sequencer;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Plugin.TargetScheduler.Test.Grading;
using NINA.Plugin.TargetScheduler.Test.Planning;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Text;
using System.Threading;

namespace NINA.Plugin.TargetScheduler.Test.Sequencer {

    [TestFixture]
    public class ImageSaveWatcherTest {
        private readonly string profileId = GraderExpertTest.DefaultProfileId.ToString();
        private static DateTime markDate = DateTime.Now.Date;

        private string testDatabasePath;
        private SchedulerDatabaseInteraction db;

        [OneTimeSetUp]
        public void OneTimeSetUp() {
            testDatabasePath = Path.Combine(Path.GetTempPath(), $"scheduler-unittest2.sqlite");
            if (File.Exists(testDatabasePath)) {
                File.Delete(testDatabasePath);
            }

            db = new SchedulerDatabaseInteraction(string.Format(@"Data Source={0};", testDatabasePath));
            Assert.That(db, Is.Not.Null);
            LoadTestDatabase();
        }

        [Test, Order(1)]
        [NonParallelizable]
        public void testImageSaveNoGrading() {
            IProfile profile = GraderExpertTest.GetMockProfile(0, 0);
            IImageSaveMediator imageSaveMediator = GetImageSaveMediator();
            Mock<ITarget> t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42);
            t1.SetupProperty(t => t.DatabaseId, 1);
            Mock<IExposure> e1 = PlanMocks.GetMockPlanExposure("Ha", 10, 0);
            e1.SetupProperty(e => e.DatabaseId, 1);
            CancellationToken token = new CancellationToken();

            int imageId = 22;
            // TODO: this needs to be a real ImageSavedEventArgs
            ImageSavedEventArgs imageData = GraderExpertTest.GetMockImageSavedEventArgs(0, 0, "Ha", 0, 0, 1.5, 0);
            imageData.MetaData.Image.Id = imageId;

            Mock<ImageSaveWatcher> mock = new Mock<ImageSaveWatcher>(profile, imageSaveMediator) { CallBase = true };
            mock.Setup(m => m.GetSchedulerDatabaseContext()).Returns(db.GetContext());
            mock.Setup(m => m.GetProfilePreference(It.IsAny<IProfile>())).Returns(new ProfilePreference(profileId));

            ImageSaveWatcher sut = mock.Object;
            sut.WaitForExposure(imageId, new ExposureWaitData(t1.Object, e1.Object, imageId, token));
            sut.ImageSaved(null, imageData);

            using (var context = db.GetContext()) {
                var ep = context.GetExposurePlan(1);
                ep.Should().NotBeNull();
                ep.Accepted.Should().Be(1);
                ep.Acquired.Should().Be(1);

                var ai = context.GetAcquiredImage(1);
                ai.Should().NotBeNull();
                ai.Accepted.Should().BeTrue();
                ai.Pending.Should().BeFalse();
                ai.GradingStatus.Should().Be(GradingStatus.Accepted);
                ai.RejectReason.Should().Be("");
            }
        }

        [Test, Order(2)]
        [NonParallelizable]
        public void testImageSaveGrading() {
            IProfile profile = GraderExpertTest.GetMockProfile(0, 0);
            IImageSaveMediator imageSaveMediator = GetImageSaveMediator();
            Mock<ITarget> t1 = PlanMocks.GetMockPlanTarget("T1", TestData.M42);
            t1.SetupProperty(t => t.DatabaseId, 1);
            t1.Object.Project.EnableGrader = true;
            Mock<IExposure> e1 = PlanMocks.GetMockPlanExposure("OIII", 10, 0);
            e1.SetupProperty(e => e.DatabaseId, 2);
            CancellationToken token = new CancellationToken();

            int imageId = 23;
            ImageSavedEventArgs imageData = GraderExpertTest.GetMockImageSavedEventArgs(0, 0, "OIII", 0, 0, 1.5, 0);
            imageData.MetaData.Image.Id = imageId;

            Mock<ImageSaveWatcher> mock = new Mock<ImageSaveWatcher>(profile, imageSaveMediator) { CallBase = true };
            mock.Setup(m => m.GetSchedulerDatabaseContext()).Returns(db.GetContext());
            mock.Setup(m => m.GetProfilePreference(It.IsAny<IProfile>())).Returns(new ProfilePreference(profileId));
            mock.Setup(m => m.GetImageGradingController()).Returns(GetMockedImageGradingController());

            ImageSaveWatcher sut = mock.Object;
            sut.WaitForExposure(imageId, new ExposureWaitData(t1.Object, e1.Object, imageId, token));
            sut.ImageSaved(null, imageData);

            using (var context = db.GetContext()) {
                var ep = context.GetExposurePlan(2);
                ep.Should().NotBeNull();
                ep.Accepted.Should().Be(0);
                ep.Acquired.Should().Be(1);

                var ai = context.GetAcquiredImage(2);
                ai.Should().NotBeNull();
                ai.Accepted.Should().BeFalse();
                ai.Pending.Should().BeTrue();
                ai.GradingStatus.Should().Be(GradingStatus.Pending);
                ai.RejectReason.Should().Be("");
            }
        }

        private IImageSaveMediator GetImageSaveMediator() {
            Mock<IImageSaveMediator> mock = new Mock<IImageSaveMediator>();
            mock.SetupAllProperties();
            return mock.Object;
        }

        private ImageGradingController GetMockedImageGradingController() {
            Mock<IImageGrader> mock = new Mock<IImageGrader>();
            mock.SetupAllProperties();
            return new ImageGradingController(mock.Object);
        }

        private void LoadTestDatabase() {
            using (var context = db.GetContext()) {
                try {
                    Project p1 = new Project(profileId);
                    p1.Name = "P1";
                    p1.Description = "";
                    p1.State = ProjectState.Active;
                    p1.ActiveDate = markDate;
                    p1.MinimumTime = 30;
                    p1.MinimumAltitude = 23;
                    p1.UseCustomHorizon = false;
                    p1.HorizonOffset = 0;
                    p1.FilterSwitchFrequency = 1;
                    p1.DitherEvery = 1;
                    p1.EnableGrader = false;
                    p1.IsMosaic = false;
                    p1.FlatsHandling = Project.FLATS_HANDLING_OFF;

                    p1.RuleWeights = new List<RuleWeight> {
                        {new RuleWeight("a", .1) },
                        {new RuleWeight("b", .2) },
                        {new RuleWeight("c", .3) }
                    };

                    ExposureTemplate etHa = new ExposureTemplate(profileId, "Ha", "Ha");
                    ExposureTemplate etOIII = new ExposureTemplate(profileId, "OIII", "OIII");
                    ExposureTemplate etSII = new ExposureTemplate(profileId, "SII", "SII");
                    context.ExposureTemplateSet.Add(etHa);
                    context.ExposureTemplateSet.Add(etOIII);
                    context.ExposureTemplateSet.Add(etSII);
                    context.SaveChanges();

                    Target t1 = new Target();
                    t1.Name = "T1";
                    t1.ra = TestData.M42.RADegrees;
                    t1.dec = TestData.M42.Dec;
                    p1.Targets.Add(t1);

                    ExposurePlan ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etHa.Id;
                    ep.Desired = 10;
                    ep.Exposure = 20;
                    t1.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etOIII.Id;
                    ep.Desired = 10;
                    ep.Exposure = 20;
                    t1.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etSII.Id;
                    ep.Desired = 10;
                    ep.Exposure = 20;
                    t1.ExposurePlans.Add(ep);
                    context.ProjectSet.Add(p1);

                    Project p2 = new Project(profileId);
                    p2.Name = "P2";
                    p1.Description = "";
                    p1.State = ProjectState.Active;
                    p1.ActiveDate = markDate;
                    p1.MinimumTime = 30;
                    p1.MinimumAltitude = 23;
                    p1.UseCustomHorizon = false;
                    p1.HorizonOffset = 0;
                    p1.FilterSwitchFrequency = 1;
                    p1.DitherEvery = 1;
                    p1.EnableGrader = false;
                    p1.IsMosaic = false;
                    p1.FlatsHandling = Project.FLATS_HANDLING_OFF;

                    p2.RuleWeights = new List<RuleWeight> {
                        {new RuleWeight("d", .4) },
                        {new RuleWeight("e", .5) },
                        {new RuleWeight("f", .6) }
                    };

                    Target t2 = new Target();
                    t2.Name = "T2";
                    t2.Enabled = false;
                    t2.ra = TestData.IC1805.RADegrees;
                    t2.dec = TestData.IC1805.Dec;
                    p2.Targets.Add(t2);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etHa.Id;
                    ep.Desired = 10;
                    ep.Exposure = 20;
                    t2.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etOIII.Id;
                    ep.Desired = 10;
                    ep.Exposure = 20;
                    t2.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etSII.Id;
                    ep.Desired = 10;
                    ep.Exposure = 20;
                    t2.ExposurePlans.Add(ep);
                    context.ProjectSet.Add(p2);

                    context.SaveChanges();
                } catch (DbEntityValidationException e) {
                    StringBuilder sb = new StringBuilder();
                    foreach (var eve in e.EntityValidationErrors) {
                        foreach (var dbeve in eve.ValidationErrors) {
                            sb.Append(dbeve.ErrorMessage).Append("\n");
                        }
                    }

                    TestContext.Error.WriteLine($"DB VALIDATION EXCEPTION: {sb}");
                    throw;
                } catch (Exception e) {
                    TestContext.Error.WriteLine($"OTHER EXCEPTION: {e.Message}\n{e}");
                    throw;
                }
            }
        }
    }
}