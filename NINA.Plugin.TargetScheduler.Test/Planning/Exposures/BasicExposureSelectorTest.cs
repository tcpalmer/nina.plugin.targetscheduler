using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Planning.Exposures {

    [TestFixture]
    public class BasicExposureSelectorTest {

        [Test]
        public void testBasicExposureSelector() {
            Mock<IProject> pp = PlanMocks.GetMockPlanProject("P1", ProjectState.Active);
            pp.SetupAllProperties();
            pp.SetupProperty(p => p.FilterSwitchFrequency, 2);
            pp.SetupProperty(p => p.DitherEvery, 1);
            pp.SetupProperty(p => p.SmartExposureOrder, false);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("T1", TestData.M31);
            pt.SetupProperty(t => t.Project, pp.Object);
            SetEPs(pt);

            BasicExposureSelector sut = new BasicExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(t => t.ExposureSelector, sut);

            IExposure e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("R");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("R");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("G");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("G");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("B");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("B");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeFalse();
        }

        [Test]
        public void testForOverDither() {
            Mock<IProject> pp = PlanMocks.GetMockPlanProject("P1", ProjectState.Active);
            pp.SetupAllProperties();
            pp.SetupProperty(p => p.FilterSwitchFrequency, 2);
            pp.SetupProperty(p => p.DitherEvery, 1);
            pp.SetupProperty(p => p.SmartExposureOrder, false);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("T1", TestData.M31);
            pt.SetupProperty(t => t.Project, pp.Object);
            SetEPs(pt);

            BasicExposureSelector sut = new BasicExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(t => t.ExposureSelector, sut);

            IExposure e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("R");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("R");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("G");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("G");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("B");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("B");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            pt.Object.ExposurePlans.ForEach(e => { if (e.FilterName != "L") e.Rejected = true; });
            sut.TargetReset();

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeFalse();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeTrue();
            sut.ExposureTaken(e);

            e = sut.Select(DateTime.Now, pp.Object, pt.Object);
            e.FilterName.Should().Be("L");
            e.PreDither.Should().BeTrue();
        }

        [Test]
        public void testAllExposurePlansRejected() {
            Mock<IProject> pp = PlanMocks.GetMockPlanProject("P1", ProjectState.Active);
            pp.SetupAllProperties();
            pp.SetupProperty(p => p.FilterSwitchFrequency, 2);
            pp.SetupProperty(p => p.DitherEvery, 1);
            pp.SetupProperty(p => p.SmartExposureOrder, false);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("T1", TestData.M31);
            pt.SetupProperty(t => t.Project, pp.Object);
            SetEPs(pt);
            pt.Object.ExposurePlans.ForEach(e => { e.Rejected = true; });

            BasicExposureSelector sut = new BasicExposureSelector(pp.Object, pt.Object, new Target());
            pt.SetupProperty(t => t.ExposureSelector, sut);
            sut.Select(new DateTime(2024, 12, 1), pp.Object, pt.Object).Should().BeNull();
        }

        private void SetEPs(Mock<ITarget> pt) {
            Mock<IExposure> Lpf = PlanMocks.GetMockPlanExposure("L", 10, 0);
            Mock<IExposure> Rpf = PlanMocks.GetMockPlanExposure("R", 10, 0);
            Mock<IExposure> Gpf = PlanMocks.GetMockPlanExposure("G", 10, 0);
            Mock<IExposure> Bpf = PlanMocks.GetMockPlanExposure("B", 10, 0);

            PlanMocks.AddMockPlanFilter(pt, Lpf);
            PlanMocks.AddMockPlanFilter(pt, Rpf);
            PlanMocks.AddMockPlanFilter(pt, Gpf);
            PlanMocks.AddMockPlanFilter(pt, Bpf);
        }
    }
}