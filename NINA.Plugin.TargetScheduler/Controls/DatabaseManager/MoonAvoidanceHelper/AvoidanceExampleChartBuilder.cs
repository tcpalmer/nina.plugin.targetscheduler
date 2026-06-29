using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager.MoonAvoidanceHelper {

    /// <summary>
    /// Working parameters for the moon avoidance example - the same set the Moon Avoidance Helper is editing.
    /// The example reflects these live, so the avoidance/rejection curve tracks edits to the upper controls.
    /// </summary>
    public struct AvoidanceExampleParameters {
        public bool ClassicEnabled;
        public double ClassicSeparation;
        public int ClassicWidth;
        public double RelaxScale;
        public double RelaxMinAltitude;
        public double RelaxMaxAltitude;
        public bool MoonDownEnabled;
    }

    /// <summary>
    /// A single time sample of the moon geometry for the example night.  The moon altitude, age, and
    /// target separation depend only on the target/date/location (not the avoidance parameters), so they are
    /// computed once and reused as the user tweaks the avoidance controls.
    /// </summary>
    public class MoonSample {
        public double TimeDouble;     // OxyPlot DateTime-axis X value
        public double MoonAltitude;   // degrees
        public double MoonAge;        // days
        public double MoonSeparation; // degrees, moon-to-target
    }

    /// <summary>
    /// The expensive, parameter-independent part of the example chart: the reusable <see cref="PlannerChartData"/>
    /// (twilight, horizon, target altitude) and the moon geometry samples across the night's dark window.
    /// </summary>
    public class AvoidanceExampleData {
        public PlannerChartData ChartData;
        public List<MoonSample> Samples;
    }

    /// <summary>
    /// Builds the moon avoidance example chart by reusing the existing augmented NINA altitude chart
    /// (<see cref="MoonAvoidExample"/> / <see cref="PlannerChartData"/>) for the twilight bands, custom horizon, and
    /// target altitude, then layering on the moon-to-target separation and the calculated avoidance (rejection)
    /// separation across astronomical dusk-to-dawn.  The avoidance separation is drawn cream where the exposure
    /// would be accepted and red where it would be rejected, mirroring <see cref="Planning.MoonAvoidanceExpert"/>.
    /// </summary>
    public static class AvoidanceExampleChartBuilder {

        // Time resolution of the separation/avoidance curves.  The moon moves slowly so a few minutes is plenty.
        private const int SampleStepMinutes = 5;

        /// <summary>
        /// Build the parameter-independent example data (chart data plus moon samples) for the selected
        /// profile/project/target on the example date.  Returns null if the inputs are incomplete or the night
        /// has no astronomical dark window.
        /// </summary>
        public static AvoidanceExampleData BuildData(IProfileService profileService, IProfile profile, Project project, Target target, DateTime exampleDate) {
            if (profile == null || project == null || target == null) {
                return null;
            }

            ObserverInfo observerInfo = new ObserverInfo {
                Latitude = profile.AstrometrySettings.Latitude,
                Longitude = profile.AstrometrySettings.Longitude,
                Elevation = profile.AstrometrySettings.Elevation
            };

            // Astronomical dusk -> astronomical dawn (sun crossing -12 degrees) defines the curve span.
            TwilightCircumstances twilight = new TwilightCircumstances(observerInfo, exampleDate);
            TimeInterval darkSpan = twilight.GetTwilightSpan(TwilightLevel.Astronomical);
            if (darkSpan == null) {
                return null;
            }

            DateTime referenceDate = NighttimeCalculator.GetReferenceDate(exampleDate);

            CustomHorizon horizon = GetCustomHorizon(profile, project);
            DeepSkyObject dso = new DeepSkyObject(string.Empty, target.Coordinates, horizon) { Name = target.Name };
            dso.SetDateAndPosition(referenceDate, observerInfo.Latitude, observerInfo.Longitude);
            dso.Refresh();

            // Show the moon altitude curve by default in the example; the user can toggle it off via the chart button.
            // This must come AFTER Refresh(): Refresh() -> UpdateHorizonAndTransit() calls
            // MoonInfo.SetReferenceDateAndObserver(), which clears the moon data points when DisplayMoon is false.
            // Setting DisplayMoon last makes its setter compute the points with nothing left to clear them.
            if (dso.Moon != null) {
                dso.Moon.DisplayMoon = true;
            }

            PlannerChartData chartData = new PlannerChartData {
                Dso = dso,
                NighttimeData = new NighttimeCalculator(profileService).Calculate(referenceDate),
                ImagingStart = DateTime.MinValue,
                ImagingStop = DateTime.MinValue
            };

            List<MoonSample> samples = new List<MoonSample>();
            for (DateTime t = darkSpan.StartTime; t <= darkSpan.EndTime; t = t.AddMinutes(SampleStepMinutes)) {
                samples.Add(new MoonSample {
                    TimeDouble = DateTimeAxis.ToDouble(t),
                    MoonAltitude = AstroUtil.GetMoonAltitude(t, observerInfo),
                    MoonAge = AstrometryUtils.GetMoonAge(t),
                    MoonSeparation = AstrometryUtils.GetMoonSeparationAngle(observerInfo, t, target.Coordinates)
                });
            }

            return new AvoidanceExampleData { ChartData = chartData, Samples = samples };
        }

        /// <summary>
        /// Create the example chart control for the (cached) data and current avoidance parameters.  Call once
        /// per geometry change; use <see cref="ComputeCurves"/> + <see cref="MoonAvoidExample.SetAvoidanceCurves"/>
        /// to refresh just the curves when only the avoidance parameters change.
        /// </summary>
        public static MoonAvoidExample BuildChart(AvoidanceExampleData data, AvoidanceExampleParameters p, double width, double height) {
            MoonAvoidExample chart = MoonAvoidExample.Create(data.ChartData, width, height);
            ComputeCurves(data, p, out IList<DataPoint> separation, out IList<DataPoint> avoidance, out IList<bool> rejected);
            chart.SetAvoidanceCurves(separation, avoidance, rejected);
            return chart;
        }

        /// <summary>
        /// Compute the example curves from the cached moon geometry and current avoidance parameters: per sample, the
        /// moon-to-target separation, the calculated avoidance (rejection) separation, and whether the exposure would
        /// be rejected at that time (drives the avoidance curve color).  The three lists are parallel and ascending in
        /// X.  Cheap - reuses the precomputed moon geometry - so it's safe to call on every parameter edit.
        /// </summary>
        public static void ComputeCurves(AvoidanceExampleData data, AvoidanceExampleParameters p,
            out IList<DataPoint> separation, out IList<DataPoint> avoidance, out IList<bool> rejected) {
            List<DataPoint> separationPoints = new List<DataPoint>(data.Samples.Count);
            List<DataPoint> avoidancePoints = new List<DataPoint>(data.Samples.Count);
            List<bool> rejectedFlags = new List<bool>(data.Samples.Count);

            for (int i = 0; i < data.Samples.Count; i++) {
                MoonSample s = data.Samples[i];
                separationPoints.Add(new DataPoint(s.TimeDouble, s.MoonSeparation));
                ComputeAvoidance(s, p, out double avoidanceValue, out bool isRejected);
                avoidancePoints.Add(new DataPoint(s.TimeDouble, avoidanceValue));
                rejectedFlags.Add(isRejected);
            }

            separation = separationPoints;
            avoidance = avoidancePoints;
            rejected = rejectedFlags;
        }

        // Mirrors MoonAvoidanceExpert.IsRejected for a single time, returning the avoidance separation to plot
        // (0 when avoidance is off) and whether the exposure would be rejected at that time.
        private static void ComputeAvoidance(MoonSample s, AvoidanceExampleParameters p, out double avoidanceSeparation, out bool rejected) {
            avoidanceSeparation = 0;
            rejected = false;

            if (!p.ClassicEnabled) {
                return;
            }

            double separationParameter = p.ClassicSeparation;
            double widthParameter = p.ClassicWidth;

            // In the relaxation zone, modulate the separation and width parameters by the moon altitude.
            if (p.RelaxScale > 0 && s.MoonAltitude <= p.RelaxMaxAltitude) {
                separationParameter = separationParameter + (p.RelaxScale * (s.MoonAltitude - p.RelaxMaxAltitude));
                double band = p.RelaxMaxAltitude - p.RelaxMinAltitude;
                widthParameter = band > 0 ? widthParameter * ((s.MoonAltitude - p.RelaxMinAltitude) / band) : 0;
            }

            // Avoidance completely off when the moon is below the relaxation min altitude.
            if (p.RelaxScale > 0 && s.MoonAltitude <= p.RelaxMinAltitude) {
                return;
            }

            double moonAvoidanceSeparation = widthParameter == 0
                ? 0
                : AstrometryUtils.GetMoonAvoidanceLorentzianSeparation(s.MoonAge, separationParameter, widthParameter);

            // Absolute avoidance: moon up (above relax max) with Moon Must Be Down enabled rejects regardless.
            if (s.MoonAltitude >= p.RelaxMaxAltitude && p.MoonDownEnabled) {
                avoidanceSeparation = Math.Max(0, moonAvoidanceSeparation);
                rejected = true;
                return;
            }

            // Relaxed into oblivion: avoidance off.
            if (separationParameter <= 0) {
                return;
            }

            avoidanceSeparation = Math.Max(0, moonAvoidanceSeparation);
            rejected = s.MoonSeparation < moonAvoidanceSeparation;
        }

        // Mirrors TargetSchedulerContainer/PlanPreviewer: use the profile's custom horizon when the project opts
        // in, otherwise a constant horizon at the project's minimum altitude.
        private static CustomHorizon GetCustomHorizon(IProfile profile, Project project) {
            return project.UseCustomHorizon && profile.AstrometrySettings.Horizon != null
                ? profile.AstrometrySettings.Horizon
                : HorizonDefinition.GetConstantHorizon(project.MinimumAltitude);
        }
    }
}
