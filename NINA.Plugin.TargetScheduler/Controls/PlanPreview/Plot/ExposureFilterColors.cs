using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot {

    /// <summary>
    /// Maps an exposure to a 'native' filter color used to tint exposure bands on the altitude plot.
    ///
    /// Matching prefers the exposure template name and falls back to the filter name.  For each candidate
    /// the string is tokenized (case-insensitive, split on non-alphanumeric) and matched against known
    /// broadband (L/R/G/B) and narrowband (Ha/OIII/SII) aliases.  When nothing matches we fall back to a
    /// neutral gray.
    /// </summary>
    public static class ExposureFilterColors {
        public static readonly Color Luminance = (Color)ColorConverter.ConvertFromString("#bbbbbb");
        public static readonly Color Red = (Color)ColorConverter.ConvertFromString("#bb2222");
        public static readonly Color Green = (Color)ColorConverter.ConvertFromString("#22bb22");
        public static readonly Color Blue = (Color)ColorConverter.ConvertFromString("#2222bb");
        public static readonly Color Ha = (Color)ColorConverter.ConvertFromString("#8b0000");
        public static readonly Color OIII = (Color)ColorConverter.ConvertFromString("#6495ED");
        public static readonly Color SII = (Color)ColorConverter.ConvertFromString("#daa520");
        public static readonly Color Other = (Color)ColorConverter.ConvertFromString("#888888");

        public static Color GetColor(string exposureTemplateName, string filterName) {
            return Match(exposureTemplateName) ?? Match(filterName) ?? Other;
        }

        private static Color? Match(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                return null;
            }

            HashSet<string> tokens = Regex.Split(name.ToLowerInvariant(), "[^a-z0-9]+")
                .Where(t => t.Length > 0)
                .ToHashSet();

            // Narrowband first so single-letter broadband rules don't pre-empt them.
            if (tokens.Overlaps(new[] { "ha", "halpha", "alpha", "hydrogen" })) { return Ha; }
            if (tokens.Overlaps(new[] { "oiii", "o3", "oxy", "oxygen" })) { return OIII; }
            if (tokens.Overlaps(new[] { "sii", "s2", "sul", "sulphur", "sulfur" })) { return SII; }

            if (tokens.Overlaps(new[] { "l", "lum", "luminance" })) { return Luminance; }
            if (tokens.Overlaps(new[] { "r", "red" })) { return Red; }
            if (tokens.Overlaps(new[] { "g", "green", "grn" })) { return Green; }
            if (tokens.Overlaps(new[] { "b", "blue", "blu" })) { return Blue; }

            return null;
        }
    }
}
