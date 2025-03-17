﻿using LinqKit;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.Entity.Validation;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Database {
    public interface ISchedulerDatabaseContext : IDisposable
    {
        DbSet<ProfilePreference> ProfilePreferenceSet { get; set; }
        DbSet<AcquiredImage> AcquiredImageSet { get; set; }
        DbSet<Project> ProjectSet { get; set; }
        DbSet<RuleWeight> RuleWeightSet { get; set; }
        DbSet<Target> TargetSet { get; set; }
        DbSet<ExposurePlan> ExposurePlanSet { get; set; }
        DbSet<ExposureTemplate> ExposureTemplateSet { get; set; }
        DbSet<OverrideExposureOrderItem> OverrideExposureOrderSet { get; set; }
        DbSet<FilterCadenceItem> FilterCadenceSet { get; set; }
        DbSet<FlatHistory> FlatHistorySet { get; set; }
        DbSet<ImageData> ImageDataSet { get; set; }
        System.Data.Entity.Database Database { get; }
        DbChangeTracker ChangeTracker { get; }
        DbContextConfiguration Configuration { get; }
        ProfilePreference GetProfilePreference(string profileId, bool createDefault = false);
        List<Project> GetAllProjects();
        List<Project> GetAllProjects(string profileId);
        List<Project> GetAllProjectsReadOnly(string profileId);
        List<Project> GetOrphanedProjects(List<string> currentProfileIdList);
        List<Project> GetActiveProjects(string profileId);
        bool HasActiveTargets(string profileId);
        List<ExposureTemplate> GetExposureTemplates(string profileId);
        Project GetProject(int projectId);
        Project GetProjectReadOnly(int projectId);
        Target GetTargetOnly(int targetId);
        Target GetTarget(int projectId, int targetId);
        Target GetTargetReadOnly(int targetId);
        Target GetTargetByProject(int projectId, int targetId);
        ExposurePlan GetExposurePlan(int id);
        List<ExposurePlan> GetExposurePlans(int targetId);
        ExposureTemplate GetExposureTemplate(int id);
        List<OverrideExposureOrderItem> GetOverrideExposureOrders(int targetId);
        void ClearExistingOverrideExposureOrders(int targetId);
        void ReplaceFilterCadences(int targetId, List<FilterCadenceItem> items);
        List<FilterCadenceItem> GetFilterCadences(int targetId);
        void ClearExistingFilterCadences(int targetId);
        List<AcquiredImage> GetAcquiredImages(int targetId, string filterName);
        List<AcquiredImage> GetAcquiredImages(int targetId);
        List<AcquiredImage> GetAcquiredImages(string profileId, DateTime newerThan);
        AcquiredImage GetAcquiredImage(int id);
        List<AcquiredImage> GetAcquiredImagesForGrading(ExposurePlan exposurePlan);
        int GetAcquiredImagesCount(DateTime olderThan, int targetId);
        void DeleteOverrideExposureOrders(int targetId);
        void DeleteAcquiredImages(DateTime olderThan, int targetId);
        void DeleteAcquiredImages(int targetId);
        List<FlatHistory> GetFlatsHistory(DateTime lightSessionDate, string profileId);
        List<FlatHistory> GetFlatsHistory(int targetId, string profileId);
        List<FlatHistory> GetFlatsHistory(List<Target> targets, string profileId);
        ImageData GetImageData(int acquiredImageId);
        ImageData GetImageData(int acquiredImageId, string tag);
        ProfilePreference SaveProfilePreference(ProfilePreference profilePreference);
        Project AddNewProject(Project project);
        Project SaveProject(Project project);
        Project PasteProject(string profileId, Project source);
        Project MoveProject(Project project, string profileId);
        bool DeleteProject(Project project, bool deleteAcquiredImagesWithTarget);
        Target AddNewTarget(Project project, Target target);
        Target SaveTarget(Target target, bool clearFilterCadenceItems = false);
        Target PasteTarget(Project project, Target source);
        bool DeleteTarget(Target target);
        Target DeleteExposurePlan(Target target, ExposurePlan exposurePlan);
        Target ResetExposurePlans(Target target);
        Target DeleteAllExposurePlans(Target target);
        ExposureTemplate SaveExposureTemplate(ExposureTemplate exposureTemplate);
        ExposureTemplate PasteExposureTemplate(string profileId, ExposureTemplate source);
        bool DeleteExposureTemplate(ExposureTemplate exposureTemplate);
        void AddExposureTemplates(List<ExposureTemplate> exposureTemplates);
        ExposureTemplate MoveExposureTemplate(ExposureTemplate exposureTemplate, string profileId);
        List<ExposureTemplate> GetOrphanedExposureTemplates(List<string> currentProfileIdList);
        bool Equals(object obj);
        int GetHashCode();
        Type GetType();
        string ToString();
        int SaveChanges();
        Task<int> SaveChangesAsync();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
        IEnumerable<DbEntityValidationResult> GetValidationErrors();
    }

    public class SchedulerDatabaseContext : DbContext, ISchedulerDatabaseContext {
        public DbSet<ProfilePreference> ProfilePreferenceSet { get; set; }
        public DbSet<AcquiredImage> AcquiredImageSet { get; set; }
        public DbSet<Project> ProjectSet { get; set; }
        public DbSet<RuleWeight> RuleWeightSet { get; set; }
        public DbSet<Target> TargetSet { get; set; }
        public DbSet<ExposurePlan> ExposurePlanSet { get; set; }
        public DbSet<ExposureTemplate> ExposureTemplateSet { get; set; }
        public DbSet<OverrideExposureOrderItem> OverrideExposureOrderSet { get; set; }
        public DbSet<FilterCadenceItem> FilterCadenceSet { get; set; }
        public DbSet<FlatHistory> FlatHistorySet { get; set; }
        public DbSet<ImageData> ImageDataSet { get; set; }

        public SchedulerDatabaseContext(string connectionString) : base(new SQLiteConnection() { ConnectionString = connectionString }, true) {
            Configuration.LazyLoadingEnabled = false;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder) {
            BackupPreTS5Database();

            base.OnModelCreating(modelBuilder);
            TSLogger.Info("Target Scheduler database: OnModelCreating");

            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            modelBuilder.Configurations.Add(new ProjectConfiguration());
            modelBuilder.Configurations.Add(new TargetConfiguration());
            modelBuilder.Configurations.Add(new ExposureTemplateConfiguration());
            modelBuilder.Configurations.Add(new AcquiredImageConfiguration());

            var sqi = new CreateOrMigrateDatabaseInitializer<SchedulerDatabaseContext>();
            System.Data.Entity.Database.SetInitializer(sqi);

            // You can add the following to write generated SQL to the console.  Don't leave it active ...
            // Database.Log = Console.Write;
        }

        public ProfilePreference GetProfilePreference(string profileId, bool createDefault = false) {
            ProfilePreference profilePreference = ProfilePreferenceSet.Where(p => p.ProfileId.Equals(profileId)).FirstOrDefault();
            if (profilePreference == null && createDefault) {
                profilePreference = new ProfilePreference(profileId);
            }

            return profilePreference;
        }

        public List<Project> GetAllProjects() {
            return ProjectSet
                .Include("targets.exposureplans.exposuretemplate")
                .Include("ruleweights")
                .ToList();
        }

        public List<Project> GetAllProjects(string profileId) {
            return ProjectSet
                .Include("targets.exposureplans.exposuretemplate")
                .Include("ruleweights")
                .Where(p => p.ProfileId.Equals(profileId))
                .ToList();
        }

        public List<Project> GetAllProjectsReadOnly(string profileId) {
            return ProjectSet
                .Include("targets.exposureplans.exposuretemplate")
                .Include("ruleweights")
                .Where(p => p.ProfileId.Equals(profileId))
                .AsNoTracking()
                .ToList();
        }

        public List<Project> GetOrphanedProjects(List<string> currentProfileIdList) {
            return ProjectSet
                .Include("targets.exposureplans.exposuretemplate")
                .Include("ruleweights")
                .Where(p => !currentProfileIdList.Contains(p.ProfileId))
                .ToList();
        }

        public List<Project> GetActiveProjects(string profileId) {
            var projects = ProjectSet
                .Include("targets.exposureplans.exposuretemplate")
                .Include("ruleweights")
                .Where(p =>
                p.ProfileId.Equals(profileId) &&
                p.state_col == (int)ProjectState.Active);

            projects.ForEach(p => {
                p.Targets.ForEach(t => t.OverrideExposureOrders = GetOverrideExposureOrders(t.Id));
                p.Targets.ForEach(t => t.FilterCadences = GetFilterCadences(t.Id));
            });

            return projects.ToList();
        }

        public bool HasActiveTargets(string profileId) {
            List<Project> projects = ProjectSet
                .AsNoTracking()
                .Include("targets")
                .Where(p =>
                p.ProfileId.Equals(profileId) &&
                p.state_col == (int)ProjectState.Active).ToList();

            foreach (Project project in projects) {
                foreach (Target target in project.Targets) {
                    if (target.Enabled) { return true; }
                }
            }

            return false;
        }

        public List<ExposureTemplate> GetExposureTemplates(string profileId) {
            return ExposureTemplateSet.Where(p => p.profileId == profileId).ToList();
        }

        public Project GetProject(int projectId) {
            Project project = ProjectSet
                .Include("targets.exposureplans.exposuretemplate")
                .Include("ruleweights")
                .Where(p => p.Id == projectId)
            .FirstOrDefault();

            project.Targets.ForEach(t => t.OverrideExposureOrders = GetOverrideExposureOrders(t.Id));
            project.Targets.ForEach(t => t.FilterCadences = GetFilterCadences(t.Id));
            project.FilterCadenceBreakingChange = false;
            return project;
        }

        public Project GetProjectReadOnly(int projectId) {
            Project project = ProjectSet
                .Include("targets.exposureplans.exposuretemplate")
                .Include("ruleweights")
                .Where(p => p.Id == projectId)
                .AsNoTracking()
            .FirstOrDefault();

            return project;
        }

        public Target GetTargetOnly(int targetId) {
            return TargetSet
                .Where(t => t.Id == targetId)
                .FirstOrDefault();
        }

        public Target GetTarget(int projectId, int targetId) {
            Target target = TargetSet
                .Include("exposureplans.exposuretemplate")
                .Where(t => t.Project.Id == projectId && t.Id == targetId)
                .FirstOrDefault();
            target.OverrideExposureOrders = GetOverrideExposureOrders(targetId);
            target.FilterCadences = GetFilterCadences(targetId);
            return target;
        }

        public Target GetTargetReadOnly(int targetId) {
            Target target = TargetSet
                .Include("exposureplans.exposuretemplate")
                .Include("Project")
                .Where(t => t.Id == targetId)
                .AsNoTracking()
                .FirstOrDefault();
            return target;
        }

        public Target GetTargetByProject(int projectId, int targetId) {
            Project project = GetProject(projectId);
            Target target = project.Targets.Where(t => t.Id == targetId).FirstOrDefault();
            target.OverrideExposureOrders = GetOverrideExposureOrders(targetId);
            target.FilterCadences = GetFilterCadences(targetId);
            return target;
        }

        public ExposurePlan GetExposurePlan(int id) {
            return ExposurePlanSet
                .Include("exposuretemplate")
                .Where(p => p.Id == id)
                .FirstOrDefault();
        }

        public List<ExposurePlan> GetExposurePlans(int targetId) {
            return ExposurePlanSet
                .Include("exposuretemplate")
                .Where(p => p.TargetId == targetId)
                .ToList();
        }

        public ExposureTemplate GetExposureTemplate(int id) {
            return ExposureTemplateSet.Where(e => e.Id == id).FirstOrDefault();
        }

        public List<OverrideExposureOrderItem> GetOverrideExposureOrders(int targetId) {
            return OverrideExposureOrderSet
                .Where(o => o.TargetId == targetId)
                .OrderBy(o => o.order).ToList();
        }

        public void ClearExistingOverrideExposureOrders(int targetId) {
            if (GetOverrideExposureOrders(targetId).Count == 0) { return; }

            using (var transaction = Database.BeginTransaction()) {
                try {
                    var predicate = PredicateBuilder.New<OverrideExposureOrderItem>();
                    predicate = predicate.And(oeo => oeo.TargetId == targetId);
                    OverrideExposureOrderSet.RemoveRange(OverrideExposureOrderSet.Where(predicate));
                    SaveChanges();
                    transaction.Commit();
                } catch (Exception e) {
                    TSLogger.Error($"error clearing override exposure order for target ID {targetId}: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                }
            }
        }

        public void ReplaceFilterCadences(int targetId, List<FilterCadenceItem> items) {
            ClearExistingFilterCadences(targetId);

            if (Common.IsEmpty(items)) {
                return;
            }

            using (var transaction = Database.BeginTransaction()) {
                try {
                    items.ForEach(item => { FilterCadenceSet.Add(item); });
                    SaveChanges();
                    transaction.Commit();
                } catch (Exception e) {
                    TSLogger.Error($"error adding filter cadence items for target ID {targetId}: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                }
            }
        }

        public List<FilterCadenceItem> GetFilterCadences(int targetId) {
            return FilterCadenceSet
                .Where(o => o.TargetId == targetId)
                .OrderBy(o => o.order).ToList();
        }

        public void ClearExistingFilterCadences(int targetId) {
            if (GetFilterCadences(targetId).Count == 0) { return; }

            using (var transaction = Database.BeginTransaction()) {
                try {
                    var predicate = PredicateBuilder.New<FilterCadenceItem>();
                    predicate = predicate.And(oeo => oeo.TargetId == targetId);
                    FilterCadenceSet.RemoveRange(FilterCadenceSet.Where(predicate));
                    SaveChanges();
                    transaction.Commit();
                } catch (Exception e) {
                    TSLogger.Error($"error clearing filter cadence for target ID {targetId}: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                }
            }
        }

        public List<AcquiredImage> GetAcquiredImages(int targetId, string filterName) {
            var images = AcquiredImageSet.Where(p =>
                p.TargetId == targetId &&
                p.FilterName == filterName)
              .OrderByDescending(p => p.acquiredDate);
            return images.ToList();
        }

        /* TODO: unclear what version this was from when creating new TS ... ?
        public List<AcquiredImage> GetAcquiredImagesByExposureId(int exposurePlanId) {
            var images = AcquiredImageSet.Where(p => p.ExposurePlanId == exposurePlanId)
              .OrderByDescending(p => p.acquiredDate);
            return images.ToList();
        }*/

        public AcquiredImage GetAcquiredImage(int id) {
            return AcquiredImageSet.Where(p => p.Id == id).FirstOrDefault();
        }

        public List<AcquiredImage> GetAcquiredImages(int targetId) {
            var images = AcquiredImageSet.Where(p => p.TargetId == targetId)
                .AsNoTracking()
                .OrderByDescending(p => p.acquiredDate);
            return images.ToList();
        }

        public List<AcquiredImage> GetAcquiredImages(string profileId, DateTime newerThan) {
            var predicate = PredicateBuilder.New<AcquiredImage>();
            long newerThanSecs = Common.DateTimeToUnixSeconds(newerThan);
            predicate = predicate.And(a => a.acquiredDate > newerThanSecs);
            predicate = predicate.And(a => a.profileId == profileId);
            return AcquiredImageSet.AsNoTracking().Where(predicate).ToList();
        }

        public List<AcquiredImage> GetAcquiredImagesForGrading(ExposurePlan exposurePlan) {
            var images = AcquiredImageSet.AsNoTracking().Where(p =>
                p.ExposureId == exposurePlan.Id &&
                p.TargetId == exposurePlan.TargetId &&
                p.FilterName == exposurePlan.ExposureTemplate.FilterName)
              .OrderByDescending(p => p.acquiredDate);
            return images.ToList();
        }

        public int GetAcquiredImagesCount(DateTime olderThan, int targetId) {
            var predicate = PredicateBuilder.New<AcquiredImage>();
            long olderThanSecs = Common.DateTimeToUnixSeconds(olderThan);
            predicate = predicate.And(a => a.acquiredDate < olderThanSecs);
            if (targetId != 0) {
                predicate = predicate.And(a => a.TargetId == targetId);
            }

            return AcquiredImageSet.AsNoTracking().AsExpandable().Where(predicate).Count();
        }

        public void DeleteOverrideExposureOrders(int targetId) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    var predicate = PredicateBuilder.New<OverrideExposureOrderItem>();
                    predicate = predicate.And(a => a.TargetId == targetId);
                    OverrideExposureOrderSet.RemoveRange(OverrideExposureOrderSet.Where(predicate));
                    SaveChanges();
                    transaction.Commit();
                } catch (Exception e) {
                    TSLogger.Error($"error deleting override exposure order for target Id {targetId}: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                }
            }
        }

        public void DeleteAcquiredImages(DateTime olderThan, int targetId) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    var predicate = PredicateBuilder.New<AcquiredImage>();
                    long olderThanSecs = Common.DateTimeToUnixSeconds(olderThan);
                    predicate = predicate.And(a => a.acquiredDate < olderThanSecs);
                    if (targetId != 0) {
                        predicate = predicate.And(a => a.TargetId == targetId);
                    }

                    AcquiredImageSet.RemoveRange(AcquiredImageSet.Where(predicate));
                    SaveChanges();
                    transaction.Commit();
                } catch (Exception e) {
                    TSLogger.Error($"error deleting acquired images: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                }
            }
        }

        public void DeleteAcquiredImages(int targetId) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    var predicate = PredicateBuilder.New<AcquiredImage>();
                    predicate = predicate.And(a => a.TargetId == targetId);
                    AcquiredImageSet.RemoveRange(AcquiredImageSet.Where(predicate));
                    SaveChanges();
                    transaction.Commit();
                } catch (Exception e) {
                    TSLogger.Error($"error deleting acquired images for target {targetId}: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                }
            }
        }

        public List<FlatHistory> GetFlatsHistory(DateTime lightSessionDate, string profileId) {
            var predicate = PredicateBuilder.New<FlatHistory>();
            long lightSessionDateSecs = Common.DateTimeToUnixSeconds(lightSessionDate);
            predicate = predicate.And(f => f.lightSessionDate == lightSessionDateSecs);
            predicate = predicate.And(f => f.profileId == profileId);
            return FlatHistorySet.AsNoTracking().Where(predicate).ToList();
        }

        public List<FlatHistory> GetFlatsHistory(int targetId, string profileId) {
            return FlatHistorySet.AsNoTracking().Where(fh => fh.targetId == targetId && fh.profileId == profileId).ToList();
        }

        public List<FlatHistory> GetFlatsHistory(List<Target> targets, string profileId) {
            List<FlatHistory> records = new List<FlatHistory>();
            foreach (Target target in targets) {
                records.AddRange(FlatHistorySet.AsNoTracking().Where(fh => fh.targetId == target.Id && fh.profileId == profileId));
            }

            return records;
        }

        public ImageData GetImageData(int acquiredImageId) {
            return ImageDataSet.Where(d => d.AcquiredImageId == acquiredImageId).FirstOrDefault();
        }

        public ImageData GetImageData(int acquiredImageId, string tag) {
            return ImageDataSet.Where(d =>
                d.AcquiredImageId == acquiredImageId &&
                d.tag == tag).FirstOrDefault();
        }

        public ProfilePreference SaveProfilePreference(ProfilePreference profilePreference) {
            TSLogger.Debug($"saving ProfilePreference Id={profilePreference.Id}");
            using (var transaction = Database.BeginTransaction()) {
                try {
                    ProfilePreferenceSet.AddOrUpdate(profilePreference);
                    SaveChanges();
                    transaction.Commit();
                    return GetProfilePreference(profilePreference.ProfileId);
                } catch (Exception e) {
                    TSLogger.Error($"error persisting project: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Project AddNewProject(Project project) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    ProjectSet.Add(project);
                    SaveChanges();
                    transaction.Commit();
                    return GetProject(project.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error adding new project: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Project SaveProject(Project project) {
            TSLogger.Debug($"saving Project Id={project.Id} Name={project.Name}");
            using (var transaction = Database.BeginTransaction()) {
                try {
                    bool fcBreakingChange = project.FilterCadenceBreakingChange;
                    ProjectSet.AddOrUpdate(project);
                    project.RuleWeights.ForEach(item => RuleWeightSet.AddOrUpdate(item));
                    SaveChanges();
                    transaction.Commit();

                    if (fcBreakingChange) {
                        project.Targets.ForEach(t => { ClearExistingFilterCadences(t.Id); });
                        project.FilterCadenceBreakingChange = false;
                    }

                    return GetProject(project.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error persisting project: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Project PasteProject(string profileId, Project source) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    Project project = source.GetPasteCopy(profileId);
                    ProjectSet.Add(project);
                    SaveChanges();
                    transaction.Commit();
                    return GetProject(project.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error pasting project: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Project MoveProject(Project project, string profileId) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    Project copy = project.GetPasteCopy(profileId);
                    ProjectSet.Add(copy);

                    project = GetProject(project.Id);
                    ProjectSet.Remove(project);
                    SaveChanges();
                    transaction.Commit();
                    return copy;
                } catch (Exception e) {
                    TSLogger.Error($"error moving project: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public bool DeleteProject(Project project, bool deleteAcquiredImagesWithTarget) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    project = GetProject(project.Id);

                    if (deleteAcquiredImagesWithTarget) {
                        AcquiredImageSet.RemoveRange(AcquiredImageSet.Where(a => a.ProjectId == project.Id));
                    }

                    ProjectSet.Remove(project);
                    SaveChanges();
                    transaction.Commit();
                    return true;
                } catch (Exception e) {
                    TSLogger.Error($"error deleting project: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return false;
                }
            }
        }

        public Target AddNewTarget(Project project, Target target) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    project = GetProject(project.Id);

                    List<string> currentNames = project.Targets.Select(t => t.Name).ToList();
                    target.Name = Utils.MakeUniqueName(currentNames, target.Name);
                    OverrideExposureOrderSet.AddRange(target.OverrideExposureOrders);
                    project.Targets.Add(target);

                    SaveChanges();
                    transaction.Commit();
                    return GetTarget(target.Project.Id, target.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error adding new target: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Target SaveTarget(Target target, bool clearFilterCadenceItems = false) {
            TSLogger.Debug($"saving Target Id={target.Id} Name={target.Name}");
            ClearExistingOverrideExposureOrders(target.Id);
            if (clearFilterCadenceItems) { ClearExistingFilterCadences(target.Id); }

            using (var transaction = Database.BeginTransaction()) {
                try {
                    TargetSet.AddOrUpdate(target);

                    target.ExposurePlans.ForEach(plan => {
                        plan.ExposureTemplate = null; // clear this (ExposureTemplateId handles the relation)
                        ExposurePlanSet.AddOrUpdate(plan);
                        plan.ExposureTemplate = GetExposureTemplate(plan.ExposureTemplateId); // add back for UI
                    });

                    OverrideExposureOrderSet.AddRange(target.OverrideExposureOrders);
                    target.OverrideExposureOrders.ForEach(oeo => {
                        OverrideExposureOrderSet.AddOrUpdate(oeo);
                    });

                    SaveChanges();
                    transaction.Commit();
                    return GetTarget(target.Project.Id, target.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error persisting target: {e.Message}\n{e.StackTrace}");
                    if (e.InnerException != null) {
                        TSLogger.Error($"inner exception: {e.InnerException.Message}\n{e.InnerException.StackTrace}");
                    }
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Target PasteTarget(Project project, Target source) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    Target target = source.GetPasteCopy(project.ProfileId);
                    project = GetProject(project.Id);
                    project.Targets.Add(target);
                    OverrideExposureOrderSet.AddRange(target.OverrideExposureOrders);
                    SaveChanges();
                    transaction.Commit();
                    return GetTarget(project.Id, target.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error pasting target: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public bool DeleteTarget(Target target) {
            ClearExistingOverrideExposureOrders(target.Id);
            ClearExistingFilterCadences(target.Id);

            using (var transaction = Database.BeginTransaction()) {
                try {
                    target = GetTarget(target.ProjectId, target.Id);
                    TargetSet.Remove(target);

                    FlatHistorySet.Where(fh => fh.targetId == target.Id).ForEach(fh => {
                        FlatHistorySet.Remove(fh);
                    });

                    SaveChanges();
                    transaction.Commit();
                    return true;
                } catch (Exception e) {
                    TSLogger.Error($"error deleting target: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return false;
                }
            }
        }

        public Target DeleteExposurePlan(Target target, ExposurePlan exposurePlan) {
            ClearExistingOverrideExposureOrders(target.Id);
            ClearExistingFilterCadences(target.Id);

            using (var transaction = Database.BeginTransaction()) {
                try {
                    TargetSet.AddOrUpdate(target);
                    exposurePlan = GetExposurePlan(exposurePlan.Id);
                    ExposurePlanSet.Remove(exposurePlan);
                    SaveChanges();
                    transaction.Commit();
                    return GetTargetByProject(target.ProjectId, target.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error deleting exposure plan: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Target ResetExposurePlans(Target target) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    TargetSet.AddOrUpdate(target);
                    foreach (var ep in target.ExposurePlans) {
                        ExposurePlan exposurePlan = GetExposurePlan(ep.Id);
                        exposurePlan.Accepted = 0;
                        exposurePlan.Acquired = 0;
                    }

                    SaveChanges();
                    transaction.Commit();
                    return GetTargetByProject(target.ProjectId, target.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error resetting exposure plan: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public Target DeleteAllExposurePlans(Target target) {
            ClearExistingOverrideExposureOrders(target.Id);
            ClearExistingFilterCadences(target.Id);

            using (var transaction = Database.BeginTransaction()) {
                try {
                    TargetSet.AddOrUpdate(target);

                    List<ExposurePlan> eps = ExposurePlanSet.Where(p => p.TargetId == target.Id).ToList();
                    foreach (ExposurePlan ep in eps) {
                        ExposurePlanSet.Remove(ep);
                    }

                    SaveChanges();
                    transaction.Commit();
                    return GetTargetByProject(target.ProjectId, target.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error deleting all exposure plans: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public ExposureTemplate SaveExposureTemplate(ExposureTemplate exposureTemplate) {
            TSLogger.Debug($"saving Exposure Template Id={exposureTemplate.Id} Name={exposureTemplate.Name}");
            using (var transaction = Database.BeginTransaction()) {
                try {
                    ExposureTemplateSet.AddOrUpdate(exposureTemplate);
                    SaveChanges();
                    transaction.Commit();
                    return GetExposureTemplate(exposureTemplate.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error persisting exposure template: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public ExposureTemplate PasteExposureTemplate(string profileId, ExposureTemplate source) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    ExposureTemplate exposureTemplate = source.GetPasteCopy(profileId);
                    ExposureTemplateSet.Add(exposureTemplate);
                    SaveChanges();
                    transaction.Commit();
                    return GetExposureTemplate(exposureTemplate.Id);
                } catch (Exception e) {
                    TSLogger.Error($"error pasting exposure template: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public bool DeleteExposureTemplate(ExposureTemplate exposureTemplate) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    exposureTemplate = GetExposureTemplate(exposureTemplate.Id);
                    ExposureTemplateSet.Remove(exposureTemplate);
                    SaveChanges();
                    transaction.Commit();
                    return true;
                } catch (Exception e) {
                    TSLogger.Error($"error deleting exposure template: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return false;
                }
            }
        }

        public void AddExposureTemplates(List<ExposureTemplate> exposureTemplates) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    exposureTemplates.ForEach(exposureTemplate => {
                        ExposureTemplateSet.AddOrUpdate(exposureTemplate);
                    });

                    SaveChanges();
                    transaction.Commit();
                } catch (Exception e) {
                    TSLogger.Error($"error adding exposure template: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                }
            }
        }

        public ExposureTemplate MoveExposureTemplate(ExposureTemplate exposureTemplate, string profileId) {
            using (var transaction = Database.BeginTransaction()) {
                try {
                    ExposureTemplate copy = exposureTemplate.GetPasteCopy(profileId);
                    ExposureTemplateSet.Add(copy);

                    exposureTemplate = GetExposureTemplate(exposureTemplate.Id);
                    ExposureTemplateSet.Remove(exposureTemplate);
                    SaveChanges();
                    transaction.Commit();
                    return copy;
                } catch (Exception e) {
                    TSLogger.Error($"error moving exposure template: {e.Message} {e.StackTrace}");
                    RollbackTransaction(transaction);
                    return null;
                }
            }
        }

        public List<ExposureTemplate> GetOrphanedExposureTemplates(List<string> currentProfileIdList) {
            return ExposureTemplateSet.Where(et => !currentProfileIdList.Contains(et.profileId)).ToList();
        }

        public static void CheckValidationErrors(Exception ex) {
            try {
                DbEntityValidationException entityValidationException = ex as DbEntityValidationException;
                if (entityValidationException != null && entityValidationException.EntityValidationErrors.Count() > 0) {
                    TSLogger.Error("Entity validation errors found:");
                    foreach (DbEntityValidationResult eve in entityValidationException.EntityValidationErrors) {
                        TSLogger.Error($"  Entity of type {eve.Entry.Entity.GetType().Name} in state {eve.Entry.State} has validation errors:");
                        foreach (var ve in eve.ValidationErrors) {
                            TSLogger.Error($"    Property {ve.PropertyName}, Error: {ve.ErrorMessage}");
                        }
                    }
                }
            } catch (Exception ex2) {
                TSLogger.Error($"exception logging entity validation errors: {ex2.Message}");
            }
        }

        private static void RollbackTransaction(DbContextTransaction transaction) {
            try {
                TSLogger.Warning("rolling back database changes");
                transaction.Rollback();
            } catch (Exception e) {
                TSLogger.Error($"error executing transaction rollback: {e.Message} {e.StackTrace}");
            }
        }

        private void BackupPreTS5Database() {
            try {
                // Be sure this is not a test
                string dataSource = Database.Connection.ConnectionString;
                if (!dataSource.Contains("NINA\\SchedulerPlugin\\schedulerdb.sqlite")) {
                    return;
                }

                // Bail if this is a first run of TS (no database at all)
                string sourceFile = Path.Combine(Common.PLUGIN_HOME, SchedulerDatabaseInteraction.DATABASE_FILENAME);
                if (!File.Exists(sourceFile)) {
                    return;
                }

                // Bail if the backup already exists
                string preTS5Backup = Path.Combine(Common.PLUGIN_HOME, $"schedulerdb-backup-pre-ts5.{SchedulerDatabaseInteraction.DATABASE_SUFFIX}");
                if (File.Exists(preTS5Backup)) {
                    return;
                }

                TSLogger.Info("detected first run with TS5, backing up TS4 database before migration");
                File.Copy(sourceFile, preTS5Backup);
            } catch (Exception ex) {
                TSLogger.Error($"failed to backup pre-TS5 database: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private class CreateOrMigrateDatabaseInitializer<TContext> : CreateDatabaseIfNotExists<TContext>,
                IDatabaseInitializer<TContext> where TContext : SchedulerDatabaseContext {

            void IDatabaseInitializer<TContext>.InitializeDatabase(TContext context) {
                if (!DatabaseExists(context)) {
                    TSLogger.Debug("creating database schema");
                    using (var transaction = context.Database.BeginTransaction()) {
                        try {
                            context.Database.ExecuteSqlCommand(GetInitialSQL());
                            transaction.Commit();
                        } catch (Exception e) {
                            Logger.Error($"error creating or initializing database: {e.Message} {e.StackTrace}");
                            TSLogger.Error($"error creating or initializing database: {e.Message} {e.StackTrace}");
                            RollbackTransaction(transaction);
                        }
                    }
                }

                // Apply any new migration scripts
                int version = context.Database.SqlQuery<int>("PRAGMA user_version").First();
                Dictionary<int, string> migrationScripts = GetMigrationSQL();
                foreach (KeyValuePair<int, string> scriptEntry in migrationScripts.OrderBy(entry => entry.Key)) {
                    if (scriptEntry.Key <= version) {
                        continue;
                    }

                    TSLogger.Info($"applying database migration script number {scriptEntry.Key}");
                    using (var transaction = context.Database.BeginTransaction()) {
                        try {
                            context.Database.ExecuteSqlCommand(scriptEntry.Value);
                            transaction.Commit();
                        } catch (Exception e) {
                            Logger.Error($"Scheduler: error applying database migration script number {scriptEntry.Key}: {e.Message} {e.StackTrace}");
                            TSLogger.Error($"error applying database migration script number {scriptEntry.Key}: {e.Message} {e.StackTrace}");
                            RollbackTransaction(transaction);
                        }
                    }
                }

                int newVersion = context.Database.SqlQuery<int>("PRAGMA user_version").First();

                // Other repairs/updates
                RepairAndUpdate(version, newVersion, context);

                if (newVersion != version) {
                    TSLogger.Info($"database updated: {version} -> {newVersion}");
                }
            }

            private bool DatabaseExists(TContext context) {
                int numTables = context.Database.SqlQuery<int>("SELECT COUNT(*) FROM sqlite_master AS TABLES WHERE TYPE = 'table'").First();
                return numTables > 0;
            }

            private string GetInitialSQL() {
                try {
                    ResourceManager rm = new ResourceManager("NINA.Plugin.TargetScheduler.Database.Initial.SQL", Assembly.GetExecutingAssembly());
                    return (string)rm.GetObject("initial_schema");
                } catch (Exception ex) {
                    Logger.Error($"failed to load Scheduler database initial SQL: {ex.Message}");
                    TSLogger.Error($"failed to load database initial SQL: {ex.Message}");
                    throw;
                }
            }

            private Dictionary<int, string> GetMigrationSQL() {
                try {
                    Dictionary<int, string> migrateScripts = new Dictionary<int, string>();
                    ResourceManager rm = new ResourceManager("NINA.Plugin.TargetScheduler.Database.Migrate.SQL", Assembly.GetExecutingAssembly());
                    ResourceSet rs = rm.GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, true, false);

                    foreach (DictionaryEntry entry in rs) {
                        if (Int32.TryParse((string)entry.Key, out int migrateNum)) {
                            migrateScripts.Add(migrateNum, (string)entry.Value);
                        }
                    }

                    return migrateScripts;
                } catch (Exception ex) {
                    Logger.Error($"failed to load Scheduler database migration scripts: {ex.Message}");
                    TSLogger.Error($"failed to load database migration scripts: {ex.Message}");
                    throw;
                }
            }

            private void RepairAndUpdate(int oldVersion, int newVersion, TContext context) {
                // If a new scoring rule was added, we need to add a rule weight record to projects that don't have it
                List<Project> projects = context.GetAllProjects();
                if (projects != null && projects.Count > 0) {
                    bool updated = false;
                    Dictionary<string, IScoringRule> rules = ScoringRule.GetAllScoringRules();
                    foreach (Project project in projects) {
                        foreach (KeyValuePair<string, IScoringRule> item in rules) {
                            RuleWeight rw = project.RuleWeights.Where(r => r.Name == item.Key).FirstOrDefault();
                            if (rw == null) {
                                TSLogger.Debug($"project '{project.Name}' is missing rule weight record: '{item.Value.Name}': adding");
                                rw = new RuleWeight(item.Value.Name, item.Value.DefaultWeight);
                                rw.Project = project;
                                context.RuleWeightSet.Add(rw);
                                updated = true;
                            }
                        }
                    }

                    if (updated) {
                        context.SaveChanges();
                    }
                }

                // Convert NINA 2 rotation to NINA 3 position angle
                if (oldVersion == 5 && newVersion == 6) {
                    projects = context.GetAllProjects();
                    if (projects != null && projects.Count > 0) {
                        bool updated = false;
                        foreach (Project project in projects) {
                            foreach (Target target in project.Targets) {
                                double rotation = target.Rotation;
                                if (rotation != 0) {
                                    target.Rotation = AstrometryUtils.ConvertRotation(rotation);
                                    updated = true;
                                }
                            }
                        }

                        if (updated) {
                            context.SaveChanges();
                            TSLogger.Debug("updated target rotation values for NINA 3");
                        }
                    }
                }

                // Clear override exposure order (meaning changed with bug fix)
                /* We don't want/need to do this for TS 4->5
                if (oldVersion == 8 && newVersion == 9) {
                    projects = context.GetAllProjects();
                    if (projects != null && projects.Count > 0) {
                        bool updated = false;
                        foreach (Project project in projects) {
                            foreach (Target target in project.Targets) {
                                if (!string.IsNullOrEmpty(target.OverrideExposureOrder)) {
                                    target.OverrideExposureOrder = null;
                                    updated = true;
                                }
                            }
                        }

                        if (updated) {
                            context.SaveChanges();
                            TSLogger.Info("cleared override exposure ordering for bug fix");
                        }
                    }
                }*/

                // TS 5
                if (oldVersion < 17 && newVersion == 17) {
                    TSLogger.Info("TS 4 -> 5 database migration");
                    Notification.ShowInformation("Migrating Target Scheduler database to new version ...");

                    // Remap grading status to new enum value
                    List<AcquiredImage> acquiredImages = context.AcquiredImageSet.Where(ai => ai != null).ToList();
                    acquiredImages.ToList().ForEach(ai => {
                        ai.gradingStatus = (int)(ai.gradingStatus == 1 ? GradingStatus.Accepted : GradingStatus.Rejected);
                        context.AcquiredImageSet.AddOrUpdate(ai);
                    });

                    // Refactor override exposure order
                    List<Target> targetList = context.TargetSet.Where(t => t != null).ToList();
                    targetList.ForEach(t => {
                        if (!string.IsNullOrEmpty(t.unusedOEO)) {
                            int targetId = t.Id;
                            string oeo = t.unusedOEO;
                            int order = 1;

                            List<ExposurePlan> eps = context.ExposurePlanSet.Where(ep => ep.TargetId == targetId).ToList();

                            string[] element = oeo.Split('|');
                            foreach (var elem in element) {
                                int action;
                                int refIdx = -1;
                                if (elem == "Dither") {
                                    action = (int)OverrideExposureOrderAction.Dither;
                                } else {
                                    action = (int)OverrideExposureOrderAction.Exposure;
                                    refIdx = GetOldOEORefIdx(Int32.Parse(elem), eps);
                                }

                                var oeoItem = new OverrideExposureOrderItem(targetId, order++, action, refIdx);
                                context.OverrideExposureOrderSet.Add(oeoItem);
                            }

                            t.unusedOEO = null;
                            context.TargetSet.AddOrUpdate(t);
                        }
                    });

                    context.SaveChanges();
                    Notification.ShowSuccess("Target Scheduler database migration complete");
                }
            }

            private int GetOldOEORefIdx(int epId, List<ExposurePlan> eps) {
                for (int i = 0; i < eps.Count; i++) {
                    if (epId == eps[i].Id) return i;
                }

                throw new Exception($"failed to find exposure plan Id for OEO: {epId}");
            }
        }
    }
}