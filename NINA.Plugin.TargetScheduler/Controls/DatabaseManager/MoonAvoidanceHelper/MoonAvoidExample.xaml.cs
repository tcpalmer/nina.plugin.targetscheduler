#region "copyright"

/*
    Forked from the Plan Preview AltitudeChart (itself copied from N.I.N.A.
    NINA.WPF.Base/View/AltitudeChart.xaml.cs) so the moon avoidance example can diverge freely.
    The original is governed by the notice below.

    Copyright � 2016 - 2026 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DataPoint = OxyPlot.DataPoint;
using LineSeries = OxyPlot.Wpf.LineSeries;
using MarkerType = OxyPlot.MarkerType;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager.MoonAvoidanceHelper {

    /// <summary>
    /// Interaction logic for MoonAvoidExample.xaml - the moon avoidance example chart.  Renders the same
    /// twilight bands, custom horizon, and target altitude as the Plan Preview altitude chart, but on a
    /// 0-180 altitude axis with full-height twilight bands so the moon-to-target separation and the calculated
    /// avoidance (rejection) separation curves (both 0-180) can be layered on across the night.
    /// </summary>
    public partial class MoonAvoidExample : UserControl {

        // Altitude (Y) axis maximum.  The twilight bands are scaled to fill this height.
        private const double AxisMaximum = 180;

        public MoonAvoidExample() {
            InitializeComponent();
            InitializeHoverToolTip();
        }

        /// <summary>
        /// Build an example chart for the supplied <see cref="PlannerChartData"/>, sizing it and binding the
        /// DSO (altitudes/horizon/moon) and nighttime data (twilight bands).
        /// </summary>
        public static MoonAvoidExample Create(PlannerChartData data, double width, double height) {
            return new MoonAvoidExample {
                Width = width,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Left,
                DataContext = data.Dso,
                NighttimeData = data.NighttimeData
            };
        }

        public static readonly DependencyProperty NighttimeDataProperty = DependencyProperty.Register(
            "NighttimeData", typeof(NighttimeData), typeof(MoonAvoidExample), new PropertyMetadata(null, OnNighttimeDataChanged));

        public NighttimeData NighttimeData {
            get => (NighttimeData)GetValue(NighttimeDataProperty);
            set => SetValue(NighttimeDataProperty, value);
        }

        // The twilight bands scaled to fill the full 0-180 axis.  NighttimeData reports them filling 0-90
        // (the standard altitude axis), so doubling the Y of each point makes them span the taller axis.
        public static readonly DependencyProperty TwilightDurationFullProperty = DependencyProperty.Register(
            "TwilightDurationFull", typeof(IList<DataPoint>), typeof(MoonAvoidExample), new PropertyMetadata(null));

        public IList<DataPoint> TwilightDurationFull {
            get => (IList<DataPoint>)GetValue(TwilightDurationFullProperty);
            set => SetValue(TwilightDurationFullProperty, value);
        }

        public static readonly DependencyProperty CivilTwilightDurationFullProperty = DependencyProperty.Register(
            "CivilTwilightDurationFull", typeof(IList<DataPoint>), typeof(MoonAvoidExample), new PropertyMetadata(null));

        public IList<DataPoint> CivilTwilightDurationFull {
            get => (IList<DataPoint>)GetValue(CivilTwilightDurationFullProperty);
            set => SetValue(CivilTwilightDurationFullProperty, value);
        }

        public static readonly DependencyProperty NauticalTwilightDurationFullProperty = DependencyProperty.Register(
            "NauticalTwilightDurationFull", typeof(IList<DataPoint>), typeof(MoonAvoidExample), new PropertyMetadata(null));

        public IList<DataPoint> NauticalTwilightDurationFull {
            get => (IList<DataPoint>)GetValue(NauticalTwilightDurationFullProperty);
            set => SetValue(NauticalTwilightDurationFullProperty, value);
        }

        public static readonly DependencyProperty NightDurationFullProperty = DependencyProperty.Register(
            "NightDurationFull", typeof(IList<DataPoint>), typeof(MoonAvoidExample), new PropertyMetadata(null));

        public IList<DataPoint> NightDurationFull {
            get => (IList<DataPoint>)GetValue(NightDurationFullProperty);
            set => SetValue(NightDurationFullProperty, value);
        }

        private static void OnNighttimeDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            MoonAvoidExample chart = (MoonAvoidExample)d;
            NighttimeData night = e.NewValue as NighttimeData;
            chart.TwilightDurationFull = ScaleToAxis(night?.TwilightDuration);
            chart.CivilTwilightDurationFull = ScaleToAxis(night?.CivilTwilightDuration);
            chart.NauticalTwilightDurationFull = ScaleToAxis(night?.NauticalTwilightDuration);
            chart.NightDurationFull = ScaleToAxis(night?.NightDuration);
        }

        // Double each twilight point's Y (clamped to the axis maximum) so a band that filled 0-90 now fills 0-180.
        private static IList<DataPoint> ScaleToAxis(IList<DataPoint> points) {
            if (points == null) {
                return null;
            }

            List<DataPoint> scaled = new List<DataPoint>(points.Count);
            foreach (DataPoint p in points) {
                scaled.Add(new DataPoint(p.X, p.Y <= 0 ? p.Y : System.Math.Min(AxisMaximum, p.Y * 2)));
            }

            return scaled;
        }

        public static readonly DependencyProperty MoonHorizontalAlignmentProperty = DependencyProperty.Register(
            "MoonHorizontalAlignment", typeof(HorizontalAlignment), typeof(MoonAvoidExample), new PropertyMetadata(HorizontalAlignment.Right));

        public HorizontalAlignment MoonHorizontalAlignment {
            get => (HorizontalAlignment)GetValue(MoonHorizontalAlignmentProperty);
            set => SetValue(MoonHorizontalAlignmentProperty, value);
        }

        public static readonly DependencyProperty MoonVerticalAlignmentProperty = DependencyProperty.Register(
            "MoonVerticalAlignment", typeof(VerticalAlignment), typeof(MoonAvoidExample), new PropertyMetadata(VerticalAlignment.Top));

        public VerticalAlignment MoonVerticalAlignment {
            get => (VerticalAlignment)GetValue(MoonVerticalAlignmentProperty);
            set => SetValue(MoonVerticalAlignmentProperty, value);
        }

        public static readonly DependencyProperty MoonMarginProperty = DependencyProperty.Register(
            "MoonMargin", typeof(Thickness), typeof(MoonAvoidExample), new PropertyMetadata(new Thickness(0, 10, 10, 0)));

        public Thickness MoonMargin {
            get => (Thickness)GetValue(MoonMarginProperty);
            set => SetValue(MoonMarginProperty, value);
        }

        public static readonly DependencyProperty ShowMoonProperty = DependencyProperty.Register(
            "ShowMoon", typeof(bool), typeof(MoonAvoidExample), new PropertyMetadata(true));

        public bool ShowMoon {
            get => (bool)GetValue(ShowMoonProperty);
            set => SetValue(ShowMoonProperty, value);
        }

        // Moon avoidance example colors.  Cream (#F6F1D5) for the moon-to-target separation curve.  The avoidance
        // (rejection) separation curve is green where the exposure would be accepted and red where it would be rejected.
        private static readonly Color SeparationColor = Color.FromRgb(0xF6, 0xF1, 0xD5);
        private static readonly Color AcceptedColor = Color.FromRgb(0x4C, 0xAF, 0x50);
        private static readonly Color RejectedColor = Colors.Red;

        private readonly List<LineSeries> avoidanceSeries = new List<LineSeries>();

        // Per-sample curve data retained for the hover tooltip (parallel lists, ascending X).
        private IList<DataPoint> hoverSeparation;
        private IList<DataPoint> hoverAvoidance;
        private IList<bool> hoverRejected;

        /// <summary>
        /// Layer the moon avoidance example curves onto the chart: the actual moon-to-target separation (cream) plus
        /// the calculated avoidance (rejection) separation, drawn green where the exposure would be accepted and red
        /// where it would be rejected.  <paramref name="separation"/> and <paramref name="avoidance"/> are parallel
        /// per-sample lists (X = OxyPlot DateTime double, Y = degrees); <paramref name="rejected"/> gives the avoidance
        /// color per sample.  Replaces any previously added avoidance curves.
        /// </summary>
        public void SetAvoidanceCurves(IList<DataPoint> separation, IList<DataPoint> avoidance, IList<bool> rejected) {
            hoverSeparation = separation;
            hoverAvoidance = avoidance;
            hoverRejected = rejected;

            foreach (LineSeries series in avoidanceSeries) {
                ExposurePlot.Series.Remove(series);
            }
            avoidanceSeries.Clear();

            AddAvoidanceCurve(separation, SeparationColor);

            SplitByRejection(avoidance, rejected, out IList<DataPoint> accepted, out IList<DataPoint> rejectedPoints);
            AddAvoidanceCurve(accepted, AcceptedColor);
            AddAvoidanceCurve(rejectedPoints, RejectedColor);

            ExposurePlot.InvalidatePlot(true);
        }

        // Split the single avoidance curve into accepted (green) and rejected (red) segments that share their boundary
        // points so the two colored runs join without a visible gap; DataPoint.Undefined breaks the inactive segment.
        private static void SplitByRejection(IList<DataPoint> avoidance, IList<bool> rejected,
            out IList<DataPoint> accepted, out IList<DataPoint> rejectedPoints) {
            List<DataPoint> acc = new List<DataPoint>(avoidance?.Count ?? 0);
            List<DataPoint> rej = new List<DataPoint>(avoidance?.Count ?? 0);

            if (avoidance != null) {
                for (int i = 0; i < avoidance.Count; i++) {
                    DataPoint point = avoidance[i];
                    bool isRejected = rejected != null && i < rejected.Count && rejected[i];
                    bool prevRejected = i > 0 && rejected != null && i - 1 < rejected.Count && rejected[i - 1];
                    bool boundary = i > 0 && prevRejected != isRejected;

                    if (boundary) {
                        acc.Add(point);
                        rej.Add(point);
                        (isRejected ? acc : rej).Add(DataPoint.Undefined);
                    } else if (isRejected) {
                        acc.Add(DataPoint.Undefined);
                        rej.Add(point);
                    } else {
                        acc.Add(point);
                        rej.Add(DataPoint.Undefined);
                    }
                }
            }

            accepted = acc;
            rejectedPoints = rej;
        }

        private void AddAvoidanceCurve(IList<DataPoint> points, Color color) {
            if (points == null || points.Count == 0) {
                return;
            }

            LineSeries series = new LineSeries {
                ItemsSource = points,
                DataFieldX = nameof(DataPoint.X),
                DataFieldY = nameof(DataPoint.Y),
                Color = color,
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };

            avoidanceSeries.Add(series);
            ExposurePlot.Series.Add(series);
        }

        // Cursor must be within this many screen pixels (vertically) of a curve for its hover label to show.
        private const double HoverThreshold = 12;

        private Popup hoverPopup;
        private TextBlock hoverText;

        // OxyPlot 2.1's WPF view has no hover tooltips, so we drive one ourselves (matching the Plan Preview chart's
        // band hover): a Popup that follows the cursor and shows the separation/avoidance angle in the curve's color.
        private void InitializeHoverToolTip() {
            hoverText = new TextBlock {
                Margin = new Thickness(6, 3, 6, 3),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };

            hoverPopup = new Popup {
                AllowsTransparency = true,
                IsHitTestVisible = false,
                Placement = PlacementMode.Relative,
                PlacementTarget = ExposurePlot,
                Child = new Border {
                    Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x20, 0x20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x60, 0x60, 0x60)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Child = hoverText
                }
            };

            ExposurePlot.PreviewMouseMove += OnPlotMouseMove;
            ExposurePlot.MouseLeave += OnPlotMouseLeave;
        }

        private void OnPlotMouseMove(object sender, MouseEventArgs e) {
            if (hoverSeparation == null && hoverAvoidance == null) {
                HideHoverToolTip();
                return;
            }

            OxyPlot.PlotModel model = ExposurePlot.ActualModel;
            if (model == null) {
                HideHoverToolTip();
                return;
            }

            Axis xAxis = null;
            Axis yAxis = null;
            foreach (Axis axis in model.Axes) {
                if (axis.Position == AxisPosition.Bottom) { xAxis = axis; } else if (axis.Position == AxisPosition.Left) { yAxis = axis; }
            }

            if (xAxis == null || yAxis == null) {
                HideHoverToolTip();
                return;
            }

            Point position = e.GetPosition(ExposurePlot);
            double dataX = xAxis.InverseTransform(position.X);

            // Pick whichever curve is closest to the cursor (vertically) within the threshold.
            double bestDistance = HoverThreshold;
            bool found = false;
            double labelValue = 0;
            Color labelColor = SeparationColor;

            double? sepY = InterpolateY(hoverSeparation, dataX);
            if (sepY.HasValue) {
                double distance = Math.Abs(yAxis.Transform(sepY.Value) - position.Y);
                if (distance <= bestDistance) {
                    bestDistance = distance;
                    found = true;
                    labelValue = sepY.Value;
                    labelColor = SeparationColor;
                }
            }

            double? avoidY = InterpolateY(hoverAvoidance, dataX);
            if (avoidY.HasValue) {
                double distance = Math.Abs(yAxis.Transform(avoidY.Value) - position.Y);
                if (distance <= bestDistance) {
                    bestDistance = distance;
                    found = true;
                    labelValue = avoidY.Value;
                    int index = NearestIndex(hoverAvoidance, dataX);
                    bool isRejected = hoverRejected != null && index < hoverRejected.Count && hoverRejected[index];
                    labelColor = isRejected ? RejectedColor : AcceptedColor;
                }
            }

            if (!found) {
                HideHoverToolTip();
                return;
            }

            hoverText.Text = $"{Math.Round(labelValue)}°";
            hoverText.Foreground = new SolidColorBrush(labelColor);
            hoverPopup.HorizontalOffset = position.X + 14;
            hoverPopup.VerticalOffset = position.Y + 12;
            if (!hoverPopup.IsOpen) {
                hoverPopup.IsOpen = true;
            }
        }

        private void OnPlotMouseLeave(object sender, MouseEventArgs e) {
            HideHoverToolTip();
        }

        private void HideHoverToolTip() {
            if (hoverPopup != null && hoverPopup.IsOpen) {
                hoverPopup.IsOpen = false;
            }
        }

        // Linear interpolation of Y at the given X across an ascending-X point list; null if X is out of range.
        private static double? InterpolateY(IList<DataPoint> points, double x) {
            if (points == null || points.Count == 0 || x < points[0].X || x > points[points.Count - 1].X) {
                return null;
            }

            for (int i = 1; i < points.Count; i++) {
                if (x <= points[i].X) {
                    DataPoint a = points[i - 1];
                    DataPoint b = points[i];
                    if (b.X == a.X) {
                        return b.Y;
                    }

                    double t = (x - a.X) / (b.X - a.X);
                    return a.Y + (t * (b.Y - a.Y));
                }
            }

            return points[points.Count - 1].Y;
        }

        private static int NearestIndex(IList<DataPoint> points, double x) {
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < points.Count; i++) {
                double distance = Math.Abs(points[i].X - x);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }
    }
}
