using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NUnit.Framework;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class FilterCadenceFactoryTest {

        [Test]
        public void testGenerateFromDatabase() {
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(m => m.FilterSwitchFrequency, 1);
            pp1.SetupProperty(m => m.SmartExposureOrder, false);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            SetEPs(pt);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            Target target = new Target();
            List<FilterCadenceItem> fcs = new List<FilterCadenceItem>();
            fcs.Add(new FilterCadenceItem(101, 1, true, FilterCadenceAction.Exposure, 0));
            fcs.Add(new FilterCadenceItem(101, 2, false, FilterCadenceAction.Exposure, 1));
            target.FilterCadences = fcs;

            // Filter cadence on database target => use
            FilterCadenceFactory sut = new FilterCadenceFactory();
            sut.Generate(pp1.Object, pt.Object, target).Count.Should().Be(2);
        }

        [Test]
        public void testGenerateFSFZero() {
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(m => m.FilterSwitchFrequency, 0);
            pp1.SetupProperty(m => m.SmartExposureOrder, false);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            SetEPs(pt);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            // Filter switch frequency = 0 => empty initial list
            FilterCadenceFactory sut = new FilterCadenceFactory();
            sut.Generate(pp1.Object, pt.Object, new Target()).Count.Should().Be(0);
        }

        [Test]
        public void testGenerateSmart() {
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(m => m.FilterSwitchFrequency, 1);
            pp1.SetupProperty(m => m.SmartExposureOrder, true);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            SetEPs(pt);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            // Smart exposure order => empty initial list
            FilterCadenceFactory sut = new FilterCadenceFactory();
            sut.Generate(pp1.Object, pt.Object, new Target()).Count.Should().Be(0);
        }

        [Test]
        public void testGenerateFSFPlus() {
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            SetEPs(pt);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            // FSF > 0
            pp1.SetupProperty(m => m.FilterSwitchFrequency, 2);
            pp1.SetupProperty(m => m.DitherEvery, 0);
            pp1.SetupProperty(m => m.SmartExposureOrder, false);
            FilterCadenceFactory sut = new FilterCadenceFactory();
            var list = sut.Generate(pp1.Object, pt.Object, new Target()).List;
            list.Should().HaveCount(8);

            AssertFilterCadence(list[0], 1, true, FilterCadenceAction.Exposure, 0);
            AssertFilterCadence(list[1], 2, false, FilterCadenceAction.Exposure, 0);
            AssertFilterCadence(list[2], 3, false, FilterCadenceAction.Exposure, 1);
            AssertFilterCadence(list[3], 4, false, FilterCadenceAction.Exposure, 1);
            AssertFilterCadence(list[4], 5, false, FilterCadenceAction.Exposure, 2);
            AssertFilterCadence(list[5], 6, false, FilterCadenceAction.Exposure, 2);
            AssertFilterCadence(list[6], 7, false, FilterCadenceAction.Exposure, 3);
            AssertFilterCadence(list[7], 8, false, FilterCadenceAction.Exposure, 3);
        }

        [Test]
        public void testGenerateOverride() {
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp1.SetupProperty(m => m.FilterSwitchFrequency, 0);
            pp1.SetupProperty(m => m.SmartExposureOrder, false);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            SetEPs(pt);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            List<OverrideExposureOrderItem> oeos = new List<OverrideExposureOrderItem>();
            oeos.Add(new OverrideExposureOrderItem(101, 1, OverrideExposureOrderAction.Exposure, 0));
            oeos.Add(new OverrideExposureOrderItem(101, 2, OverrideExposureOrderAction.Exposure, 1));
            oeos.Add(new OverrideExposureOrderItem(101, 3, OverrideExposureOrderAction.Dither, -1));

            Target target = new Target();
            target.OverrideExposureOrders = oeos;

            FilterCadenceFactory sut = new FilterCadenceFactory();
            var list = sut.Generate(pp1.Object, pt.Object, target).List;
            list.Should().HaveCount(3);

            AssertFilterCadence(list[0], 1, true, FilterCadenceAction.Exposure, 0);
            AssertFilterCadence(list[1], 2, false, FilterCadenceAction.Exposure, 1);
            AssertFilterCadence(list[2], 3, false, FilterCadenceAction.Dither, -1);
        }

        [Test]
        public void testRegenerateOnCorruptPersistedCadence() {
            Mock<IProject> pp1 = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            Mock<ITarget> pt = PlanMocks.GetMockPlanTarget("IC1805", TestData.IC1805);
            SetEPs(pt);
            PlanMocks.AddMockPlanTarget(pp1, pt);

            pp1.SetupProperty(m => m.FilterSwitchFrequency, 2);
            pp1.SetupProperty(m => m.DitherEvery, 0);
            pp1.SetupProperty(m => m.SmartExposureOrder, false);

            // Corrupt persisted cadence: orders 1,2 duplicated with two next=true (the write-race symptom).
            Target target = new Target();
            target.FilterCadences = new List<FilterCadenceItem> {
                new FilterCadenceItem(101, 1, true, FilterCadenceAction.Exposure, 0),
                new FilterCadenceItem(101, 2, false, FilterCadenceAction.Exposure, 1),
                new FilterCadenceItem(101, 1, true, FilterCadenceAction.Exposure, 0),
                new FilterCadenceItem(101, 2, false, FilterCadenceAction.Exposure, 1),
            };

            // Invalid persisted cadence is discarded and regenerated from filter switch frequency.
            FilterCadenceFactory sut = new FilterCadenceFactory();
            var list = sut.Generate(pp1.Object, pt.Object, target).List;
            list.Should().HaveCount(8);
            AssertFilterCadence(list[0], 1, true, FilterCadenceAction.Exposure, 0);
            AssertFilterCadence(list[7], 8, false, FilterCadenceAction.Exposure, 3);
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

        private void AssertFilterCadence(IFilterCadenceItem fc, int order, bool next, FilterCadenceAction action, int refIdx) {
            fc.Order.Should().Be(order);
            fc.Next.Should().Be(next);
            fc.Action.Should().Be(action);
            fc.ReferenceIdx.Should().Be(refIdx);
        }
    }
}