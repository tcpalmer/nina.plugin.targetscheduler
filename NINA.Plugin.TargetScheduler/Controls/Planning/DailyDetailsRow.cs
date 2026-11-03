using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Windows;

namespace NINA.Plugin.TargetScheduler.Controls.Planning {

    /// <summary>
    /// View adapter for one night of the Daily Details table, including the altitude chart shown when the row is selected.
    /// Rows are updated in place when the plan is regenerated so the table's selection (and open details) is preserved.
    /// </summary>
    public class DailyDetailsRow : BaseINPC {
        private const double ChartWidth = 560;
        private const double ChartHeight = 200;

        private DailyDetail detail;
        private TargetPlanningParameters parameters;
        private readonly IProfileService profileService;
        private readonly string primaryColor;
        private readonly string errorColor;

        public DailyDetailsRow(DailyDetail detail, TargetPlanningParameters parameters, IProfileService profileService) {
            this.detail = detail;
            this.parameters = parameters;
            this.profileService = profileService;

            primaryColor = profileService.ActiveProfile.ColorSchemaSettings.ColorSchema.PrimaryColor.ToString();
            errorColor = profileService.ActiveProfile.ColorSchemaSettings.ColorSchema.NotificationWarningColor.ToString();
        }

        /// <summary>
        /// Replace the row's results with those for the same night from a regenerated plan.  The chart is dropped so it's
        /// rebuilt (only if the row's details are showing) for the new parameters.
        /// </summary>
        public void Update(DailyDetail detail, TargetPlanningParameters parameters) {
            this.detail = detail;
            this.parameters = parameters;
            chart = null;
            RaiseAllPropertiesChanged();
        }

        public DateTime NightDate => detail.NightDate;

        public bool Status => detail.Accepted;
        public string StatusMessage => detail.Accepted ? "Imaging possible" : $"Rejected: {detail.RejectedReason}";

        public DateTime StartImagingTime => detail.StartImagingTime;
        public DateTime EndImagingTime => detail.EndImagingTime;
        public DateTime TransitTime => detail.TransitTime;

        public string StartLimitingFactor => LimitedBy(detail.StartLimitingFactor);
        public string EndLimitingFactor => LimitedBy(detail.EndLimitingFactor);

        public string ImagingTime => Utils.MtoHM(detail.ImagingMinutes);
        public string ImagingTimeColor => detail.ImagingMinutes < parameters.MinimumTime ? errorColor : primaryColor;

        public double MoonIllumination => detail.MoonIllumination * 100;
        public string MoonIlluminationColor => primaryColor;

        public double MoonSeparation => detail.MoonSeparation;
        public string MoonSeparationColor => detail.MoonRejected ? errorColor : primaryColor;

        public string MoonAvoidanceSeparationDisplay => parameters.MoonAvoidanceEnabled && detail.MoonAvoidanceSeparation.HasValue
            ? $"{Math.Max(0, detail.MoonAvoidanceSeparation.Value):F0}°"
            : "n/a";

        public string MoonAvoidanceSeparationColor => detail.MoonRejected ? errorColor : primaryColor;

        private FrameworkElement chart;

        // Built on first access (when the row details are first shown) since it's comparatively expensive
        public FrameworkElement Chart {
            get {
                if (chart == null) {
                    chart = BuildChart();
                }

                return chart;
            }
        }

        private static string LimitedBy(string factor) {
            return string.IsNullOrEmpty(factor) ? "" : $"  (limited by {factor})";
        }

        private FrameworkElement BuildChart() {
            try {
                IProfile profile = parameters.Profile;
                DateTime referenceDate = NighttimeCalculator.GetReferenceDate(detail.NightDate.AddHours(13));

                DeepSkyObject dso = new DeepSkyObject(string.Empty, parameters.Coordinates, GetCustomHorizon(profile)) { Name = parameters.TargetName };
                dso.SetDateAndPosition(referenceDate, profile.AstrometrySettings.Latitude, profile.AstrometrySettings.Longitude);
                dso.Refresh();

                // Must follow Refresh(), which otherwise clears the moon data points (see AvoidanceExampleChartBuilder)
                if (dso.Moon != null) {
                    dso.Moon.DisplayMoon = true;
                }

                PlannerChartData chartData = new PlannerChartData {
                    Dso = dso,
                    NighttimeData = new NighttimeCalculator(profileService).Calculate(referenceDate),
                    ImagingStart = detail.HasWindow ? detail.StartImagingTime : DateTime.MinValue,
                    ImagingStop = detail.HasWindow ? detail.EndImagingTime : DateTime.MinValue
                };

                return AltitudeChart.Create(chartData, ChartWidth, ChartHeight);
            } catch (Exception ex) {
                TSLogger.Error($"failed to build target planning altitude chart: {ex.Message} {ex.StackTrace}");
                return null;
            }
        }

        // Same horizon choice as the scheduler: the profile's custom horizon if the project opts in (and one exists),
        // otherwise a constant horizon at the minimum altitude
        private CustomHorizon GetCustomHorizon(IProfile profile) {
            return parameters.UseCustomHorizon && profile.AstrometrySettings.Horizon != null
                ? profile.AstrometrySettings.Horizon
                : HorizonDefinition.GetConstantHorizon(parameters.MinimumAltitude);
        }
    }
}
