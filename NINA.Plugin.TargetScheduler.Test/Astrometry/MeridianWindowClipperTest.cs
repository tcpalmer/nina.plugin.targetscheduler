using FluentAssertions;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Planning;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Astrometry {

    [TestFixture]
    public class MeridianWindowClipperTest {

        [Test]
        public void ClipTest16() {
            // Case 1: ------S------M======T======M -> start before the entire span (clip start)
            // Case 6: M======T======M------E------ -> end after the entire span (clip end)
            DateTime start = DateTime.Now.Date.AddHours(1);
            DateTime transit = start.AddHours(2);
            DateTime end = start.AddHours(4);

            TimeInterval ti = new MeridianWindowClipper().Clip(start, transit, end, 60);
            AssertTimeInterval(ti, transit.AddSeconds(-60 * 60), transit.AddSeconds(60 * 60));
        }

        [Test]
        public void ClipTest17() {
            // Case 1: ------S------M======T======M -> start before the entire span (clip start)
            // Case 7: ------M===E===T======M------ -> end in span, before transit (end no change)
            DateTime start = DateTime.Now.Date.AddHours(1);
            DateTime transit = start.AddHours(2);
            DateTime end = transit.AddMinutes(-5);

            TimeInterval ti = new MeridianWindowClipper().Clip(start, transit, end, 60);
            AssertTimeInterval(ti, transit.AddSeconds(-60 * 60), end);
        }

        [Test]
        public void ClipTest18() {
            // Case 1: ------S------M======T======M -> start before the entire span (clip start)
            // Case 8: ------M======T===E===M------ -> end in span, after transit (end no change)
            DateTime start = DateTime.Now.Date.AddHours(1);
            DateTime transit = start.AddHours(2);
            DateTime end = transit.AddMinutes(5);

            TimeInterval ti = new MeridianWindowClipper().Clip(start, transit, end, 60);
            AssertTimeInterval(ti, transit.AddSeconds(-60 * 60), end);
        }

        [Test]
        public void ClipTest2() {
            // Case 2: M======T======M------S------ -> start after the entire span (reject)
            DateTime start = DateTime.Now.Date.AddHours(4);
            DateTime transit = start.AddHours(-2);
            DateTime end = start.AddHours(1);

            TimeInterval ti = new MeridianWindowClipper().Clip(start, transit, end, 60);
            ti.Should().BeNull();
        }

        [Test]
        public void ClipTest36() {
            // Case 3: ------M===S===T======M------ -> start in span, before transit (start no change)
            // Case 6: M======T======M------E------ -> end after the entire span (clip end)
            DateTime start = DateTime.Now.Date.AddHours(3);
            DateTime transit = start.AddHours(.5);
            DateTime end = start.AddHours(3);

            TimeInterval ti = new MeridianWindowClipper().Clip(start, transit, end, 60);
            AssertTimeInterval(ti, start, transit.AddSeconds(60 * 60));
        }

        [Test]
        public void ClipTest5() {
            // Case 5: ------E------M======T======M -> end before the entire span (reject)
            DateTime start = DateTime.Now.Date.AddHours(1);
            DateTime transit = start.AddHours(4);
            DateTime end = start.AddHours(2);

            TimeInterval ti = new MeridianWindowClipper().Clip(start, transit, end, 60);
            ti.Should().BeNull();
        }

        [Test]
        public void ClipTestBadTransit() {
            DateTime start = DateTime.Now;
            DateTime end = DateTime.Now.AddHours(1);

            TimeInterval ti = new MeridianWindowClipper().Clip(start, DateTime.MinValue, end, 60);
            ti.Should().BeNull();
        }

        private void AssertTimeInterval(TimeInterval interval, DateTime expectedStart, DateTime expectedEnd) {
            TimeSpan precision = TimeSpan.FromSeconds(1);
            interval.StartTime.Should().BeCloseTo(expectedStart, precision);
            interval.EndTime.Should().BeCloseTo(expectedEnd, precision);
        }
    }
}