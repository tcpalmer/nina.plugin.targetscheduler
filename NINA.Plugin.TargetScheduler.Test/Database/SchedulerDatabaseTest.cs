using FluentAssertions;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Plugin.TargetScheduler.Test.Planning;
using NINA.WPF.Base.Interfaces.Mediator;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Test.Database {

    [TestFixture]
    public class SchedulerDatabaseTest {
        private const string profileId = "01234567-abcd-9876-gfed-0123456abcde";
        private static DateTime markDate = DateTime.Now.Date;

        private string testDatabasePath;
        private SchedulerDatabaseInteraction db;

        [OneTimeSetUp]
        public void OneTimeSetUp() {
            DllLoader.LoadDll(Path.Combine("SQLite", "SQLite.Interop.dll"));

            testDatabasePath = Path.Combine(Path.GetTempPath(), $"scheduler-unittest.sqlite");
            if (File.Exists(testDatabasePath)) {
                File.Delete(testDatabasePath);
            }

            db = new SchedulerDatabaseInteraction(string.Format(@"Data Source={0};", testDatabasePath));
            Assert.That(db, Is.Not.Null);
            LoadTestDatabase();
        }

        [Test, Order(1)]
        [NonParallelizable]
        public void TestLoad() {
            using (var context = db.GetContext()) {
                context.HasActiveTargets("").Should().BeFalse();
                context.HasActiveTargets(profileId).Should().BeTrue();

                context.GetAllProjects("").Count.Should().Be(0);

                List<Project> projects = context.GetAllProjects(profileId);
                projects.Count.Should().Be(2);

                Project p1 = projects[0];
                p1.Name.Should().Be("Project: M42");
                p1.Targets.Count.Should().Be(1);

                p1.MinimumTime.Should().Be(60);
                p1.MinimumAltitude.Should().BeApproximately(23, 0.001);
                p1.UseCustomHorizon.Should().BeFalse();
                p1.HorizonOffset.Should().BeApproximately(11, 0.001);
                p1.FilterSwitchFrequency.Should().Be(12);
                p1.DitherEvery.Should().Be(14);
                p1.EnableGrader.Should().BeFalse();
                p1.IsMosaic.Should().BeTrue();
                p1.FlatsHandling.Should().Be(Project.FLATS_HANDLING_OFF);

                p1.RuleWeights[0].Name.Should().Be("a");
                p1.RuleWeights[1].Name.Should().Be("b");
                p1.RuleWeights[2].Name.Should().Be("c");
                p1.RuleWeights[0].Weight.Should().BeApproximately(.1, 0.001);
                p1.RuleWeights[1].Weight.Should().BeApproximately(.2, 0.001);
                p1.RuleWeights[2].Weight.Should().BeApproximately(.3, 0.001);

                Target t1p1 = p1.Targets[0];
                t1p1.Name.Should().Be("M42");
                t1p1.Enabled.Should().BeTrue();
                t1p1.RA.Should().BeApproximately(83.82, 0.001);
                t1p1.Dec.Should().BeApproximately(-5.391, 0.001);
                t1p1.Rotation.Should().BeApproximately(0, 0.001);
                t1p1.ROI.Should().BeApproximately(100, 0.001);

                t1p1.ExposurePlans.Count.Should().Be(3);
                t1p1.ExposurePlans[0].ExposureTemplate.FilterName.Should().Be("Ha");
                t1p1.ExposurePlans[1].ExposureTemplate.FilterName.Should().Be("OIII");
                t1p1.ExposurePlans[2].ExposureTemplate.FilterName.Should().Be("SII");

                Project p2 = projects[1];
                p2.Name.Should().Be("Project: IC1805");
                p2.Targets.Count.Should().Be(1);

                p2.MinimumTime.Should().Be(90);
                p2.MinimumAltitude.Should().BeApproximately(24, 0.001);
                p2.UseCustomHorizon.Should().BeTrue();
                p2.HorizonOffset.Should().BeApproximately(12, 0.001);
                p2.FilterSwitchFrequency.Should().Be(14);
                p2.DitherEvery.Should().Be(16);
                p2.EnableGrader.Should().BeFalse();
                p2.IsMosaic.Should().BeFalse();
                p2.FlatsHandling.Should().Be(3);

                p2.RuleWeights[0].Name.Should().Be("d");
                p2.RuleWeights[1].Name.Should().Be("e");
                p2.RuleWeights[2].Name.Should().Be("f");
                p2.RuleWeights[0].Weight.Should().BeApproximately(.4, 0.001);
                p2.RuleWeights[1].Weight.Should().BeApproximately(.5, 0.001);
                p2.RuleWeights[2].Weight.Should().BeApproximately(.6, 0.001);

                Target t1p2 = p2.Targets[0];
                t1p2.Name.Should().Be("IC1805");
                t1p2.Enabled.Should().BeFalse();
                t1p2.RA.Should().BeApproximately(38.175, 0.001);
                t1p2.Dec.Should().BeApproximately(61.45, 0.001);
                t1p2.Rotation.Should().BeApproximately(0, 0.001);
                t1p2.ROI.Should().BeApproximately(100, 0.001);
                t1p2.ExposurePlans.Count.Should().Be(3);
                t1p2.ExposurePlans[0].ExposureTemplate.FilterName.Should().Be("Ha");
                t1p2.ExposurePlans[1].ExposureTemplate.FilterName.Should().Be("OIII");
                t1p2.ExposurePlans[2].ExposureTemplate.FilterName.Should().Be("SII");

                context.GetExposureTemplates("").Count.Should().Be(0);
                List<ExposureTemplate> ets = context.GetExposureTemplates(profileId);
                ets.Count.Should().Be(3);
                ets[0].FilterName.Should().Be("Ha");
                ets[1].FilterName.Should().Be("OIII");
                ets[2].FilterName.Should().Be("SII");
                ets[0].MoonAvoidanceEnabled.Should().BeFalse();
                ets[1].MoonAvoidanceEnabled.Should().BeFalse();
                ets[2].MoonAvoidanceEnabled.Should().BeFalse();

                // Test GetActiveProjects
                projects = context.GetActiveProjects(profileId);
                projects.Count.Should().Be(2);
                projects[0].Name.Should().Be("Project: M42");
                projects[1].Name.Should().Be("Project: IC1805");
            }
        }

        [Test, Order(2)]
        [NonParallelizable]
        public void TestWriteAcquiredImage() {
            using (var context = db.GetContext()) {
                context.GetAcquiredImages(1, "Ha").Count.Should().Be(0);
                context.GetAcquiredImages(1, "OIII").Count.Should().Be(0);
                context.GetAcquiredImages(1, "SII").Count.Should().Be(0);
                context.GetAcquiredImages(1, "nada").Count.Should().Be(0);

                ImageSavedEventArgs msg = PlanMocks.GetImageSavedEventArgs(markDate.AddDays(1), "Ha");
                context.AcquiredImageSet.Add(new AcquiredImage("abcd-1234", 1, 1, 1, markDate.AddDays(1), "Ha", GradingStatus.Pending, "rr1", new ImageMetadata(msg, 1, 100, 0)));
                context.AcquiredImageSet.Add(new AcquiredImage("abcd-1234", 1, 1, 1, markDate.AddDays(1).AddMinutes(1), "Ha", GradingStatus.Accepted, "rr2", new ImageMetadata(msg, 2, 100, 0)));
                context.AcquiredImageSet.Add(new AcquiredImage("abcd-1234", 1, 1, 1, markDate.AddDays(1).AddMinutes(2), "Ha", GradingStatus.Accepted, "rr3", new ImageMetadata(msg, 3, 100, 0)));
                context.AcquiredImageSet.Add(new AcquiredImage("abcd-1234", 1, 1, 1, markDate.AddDays(1).AddMinutes(3), "Ha", GradingStatus.Rejected, "rr4", new ImageMetadata(msg, 4, 100, 0)));

                msg.MetaData.Rotator.MechanicalPosition = ImageMetadata.NO_ROTATOR_ANGLE;
                context.AcquiredImageSet.Add(new AcquiredImage("abcd-1234", 1, 1, 1, markDate.AddDays(1).AddMinutes(4), "Ha", GradingStatus.Rejected, "rr5", new ImageMetadata(msg, 5, 100, 0)));

                context.SaveChanges();

                List<AcquiredImage> ai = context.GetAcquiredImages(1, "Ha");
                ai.Count.Should().Be(5);

                // Confirm descending order
                ai[0].AcquiredDate.Should().BeExactly(markDate.AddDays(1).AddMinutes(4).TimeOfDay);
                ai[1].AcquiredDate.Should().BeExactly(markDate.AddDays(1).AddMinutes(3).TimeOfDay);
                ai[2].AcquiredDate.Should().BeExactly(markDate.AddDays(1).AddMinutes(2).TimeOfDay);
                ai[3].AcquiredDate.Should().BeExactly(markDate.AddDays(1).AddMinutes(1).TimeOfDay);
                ai[4].AcquiredDate.Should().BeExactly(markDate.AddDays(1).AddMinutes(0).TimeOfDay);

                ai[0].GradingStatus.Should().Be(GradingStatus.Rejected);
                ai[1].GradingStatus.Should().Be(GradingStatus.Rejected);
                ai[2].GradingStatus.Should().Be(GradingStatus.Accepted);
                ai[3].GradingStatus.Should().Be(GradingStatus.Accepted);
                ai[4].GradingStatus.Should().Be(GradingStatus.Pending);

                ai[0].RejectReason.Should().Be("rr5");
                ai[1].RejectReason.Should().Be("rr4");
                ai[2].RejectReason.Should().Be("rr3");
                ai[3].RejectReason.Should().Be("rr2");
                ai[4].RejectReason.Should().Be("rr1");

                ai[0].Metadata.SessionId.Should().Be(5);
                ai[1].Metadata.SessionId.Should().Be(4);
                ai[2].Metadata.SessionId.Should().Be(3);
                ai[3].Metadata.SessionId.Should().Be(2);
                ai[4].Metadata.SessionId.Should().Be(1);

                ai[0].Metadata.ExposureStartTime.Should().BeExactly(markDate.AddDays(1).AddMinutes(4).TimeOfDay);
                ai[1].Metadata.ExposureStartTime.Should().BeExactly(markDate.AddDays(1).AddMinutes(3).TimeOfDay);
                ai[2].Metadata.ExposureStartTime.Should().BeExactly(markDate.AddDays(1).AddMinutes(2).TimeOfDay);
                ai[3].Metadata.ExposureStartTime.Should().BeExactly(markDate.AddDays(1).AddMinutes(1).TimeOfDay);
                ai[4].Metadata.ExposureStartTime.Should().BeExactly(markDate.AddDays(1).AddMinutes(0).TimeOfDay);

                ai[1].Metadata.RotatorMechanicalPosition.Should().Be(308);
                ai[0].Metadata.RotatorMechanicalPosition.Should().Be(ImageMetadata.NO_ROTATOR_ANGLE);

                // Associated image data
                byte[] data1 = new byte[] { 0x21, 0x22, 0x23, 0x24, 0x25 };
                byte[] data2 = new byte[] { 0x26, 0x27, 0x28, 0x29, 0x2a };
                byte[] data3 = new byte[] { 0x2b, 0x2c, 0x2d, 0x2e, 0x2f };
                context.ImageDataSet.Add(new ImageData("tag1", data1, ai[0].Id, 1, 2));
                context.ImageDataSet.Add(new ImageData("tag2", data2, ai[0].Id, 3, 4));
                context.ImageDataSet.Add(new ImageData("tag1", data3, ai[1].Id, 5, 6));
                context.SaveChanges();

                ImageData id = context.GetImageData(ai[0].Id, "tag1");
                id.Tag.Should().Be("tag1");
                string s = Encoding.Default.GetString(id.Data);
                s.Should().Be("!\"#$%");
                id.Width.Should().Be(1);
                id.Height.Should().Be(2);

                id = context.GetImageData(ai[0].Id, "tag2");
                id.Tag.Should().Be("tag2");
                s = Encoding.Default.GetString(id.Data);
                s.Should().Be("&'()*");
                id.Width.Should().Be(3);
                id.Height.Should().Be(4);

                id = context.GetImageData(ai[1].Id, "tag1");
                id.Tag.Should().Be("tag1");
                s = Encoding.Default.GetString(id.Data);
                s.Should().Be("+,-./");
                id.Width.Should().Be(5);
                id.Height.Should().Be(6);

                context.GetImageData(ai[1].Id, "tag2").Should().BeNull();
                context.GetImageData(ai[2].Id, "tag1").Should().BeNull();

                // Test non-nullable filter column
                context.AcquiredImageSet.Add(new AcquiredImage("abcd-1234", 1, 1, 1, markDate.AddDays(1), null, GradingStatus.Pending, "rr1", new ImageMetadata(msg, 1, 100, 0)));
                context.Invoking(db => db.SaveChanges())
                    .Should().Throw<DbEntityValidationException>()
                    .WithMessage("Validation failed for one or more entities. See 'EntityValidationErrors' property for more details.");

                context.AcquiredImageSet.Add(new AcquiredImage("abcd-1234", 1, 1, 1, markDate.AddDays(1), "", GradingStatus.Pending, "rr1", new ImageMetadata(msg, 1, 100, 0)));
                context.Invoking(db => db.SaveChanges())
                    .Should().Throw<DbEntityValidationException>()
                    .WithMessage("Validation failed for one or more entities. See 'EntityValidationErrors' property for more details.");
            }
        }

        [Test, Order(3)]
        [NonParallelizable]
        public void TestWriteUpdateExposurePlans() {
            using (var context = db.GetContext()) {
                Target target = context.GetTarget(1, 1);
                ExposurePlan fp = target.ExposurePlans.Where(t => t.ExposureTemplate.FilterName == "Ha").First();
                fp.Acquired += 2;
                fp.Accepted += 1;
                context.SaveChanges();
            }

            using (var context = db.GetContext()) {
                Target target = context.GetTarget(1, 1);
                ExposurePlan fp = target.ExposurePlans.Where(t => t.ExposureTemplate.FilterName == "Ha").First();
                fp.Desired.Should().Be(3);
                fp.Acquired.Should().Be(2);
                fp.Accepted.Should().Be(1);
            }
        }

        [Test, Order(4)]
        [NonParallelizable]
        public void TestPasteProject() {
            using (var context = db.GetContext()) {
                List<Project> projects = context.GetAllProjects(profileId);
                projects.Count.Should().Be(2);
                Project p2 = projects[1];

                Target p2t1 = p2.Targets.First();
                //TestContext.WriteLine($"P2T1: {p2t1}");

                Project pasted = context.PasteProject("abcd-9876", p2);
                pasted.Should().NotBeNull();

                Target t = pasted.Targets.First();
                t.ra = 1.23;
                context.SaveTarget(t);

                Project pasted2 = context.PasteProject("abcd-9876", pasted);
                Target pasted2t1 = pasted2.Targets.First();
                //TestContext.WriteLine($"PS2T1: {pasted2t1}");
            }
        }

        [Test, Order(5)]
        [NonParallelizable]
        public void TestPasteTarget() {
            using (var context = db.GetContext()) {
                List<Project> projects = context.GetAllProjects(profileId);
                projects.Count.Should().Be(2);

                Project p1 = projects[0];
                Project p2 = projects[1];
                Target p2t1 = p2.Targets[0];

                context.PasteTarget(p1, p2t1).Should().NotBeNull();
            }
        }

        [Test, Order(6)]
        [NonParallelizable]
        public void TestNewExposurePlan() {
            using (var context = db.GetContext()) {
                ExposureTemplate et = context.GetExposureTemplate(1);
                ExposurePlan ep = new ExposurePlan(et.profileId);
                ep.ExposureTemplateId = et.Id;
                ep.Exposure = 120;
                ep.Desired = 10;

                List<Project> projects = context.GetAllProjects(profileId);
                Target p2t1 = projects[1].Targets[0];
                p2t1.ExposurePlans.Count.Should().Be(3);
                p2t1.ExposurePlans.Add(ep);

                Target t = context.SaveTarget(p2t1);
                t.ExposurePlans.Count.Should().Be(4);
            }
        }

        [Test, Order(7)]
        [NonParallelizable]
        public void TestProfilePreference() {
            using (var context = db.GetContext()) {
                ProfilePreference pp = context.GetProfilePreference("abcd-1234");
                pp.Should().BeNull();

                string pid = "abcd-1234";
                pp = new ProfilePreference(pid);

                pp.ProfileId.Should().Be(pid);
                pp.ParkOnWait.Should().BeFalse();
                pp.ExposureThrottle.Should().BeApproximately(125, 0.001);
                pp.EnableSmartPlanWindow.Should().BeTrue();
                pp.EnableDeleteAcquiredImagesWithTarget.Should().BeTrue();
                pp.EnableSlewCenter.Should().BeTrue();

                pp.EnableGradeRMS = true;
                pp.EnableGradeStars = true;
                pp.EnableGradeHFR = true;
                pp.MaxGradingSampleSize = 100;
                pp.RMSPixelThreshold = 1;
                pp.DetectedStarsSigmaFactor = 2;
                pp.HFRSigmaFactor = 3;

                context.ProfilePreferenceSet.Add(pp);
                context.SaveChanges();

                ProfilePreference pp2 = context.GetProfilePreference(pid);
                pp2.Should().NotBeNull();

                pp2.EnableGradeRMS.Should().BeTrue();
                pp2.EnableGradeStars.Should().BeTrue();
                pp2.EnableGradeHFR.Should().BeTrue();
                pp2.MaxGradingSampleSize.Should().Be(100);
                pp2.RMSPixelThreshold.Should().BeApproximately(1, 0.001);
                pp2.DetectedStarsSigmaFactor.Should().BeApproximately(2, 0.001);
                pp2.HFRSigmaFactor.Should().BeApproximately(3, 0.001);
            }
        }

        [Test, Order(8)]
        [NonParallelizable]
        public void TestDeleteExposurePlans() {
            using (var context = db.GetContext()) {
                ExposureTemplate et = context.GetExposureTemplate(1);
                ExposurePlan ep = new ExposurePlan(et.profileId);
                ep.ExposureTemplateId = et.Id;
                ep.Exposure = 120;
                ep.Desired = 10;

                List<Project> projects = context.GetAllProjects(profileId);
                Target p2t1 = projects[1].Targets[0];
                p2t1.ExposurePlans.Count.Should().Be(4);
                Target t = context.DeleteAllExposurePlans(p2t1);
                t.ExposurePlans.Count.Should().Be(0);
            }
        }

        [Test, Order(9)]
        [NonParallelizable]
        public void TestFlatHistory() {
            DateTime dt = DateTime.Now;
            FlatHistory record1 = new FlatHistory(1, dt, dt.AddDays(2), 23, "abcd-1234", FlatHistory.FLAT_TYPE_PANEL, "Ha", 10, 20, new BinningMode(2, 2), 0, 123.4, 89);
            FlatHistory record2 = new FlatHistory(1, dt.AddDays(1), dt.AddDays(3), 24, "abcd-1234", FlatHistory.FLAT_TYPE_SKY, "O3", 10, 20, new BinningMode(2, 2), 0, 123.4, 89);
            FlatHistory record3 = new FlatHistory(1, dt.AddDays(1), dt.AddDays(4), 25, "abcd-1234", FlatHistory.FLAT_TYPE_PANEL, "S2", 10, 20, new BinningMode(2, 2), 0, ImageMetadata.NO_ROTATOR_ANGLE, 89);

            using (var context = db.GetContext()) {
                context.FlatHistorySet.Add(record1);
                context.FlatHistorySet.Add(record2);
                context.FlatHistorySet.Add(record3);
                context.SaveChanges();
            }

            using (var context = db.GetContext()) {
                List<FlatHistory> records = context.GetFlatsHistory(dt.AddDays(-1), "abcd-1234");
                records.Count.Should().Be(0);
                records = context.GetFlatsHistory(dt, "abcd-1234");
                records.Count.Should().Be(1);

                FlatHistory sut = records[0];
                sut.TargetId.Should().Be(1);
                Assert.That(sut.LightSessionDate, Is.EqualTo(dt).Within(TimeSpan.FromSeconds(1.0)));
                Assert.That(sut.FlatsTakenDate, Is.EqualTo(dt.AddDays(2)).Within(TimeSpan.FromSeconds(1.0)));
                sut.LightSessionId.Should().Be(23);
                sut.ProfileId.Should().Be("abcd-1234");
                sut.FlatsType.Should().Be(FlatHistory.FLAT_TYPE_PANEL);
                sut.FilterName.Should().Be("Ha");
                sut.Gain.Should().Be(10);
                sut.Offset.Should().Be(20);
                sut.BinningMode.X.Should().Be(2);
                sut.ReadoutMode.Should().Be(0);
                sut.Rotation.Should().Be(123.4);
                sut.ROI.Should().Be(89);

                records = context.GetFlatsHistory(1, "abcd-1234");
                records.Sort();
                records.Count.Should().Be(3);

                sut = records[0];
                sut.TargetId.Should().Be(1);
                Assert.That(sut.LightSessionDate, Is.EqualTo(dt).Within(TimeSpan.FromSeconds(1.0)));
                Assert.That(sut.FlatsTakenDate, Is.EqualTo(dt.AddDays(2)).Within(TimeSpan.FromSeconds(1.0)));
                sut.ProfileId.Should().Be("abcd-1234");
                sut.FlatsType.Should().Be(FlatHistory.FLAT_TYPE_PANEL);
                sut.FilterName.Should().Be("Ha");
                sut.Gain.Should().Be(10);
                sut.Offset.Should().Be(20);
                sut.BinningMode.X.Should().Be(2);
                sut.ReadoutMode.Should().Be(0);
                sut.Rotation.Should().Be(123.4);
                sut.ROI.Should().Be(89);

                sut = records[1];
                sut.TargetId.Should().Be(1);
                Assert.That(sut.LightSessionDate, Is.EqualTo(dt.AddDays(1)).Within(TimeSpan.FromSeconds(1.0)));
                Assert.That(sut.FlatsTakenDate, Is.EqualTo(dt.AddDays(3)).Within(TimeSpan.FromSeconds(1.0)));
                sut.LightSessionId.Should().Be(24);
                sut.ProfileId.Should().Be("abcd-1234");
                sut.FlatsType.Should().Be(FlatHistory.FLAT_TYPE_SKY);
                sut.FilterName.Should().Be("O3");
                sut.Gain.Should().Be(10);
                sut.Offset.Should().Be(20);
                sut.BinningMode.X.Should().Be(2);
                sut.ReadoutMode.Should().Be(0);
                sut.Rotation.Should().Be(123.4);
                sut.ROI.Should().Be(89);

                sut = records[2];
                sut.TargetId.Should().Be(1);
                Assert.That(sut.LightSessionDate, Is.EqualTo(dt.AddDays(1)).Within(TimeSpan.FromSeconds(1.0)));
                Assert.That(sut.FlatsTakenDate, Is.EqualTo(dt.AddDays(4)).Within(TimeSpan.FromSeconds(1.0)));
                sut.LightSessionId.Should().Be(25);
                sut.ProfileId.Should().Be("abcd-1234");
                sut.FlatsType.Should().Be(FlatHistory.FLAT_TYPE_PANEL);
                sut.FilterName.Should().Be("S2");
                sut.Gain.Should().Be(10);
                sut.Offset.Should().Be(20);
                sut.BinningMode.X.Should().Be(2);
                sut.ReadoutMode.Should().Be(0);
                sut.Rotation.Should().Be(ImageMetadata.NO_ROTATOR_ANGLE);
                sut.ROI.Should().Be(89);
            }
        }

        [Test, Order(10)]
        [NonParallelizable]
        public void TestOverrideExposureOrder() {
            using (var context = db.GetContext()) {
                OverrideExposureOrderItem oeo1 = new(2, 1, OverrideExposureOrderAction.Exposure, 0);
                OverrideExposureOrderItem oeo2 = new(2, 2, OverrideExposureOrderAction.Dither, -1);
                context.OverrideExposureOrderSet.Add(oeo1);
                context.OverrideExposureOrderSet.Add(oeo2);
                context.SaveChanges();

                Target t1 = context.GetTarget(1, 1);
                t1.OverrideExposureOrders.Count.Should().Be(0);

                Target t2 = context.GetTarget(2, 2);
                t2.OverrideExposureOrders.Count.Should().Be(2);
                oeo1.Equals(t2.OverrideExposureOrders[0]).Should().BeTrue();
                oeo2.Equals(t2.OverrideExposureOrders[1]).Should().BeTrue();

                context.ClearExistingOverrideExposureOrders(2);
                context.SaveChanges();

                t2 = context.GetTarget(2, 2);
                t2.OverrideExposureOrders.Count.Should().Be(0);
            }
        }

        [Test, Order(11)]
        [NonParallelizable]
        public void TestFilterCadence() {
            using (var context = db.GetContext()) {
                FilterCadenceItem fc1 = new(2, 1, true, FilterCadenceAction.Exposure, 1);
                FilterCadenceItem fc2 = new(2, 2, false, FilterCadenceAction.Dither, -1);
                List<FilterCadenceItem> list = new List<FilterCadenceItem>() { fc1, fc2 };
                context.ReplaceFilterCadences(2, list);
                context.SaveChanges();

                Target t1 = context.GetTarget(1, 1);
                t1.FilterCadences.Count.Should().Be(0);

                Target t2 = context.GetTarget(2, 2);
                t2.FilterCadences.Count.Should().Be(2);
                fc1.Equals(t2.FilterCadences[0]).Should().BeTrue();
                fc2.Equals(t2.FilterCadences[1]).Should().BeTrue();

                context.ClearExistingFilterCadences(2);
                context.SaveChanges();

                t2 = context.GetTarget(2, 2);
                t2.FilterCadences.Count.Should().Be(0);
            }
        }

        [Test, Order(12)]
        [NonParallelizable]
        public void TestFilterSwitchFrequencyChange() {
            using (var context = db.GetContext()) {
                Project p1 = new Project(profileId);
                p1.Name = "Project: FSF Change";
                p1.Description = "";
                p1.State = ProjectState.Active;
                p1.ActiveDate = markDate;
                p1.MinimumTime = 60;
                p1.MinimumAltitude = 23;
                p1.UseCustomHorizon = false;
                p1.HorizonOffset = 11;
                p1.FilterSwitchFrequency = 12;
                p1.DitherEvery = 14;
                p1.EnableGrader = false;
                p1.IsMosaic = true;
                p1.FlatsHandling = Project.FLATS_HANDLING_OFF;

                p1.RuleWeights = new List<RuleWeight> {
                        {new RuleWeight("a", .1) },
                        {new RuleWeight("b", .2) },
                        {new RuleWeight("c", .3) }
                    };

                Target t1 = new Target();
                t1.Name = "T10";
                t1.ra = TestData.M42.RADegrees;
                t1.dec = TestData.M42.Dec;
                p1.Targets.Add(t1);

                p1 = context.AddNewProject(p1);

                FilterCadenceItem fc1 = new(t1.Id, 1, true, FilterCadenceAction.Exposure, 1);
                FilterCadenceItem fc2 = new(t1.Id, 2, false, FilterCadenceAction.Dither, -1);
                List<FilterCadenceItem> list = new List<FilterCadenceItem>() { fc1, fc2 };
                context.ReplaceFilterCadences(t1.Id, list);
                context.SaveChanges();

                var fcs = context.GetFilterCadences(t1.Id);
                fcs.Count.Should().Be(2);

                p1.MinimumAltitude++; // does not trigger FC clear
                p1 = context.SaveProject(p1);
                fcs = context.GetFilterCadences(t1.Id);
                fcs.Count.Should().Be(2);

                p1.FilterSwitchFrequency++; // should trigger FC clear
                p1 = context.SaveProject(p1);
                fcs = context.GetFilterCadences(t1.Id);
                fcs.Count.Should().Be(0);
            }
        }

        [Test, Order(13)]
        [NonParallelizable]
        public void TestGetByGuid() {
            using (var context = db.GetContext()) {
                context.GetAcquiredImageByGuid("foo").Should().BeNull();
                context.GetExposurePlanByGuid("foo").Should().BeNull();
                context.GetExposureTemplateByGuid("foo").Should().BeNull();
                context.GetProjectByGuid("foo").Should().BeNull();
                context.GetTargetByGuid("foo").Should().BeNull();

                List<Project> projects = context.GetAllProjects(profileId);
                projects.Count.Should().Be(3);
                Project p2 = projects[1];

                Project p2Reload = context.GetProjectByGuid(p2.Guid);
                p2Reload.Should().NotBeNull();
                p2Reload.Name.Should().Be(p2.Name);

                Target p2t1 = p2.Targets.First();

                Target tReload = context.GetTargetByGuid(p2t1.Guid);
                tReload.Should().NotBeNull();
                tReload.Name.Should().Be(p2t1.Name);
            }
        }

        private void LoadTestDatabase() {
            using (var context = db.GetContext()) {
                try {
                    Project p1 = new Project(profileId);
                    p1.Name = "Project: M42";
                    p1.Description = "";
                    p1.State = ProjectState.Active;
                    p1.ActiveDate = markDate;
                    p1.MinimumTime = 60;
                    p1.MinimumAltitude = 23;
                    p1.UseCustomHorizon = false;
                    p1.HorizonOffset = 11;
                    p1.FilterSwitchFrequency = 12;
                    p1.DitherEvery = 14;
                    p1.EnableGrader = false;
                    p1.IsMosaic = true;
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
                    t1.Name = "M42";
                    t1.ra = TestData.M42.RADegrees;
                    t1.dec = TestData.M42.Dec;
                    p1.Targets.Add(t1);

                    ExposurePlan ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etHa.Id;
                    ep.Desired = 3;
                    ep.Exposure = 20;
                    t1.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etOIII.Id;
                    ep.Desired = 3;
                    ep.Exposure = 20;
                    t1.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etSII.Id;
                    ep.Desired = 3;
                    ep.Exposure = 20;
                    t1.ExposurePlans.Add(ep);
                    context.ProjectSet.Add(p1);

                    Project p2 = new Project(profileId);
                    p2.Name = "Project: IC1805";
                    p2.Description = "";
                    p2.State = ProjectState.Active;
                    p2.ActiveDate = markDate;
                    p2.MinimumTime = 90;
                    p2.MinimumAltitude = 24;
                    p2.UseCustomHorizon = true;
                    p2.HorizonOffset = 12;
                    p2.FilterSwitchFrequency = 14;
                    p2.DitherEvery = 16;
                    p2.EnableGrader = false;
                    p2.IsMosaic = false;
                    p2.FlatsHandling = 3;

                    p2.RuleWeights = new List<RuleWeight> {
                        {new RuleWeight("d", .4) },
                        {new RuleWeight("e", .5) },
                        {new RuleWeight("f", .6) }
                    };

                    Target t2 = new Target();
                    t2.Name = "IC1805";
                    t2.Enabled = false;
                    t2.ra = TestData.IC1805.RADegrees;
                    t2.dec = TestData.IC1805.Dec;
                    p2.Targets.Add(t2);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etHa.Id;
                    ep.Desired = 5;
                    ep.Exposure = 20;
                    t2.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etOIII.Id;
                    ep.Desired = 5;
                    ep.Exposure = 20;
                    t2.ExposurePlans.Add(ep);

                    ep = new ExposurePlan(profileId);
                    ep.ExposureTemplateId = etSII.Id;
                    ep.Desired = 5;
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