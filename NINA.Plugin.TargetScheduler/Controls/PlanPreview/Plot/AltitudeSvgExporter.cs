using NINA.Astrometry;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot {

    /// <summary>
    /// Renders a <see cref="PlannerChartData"/> as a self-contained SVG string for inclusion in the HTML
    /// planner report.  Builds an OxyPlot <see cref="PlotModel"/> mirroring the WPF <see cref="AltitudeChart"/>
    /// (twilight bands, altitude curve, horizon, exposure bands, now/start/stop lines) and exports it with
    /// <see cref="SvgExporter"/>.  Colors are fixed to the NINA dark theme since dynamic brushes aren't
    /// available outside the visual tree.
    /// </summary>
    public static class AltitudeSvgExporter {
        private static readonly OxyColor Background = OxyColor.Parse("#1E1E1E");
        private static readonly OxyColor Foreground = OxyColor.Parse("#C7C6C3");
        private static readonly OxyColor Gridline = OxyColor.FromAColor(0x40, Foreground);
        private static readonly OxyColor AltitudeColor = OxyColor.Parse("#C7C6C3");
        private static readonly OxyColor HorizonFill = OxyColor.FromArgb(0x80, 0x44, 0x44, 0x44);
        private static readonly OxyColor TwilightColor = OxyColor.Parse("#73737E");
        private static readonly OxyColor CivilTwilightColor = OxyColor.Parse("#757E8C");
        private static readonly OxyColor NauticalTwilightColor = OxyColors.LightSlateGray;
        private static readonly OxyColor NightColor = OxyColors.Black;
        private static readonly OxyColor ImagingStartColor = OxyColor.Parse("#4CAF50");
        private static readonly OxyColor ImagingStopColor = OxyColor.Parse("#E57373");

        // Tint applied to band colors so the altitude curve and twilight remain visible through the bands.
        private const byte BandAlpha = 0x70;

        private const double AxisMin = 0;
        private const double AxisMax = 90;

        public static string Export(PlannerChartData data, double width, double height) {
            PlotModel model = BuildModel(data);
            SvgExporter exporter = new SvgExporter {
                Width = width,
                Height = height,
                IsDocument = false
            };

            // ExportToString still emits an <?xml ...?> prolog; strip everything before <svg so the result
            // can be embedded inline within the HTML report body.
            string svg = exporter.ExportToString(model);
            int start = svg.IndexOf("<svg", StringComparison.Ordinal);
            return start > 0 ? svg.Substring(start) : svg;
        }

        private static PlotModel BuildModel(PlannerChartData data) {
            PlotModel model = new PlotModel {
                Background = Background,
                PlotAreaBackground = Background,
                PlotAreaBorderColor = Gridline,
                TextColor = Foreground,
                Padding = new OxyThickness(4)
            };

            model.Axes.Add(new LinearAxis {
                Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = 90,
                MajorStep = 30,
                MajorGridlineStyle = LineStyle.LongDash,
                MajorGridlineColor = Gridline,
                TextColor = Foreground,
                AxislineColor = Foreground,
                TicklineColor = Foreground,
                IsZoomEnabled = false,
                IsPanEnabled = false
            });

            model.Axes.Add(new DateTimeAxis {
                Position = AxisPosition.Bottom,
                IntervalType = DateTimeIntervalType.Hours,
                StringFormat = "HH",
                TextColor = Foreground,
                AxislineColor = Foreground,
                TicklineColor = Foreground,
                IsZoomEnabled = false,
                IsPanEnabled = false
            });

            if (data.NighttimeData != null) {
                NighttimeData night = data.NighttimeData;
                AddAreaSeries(model, night.TwilightDuration, TwilightColor);
                AddAreaSeries(model, night.CivilTwilightDuration, CivilTwilightColor);
                AddAreaSeries(model, night.NauticalTwilightDuration, NauticalTwilightColor);
                AddAreaSeries(model, night.NightDuration, NightColor);
            }

            if (data.Dso != null) {
                AddLineSeries(model, data.Dso.Altitudes, AltitudeColor);
                AddAreaSeries(model, data.Dso.Horizon, HorizonFill);
            }

            AddExposureBands(model, data.ExposureRuns);
            AddVerticalLine(model, data.ImagingStart, ImagingStartColor);
            AddVerticalLine(model, data.ImagingStop, ImagingStopColor);
            AddNowLine(model, data.NighttimeData);
            AddMaxAltitude(model, data.Dso);

            return model;
        }

        private static void AddAreaSeries(PlotModel model, IEnumerable<DataPoint> points, OxyColor fill) {
            if (points == null) {
                return;
            }

            AreaSeries series = new AreaSeries {
                Color = OxyColors.Transparent,
                Fill = fill
            };
            series.Points.AddRange(Clamp(points));
            if (series.Points.Count > 0) {
                model.Series.Add(series);
            }
        }

        private static void AddLineSeries(PlotModel model, IEnumerable<DataPoint> points, OxyColor color) {
            if (points == null) {
                return;
            }

            LineSeries series = new LineSeries { Color = color, StrokeThickness = 1.5 };
            series.Points.AddRange(Clamp(points));
            if (series.Points.Count > 0) {
                model.Series.Add(series);
            }
        }

        // OxyPlot's SVG exporter does not clip series to the plot area (unlike the WPF renderer), so points
        // outside the 0-90 altitude range would overrun the chart.  Clamp Y into range to keep them contained.
        private static IEnumerable<DataPoint> Clamp(IEnumerable<DataPoint> points) {
            foreach (DataPoint p in points) {
                yield return new DataPoint(p.X, Math.Max(AxisMin, Math.Min(AxisMax, p.Y)));
            }
        }

        private static void AddExposureBands(PlotModel model, IEnumerable<ExposureRun> runs) {
            if (runs == null) {
                return;
            }

            foreach (ExposureRun run in runs) {
                model.Annotations.Add(new RectangleAnnotation {
                    MinimumX = DateTimeAxis.ToDouble(run.Start),
                    MaximumX = DateTimeAxis.ToDouble(run.End),
                    MinimumY = 0,
                    MaximumY = 90,
                    Fill = OxyColor.FromArgb(BandAlpha, run.Color.R, run.Color.G, run.Color.B),
                    Stroke = OxyColors.Transparent,
                    StrokeThickness = 0,
                    Layer = AnnotationLayer.AboveSeries
                });
            }
        }

        private static void AddVerticalLine(PlotModel model, DateTime time, OxyColor color) {
            if (time == DateTime.MinValue) {
                return;
            }

            model.Annotations.Add(new LineAnnotation {
                Type = LineAnnotationType.Vertical,
                X = DateTimeAxis.ToDouble(time),
                MaximumY = 90,
                Color = color,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            });
        }

        private static void AddNowLine(PlotModel model, NighttimeData night) {
            if (night?.Ticker == null) {
                return;
            }

            model.Annotations.Add(new LineAnnotation {
                Type = LineAnnotationType.Vertical,
                X = night.Ticker.OxyNow,
                MaximumY = 90,
                Color = Foreground,
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash,
                Text = "Now",
                TextColor = Foreground
            });
        }

        private static void AddMaxAltitude(PlotModel model, DeepSkyObject dso) {
            if (dso == null || dso.MaxAltitude.Y <= 0) {
                return;
            }

            model.Annotations.Add(new PointAnnotation {
                X = dso.MaxAltitude.X,
                Y = dso.MaxAltitude.Y,
                Shape = MarkerType.Circle,
                Size = 3,
                Fill = Foreground,
                Text = $"{dso.MaxAltitude.Y:0}°",
                TextColor = Foreground
            });
        }
    }
}
