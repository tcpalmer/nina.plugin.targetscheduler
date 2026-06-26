using System;
using System.Windows.Media;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot {

    /// <summary>
    /// A contiguous span of exposures using the same filter, rendered as a tinted band on the altitude plot.
    /// </summary>
    public class ExposureRun {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public Color Color { get; set; }
        public string FilterName { get; set; }
        public string ExposureTemplateName { get; set; }
    }
}
