using Newtonsoft.Json;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Database.ExportImport {

    public class ExportProfile {
        public const string METADATA_FILE = "metadata.json";
        public const string PROFILEPREFS_FILE = "profilePreference.json";
        public const string PROJECTS_FILE = "projects.json";
        public const string EXPOSURETEMPLATES_FILE = "exposureTemplates.json";
        public const string ACQUIREDIMAGES_FILE = "acquiredImages.json";
        public const string IMAGEDATA_FILE = "imageData.json";

        private readonly ProfileMeta profileMeta;
        private readonly bool exportAcquiredImages;

        public ExportProfile(ProfileMeta profileMeta, bool exportAcquiredImages) {
            this.profileMeta = profileMeta;
            this.exportAcquiredImages = exportAcquiredImages;
        }

        public ExportStatus Export() {
            ExportStatus status = new ExportStatus();
            string exportDir = null;
            TSLogger.Info($"exporting profile '{profileMeta.Name}' to zip file");

            try {
                exportDir = CreateExportDir();
                TSLogger.Debug($"profile export temp dir: {exportDir}");

                WriteMetadata(status, exportDir);
                WriteProfiledata(status, exportDir);

                string zipFileName = ZipExportDir(exportDir);
                TSLogger.Debug($"profile export zip file: {zipFileName}");

                status.IsSuccess = true;
                status.TempZipFileName = zipFileName;
                return status;
            } catch (Exception ex) {
                TSLogger.Error($"failed to export profile '{profileMeta.Name}': {ex.Message}\n{ex.StackTrace}");
                status.IsSuccess = false;
                status.ErrorMessage = $"failed to export profile '{profileMeta.Name}': {ex.Message}";
                return status;
            } finally {
                if (exportDir != null && Directory.Exists(exportDir)) { Directory.Delete(exportDir, true); }
            }
        }

        private void WriteMetadata(ExportStatus exportStatus, string exportDir) {
            var exportMetadata = new ExportMetadata {
                ExportDate = DateTime.Now,
                TargetSchedulerVersion = GetPluginVersion(),
                DatabaseVersion = GetDatabaseVersion(),
                ExportedProfileName = profileMeta.Name,
                ExportedProfileId = profileMeta.Id.ToString(),
            };

            var jsonString = JsonConvert.SerializeObject(exportMetadata, Formatting.Indented);
            File.WriteAllText(Path.Combine(exportDir, METADATA_FILE), jsonString);
        }

        private void WriteProfiledata(ExportStatus exportStatus, string exportDir) {
            string profileId = profileMeta.Id.ToString();

            using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                var profilePrefs = context.GetProfilePreferenceForExport(profileId);
                if (profilePrefs != null) {
                    Serialize(profilePrefs, Path.Combine(exportDir, PROFILEPREFS_FILE));
                }

                var projects = context.GetProjectsForExport(profileId);
                if (projects.Any()) {
                    Serialize(projects, Path.Combine(exportDir, PROJECTS_FILE));
                    exportStatus.NumProjects = projects.Count;
                    int numTargets = 0;
                    projects.ForEach(p => numTargets += p.Targets.Count);
                    exportStatus.NumTargets = numTargets;
                }

                var exposureTemplates = context.GetExposureTemplatesForExport(profileId);
                if (exposureTemplates.Any()) {
                    Serialize(exposureTemplates, Path.Combine(exportDir, EXPOSURETEMPLATES_FILE));
                    exportStatus.NumExposureTemplates = exposureTemplates.Count;
                }

                if (exportAcquiredImages) {
                    var acquiredImages = context.GetAcquiredImagesForExport(profileId);
                    if (acquiredImages.Any()) {
                        Serialize(acquiredImages, Path.Combine(exportDir, ACQUIREDIMAGES_FILE));
                        exportStatus.NumAcquiredImageRows = acquiredImages.Count;
                    }

                    var imageData = context.GetImageDataForExport(profileId);
                    if (imageData.Any()) {
                        Serialize(imageData, Path.Combine(exportDir, IMAGEDATA_FILE));
                    }
                }
            }
        }

        private void Serialize(object obj, string fileName) {
            using (StreamWriter sw = File.CreateText(fileName)) {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(sw, obj);
            }
        }

        private string CreateExportDir() {
            string exportDir = Path.Combine(Path.GetTempPath(), "ts-profile-export");
            if (Directory.Exists(exportDir)) { Directory.Delete(exportDir, true); }
            Directory.CreateDirectory(exportDir);
            return exportDir;
        }

        private string ZipExportDir(string exportDir) {
            string zipPath = Path.GetTempPath() + Guid.NewGuid().ToString() + ".zip";
            ZipFile.CreateFromDirectory(exportDir, zipPath);
            return zipPath;
        }

        private string GetPluginVersion() {
            return this.GetType().Assembly?.GetName().Version?.ToString();
        }

        public static string GetDatabaseVersion() {
            try {
                using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                    int version = context.Database.SqlQuery<int>("PRAGMA user_version").First();
                    return version.ToString();
                }
            } catch (Exception ex) {
                return $"failed to get TS database version: {ex.Message}";
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ExportMetadata {
        [JsonProperty] public DateTime ExportDate { get; set; }
        [JsonProperty] public string TargetSchedulerVersion { get; set; }
        [JsonProperty] public string DatabaseVersion { get; set; }
        [JsonProperty] public string ExportedProfileName { get; set; }
        [JsonProperty] public string ExportedProfileId { get; set; }

        public ExportMetadata() {
        }
    }

    public class ExportStatus {
        public bool IsSuccess { get; set; } = false;
        public string TempZipFileName { get; set; }
        public string ErrorMessage { get; set; }

        public int NumExposureTemplates { get; set; }
        public int NumProjects { get; set; }
        public int NumTargets { get; set; }
        public int NumAcquiredImageRows { get; set; }

        public ExportStatus() {
        }

        public string GetDetails() {
            StringBuilder sb = new StringBuilder();

            if (!IsSuccess) {
                sb.AppendLine(ErrorMessage);
                return sb.ToString();
            }

            sb.AppendLine("Exported:");
            sb.AppendLine($"  {NumExposureTemplates} exposure templates");
            sb.AppendLine($"  {NumProjects} projects");
            sb.AppendLine($"  {NumTargets} targets");
            if (NumAcquiredImageRows > 0) {
                sb.AppendLine($"  {NumAcquiredImageRows} acquired image rows");
            }

            return sb.ToString();
        }
    }
}