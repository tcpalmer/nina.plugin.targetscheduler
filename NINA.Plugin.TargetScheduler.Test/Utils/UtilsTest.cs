using FluentAssertions;
using Moq;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Test.Util {

    [TestFixture]
    public class UtilsTest {

        [Test]
        [TestCase(0, "0h 0m")]
        [TestCase(32, "0h 32m")]
        [TestCase(61, "1h 1m")]
        [TestCase(719, "11h 59m")]
        [TestCase(720, "12h 0m")]
        public void TestMtoHM(int min, string expected) {
            Utils.MtoHM(min).Should().Be(expected);
        }

        [Test]
        [TestCase(0, "  0h 00m 00s")]
        [TestCase(32, "  0h 00m 32s")]
        [TestCase(61, "  0h 01m 01s")]
        [TestCase(3600, "  1h 00m 00s")]
        [TestCase(3601, "  1h 00m 01s")]
        [TestCase(10000, "  2h 46m 40s")]
        [TestCase(105924, " 29h 25m 24s")]
        public void TestStoHMS(int secs, string expected) {
            Utils.StoHMS(secs).Should().Be(expected);
        }

        [Test]
        [TestCase(null, 0)]
        [TestCase("", 0)]
        [TestCase("0h 0m", 0)]
        [TestCase("0h 32m", 32)]
        [TestCase("1h 1m", 61)]
        [TestCase("11h 59m", 719)]
        [TestCase("12h 0m", 720)]
        public void TestHMtoM(string hm, int expected) {
            Utils.HMtoM(hm).Should().Be(expected);
        }

        [Test]
        [TestCase(null, " (1)")]
        [TestCase("", " (1)")]
        [TestCase("foo", "foo (1)")]
        [TestCase("foo (1)", "foo (2)")]
        [TestCase("foo (99)", "foo (100)")]
        public void TestCopiedItemName(string name, string expected) {
            string sut = Utils.CopiedItemName(name);
            sut.Should().Be(expected);
        }

        [Test]
        public void TestMakeUniqueName() {
            Utils.MakeUniqueName(null, null).Should().Be(" (1)");
            Utils.MakeUniqueName(null, "").Should().Be(" (1)");
            Utils.MakeUniqueName(null, "foo").Should().Be("foo");
            Utils.MakeUniqueName(new List<string>(), "foo").Should().Be("foo");

            List<string> current = new List<string>() { "foo", "bar" };
            Utils.MakeUniqueName(current, "foo").Should().Be("foo (1)");

            current.Add("foo (16)");
            Utils.MakeUniqueName(current, "foo").Should().Be("foo (17)");

            current.Add("foo (1)");
            current.Add("foo (8)");
            Utils.MakeUniqueName(current, "foo").Should().Be("foo (17)");
            Utils.MakeUniqueName(current, "foo (7)").Should().Be("foo (7)");
            Utils.MakeUniqueName(current, "foo (8)").Should().Be("foo (17)");
        }

        [Test]
        public void TestLookupFilter() {
            Action action = () => Utils.LookupFilter(null, "");
            action.Should().Throw<ArgumentNullException>().WithMessage("Value cannot be null. (Parameter 'profile')");

            Mock<IFilterWheelSettings> mockFWSettings = new Mock<IFilterWheelSettings>();
            ObserveAllCollection<FilterInfo> filters = new ObserveAllCollection<FilterInfo>();
            filters.Add(new FilterInfo() { Name = "Lum" });
            mockFWSettings.SetupProperty(s => s.FilterWheelFilters, filters);
            Mock<IProfile> mockProfile = new Mock<IProfile>();
            mockProfile.SetupProperty(p => p.FilterWheelSettings, mockFWSettings.Object);

            Utils.LookupFilter(mockProfile.Object, "Lum").Name.Should().Be("Lum");

            action = () => Utils.LookupFilter(mockProfile.Object, "Red");
            action.Should().Throw<SequenceEntityFailedException>().WithMessage("failed to find FilterInfo for filter: Red");
        }

        [Test]
        public void TestFormatDateTimeFull() {
            Utils.FormatDateTimeFull(null).Should().Be("n/a");
            Utils.FormatDateTimeFull(new DateTime(2025, 1, 2, 3, 4, 5)).Should().Be("2025-01-02 03:04:05 -05:00");
            Utils.FormatDateTimeFull(new DateTime(2025, 6, 21, 3, 4, 5)).Should().Be("2025-06-21 03:04:05 -04:00");
            Utils.FormatDateTimeFull(new DateTime(2025, 12, 31, 23, 59, 59)).Should().Be("2025-12-31 23:59:59 -05:00");
        }

        [Test]
        public void TestFormatDateTime() {
            Utils.FormatDateTime(null).Should().Be("n/a");
            Utils.FormatDateTime(new DateTime(2025, 1, 2, 3, 4, 5)).Should().Be("2025-01-02 03:04:05");
            Utils.FormatDateTime(new DateTime(2025, 6, 21, 3, 4, 5)).Should().Be("2025-06-21 03:04:05");
            Utils.FormatDateTime(new DateTime(2025, 12, 31, 23, 59, 59)).Should().Be("2025-12-31 23:59:59");
        }

        [Test]
        public void TestFormatCoordinates() {
            Utils.FormatCoordinates(null).Should().Be("n/a");
            Utils.FormatCoordinates(TestData.M31).Should().Be("00:42:44 41° 16' 07\"");
        }

        [Test]
        [TestCase(null, "")]
        [TestCase(0, "0")]
        [TestCase(1, "1")]
        [TestCase(999, "999")]
        public void TestFormatInt(int? val, string expected) {
            Utils.FormatInt(val).Should().Be(expected);
        }

        [Test]
        [TestCase(double.NaN, "")]
        [TestCase(1, "1")]
        [TestCase(.0001, "0.0001")]
        [TestCase(.00001, "0")]
        [TestCase(99999.9901, "99999.9901")]
        public void TestFormatDbl(double val, string expected) {
            Utils.FormatDbl(val).Should().Be(expected);
        }

        [Test]
        [TestCase(double.NaN, "{0:0.####}", "")]
        [TestCase(.0001, "{0:0.#####}", "0.0001")]
        [TestCase(.00001, "{0:0.#####}", "0.00001")]
        public void TestFormatDbl2(double val, string fmt, string expected) {
            Utils.FormatDbl(val, fmt).Should().Be(expected);
        }

        [Test]
        [TestCase(double.NaN, "--")]
        [TestCase(-0.1, "--")]
        [TestCase(.0001, "0.0001")]
        [TestCase(.00001, "0")]
        [TestCase(99999.9901, "99999.9901")]
        public void TestFormatHF(double val, string expected) {
            Utils.FormatHF(val).Should().Be(expected);
        }

        [Test]
        public void TestMidpoint() {
            DateTime start = DateTime.Now;
            DateTime mid = Utils.GetMidpointTime(start, start.AddHours(1));
            mid.Should().Be(start.AddMinutes(30));
        }

        [Test]
        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        [TestCase(100, 1)]
        [TestCase(200, 1)]
        [TestCase(30, 0.3)]
        public void TestConcertROI(double val, double expected) {
            Utils.ConvertROI(val).Should().Be(expected);
        }

        [Test]
        [TestCase(0, "0h 0m 0s")]
        [TestCase(75, "5h 0m 0s")]
        [TestCase(90.25, "6h 1m 0s")]
        [TestCase(345.25, "23h 1m 0s")]
        public void TestGetRAString(double raDegrees, string expected) {
            Utils.GetRAString(raDegrees).Should().Be(expected);
        }

        [Test]
        public void TestCancelException() {
            Utils.IsCancelException(null).Should().BeFalse();
            Utils.IsCancelException(new Exception("foo")).Should().BeFalse();

            Utils.IsCancelException(new TaskCanceledException()).Should().BeTrue();
            Utils.IsCancelException(new OperationCanceledException()).Should().BeTrue();

            Utils.IsCancelException(new Exception("A task was canceled.")).Should().BeTrue();
            Utils.IsCancelException(new Exception("cancelled")).Should().BeTrue();
        }

        [Test]
        public void TestMoveFile() {
            string tempDir = null;
            try {
                tempDir = Path.Combine(Path.GetTempPath(), "movefiletest");
                Directory.CreateDirectory(tempDir);

                string srcFile1 = Path.Combine(tempDir, "test1.txt");
                File.WriteAllText(srcFile1, "this is test file 1");
                string srcFile2 = Path.Combine(tempDir, "test2.txt");
                File.WriteAllText(srcFile2, "this is test file 2");

                string dstDir = Path.Combine(Path.GetDirectoryName(srcFile1), "rejected");
                Utils.MoveFile(srcFile1, dstDir).Should().BeTrue();
                Utils.MoveFile(srcFile2, dstDir).Should().BeTrue();

                string newFile1 = Path.Combine(dstDir, Path.GetFileName(srcFile1));
                File.Exists(newFile1).Should().BeTrue();

                string newFile2 = Path.Combine(dstDir, Path.GetFileName(srcFile2));
                File.Exists(newFile2).Should().BeTrue();
            } finally {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        [TestCase(null, Int32.MinValue)]
        [TestCase("", Int32.MinValue)]
        [TestCase("foo", Int32.MinValue)]
        [TestCase("-100", -100)]
        [TestCase("-1", -1)]
        [TestCase("0", 0)]
        [TestCase("1", 1)]
        [TestCase("100", 100)]
        public void TestStringToInt(string s, int expected) {
            Utils.StringToInt(s).Should().Be(expected);
        }
    }
}