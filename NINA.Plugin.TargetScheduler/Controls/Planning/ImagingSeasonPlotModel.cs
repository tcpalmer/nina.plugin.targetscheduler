using NINA.Core.Utility.ColorSchema;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace NINA.Plugin.TargetScheduler.Controls.Planning {

    /// <summary>
    /// Imaging season chart: nightly imaging time (as area series, colored by whether the night is accepted) above the
    /// nightly moon illumination and moon-target separation.
    /// </summary>
    public class ImagingSeasonPlotModel {
        public PlotModel PlotModel { get; private set; }
        public PlotController PlotController { get; private set; }

        private readonly ColorSchema colorSchema;

        public ImagingSeasonPlotModel(ColorSchema colorSchema, string targetName, List<DailyDetail> season) {
            this.colorSchema = colorSchema;
            PlotController = new PlotController();

            PlotModel = new PlotModel {
                Background = ConvertColor(colorSchema.BackgroundColor),
                PlotAreaBackground = ConvertColor(colorSchema.BackgroundColor),
                PlotAreaBorderColor = ConvertColor(colorSchema.BorderColor),
                TextColor = ConvertColor(colorSchema.PrimaryColor),
            };

            PlotModel.Axes.Add(GetXAxis());
            AddImagingTime(season);
            AddMoon(season);
            AddTitle(targetName, season);
        }

        private void AddImagingTime(List<DailyDetail> season) {
            PlotModel.Axes.Add(new TimeSpanAxis {
                Title = "Imaging Hours",
                Key = "Imaging Hours",
                StartPosition = 0.4,
                EndPosition = 1,
                AxisTitleDistance = 12,
                IntervalLength = 30,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                AxislineColor = ConvertColor(colorSchema.PrimaryColor),
                MajorGridlineStyle = LineStyle.LongDash,
                MajorGridlineColor = ConvertColor(colorSchema.PrimaryColor, 60),
                Minimum = 0,
                MaximumPadding = 0.07,
                MinorStep = 900,
                Position = AxisPosition.Left,
                StringFormat = "h:mm",
                TextColor = ConvertColor(colorSchema.PrimaryColor),
                TicklineColor = ConvertColor(colorSchema.SecondaryColor),
            });

            // One area series per run of accepted/rejected nights, each extended by a night so adjacent areas join
            foreach ((int start, int end, bool accepted) span in GetSpans(season)) {
                AreaSeries areaSeries = GetAreaSeries(span.accepted);
                int end = Math.Min(span.end + 1, season.Count - 1);
                for (int i = span.start; i <= end; i++) {
                    areaSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(season[i].NightDate),
                        TimeSpanAxis.ToDouble(TimeSpan.FromMinutes(season[i].ImagingMinutes))));
                }

                PlotModel.Series.Add(areaSeries);
            }
        }

        private void AddMoon(List<DailyDetail> season) {
            PlotModel.Axes.Add(new LinearAxis {
                Title = "Moon Illumination",
                Key = "Moon Illumination",
                StartPosition = 0,
                EndPosition = 0.35,
                AxisTitleDistance = 12,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                AxislineColor = ConvertColor(colorSchema.PrimaryColor),
                Minimum = 0,
                Maximum = 100,
                MaximumPadding = 0.07,
                MinorStep = 10,
                Position = AxisPosition.Left,
                TextColor = ConvertColor(colorSchema.PrimaryColor),
                TicklineColor = ConvertColor(colorSchema.SecondaryColor),
            });

            PlotModel.Axes.Add(new LinearAxis {
                Title = "Moon Separation",
                Key = "Moon Separation",
                StartPosition = 0,
                EndPosition = 0.35,
                AxisTitleDistance = 12,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                AxislineColor = ConvertColor(colorSchema.PrimaryColor),
                Minimum = 0,
                Maximum = 180,
                MinorStep = 10,
                Position = AxisPosition.Right,
                TextColor = ConvertColor(colorSchema.PrimaryColor),
                TicklineColor = ConvertColor(colorSchema.SecondaryColor),
            });

            LineSeries illumSeries = new LineSeries {
                Title = "Moon Illumination",
                Color = ConvertColor(Colors.Gold, 90),
                TrackerFormatString = "Illumination\n{2:MM/dd/yyyy}\n{4:0}%",
                YAxisKey = "Moon Illumination",
            };

            LineSeries separationSeries = new LineSeries {
                Title = "Moon Separation",
                Color = ConvertColor(Colors.IndianRed, 90),
                TrackerFormatString = "Separation\n{2:MM/dd/yyyy}\n{4:0}°",
                YAxisKey = "Moon Separation",
            };

            foreach (DailyDetail detail in season) {
                illumSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(detail.NightDate), detail.MoonIllumination * 100));
                separationSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(detail.NightDate), detail.MoonSeparation));
            }

            PlotModel.Series.Add(illumSeries);
            PlotModel.Series.Add(separationSeries);
        }

        private void AddTitle(string targetName, List<DailyDetail> season) {
            int startYear = season.First().NightDate.Year;
            int endYear = season.Last().NightDate.Year;

            StringBuilder sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(targetName) ? "User coords" : targetName).Append('\n');
            sb.Append(startYear);
            if (startYear != endYear) {
                sb.Append('-').Append(endYear);
            }

            int maxImagingMinutes = season.Max(d => d.ImagingMinutes);
            PlotModel.Annotations.Add(new TextAnnotation {
                Text = sb.ToString(),
                Background = ConvertColor(colorSchema.BackgroundColor),
                Stroke = ConvertColor(colorSchema.BorderColor),
                StrokeThickness = 2,
                TextColor = ConvertColor(colorSchema.PrimaryColor),
                TextPosition = new DataPoint(DateTimeAxis.ToDouble(season.First().NightDate.AddDays(15)),
                                             TimeSpanAxis.ToDouble(TimeSpan.FromMinutes(maxImagingMinutes * .87))),
            });
        }

        private Axis GetXAxis() {
            return new DateTimeAxis {
                IntervalType = DateTimeIntervalType.Months,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                AxislineColor = ConvertColor(colorSchema.PrimaryColor),
                MajorGridlineStyle = LineStyle.LongDash,
                MajorGridlineColor = ConvertColor(colorSchema.PrimaryColor, 60),
                MinorIntervalType = DateTimeIntervalType.Weeks,
                Position = AxisPosition.Bottom,
                StringFormat = "MMM",
                TextColor = ConvertColor(colorSchema.PrimaryColor),
                TicklineColor = ConvertColor(colorSchema.SecondaryColor),
            };
        }

        private AreaSeries GetAreaSeries(bool accepted) {
            return new AreaSeries {
                Color = ConvertColor(accepted ? colorSchema.SecondaryColor : colorSchema.NotificationErrorColor),
                Color2 = ConvertColor(accepted ? colorSchema.PrimaryColor : colorSchema.NotificationErrorTextColor),
                TrackerFormatString = "{2:MM/dd/yyyy}\n{4}",
                YAxisKey = "Imaging Hours"
            };
        }

        /// <summary>
        /// Split the season into consecutive runs of accepted or rejected nights.
        /// </summary>
        public static List<(int start, int end, bool accepted)> GetSpans(List<DailyDetail> season) {
            List<(int, int, bool)> spans = new List<(int, int, bool)>();
            if (season.Count == 0) {
                return spans;
            }

            int start = 0;
            for (int i = 1; i <= season.Count; i++) {
                if (i == season.Count || season[i].Accepted != season[start].Accepted) {
                    spans.Add((start, i - 1, season[start].Accepted));
                    start = i;
                }
            }

            return spans;
        }

        private static OxyColor ConvertColor(Color color, byte alpha) {
            return OxyColor.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static OxyColor ConvertColor(Color color) {
            return OxyColor.FromRgb(color.R, color.G, color.B);
        }
    }
}
