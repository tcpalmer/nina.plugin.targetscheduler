using Newtonsoft.Json;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile;
using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Database.ExportImport {

    public class ImportProfile {
        private readonly ProfileMeta importProfileMeta;
        private readonly string importProfileId;
        private readonly string zipFile;
        private readonly bool importAcquiredImages;

        public ImportProfile(ProfileMeta profileMeta, string zipFile, bool importAcquiredImages) {
            this.importProfileMeta = profileMeta;
            this.importProfileId = profileMeta.Id.ToString();
            this.zipFile = zipFile;
            this.importAcquiredImages = importAcquiredImages;
        }

        public ImportStatus Import() {
            ImportStatus status = new ImportStatus();
            string importDir = null;

            try {
                importDir = UnZipExport();
                ExportMetadata metadata = ReadMetadata(importDir);
                TSLogger.Info($"importing exported profile from {metadata.ExportDate} for profile '{metadata.ExportedProfileName}'");

                if (!ConfirmVersions(metadata)) {
                    status.IsSuccess = false;
                    status.ErrorMessage = $"failed to import: database version of export does not match this instance of Target Scheduler";
                    return status;
                }

                ImportData(status, importDir);

                TSLogger.Info("profile import complete");
                status.IsSuccess = true;
                return status;
            } catch (Exception ex) {
                TSLogger.Error($"failed to import to profile '{importProfileMeta.Name}': {ex.Message}\n{ex.StackTrace}");
                status.IsSuccess = false;
                status.ErrorMessage = $"Failed to import: {ex.Message}";
                return status;
            } finally {
                if (importDir != null && Directory.Exists(importDir)) { Directory.Delete(importDir, true); }
            }
        }

        private ImportStatus ImportData(ImportStatus status, string importDir) {
            ProfilePreference profilePreference = null;
            List<ExposureTemplate> exposureTemplates = null;
            List<Project> projects = null;
            List<AcquiredImage> acquiredImages = null;
            List<ImageData> imageData = null;

            string fileName = Path.Combine(importDir, ExportProfile.PROFILEPREFS_FILE);
            if (File.Exists(fileName)) {
                profilePreference = Deserialize<ProfilePreference>(fileName);
            }

            fileName = Path.Combine(importDir, ExportProfile.EXPOSURETEMPLATES_FILE);
            if (File.Exists(fileName)) {
                exposureTemplates = Deserialize<List<ExposureTemplate>>(fileName);
            }

            fileName = Path.Combine(importDir, ExportProfile.PROJECTS_FILE);
            if (File.Exists(fileName)) {
                projects = Deserialize<List<Project>>(fileName);
            }

            fileName = Path.Combine(importDir, ExportProfile.ACQUIREDIMAGES_FILE);
            if (importAcquiredImages && File.Exists(fileName)) {
                acquiredImages = Deserialize<List<AcquiredImage>>(fileName);
            }

            fileName = Path.Combine(importDir, ExportProfile.IMAGEDATA_FILE);
            if (importAcquiredImages && File.Exists(fileName)) {
                imageData = Deserialize<List<ImageData>>(fileName);
            }

            // All rows will be added new to the database so we have to remap embedded old ids to new where necessary
            Dictionary<int, int> exposureTemplateIdMap = null;
            Dictionary<int, int> projectsIdMap = null;
            Dictionary<int, int> targetsIdMap = null;
            Dictionary<int, int> exposurePlansIdMap = null;

            using (var context = new SchedulerDatabaseInteraction().GetContext()) {
                using (var transaction = context.Database.BeginTransaction()) {
                    try {
                        if (profilePreference != null) {
                            if (context.GetProfilePreference(importProfileId) == null) {
                                TSLogger.Info("importing profile preference from exported profile");
                                profilePreference.Id = 0;
                                profilePreference.ProfileId = importProfileId;
                                context.ProfilePreferenceSet.AddOrUpdate(profilePreference);
                            } else {
                                TSLogger.Info("existing profile preference, not overwriting from import");
                            }
                        }

                        if (exposureTemplates != null) {
                            exposureTemplateIdMap = new Dictionary<int, int>(exposureTemplates.Count);
                            HashSet<string> referencedFilters = new HashSet<string>();

                            exposureTemplates.ForEach(e => {
                                int oldId = e.Id;
                                e.Id = 0;
                                e.ProfileId = importProfileId;
                                context.ExposureTemplateSet.Add(e);
                                context.SaveChanges();
                                exposureTemplateIdMap.Add(oldId, e.Id);

                                referencedFilters.Add(e.FilterName);
                            });

                            status.NumExposureTemplates = exposureTemplates.Count;
                            status.ReferencedFilters = referencedFilters.ToList();
                            TSLogger.Info($"imported {exposureTemplates.Count} exposure templates from exported profile");
                        }

                        if (projects != null) {
                            projectsIdMap = new Dictionary<int, int>(projects.Count);
                            targetsIdMap = new Dictionary<int, int>();
                            exposurePlansIdMap = new Dictionary<int, int>();

                            List<Target> targets = new List<Target>();

                            projects.ForEach(p => {
                                targets.AddRange(p.Targets);
                                Project copy = p.Clone();
                                copy.Targets = null;
                                copy.Id = 0;
                                copy.ProfileId = importProfileId;
                                context.ProjectSet.Add(copy);
                                context.SaveChanges();
                                projectsIdMap.Add(p.Id, copy.Id);
                            });

                            foreach (var target in targets) {
                                int oldId = target.Id;
                                target.Id = 0;
                                target.ProjectId = projectsIdMap.GetValueOrDefault(target.ProjectId);
                                List<int> oldIds = new List<int>(target.ExposurePlans.Count);
                                target.ExposurePlans.ForEach(ep => {
                                    oldIds.Add(ep.Id);
                                    ep.Id = 0;
                                    ep.ProfileId = importProfileId;
                                    ep.ExposureTemplateId = exposureTemplateIdMap.GetValueOrDefault(ep.ExposureTemplateId);
                                });

                                context.TargetSet.Add(target);
                                context.SaveChanges();
                                targetsIdMap.Add(oldId, target.Id);

                                for (int i = 0; i < oldIds.Count; i++) {
                                    exposurePlansIdMap.Add(oldIds[i], target.ExposurePlans[i].Id);
                                }
                            }

                            status.NumProjects = projects.Count;
                            status.NumTargets = targets.Count;

                            TSLogger.Info($"imported {projects.Count} projects from exported profile");
                            TSLogger.Info($"imported {targets.Count} targets from exported profile");
                        }

                        if (acquiredImages != null) {
                            Dictionary<int, int> acquiredImagesIdMap = new Dictionary<int, int>(acquiredImages.Count);

                            acquiredImages.ForEach(ai => {
                                int oldId = ai.Id;
                                ai.Id = 0;
                                ai.ProfileId = importProfileId;
                                ai.ProjectId = projectsIdMap.GetValueOrDefault(ai.ProjectId);
                                ai.TargetId = targetsIdMap.GetValueOrDefault(ai.TargetId);
                                ai.ExposureId = exposurePlansIdMap.GetValueOrDefault(ai.ExposureId);

                                context.AcquiredImageSet.Add(ai);
                                context.SaveChanges();
                                acquiredImagesIdMap.Add(oldId, ai.Id);
                            });

                            status.NumAcquiredImageRows = acquiredImages.Count;
                            TSLogger.Info($"imported {acquiredImages.Count} acquired image records from exported profile");

                            if (imageData != null) {
                                imageData.ForEach(i => {
                                    i.Id = 0;
                                    i.AcquiredImageId = acquiredImagesIdMap.GetValueOrDefault(i.AcquiredImageId);
                                });

                                context.ImageDataSet.AddRange(imageData);
                                TSLogger.Info($"imported {imageData.Count} image data records from exported profile");
                            }
                        }

                        context.SaveChanges();
                        transaction.Commit();

                        status.IsSuccess = true;
                    } catch (Exception ex) {
                        try {
                            TSLogger.Error($"database error importing profile, rolling back: {ex.Message}\n{ex.StackTrace}");
                            transaction.Rollback();
                        } catch (Exception e) {
                            TSLogger.Error($"error executing transaction rollback: {e.Message}\n{e.StackTrace}");
                        }

                        throw;
                    }

                    return status;
                }
            }
        }

        private T Deserialize<T>(string fileName) {
            using (StreamReader sr = new StreamReader(fileName)) {
                using (var jsonTextReader = new JsonTextReader(sr)) {
                    var serializer = new JsonSerializer();
                    return serializer.Deserialize<T>(jsonTextReader);
                }
            }

            //  return JsonConvert.DeserializeObject<T>(File.ReadAllText(fileName));
        }

        private string UnZipExport() {
            string importDir = Path.Combine(Path.GetTempPath(), "ts-profile-import");
            if (Directory.Exists(importDir)) { Directory.Delete(importDir, true); }
            ZipFile.ExtractToDirectory(zipFile, importDir);
            return importDir;
        }

        private ExportMetadata ReadMetadata(string importDir) {
            string fileName = Path.Combine(importDir, ExportProfile.METADATA_FILE);
            if (!File.Exists(fileName)) {
                throw new Exception("Profile Zip metadata file missing.  Are you sure this Zip is from a profile export?");
            }

            return JsonConvert.DeserializeObject<ExportMetadata>(File.ReadAllText(fileName));
        }

        private bool ConfirmVersions(ExportMetadata metadata) {
            string importDatabaseVersion = ExportProfile.GetDatabaseVersion();
            if (metadata.DatabaseVersion != importDatabaseVersion) {
                TSLogger.Error($"Can't import profile due to mismatched database versions: export is {metadata.DatabaseVersion}, current is {importDatabaseVersion}");
                return false;
            }

            return true;
        }
    }

    public class ImportStatus {
        public bool IsSuccess { get; set; } = false;
        public string ErrorMessage { get; set; }
        public List<string> ReferencedFilters { get; set; }

        public int NumExposureTemplates { get; set; }
        public int NumProjects { get; set; }
        public int NumTargets { get; set; }
        public int NumAcquiredImageRows { get; set; }

        public ImportStatus() {
        }

        public string GetDetails() {
            StringBuilder sb = new StringBuilder();

            if (!IsSuccess) {
                sb.AppendLine(ErrorMessage);
                return sb.ToString();
            }

            sb.AppendLine("Imported:");
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