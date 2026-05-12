using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NINA.Plugin.TargetScheduler.Controls.AcquiredImages.ImageView {

    public partial class ImageViewerWindow : Window {
        private double _scale = 1.0;
        private bool _isDragging = false;
        private Point _lastDragPoint;

        public ImageViewerWindow(string resolvedFilePath) {
            InitializeComponent();
            Title = $"Image Viewer - {Path.GetFileName(resolvedFilePath)}";

            var vm = new ImageViewerVM();
            DataContext = vm;

            vm.ImageLoaded += (s, e) => {
                Dispatcher.BeginInvoke(FitToWindow, DispatcherPriority.Loaded);
            };

            _ = vm.LoadAsync(resolvedFilePath, TargetScheduler.ImageDataFactory);
        }

        private void FitToWindow() {
            if (PART_Image.Source is not BitmapSource bmp) return;
            double viewW = PART_ScrollViewer.ActualWidth;
            double viewH = PART_ScrollViewer.ActualHeight;
            if (viewW <= 0 || viewH <= 0 || bmp.PixelWidth <= 0 || bmp.PixelHeight <= 0) return;

            double newScale = Math.Min(viewW / bmp.PixelWidth, viewH / bmp.PixelHeight);
            SetScale(newScale);
        }

        private void SetScale(double newScale) {
            _scale = Math.Max(0.02, Math.Min(20.0, newScale));
            PART_ImageScale.ScaleX = _scale;
            PART_ImageScale.ScaleY = _scale;
            PART_ZoomLabel.Text = $"{_scale * 100:F0}%";
        }

        private void FitButton_Click(object sender, RoutedEventArgs e) {
            FitToWindow();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e) {
            ZoomAroundCenter(1.25);
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) {
            ZoomAroundCenter(0.8);
        }

        private void Zoom1to1Button_Click(object sender, RoutedEventArgs e) {
            SetScale(1.0);
        }

        private void ZoomAroundCenter(double factor) {
            double cx = PART_ScrollViewer.ActualWidth / 2.0;
            double cy = PART_ScrollViewer.ActualHeight / 2.0;
            ApplyZoom(factor, cx, cy);
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (PART_Image.Source == null) return;

            double factor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
            Point viewportPos = e.GetPosition(PART_ScrollViewer);
            ApplyZoom(factor, viewportPos.X, viewportPos.Y);
            e.Handled = true;
        }

        private void ApplyZoom(double factor, double vpX, double vpY) {
            double oldScale = _scale;
            double newScale = Math.Max(0.02, Math.Min(20.0, oldScale * factor));
            if (Math.Abs(newScale - oldScale) < 0.0001) return;

            double offsetX = PART_ScrollViewer.HorizontalOffset;
            double offsetY = PART_ScrollViewer.VerticalOffset;

            // Content coordinate under the viewport anchor point before zoom
            double contentX = (offsetX + vpX) / oldScale;
            double contentY = (offsetY + vpY) / oldScale;

            SetScale(newScale);
            PART_ScrollViewer.UpdateLayout();

            // Scroll so the same content point stays under the anchor
            PART_ScrollViewer.ScrollToHorizontalOffset(contentX * newScale - vpX);
            PART_ScrollViewer.ScrollToVerticalOffset(contentY * newScale - vpY);
        }

        private void ScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (PART_Image.Source == null) return;
            _isDragging = true;
            _lastDragPoint = e.GetPosition(PART_ScrollViewer);
            Mouse.Capture(PART_ScrollViewer);
            PART_ScrollViewer.Cursor = Cursors.Hand;
            e.Handled = true;
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e) {
            if (!_isDragging) return;
            Point current = e.GetPosition(PART_ScrollViewer);
            double dx = _lastDragPoint.X - current.X;
            double dy = _lastDragPoint.Y - current.Y;
            _lastDragPoint = current;
            PART_ScrollViewer.ScrollToHorizontalOffset(PART_ScrollViewer.HorizontalOffset + dx);
            PART_ScrollViewer.ScrollToVerticalOffset(PART_ScrollViewer.VerticalOffset + dy);
        }

        private void ScrollViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!_isDragging) return;
            _isDragging = false;
            Mouse.Capture(null);
            PART_ScrollViewer.Cursor = Cursors.Arrow;
        }
    }
}
