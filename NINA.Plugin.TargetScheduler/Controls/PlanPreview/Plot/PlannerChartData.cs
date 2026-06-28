using NINA.Astrometry;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot {

    /// <summary>
    /// Render-ready altitude chart data for a single aggregated target block: the refreshed
    /// <see cref="DeepSkyObject"/> (altitudes/horizon/moon), the night's <see cref="NighttimeData"/>,
    /// the imaging window, and the exposure bands.  Consumed by both the WPF <see cref="AltitudeChart"/>
    /// control (Plan Preview and the in-UI report) and the <see cref="AltitudeSvgExporter"/> (HTML report).
    /// </summary>
    public class PlannerChartData {
        public DeepSkyObject Dso { get; set; }
        public NighttimeData NighttimeData { get; set; }
        public DateTime ImagingStart { get; set; }
        public DateTime ImagingStop { get; set; }
        public List<ExposureRun> ExposureRuns { get; set; } = new List<ExposureRun>();
    }
}
