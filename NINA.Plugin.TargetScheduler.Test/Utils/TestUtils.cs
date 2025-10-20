using System.Text.RegularExpressions;

namespace NINA.Plugin.TargetScheduler.Test.Util {

    public class TestUtils {
        private static Regex guidRegex = new Regex("^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$");

        public static bool ValidGuid(string guid) {
            return guid != null && guidRegex.IsMatch(guid);
        }
    }
}