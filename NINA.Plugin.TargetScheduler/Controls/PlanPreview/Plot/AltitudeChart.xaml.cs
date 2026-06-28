#region "copyright"

/*
    This control is copied from N.I.N.A. (NINA.WPF.Base/View/AltitudeChart.xaml.cs) so that
    Target Scheduler can augment it independently.  The original is governed by the notice below.

    Copyright � 2016 - 2026 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Astrometry;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RectangleAnnotation = OxyPlot.Wpf.RectangleAnnotation;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview.Plot {

    /// <summary>
    /// Interaction logic for AltitudeChart.xaml
    /// </summary>
    public partial class AltitudeChart : UserControl {

        public AltitudeChart() {
            InitializeComponent();
            InitializeBandToolTip();
        }

        /// <summary>
        /// Builds an altitude chart for the supplied <see cref="PlannerChartData"/>, sizing it and binding
        /// the DSO, nighttime data, imaging window, and exposure bands.  Shared by the Plan Preview tree and
        /// the in-UI planner report so both render an identical chart.
        /// </summary>
        public static AltitudeChart Create(PlannerChartData data, double width, double height) {
            AltitudeChart chart = new AltitudeChart {
                Width = width,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Left,
                DataContext = data.Dso,
                NighttimeData = data.NighttimeData,
                ImagingStart = data.ImagingStart,
                ImagingStop = data.ImagingStop
            };

            chart.SetExposureRuns(data.ExposureRuns);
            return chart;
        }

        public static DependencyProperty NighttimeDataProperty = DependencyProperty.Register("NighttimeData", typeof(NighttimeData), typeof(AltitudeChart));

        public NighttimeData NighttimeData {
            get => (NighttimeData)GetValue(NighttimeDataProperty);
            set => SetValue(NighttimeDataProperty, value);
        }

        public static DependencyProperty AnnotateAltitudeAxisProperty = DependencyProperty.Register("AnnotateAltitudeAxis", typeof(bool), typeof(AltitudeChart), new PropertyMetadata(true));

        public bool AnnotateAltitudeAxis {
            get => (bool)GetValue(AnnotateAltitudeAxisProperty);
            set => SetValue(AnnotateAltitudeAxisProperty, value);
        }

        public static DependencyProperty AnnotateTimeAxisProperty = DependencyProperty.Register("AnnotateTimeAxis", typeof(bool), typeof(AltitudeChart), new PropertyMetadata(true));

        public bool AnnotateTimeAxis {
            get => (bool)GetValue(AnnotateTimeAxisProperty);
            set => SetValue(AnnotateTimeAxisProperty, value);
        }

        public static DependencyProperty MoonHorizontalAlignmentProperty = DependencyProperty.Register("MoonHorizontalAlignment", typeof(HorizontalAlignment), typeof(AltitudeChart), new PropertyMetadata(HorizontalAlignment.Right));

        public HorizontalAlignment MoonHorizontalAlignment {
            get => (HorizontalAlignment)GetValue(MoonHorizontalAlignmentProperty);
            set => SetValue(MoonHorizontalAlignmentProperty, value);
        }

        public static DependencyProperty MoonVerticalAlignmentProperty = DependencyProperty.Register("MoonVerticalAlignment", typeof(VerticalAlignment), typeof(AltitudeChart), new PropertyMetadata(VerticalAlignment.Top));

        public VerticalAlignment MoonVerticalAlignment {
            get => (VerticalAlignment)GetValue(MoonVerticalAlignmentProperty);
            set => SetValue(MoonVerticalAlignmentProperty, value);
        }

        public static DependencyProperty MoonMarginProperty = DependencyProperty.Register("MoonMargin", typeof(Thickness), typeof(AltitudeChart), new PropertyMetadata(new Thickness(0, 10, 10, 0)));

        public Thickness MoonMargin {
            get => (Thickness)GetValue(MoonMarginProperty);
            set => SetValue(MoonMarginProperty, value);
        }

        public static DependencyProperty ShowMoonProperty = DependencyProperty.Register("ShowMoon", typeof(bool), typeof(AltitudeChart), new PropertyMetadata(true));

        public bool ShowMoon {
            get => (bool)GetValue(ShowMoonProperty);
            set => SetValue(ShowMoonProperty, value);
        }

        public static DependencyProperty ImagingStartProperty = DependencyProperty.Register("ImagingStart", typeof(DateTime), typeof(AltitudeChart), new PropertyMetadata(DateTime.MinValue));

        public DateTime ImagingStart {
            get => (DateTime)GetValue(ImagingStartProperty);
            set => SetValue(ImagingStartProperty, value);
        }

        public static DependencyProperty ImagingStopProperty = DependencyProperty.Register("ImagingStop", typeof(DateTime), typeof(AltitudeChart), new PropertyMetadata(DateTime.MinValue));

        public DateTime ImagingStop {
            get => (DateTime)GetValue(ImagingStopProperty);
            set => SetValue(ImagingStopProperty, value);
        }

        // Tint applied to the solid filter colors so the altitude curve and twilight remain visible through the bands.
        private const byte BandAlpha = 0x70;

        private readonly List<RectangleAnnotation> exposureAnnotations = new List<RectangleAnnotation>();

        // Plot-coordinate X ranges of each band plus its hover label, used to drive the hover tooltip.
        private readonly List<(double MinX, double MaxX, string Label)> exposureBands = new List<(double, double, string)>();

        private Popup bandPopup;
        private TextBlock bandPopupText;

        // OxyPlot 2.1's WPF view has no built-in hover tooltips, so we drive one ourselves: a Popup that follows the
        // cursor and shows the exposure template name immediately on hover.  This is a hover-only (no button) behavior
        // so it doesn't interfere with the left-click-drag tracker.
        private void InitializeBandToolTip() {
            bandPopupText = new TextBlock {
                Margin = new Thickness(6, 3, 6, 3),
                FontSize = 11,
                Foreground = Brushes.White
            };

            bandPopup = new Popup {
                AllowsTransparency = true,
                IsHitTestVisible = false,
                Placement = PlacementMode.Relative,
                PlacementTarget = ExposurePlot,
                Child = new Border {
                    Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x20, 0x20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x60, 0x60, 0x60)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Child = bandPopupText
                }
            };

            ExposurePlot.PreviewMouseMove += OnExposurePlotMouseMove;
            ExposurePlot.MouseLeave += OnExposurePlotMouseLeave;
        }

        private void OnExposurePlotMouseMove(object sender, MouseEventArgs e) {
            Point position = e.GetPosition(ExposurePlot);
            string label = HitTestBandLabel(position);

            if (label == null) {
                HideBandToolTip();
                return;
            }

            if (!ReferenceEquals(bandPopupText.Text, label)) {
                bandPopupText.Text = label;
            }

            bandPopup.HorizontalOffset = position.X + 14;
            bandPopup.VerticalOffset = position.Y + 12;
            if (!bandPopup.IsOpen) {
                bandPopup.IsOpen = true;
            }
        }

        private void OnExposurePlotMouseLeave(object sender, MouseEventArgs e) {
            HideBandToolTip();
        }

        private void HideBandToolTip() {
            if (bandPopup != null && bandPopup.IsOpen) {
                bandPopup.IsOpen = false;
            }
        }

        private string HitTestBandLabel(Point position) {
            if (exposureBands.Count == 0) {
                return null;
            }

            OxyPlot.PlotModel model = ExposurePlot.ActualModel;
            if (model == null) {
                return null;
            }

            Axis xAxis = null;
            foreach (Axis axis in model.Axes) {
                if (axis.Position == AxisPosition.Bottom) {
                    xAxis = axis;
                    break;
                }
            }

            if (xAxis == null) {
                return null;
            }

            double dataX = xAxis.InverseTransform(position.X);
            foreach ((double MinX, double MaxX, string Label) band in exposureBands) {
                if (dataX >= band.MinX && dataX <= band.MaxX) {
                    return band.Label;
                }
            }

            return null;
        }

        /// <summary>
        /// Render the supplied exposure runs as tinted vertical bands (Y 0-90, X = run start/end).  Replaces any
        /// previously rendered bands.  Bands are inserted ahead of the XAML line annotations so the now/start/stop
        /// lines continue to draw on top.
        /// </summary>
        public void SetExposureRuns(IEnumerable<ExposureRun> runs) {
            HideBandToolTip();

            foreach (RectangleAnnotation annotation in exposureAnnotations) {
                ExposurePlot.Annotations.Remove(annotation);
            }
            exposureAnnotations.Clear();
            exposureBands.Clear();

            if (runs != null) {
                int insertIndex = 0;
                foreach (ExposureRun run in runs) {
                    double minX = DateTimeAxis.ToDouble(run.Start);
                    double maxX = DateTimeAxis.ToDouble(run.End);

                    RectangleAnnotation band = new RectangleAnnotation {
                        MinimumX = minX,
                        MaximumX = maxX,
                        MinimumY = 0,
                        MaximumY = 90,
                        Fill = Color.FromArgb(BandAlpha, run.Color.R, run.Color.G, run.Color.B),
                        Stroke = Colors.Transparent,
                        StrokeThickness = 0,
                        Layer = OxyPlot.Annotations.AnnotationLayer.AboveSeries
                    };

                    exposureAnnotations.Add(band);
                    ExposurePlot.Annotations.Insert(insertIndex++, band);

                    string label = !string.IsNullOrWhiteSpace(run.ExposureTemplateName) ? run.ExposureTemplateName : run.FilterName;
                    exposureBands.Add((minX, maxX, label));
                }
            }

            ExposurePlot.InvalidatePlot(true);
        }
    }
}
