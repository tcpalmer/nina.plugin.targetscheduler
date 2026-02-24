using NINA.Core.Model;
using NINA.Equipment.Model;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Flats;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Collections.Concurrent;
using System.Data.Entity.Migrations;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Sequencer {

    /// <summary>
    /// Handle tasks during the image save pipeline.  If grading isn't enabled, increment the exposure plan
    /// accepted count and write the AcquiredImage record marked accepted.  If grading is enabled, treat the
    /// exposure as pended and queue it for grading.  Also, add the Flat Session ID and Project Name file
    /// patterns.
    /// </summary>
    public class ImageSaveWatcher : IImageSaveWatcher {
        private object lockObj = new object();
        private IProfile profile;
        private ProfilePreference profilePreference;
        private IImageSaveMediator imageSaveMediator;
        private CancellationToken token;
        private ConcurrentDictionary<int, ExposureWaitData> exposureDictionary;

        public ImageSaveWatcher(IProfile profile, IImageSaveMediator imageSaveMediator) {
            this.profile = profile;
            this.profilePreference = GetProfilePreference(profile);
            this.imageSaveMediator = imageSaveMediator;
            exposureDictionary = new ConcurrentDictionary<int, ExposureWaitData>(Environment.ProcessorCount * 2, 31);
        }

        public void Start(CancellationToken token) {
            this.token = token;
            if (!exposureDictionary.IsEmpty) { Stop(); }

            imageSaveMediator.ImageSaved += ImageSaved;
            imageSaveMediator.BeforeFinalizeImageSaved += BeforeFinalizeImageSaved;
            TSLogger.Debug($"start watching image saves");
        }

        public void WaitForExposure(int identifier, ExposureWaitData waitData) {
            TSLogger.Debug($"registering image save exposure wait: {waitData}");
            exposureDictionary.TryAdd(identifier, waitData);
        }

        public void WaitForAllImagesSaved() {
            // Wait for any remaining images to come through: poll every 400ms and bail out after 80 secs
            TSLogger.Debug($"waiting for exposures to complete:\n{ExposureIdsLog()}");
            int count = 0;
            while (!exposureDictionary.IsEmpty && !token.IsCancellationRequested) {
                if (++count == 200) {
                    TSLogger.Warning($"timed out or canceled waiting on all exposures to be processed, remaining:\n{ExposureIdsLog()}");
                    break;
                }

                Thread.Sleep(400);
            }
        }

        public void Stop() {
            TSLogger.Debug($"stopping image save watcher");
            WaitForAllImagesSaved();

            imageSaveMediator.ImageSaved -= ImageSaved;
            imageSaveMediator.BeforeFinalizeImageSaved -= BeforeFinalizeImageSaved;
            exposureDictionary.Clear();

            TSLogger.Debug($"stop watching image saves");
        }

        private Task BeforeFinalizeImageSaved(object sender, BeforeFinalizeImageSavedEventArgs args) {
            if (args.Image.RawImageData.MetaData.Image.ImageType != CaptureSequence.ImageTypes.LIGHT) {
                return Task.CompletedTask;
            }

            ExposureWaitData waitData = GetWaitData(args);
            if (waitData == null) { return Task.CompletedTask; }

            ITarget target = waitData.Target;
            string sessionIdentifier = FlatsExpert.FormatSessionIdentifier(target.Project.SessionId);

            ImagePattern proto = TargetScheduler.FlatSessionIdImagePattern;
            args.AddImagePattern(new ImagePattern(proto.Key, proto.Description) { Value = sessionIdentifier });

            string projectName = target?.Project?.Name ?? string.Empty;
            proto = TargetScheduler.ProjectNameImagePattern;
            args.AddImagePattern(new ImagePattern(proto.Key, proto.Description) { Value = projectName });

            return Task.CompletedTask;
        }

        public virtual async void ImageSaved(object sender, ImageSavedEventArgs imageSavedEventArgs) {
            if (imageSavedEventArgs.MetaData.Image.ImageType != CaptureSequence.ImageTypes.LIGHT) {
                return;
            }

            ExposureWaitData waitData = GetWaitData(imageSavedEventArgs);
            if (waitData == null) { return; }

            ITarget target = waitData.Target;
            IExposure exposure = waitData.Exposure;

            try {
                TSLogger.Debug($"starting ImageSaved: eId={exposure.DatabaseId}, imageId={imageSavedEventArgs.MetaData.Image.Id}");
                if (!profilePreference.DoSkipSimulatedUpdates) {
                    bool autoAccepted = !target.Project.EnableGrader;
                    int acquiredImageId = UpdateDatabase(waitData, imageSavedEventArgs, autoAccepted);
                    if (!autoAccepted) {
                        GradingWorkData workData = GetGradingWorkData(waitData, acquiredImageId, imageSavedEventArgs);
                        await GetImageGradingController().Enqueue(workData, waitData.Token);
                    }
                } else {
                    TSLogger.Debug($"simulated run enabled, skipping all database updates associated with image save");
                }
            } catch (Exception ex) {
                TSLogger.Error($"exception in ImageSaveWatcher.ImageSaved: {ex.Message}\n{ex.StackTrace}");
            } finally {
                TSLogger.Debug($"ImageSaved completed: {waitData}");
                RemoveWaitData(waitData.ImageId);
            }
        }

        protected ExposureWaitData GetWaitData(object args) {
            int imageId = -1;

            BeforeFinalizeImageSavedEventArgs beforeArgs = args as BeforeFinalizeImageSavedEventArgs;
            if (beforeArgs != null) {
                imageId = beforeArgs.Image.RawImageData.MetaData.Image.Id;
            } else {
                ImageSavedEventArgs imageSavedArgs = args as ImageSavedEventArgs;
                if (imageSavedArgs != null) {
                    imageId = imageSavedArgs.MetaData.Image.Id;
                }
            }

            ExposureWaitData waitData;
            if (exposureDictionary.TryGetValue(imageId, out waitData)) {
                return waitData;
            }

            TSLogger.Warning($"failed to find exposure wait data for image id: {imageId}");
            return null;
        }

        private void RemoveWaitData(int imageId) {
            ExposureWaitData old;
            exposureDictionary.TryRemove((int)imageId, out old);
        }

        public virtual ProfilePreference GetProfilePreference(IProfile profile) {
            return new SchedulerPlanLoader(profile).GetProfilePreferences();
        }

        public virtual ISchedulerDatabaseContext GetSchedulerDatabaseContext() {
            return new SchedulerDatabaseInteraction().GetContext();
        }

        public virtual ImageGradingController GetImageGradingController() {
            return ImageGradingController.Instance;
        }

        private GradingWorkData GetGradingWorkData(ExposureWaitData waitData, int acquiredImageId, ImageSavedEventArgs imageSavedEventArgs) {
            ImageGraderPreferences prefs = new ImageGraderPreferences(profile, profilePreference);
            return new GradingWorkData(waitData.Target.DatabaseId, waitData.Exposure.DatabaseId, acquiredImageId, imageSavedEventArgs.MetaData.Image.Id, prefs);
        }

        private int UpdateDatabase(ExposureWaitData waitData, ImageSavedEventArgs imageSavedEventArgs, bool accepted) {
            lock (lockObj) {
                using (var context = GetSchedulerDatabaseContext()) {
                    using (var transaction = context.Database.BeginTransaction()) {
                        try {
                            ITarget target = waitData.Target;
                            IExposure exposure = waitData.Exposure;
                            ExposurePlan exposurePlan = context.GetExposurePlan(exposure.DatabaseId);

                            if (exposurePlan != null) {
                                exposurePlan.Acquired++;
                                if (accepted) { exposurePlan.Accepted++; }
                                context.ExposurePlanSet.AddOrUpdate(exposurePlan);
                            } else {
                                TSLogger.Warning($"failed to get exposure plan for id={exposure.DatabaseId}");
                            }

                            AcquiredImage acquiredImage = new AcquiredImage(
                                profile.Id.ToString(),
                                target.Project.DatabaseId,
                                target.DatabaseId,
                                exposure.DatabaseId,
                                imageSavedEventArgs.MetaData.Image.ExposureStart,
                                exposure.FilterName,
                                accepted ? GradingStatus.Accepted : GradingStatus.Pending,
                                string.Empty,
                                new ImageMetadata(imageSavedEventArgs, target.Project.SessionId, target.ROI, exposure.ReadoutMode));
                            AcquiredImage entity = context.AcquiredImageSet.Add(acquiredImage);

                            (int width, int height, byte[] data) = Thumbnails.CreateThumbnail(imageSavedEventArgs.Image);
                            context.ImageDataSet.Add(new ImageData("", data, entity.Id, width, height));

                            context.SaveChanges();
                            transaction.Commit();
                            return entity.Id;
                        } catch (Exception e) {
                            TSLogger.Error($"exception updating database for saved image: {e.Message}\n{e.StackTrace}");
                            SchedulerDatabaseContext.CheckValidationErrors(e);
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
        }

        private string ExposureIdsLog() {
            StringBuilder sb = new StringBuilder();
            foreach (var entry in exposureDictionary) {
                sb.AppendLine($"{entry.Key}: {entry.Value}");
            }

            return sb.ToString();
        }
    }

    public class ExposureWaitData {
        public ITarget Target { get; private set; }
        public IExposure Exposure { get; private set; }
        public int ImageId { get; private set; }
        public string ExposureGuid { get; private set; }
        public CancellationToken Token { get; private set; }

        public ExposureWaitData(ITarget target, IExposure exposure, int imageId, CancellationToken token) :
            this(target, exposure, imageId, "n/a", token) { }

        public ExposureWaitData(ITarget target, IExposure exposure, int imageId, string exposureGuid, CancellationToken token) {
            Target = target;
            Exposure = exposure;
            ImageId = imageId;
            ExposureGuid = exposureGuid;
            Token = token;
        }

        public override string ToString() {
            return $"target: {Target.Name}, exposure: {Exposure.FilterName}/{Exposure.DatabaseId}, imageId: {ImageId}, exposureGuid: {ExposureGuid}";
        }
    }

    public interface IImageSaveWatcher {

        void Start(CancellationToken token);

        void WaitForExposure(int identifier, ExposureWaitData waitData);

        void WaitForAllImagesSaved();

        void Stop();
    }
}