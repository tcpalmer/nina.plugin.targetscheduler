using FluentAssertions;
using LinqKit;
using Moq;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NUnit.Framework;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Planning.Exposures {

    [TestFixture]
    public class SmartExposureRotateManagerTest {

        [SetUp]
        public void Setup() {
            SmartExposureRotateCache.Clear();
        }

        [TearDown]
        public void TearDown() {
            SmartExposureRotateCache.Clear();
        }

        [Test]
        public void testAllActive() {
            Mock<IProject> pp = PlanMocks.GetMockPlanProject("P1", ProjectState.Active);
            ExposureCompletionHelper helper = new ExposureCompletionHelper(true, 0, 125);

            pp.SetupAllProperties();
            pp.SetupProperty(p => p.DitherEvery, 1);
            pp.SetupProperty(p => p.SmartExposureOrder, true);
            pp.SetupProperty(p => p.FilterSwitchFrequency, 2);
            pp.SetupProperty(p => p.ExposureCompletionHelper, helper);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("T1", TestData.M31);
            pt.SetupProperty(t => t.Project, pp.Object);
            pt.SetupProperty(t => t.DatabaseId, 1);
            SetEPs(pt);

            string[] expected = { "L", "L", "R", "R", "G", "G", "B", "B",
                                  "L", "L", "R", "R", "G", "G", "B", "B",
                                  "L", "L", "R", "R", "G", "G", "B", "B" };
            SmartExposureRotateManager sut = new SmartExposureRotateManager(pt.Object, 2);

            expected.ForEach(e => {
                IExposure selected = sut.Select(pt.Object.ExposurePlans);
                selected.FilterName.Should().Be(e);
                sut.ExposureTaken(selected);
            });
        }

        [Test]
        public void testSomeActive() {
            Mock<IProject> pp = PlanMocks.GetMockPlanProject("P1", ProjectState.Active);
            ExposureCompletionHelper helper = new ExposureCompletionHelper(true, 0, 125);

            pp.SetupAllProperties();
            pp.SetupProperty(p => p.DitherEvery, 1);
            pp.SetupProperty(p => p.SmartExposureOrder, true);
            pp.SetupProperty(p => p.FilterSwitchFrequency, 2);
            pp.SetupProperty(p => p.ExposureCompletionHelper, helper);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("T1", TestData.M31);
            pt.SetupProperty(t => t.Project, pp.Object);
            pt.SetupProperty(t => t.DatabaseId, 1);
            SetEPs(pt);

            string[] expected1 = { "L", "L", "G", "G", "L", "L", "G", "G", "L", "L", "G", "G" };
            List<IExposure> actives = new List<IExposure> { pt.Object.ExposurePlans[0], pt.Object.ExposurePlans[2] };
            SmartExposureRotateManager sut = new SmartExposureRotateManager(pt.Object, 2);

            expected1.ForEach(e => {
                IExposure selected = sut.Select(actives);
                selected.FilterName.Should().Be(e);
                sut.ExposureTaken(selected);
            });

            string[] expected2 = { "R", "R", "B", "B", "R", "R", "B", "B", "R", "R", "B", "B" };
            actives = new List<IExposure> { pt.Object.ExposurePlans[1], pt.Object.ExposurePlans[3] };

            expected2.ForEach(e => {
                IExposure selected = sut.Select(actives);
                selected.FilterName.Should().Be(e);
                sut.ExposureTaken(selected);
            });
        }

        private void SetEPs(Mock<ITarget> pt) {
            Mock<IExposure> Lpf = PlanMocks.GetMockPlanExposure("L", 10, 0);
            Mock<IExposure> Rpf = PlanMocks.GetMockPlanExposure("R", 10, 0);
            Mock<IExposure> Gpf = PlanMocks.GetMockPlanExposure("G", 10, 0);
            Mock<IExposure> Bpf = PlanMocks.GetMockPlanExposure("B", 10, 0);

            Lpf.SetupProperty(e => e.DatabaseId, 1);
            Rpf.SetupProperty(e => e.DatabaseId, 2);
            Gpf.SetupProperty(e => e.DatabaseId, 3);
            Bpf.SetupProperty(e => e.DatabaseId, 4);

            PlanMocks.AddMockPlanFilter(pt, Lpf);
            PlanMocks.AddMockPlanFilter(pt, Rpf);
            PlanMocks.AddMockPlanFilter(pt, Gpf);
            PlanMocks.AddMockPlanFilter(pt, Bpf);
        }
    }
}