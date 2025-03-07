using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.SyncService.Sync;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Grading {

    public class GraderExpert {
        private IProfile profile;
        private IImageGraderPreferences preferences;
        private ImageMetadata imageMetadata;

        public bool NoGradingMetricsEnabled => noGradingMetricsEnabled();
        public bool EnableGradeRMS => enableGradeRMS();

        public GraderExpert(GradingWorkData workData, ImageMetadata imageMetadata) : this(workData.GraderPreferences, imageMetadata) {
        }

        public GraderExpert(IImageGraderPreferences preferences, ImageMetadata imageMetadata) {
            this.profile = preferences.Profile;
            this.preferences = preferences;
            this.imageMetadata = imageMetadata;
        }

        public bool GradeRMS() {
            if (!preferences.EnableGradeRMS) return true;

            if (imageMetadata.GuidingRMS == 0 || imageMetadata.GuidingRMSScale == 0) {
                TSLogger.Debug("image grading: guiding RMS not available");
                return true;
            }

            double guidingRMSArcSecs = imageMetadata.GuidingRMS * imageMetadata.GuidingRMSScale;
            if (guidingRMSArcSecs <= 0) {
                TSLogger.Debug("image grading: guiding RMS not valid for grading");
                return true;
            }

            try {
                double pixelSize = profile.CameraSettings.PixelSize;
                double focalLenth = profile.TelescopeSettings.FocalLength;
                double binning = GetBinning();
                double cameraArcSecsPerPixel = (pixelSize / focalLenth) * 206.265 * binning;
                double cameraRMSPerPixel = guidingRMSArcSecs * cameraArcSecsPerPixel;

                TSLogger.Debug($"image grading: RMS pixelSize={pixelSize} focalLength={focalLenth} bin={binning} cameraArcSecsPerPixel={cameraArcSecsPerPixel} cameraRMSPerPixel={cameraRMSPerPixel}");
                return (cameraRMSPerPixel > preferences.RMSPixelThreshold) ? false : true;
            } catch (Exception e) {
                TSLogger.Warning($"image grading: failed to determine RMS error in main camera pixels: {e.Message}\n{e.StackTrace}");
                return true;
            }
        }

        public bool GradeStars(List<AcquiredImage> population) {
            if (!preferences.EnableGradeStars) return true;

            List<double> samples = GetSamples(population, i => { return i.Metadata.DetectedStars; });
            TSLogger.Debug("image grading: detected star count ->");
            int detectedStars = imageMetadata.DetectedStars;
            if (detectedStars == 0 || !WithinAcceptableVariance(samples, detectedStars, preferences.DetectedStarsSigmaFactor, true)) {
                return false;
            }

            return true;
        }

        public bool GradeHFR(List<AcquiredImage> population) {
            if (!preferences.EnableGradeHFR) return true;

            double hfr = imageMetadata.HFR;
            if (preferences.AutoAcceptLevelHFR > 0 && hfr <= preferences.AutoAcceptLevelHFR) {
                TSLogger.Debug($"image grading: HFR auto accepted: actual ({hfr}) <= level ({preferences.AutoAcceptLevelHFR})");
                return true;
            }

            List<double> samples = GetSamples(population, i => { return i.Metadata.HFR; });
            TSLogger.Debug("image grading: HFR ->");
            if (NearZero(hfr) || !WithinAcceptableVariance(samples, hfr, preferences.HFRSigmaFactor, false)) {
                return false;
            }

            return true;
        }

        public bool GradeFWHM(List<AcquiredImage> population) {
            if (!preferences.EnableGradeFWHM) return true;

            double fwhm = imageMetadata.FWHM;
            if (Double.IsNaN(fwhm)) {
                TSLogger.Warning("image grading: FWHM grading is enabled but image doesn't have FWHM metric.  Is Hocus Focus installed, enabled, and configured for star detection?");
            } else {
                if (preferences.AutoAcceptLevelFWHM > 0 && fwhm <= preferences.AutoAcceptLevelFWHM) {
                    TSLogger.Debug($"image grading: FWHM auto accepted: actual ({fwhm}) <= level ({preferences.AutoAcceptLevelFWHM})");
                    return true;
                }

                List<double> samples = GetSamples(population, i => { return i.Metadata.FWHM; });
                if (SamplesHaveData(samples)) {
                    TSLogger.Debug("image grading: FWHM ->");
                    if (NearZero(fwhm) || !WithinAcceptableVariance(samples, fwhm, preferences.FWHMSigmaFactor, false)) {
                        TSLogger.Debug("image grading: failed FWHM grading => NOT accepted");
                        return false;
                    }
                } else {
                    TSLogger.Warning("All comparison samples for FWHM don't have valid data, skipping FWHM grading");
                }
            }

            return true;
        }

        public bool GradeEccentricity(List<AcquiredImage> population) {
            if (!preferences.EnableGradeEccentricity) return true;

            double eccentricity = imageMetadata.Eccentricity;
            if (eccentricity == Double.NaN) {
                TSLogger.Warning("image grading: eccentricity grading is enabled but image doesn't have eccentricity metric.  Is Hocus Focus installed, enabled, and configured for star detection?");
            } else {
                if (preferences.AutoAcceptLevelEccentricity > 0 && eccentricity <= preferences.AutoAcceptLevelEccentricity) {
                    TSLogger.Debug($"image grading: eccentricity auto accepted: actual ({eccentricity}) <= level ({preferences.AutoAcceptLevelEccentricity})");
                    return true;
                }

                List<double> samples = GetSamples(population, i => { return i.Metadata.Eccentricity; });
                if (SamplesHaveData(samples)) {
                    TSLogger.Debug("image grading: eccentricity ->");
                    if (NearZero(eccentricity) || !WithinAcceptableVariance(samples, eccentricity, preferences.EccentricitySigmaFactor, false)) {
                        TSLogger.Debug("image grading: failed eccentricity grading => NOT accepted");
                        return false;
                    }
                } else {
                    TSLogger.Warning("All comparison samples for eccentricity don't have valid data, skipping eccentricity grading");
                }
            }

            return true;
        }

        private List<double> GetSamples(List<AcquiredImage> population, Func<AcquiredImage, double> Sample) {
            List<double> samples = new List<double>();
            population.ForEach(i => samples.Add(Sample(i)));
            return samples;
        }

        private bool WithinAcceptableVariance(List<double> samples, double newSample, double sigmaFactor, bool positiveImprovement) {
            TSLogger.Debug($"    samples={SamplesToString(samples)}");
            (double mean, double stddev) = SampleStandardDeviation(samples);

            if (preferences.AcceptImprovement) {
                if (positiveImprovement && newSample > mean) {
                    TSLogger.Debug($"    mean={mean} sample={newSample} (acceptable: improved)");
                    return true;
                }
                if (!positiveImprovement && newSample < mean) {
                    TSLogger.Debug($"    mean={mean} sample={newSample} (acceptable: improved)");
                    return true;
                }
            }

            double variance = Math.Abs(mean - newSample);
            TSLogger.Debug($"    mean={mean} stddev={stddev} sample={newSample} variance={variance} sigmaX={sigmaFactor}");
            return variance <= (stddev * sigmaFactor);
        }

        /// <summary>
        /// Determine the mean and the sample (not population) standard deviation of a set of samples.
        /// </summary>
        /// <param name="samples"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public (double, double) SampleStandardDeviation(List<double> samples) {
            if (samples == null || samples.Count < 3) {
                throw new Exception("must have >= 3 samples");
            }

            double mean = samples.Average();
            double sum = samples.Sum(d => Math.Pow(d - mean, 2));
            double stddev = Math.Sqrt((sum) / (samples.Count - 1));

            return (mean, stddev);
        }

        private bool SamplesHaveData(List<double> samples) {
            foreach (double sample in samples) {
                if (sample <= 0 || Double.IsNaN(sample)) {
                    return false;
                }
            }

            return true;
        }

        private bool NearZero(double value) {
            return Math.Abs(value) <= 0.001;
        }

        private double GetBinning() {
            try {
                string bin = imageMetadata.Binning;
                if (string.IsNullOrEmpty(bin)) {
                    return 1;
                }

                return double.Parse(bin.Substring(0, 1));
            } catch (Exception) {
                return 1;
            }
        }

        private string SamplesToString(List<double> samples) {
            StringBuilder sb = new StringBuilder();
            samples.ForEach(s => sb.Append($"{s}, "));
            return sb.ToString();
        }

        private bool noGradingMetricsEnabled() {
            if (!preferences.EnableGradeStars && !preferences.EnableGradeHFR &&
                !preferences.EnableGradeFWHM && !preferences.EnableGradeEccentricity) {
                return true;
            }

            return false;
        }

        private bool enableGradeRMS() {
            // Disable RMS grading if running as a sync client since no guiding data will be available
            if (preferences.EnableGradeRMS && SyncManager.Instance.IsRunning && !SyncManager.Instance.IsServer) {
                return false;
            }

            return preferences.EnableGradeRMS;
        }
    }
}