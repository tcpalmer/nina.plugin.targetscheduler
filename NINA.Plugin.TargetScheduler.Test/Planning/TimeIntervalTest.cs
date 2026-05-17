using FluentAssertions;
using NINA.Plugin.TargetScheduler.Planning;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class TimeIntervalTest {

        [Test]
        public void TestOk() {
            TimeInterval sut = new TimeInterval(DateTime.Now, DateTime.Now.AddSeconds(1));
            sut.Duration.Should().Be(1);
        }

        [Test]
        public void TestOverlap() {
            DateTime t1 = DateTime.Now.Date.AddMinutes(1);
            DateTime t2 = DateTime.Now.Date.AddMinutes(2);
            DateTime t3 = DateTime.Now.Date.AddMinutes(3);
            DateTime t4 = DateTime.Now.Date.AddMinutes(4);

            TimeInterval ti1 = new TimeInterval(t1, t2);
            TimeInterval ti2 = new TimeInterval(t3, t4);
            TimeInterval overlap = ti1.Overlap(ti2);
            Assert.That(overlap, Is.Null);
            overlap = ti2.Overlap(ti1);
            Assert.That(overlap, Is.Null);

            ti1 = new TimeInterval(t2, t4);
            ti2 = new TimeInterval(t1, t3);
            overlap = ti1.Overlap(ti2);
            overlap.StartTime.Should().Be(t2);
            overlap.EndTime.Should().Be(t3);

            ti1 = new TimeInterval(t1, t3);
            ti2 = new TimeInterval(t2, t4);
            overlap = ti1.Overlap(ti2);
            overlap.StartTime.Should().Be(t2);
            overlap.EndTime.Should().Be(t3);

            ti1 = new TimeInterval(t1, t4);
            ti2 = new TimeInterval(t2, t3);
            overlap = ti1.Overlap(ti2);
            overlap.StartTime.Should().Be(t2);
            overlap.EndTime.Should().Be(t3);

            ti1 = new TimeInterval(t2, t3);
            ti2 = new TimeInterval(t1, t4);
            overlap = ti1.Overlap(ti2);
            overlap.StartTime.Should().Be(t2);
            overlap.EndTime.Should().Be(t3);

            // same start
            ti1 = new TimeInterval(t1, t2);
            ti2 = new TimeInterval(t1, t3);
            overlap = ti1.Overlap(ti2);
            overlap.StartTime.Should().Be(t1);
            overlap.EndTime.Should().Be(t2);

            // same end
            ti1 = new TimeInterval(t1, t4);
            ti2 = new TimeInterval(t2, t4);
            overlap = ti1.Overlap(ti2);
            overlap.StartTime.Should().Be(t2);
            overlap.EndTime.Should().Be(t4);

            // same both
            ti1 = new TimeInterval(t1, t4);
            ti2 = new TimeInterval(t1, t4);
            overlap = ti1.Overlap(ti2);
            overlap.StartTime.Should().Be(t1);
            overlap.EndTime.Should().Be(t4);
        }

        [Test]
        public void TestContains() {
            DateTime now = DateTime.Now;
            TimeInterval sut = new TimeInterval(now, now.AddHours(1));
            sut.Contains(now.AddSeconds(-1)).Should().BeFalse();
            sut.Contains(now).Should().BeTrue();
            sut.Contains(now.AddSeconds(1)).Should().BeTrue();
            sut.Contains(now.AddHours(1)).Should().BeTrue();
            sut.Contains(now.AddHours(2)).Should().BeFalse();
        }

        [Test]
        public void TestTotalTimeSpan() {
            DateTime now = DateTime.Now;
            TimeInterval ti1 = new TimeInterval(now, now.AddHours(1));
            TimeInterval ti2 = new TimeInterval(now.AddHours(-1), now.AddHours(1));
            TimeInterval ti3 = new TimeInterval(now, now.AddHours(2));

            TimeInterval sut = TimeInterval.GetTotalTimeSpan(ti1, ti2, ti3);
            sut.StartTime.Should().Be(ti2.StartTime);
            sut.EndTime.Should().Be(ti3.EndTime);
        }

        [Test]
        public void TestBad() {
            var ex = Assert.Throws<ArgumentException>(() => new TimeInterval(DateTime.Now, DateTime.Now.AddSeconds(-1)));
            Assert.That(ex.Message, Is.EqualTo("startTime must be before endTime"));
        }
    }
}