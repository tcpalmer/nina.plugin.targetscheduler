using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Util {

    public class Utils {
        public static readonly string DateFMT = "yyyy-MM-dd HH:mm:ss";

        public static FilterInfo LookupFilter(IProfile profile, string filterName) {
            if (profile == null) throw new ArgumentNullException("profile");

            foreach (FilterInfo filterInfo in profile?.FilterWheelSettings?.FilterWheelFilters) {
                if (filterInfo.Name == filterName) {
                    return filterInfo;
                }
            }

            throw new SequenceEntityFailedException($"failed to find FilterInfo for filter: {filterName}");
        }

        public static string MtoHM(int minutes) {
            decimal hours = Math.Floor((decimal)minutes / 60);
            int min = minutes % 60;
            return $"{hours}h {min}m";
        }

        public static string StoHMS(int seconds) {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            int hours = time.Days > 0 ? time.Hours + (time.Days * 24) : time.Hours;
            return string.Format("{0,3:##0}h {1,2:00}m {2,2:00}s", hours, time.Minutes, time.Seconds);
        }

        public static int HMtoM(string hm) {
            if (string.IsNullOrEmpty(hm)) {
                return 0;
            }

            Regex re = new Regex(@"(\d+)h\s*(\d+)m");
            Match match = re.Match(hm);
            if (match.Success) {
                int hours = int.Parse(match.Groups[1].Value);
                int minutes = int.Parse(match.Groups[2].Value);
                return hours * 60 + minutes;
            }

            return 0;
        }

        public static string CopiedItemName(string name) {
            if (string.IsNullOrEmpty(name)) {
                return " (1)";
            }

            Regex re = new Regex(@"^(.*) \((\d+)\)$");
            Match match = re.Match(name);
            if (match.Success) {
                string baseName = match.Groups[1].Value;
                int count = int.Parse(match.Groups[2].Value);
                return $"{baseName} ({count + 1})";
            }

            return name + " (1)";
        }

        public static string MakeUniqueName(List<string> currentNames, string name) {
            if (string.IsNullOrEmpty(name)) {
                return " (1)";
            }

            name = name.Trim();
            if (currentNames == null || currentNames.Count == 0 || !currentNames.Contains(name)) {
                return name;
            }

            // Find current with max count
            List<int> dupCounts = new List<int>();
            string baseName = Regex.Replace(name, @" \((\d+)\)$", "");
            Regex re = new Regex($"^{baseName} " + @"\((\d+)\)$");

            foreach (string current in currentNames) {
                Match match = re.Match(current);
                if (match.Success) {
                    dupCounts.Add(int.Parse(match.Groups[1].Value));
                }
            }

            int newMax = dupCounts.Count == 0 ? 1 : dupCounts.Max() + 1;
            return $"{baseName} ({newMax})";
        }

        public static string FormatDateTime(DateTime? dateTime) {
            return dateTime.HasValue ? String.Format("{0:yyyy-MM-dd HH:mm:ss}", dateTime) : "n/a";
        }

        public static string FormatTimespanHoursMinutes(TimeSpan timeSpan) {
            return timeSpan.ToString(@"hh\:mm");
            

        }
        public static string FormatDateTimeFull(DateTime? dateTime) {
            return dateTime.HasValue ? String.Format("{0:yyyy-MM-dd HH:mm:ss zzzz}", dateTime) : "n/a";
        }

        public static string FormatCoordinates(Coordinates coordinates) {
            return coordinates == null ? "n/a" : $"{coordinates.RAString} {coordinates.DecString}";
        }

        public static string FormatDegrees(double degrees) {
            return AstroUtil.DegreesToDMS(degrees);
        }

        public static string FormatInt(int? i) {
            return i.HasValue ? i.ToString() : "";
        }

        public static string FormatDbl(double d) {
            return FormatDbl(d, "{0:0.####}");
        }

        public static string FormatDbl(double d, string format) {
            return double.IsNaN(d) ? "" : string.Format(format, d);
        }

        public static string FormatHF(double d) {
            return double.IsNaN(d) || d <= 0 ? "--" : string.Format("{0:0.####}", d);
        }

        public static void TestWait(int seconds) {
            TSLogger.Debug($"********** TESTING: waiting for {seconds}s ...");
            Thread.Sleep(seconds * 1000);
            TSLogger.Debug("********** TESTING: wait complete");
        }

        public static DateTime GetMidpointTime(DateTime startTime, DateTime endTime) {
            long span = (long)endTime.Subtract(startTime).TotalSeconds;
            return startTime.AddSeconds(span / 2);
        }

        // Cobbled from NINA (NINA private)
        public static string DegreesToDMS(double value, string pattern) {
            bool negative = false;
            if (value < 0) {
                negative = true;
                value = -value;
            }
            if (negative) {
                pattern = "-" + pattern;
            }

            var degree = Math.Floor(value);
            var arcmin = Math.Floor(AstroUtil.DegreeToArcmin(value - degree));
            var arcminDeg = AstroUtil.ArcminToDegree(arcmin);

            var arcsec = Math.Round(AstroUtil.DegreeToArcsec(value - degree - arcminDeg), 0);
            if (arcsec == 60) {
                /* If arcsec got rounded to 60 add to arcmin instead */
                arcsec = 0;
                arcmin += 1;

                if (arcmin == 60) {
                    /* If arcmin got rounded to 60 add to degree instead */
                    arcmin = 0;
                    degree += 1;
                }
            }

            // Prevent "-0" when using ToString
            if (arcsec == 0) { arcsec = 0; }
            if (arcmin == 0) { arcmin = 0; }
            if (degree == 0) { degree = 0; }

            return string.Format(pattern, degree, arcmin, arcsec);
        }

        public static string GetRAString(double raDegrees) {
            return DegreesToDMS(AstroUtil.DegreesToHours(raDegrees), "{0:0}h {1:0}m {2:0}s");
        }

        public static bool IsCancelException(Exception ex) {
            if (ex == null) { return false; }

            if (ex is TaskCanceledException) { return true; }
            if (ex is OperationCanceledException) { return true; }
            if (ex.Message.Contains("canceled")) { return true; }
            if (ex.Message.Contains("cancelled")) { return true; }

            if (ex.InnerException != null) {
                return IsCancelException(ex.InnerException);
            }

            return false;
        }

        public static bool MoveFile(string srcFile, string dstDir) {
            try {
                if (!Directory.Exists(dstDir)) {
                    Directory.CreateDirectory(dstDir);
                }

                File.Move(srcFile, Path.Combine(dstDir, Path.GetFileName(srcFile)));
                return true;
            } catch (Exception ex) {
                TSLogger.Error($"failed to move file {srcFile} to {dstDir}: {ex.Message}");
                return false;
            }
        }

        public static int StringToInt(string s) {
            int i;
            return Int32.TryParse(s, out i) ? i : int.MinValue;
        }

        private Utils() {
        }
    }
}