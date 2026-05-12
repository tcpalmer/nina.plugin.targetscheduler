using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NINA.Plugin.TargetScheduler.Controls.AcquiredImages.ImageView {

    public class ImageViewerVM : BaseINPC {

        public ImageViewerVM() {
        }

        private BitmapSource displayImage;

        public BitmapSource DisplayImage {
            get => displayImage;
            private set {
                displayImage = value;
                RaisePropertyChanged(nameof(DisplayImage));
            }
        }

        private bool isLoading = true;

        public bool IsLoading {
            get => isLoading;
            private set {
                isLoading = value;
                RaisePropertyChanged(nameof(IsLoading));
            }
        }

        private string statusText = "Loading image...";

        public string StatusText {
            get => statusText;
            private set {
                statusText = value;
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public event EventHandler ImageLoaded;

        public async Task LoadAsync(string filePath, IImageDataFactory imageDataFactory) {
            if (imageDataFactory == null) {
                StatusText = "Image data factory not available";
                IsLoading = false;
                return;
            }

            try {
                IsLoading = true;
                StatusText = "Loading image...";

                IImageData imageData = await imageDataFactory.CreateFromFile(
                    filePath, 16, false, RawConverterEnum.DCRAW, CancellationToken.None);

                IRenderedImage rendered = imageData.RenderImage();
                IRenderedImage stretched = await rendered.Stretch(factor: 0.2, blackClipping: -2.8, unlinked: false);

                BitmapSource bitmap = await Task.Run(() => {
                    BitmapSource source = stretched.Image;
                    source.Freeze();
                    return source;
                });

                DisplayImage = bitmap;
                StatusText = $"{imageData.Properties.Width} × {imageData.Properties.Height}";
                ImageLoaded?.Invoke(this, EventArgs.Empty);
            } catch (Exception ex) {
                TSLogger.Error($"ImageViewer: failed to load {filePath}: {ex.Message}");
                StatusText = $"Failed to load: {ex.Message}";
            } finally {
                IsLoading = false;
            }
        }
    }
}
