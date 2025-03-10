using FluentAssertions;
using NINA.Plugin.TargetScheduler.Controls.Reporting;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NUnit.Framework;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Controls.Reporting {

    [TestFixture]
    public class TargetAcquisitionSummaryTest {

        [Test]
        public void testBasic() {
            Target target = new Target();
            List<AcquiredImage> list = new List<AcquiredImage>();
            list.Add(GetAI("Lum", 300, GradingStatus.Accepted));
            TargetAcquisitionSummary sut = new TargetAcquisitionSummary(list);
            sut.Rows.Count.Should().Be(2);
            AssertRow(sut.Rows[0], "Lum", 1, 300, 300, 0, 0);
            AssertRow(sut.Rows[1], TargetAcquisitionSummary.TOTAL_LBL, 1, 300, 300, 0, 0);

            list.Add(GetAI("Lum", 300, GradingStatus.Rejected));
            sut = new TargetAcquisitionSummary(list);
            sut.Rows.Count.Should().Be(2);
            AssertRow(sut.Rows[0], "Lum", 2, 600, 300, 300, 0);
            AssertRow(sut.Rows[1], TargetAcquisitionSummary.TOTAL_LBL, 2, 600, 300, 300, 0);

            list.Add(GetAI("Lum", 300, GradingStatus.Pending));
            sut = new TargetAcquisitionSummary(list);
            sut.Rows.Count.Should().Be(2);
            AssertRow(sut.Rows[0], "Lum", 3, 900, 300, 300, 300);
            AssertRow(sut.Rows[1], TargetAcquisitionSummary.TOTAL_LBL, 3, 900, 300, 300, 300);

            list.Add(GetAI("Red", 180, GradingStatus.Accepted));
            sut = new TargetAcquisitionSummary(list);
            sut.Rows.Count.Should().Be(3);
            AssertRow(sut.Rows[0], "Lum", 3, 900, 300, 300, 300);
            AssertRow(sut.Rows[1], "Red", 1, 180, 180, 0, 0);
            AssertRow(sut.Rows[2], TargetAcquisitionSummary.TOTAL_LBL, 4, 1080, 480, 300, 300);

            list.Add(GetAI("Grn", 600, GradingStatus.Rejected));
            sut = new TargetAcquisitionSummary(list);
            sut.Rows.Count.Should().Be(4);
            AssertRow(sut.Rows[0], "Lum", 3, 900, 300, 300, 300);
            AssertRow(sut.Rows[1], "Red", 1, 180, 180, 0, 0);
            AssertRow(sut.Rows[2], "Grn", 1, 600, 0, 600, 0);
            AssertRow(sut.Rows[3], TargetAcquisitionSummary.TOTAL_LBL, 5, 1680, 480, 900, 300);

            for (int i = 0; i < 21; i++) {
                list.Add(GetAI("Blu", 610, GradingStatus.Pending));
            }

            sut = new TargetAcquisitionSummary(list);
            sut.Rows.Count.Should().Be(5);
            AssertRow(sut.Rows[0], "Lum", 3, 900, 300, 300, 300);
            AssertRow(sut.Rows[1], "Red", 1, 180, 180, 0, 0);
            AssertRow(sut.Rows[2], "Grn", 1, 600, 0, 600, 0);
            AssertRow(sut.Rows[3], "Blu", 21, 12810, 0, 0, 12810);
            AssertRow(sut.Rows[4], TargetAcquisitionSummary.TOTAL_LBL, 26, 14490, 480, 900, 13110);
        }

        [Test]
        public void testEmpty() {
            TargetAcquisitionSummary sut = new TargetAcquisitionSummary(null);
            sut.Rows.Count.Should().Be(0);

            TargetAcquisitionSummaryRow row = new TargetAcquisitionSummaryRow(null, null);
            row.Exposures.Should().Be(0);
            row.TotalTime.Should().Be(0);
            row.AcceptedTime.Should().Be(0);
            row.RejectedTime.Should().Be(0);
            row.PendingTime.Should().Be(0);
        }

        private AcquiredImage GetAI(string filterName, double duration, GradingStatus status) {
            AcquiredImage ai = new AcquiredImage(new ImageMetadata() { ExposureDuration = duration });
            ai.FilterName = filterName;
            ai.GradingStatus = status;
            return ai;
        }

        private void AssertRow(TargetAcquisitionSummaryRow row, string key, int exp, int tt, int at, int rt, int pt) {
            row.Key.Should().Be(key);
            row.Exposures.Should().Be(exp);
            row.TotalTime.Should().Be(tt);
            row.AcceptedTime.Should().Be(at);
            row.RejectedTime.Should().Be(rt);
            row.PendingTime.Should().Be(pt);
        }
    }
}