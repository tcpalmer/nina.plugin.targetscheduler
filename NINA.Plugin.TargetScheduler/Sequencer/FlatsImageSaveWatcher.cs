using NINA.Core.Model;
using NINA.Equipment.Model;
using NINA.Plugin.TargetScheduler.Flats;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    public class FlatsImageSaveWatcher {
        private IImageSaveMediator imageSaveMediator;
        private int expectedCount;
        private int actualCount = 0;

        public FlatsImageSaveWatcher(IImageSaveMediator imageSaveMediator, int expectedCount) {
            this.imageSaveMediator = imageSaveMediator;
            this.expectedCount = expectedCount;

            imageSaveMediator.BeforeImageSaved += BeforeImageSaved;
            imageSaveMediator.ImageSaved += ImageSaved;
            imageSaveMediator.BeforeFinalizeImageSaved += BeforeFinalizeImageSaved;

            TSLogger.Debug($"start watching flat image saves, expecting {expectedCount} total");
        }

        public void WaitForAllImagesSaved() {
            if (actualCount >= expectedCount) { Stop(); }

            // Wait for any remaining images to come through: poll every 400ms and bail out after 60 secs
            TSLogger.Debug($"waiting for flat exposures to complete, remaining: {expectedCount - actualCount}:");
            int count = 0;
            while (actualCount < expectedCount) {
                if (++count == 150) {
                    TSLogger.Warning($"timed out waiting on all flat exposures to be processed, remaining: {expectedCount - actualCount}");
                    break;
                }

                Thread.Sleep(400);
            }

            Stop();
        }

        private Task BeforeImageSaved(object sender, BeforeImageSavedEventArgs args) {
            if (args.Image.MetaData.Image.ImageType != CaptureSequence.ImageTypes.FLAT) {
                return Task.CompletedTask;
            }

            string overloadedName = args.Image.MetaData.Target.Name;
            (string targetName, string sessionId, string projectName) = FlatsExpert.DeOverloadTargetName(overloadedName);

            TSLogger.Debug($"TS Flats: BeforeImageSaved: {projectName}/{targetName} sid={sessionId} filter={args.Image?.MetaData?.FilterWheel?.Filter}, {actualCount + 1} of {expectedCount}");
            args.Image.MetaData.Target.Name = targetName;
            args.Image.MetaData.Sequence.Title = overloadedName;

            return Task.CompletedTask;
        }

        private Task BeforeFinalizeImageSaved(object sender, BeforeFinalizeImageSavedEventArgs args) {
            if (args.Image.RawImageData.MetaData.Image.ImageType != CaptureSequence.ImageTypes.FLAT) {
                return Task.CompletedTask;
            }

            (string targetName, string sessionId, string projectName) = FlatsExpert.DeOverloadTargetName(args.Image?.RawImageData?.MetaData?.Sequence?.Title);

            string sessionIdentifier = FlatsExpert.FormatSessionIdentifier(int.Parse(sessionId));
            ImagePattern proto = TargetScheduler.FlatSessionIdImagePattern;
            args.AddImagePattern(new ImagePattern(proto.Key, proto.Description) { Value = sessionIdentifier });

            proto = TargetScheduler.ProjectNameImagePattern;
            args.AddImagePattern(new ImagePattern(proto.Key, proto.Description) { Value = projectName });

            TSLogger.Debug($"TS Flats: BeforeFinalizeImageSaved: for {projectName}/{targetName}: sid={sessionIdentifier}, {actualCount + 1} of {expectedCount}");

            return Task.CompletedTask;
        }

        private void ImageSaved(object sender, ImageSavedEventArgs args) {
            if (args.MetaData.Image.ImageType != CaptureSequence.ImageTypes.FLAT) {
                return;
            }

            actualCount++;
            TSLogger.Debug($"TS Flats: ImageSaved: {args.MetaData?.Target?.Name} filter={args.Filter} file={args.PathToImage?.LocalPath}, {actualCount} of {expectedCount}");
        }

        private void Stop() {
            Thread.Sleep(2000); // extra wait since our last notification was before the final file move

            imageSaveMediator.BeforeImageSaved -= BeforeImageSaved;
            imageSaveMediator.ImageSaved -= ImageSaved;
            imageSaveMediator.BeforeFinalizeImageSaved -= BeforeFinalizeImageSaved;
        }
    }
}