using FluentAssertions;
using NINA.Plugin.TargetScheduler.Sequencer;
using NUnit.Framework;
using System.Linq;
using System.Windows.Data;

namespace NINA.Plugin.TargetScheduler.Test.Sequencer {

    [TestFixture]
    public class SchedulerProgressVMTest {

        [Test]
        public void testProgressItemListLazyCreationAndGrouping() {
            SchedulerProgressVM sut = new SchedulerProgressVM();

            sut.ProgressItemList.Should().NotBeNull();
            sut.ItemsView.Should().NotBeNull();
            sut.ItemsView.GroupDescriptions.Should().HaveCount(1);
            sut.ItemsView.GroupDescriptions[0].Should().BeOfType<PropertyGroupDescription>();
            ((PropertyGroupDescription)sut.ItemsView.GroupDescriptions[0]).PropertyName.Should().Be("Group");
        }

        [Test]
        public void testAddAppendsRowUnderCurrentGroup() {
            SchedulerProgressVM sut = new SchedulerProgressVM();
            sut.TargetStart("P1", "T1");

            sut.Add(SchedulerProgressVM.SlewLabel);

            sut.ProgressItemList.Should().HaveCount(1);
            sut.CurrentRow.Should().NotBeNull();
            sut.ProgressItemList[0].ItemName.Should().Be(SchedulerProgressVM.SlewLabel);
            sut.ProgressItemList[0].Group.Should().Be(sut.CurrentGroup);
        }

        [Test]
        public void testWaitStartSetsCurrentGroup() {
            SchedulerProgressVM sut = new SchedulerProgressVM();

            sut.WaitStart(null);

            sut.CurrentGroup.Should().StartWith("Waiting :");
        }

        [Test]
        public void testSameSwitchFilterDedupesConsecutiveSameFilter() {
            SchedulerProgressVM sut = new SchedulerProgressVM();
            sut.TargetStart("P1", "T1");

            sut.Add(SchedulerProgressVM.SwitchFilterLabel, "Ha");
            sut.Add(SchedulerProgressVM.SwitchFilterLabel, "Ha");

            sut.ProgressItemList.Should().HaveCount(1);
        }

        [Test]
        public void testSameSwitchFilterAllowsDifferentFilter() {
            SchedulerProgressVM sut = new SchedulerProgressVM();
            sut.TargetStart("P1", "T1");

            sut.Add(SchedulerProgressVM.SwitchFilterLabel, "Ha");
            sut.Add(SchedulerProgressVM.SwitchFilterLabel, "OIII");

            sut.ProgressItemList.Should().HaveCount(2);
            sut.ProgressItemList.Last().FilterName.Should().Be("OIII");
        }

        [Test]
        public void testSameSwitchFilterAllowsSameFilterInNewGroup() {
            SchedulerProgressVM sut = new SchedulerProgressVM();

            sut.TargetStart("P1", "T1");
            sut.Add(SchedulerProgressVM.SwitchFilterLabel, "Ha");

            sut.TargetStart("P2", "T2");
            sut.Add(SchedulerProgressVM.SwitchFilterLabel, "Ha");

            sut.ProgressItemList.Should().HaveCount(2);
        }

        [Test]
        public void testResetClearsProgressItemList() {
            SchedulerProgressVM sut = new SchedulerProgressVM();
            sut.TargetStart("P1", "T1");
            sut.Add(SchedulerProgressVM.SlewLabel);
            sut.ProgressItemList.Should().HaveCount(1);

            sut.Reset();

            sut.ProgressItemList.Should().BeEmpty();
        }
    }
}
