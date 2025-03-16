using ASCOM.Com;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.Util;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using static EDSDKLib.EDSDK;

namespace NINA.Plugin.TargetScheduler.API {
    public class Controller : WebApiController {
        private ISchedulerDatabaseContext database;
        private ISchedulerDatabaseInteraction databaseInteraction;
        private IProfileService profileService;

        private ExposureCompletionHelper GetExposureCompletionHelper(Project project) {
            ProfilePreference profilePreference = database.GetProfilePreference(project.ProfileId, true);

            if (profilePreference == null) {
                throw new HttpException(500);
            }

            return new ExposureCompletionHelper(project.EnableGrader, profilePreference.DelayGrading,
                profilePreference.ExposureThrottle);
        }

        public Controller(ISchedulerDatabaseInteraction d, IProfileService p) {
            databaseInteraction = d;
            database = d.GetContext();
            profileService = p;
        }

        [Route(HttpVerbs.Get, "/version")]
        public string GetVersion() {
            return Assembly.GetAssembly(typeof(TargetScheduler)).GetName().Version.ToString();
        }

        [Route(HttpVerbs.Get, "/profiles")]
        public IEnumerable<ProfileResponse> GetProfiles() {
            return profileService.Profiles.Select(m => new ProfileResponse(m));
        }

        [Route(HttpVerbs.Get, "/profiles/{id}/projects")]
        public IEnumerable<ProjectResponse> GetProjects(string id) {
            return database.GetAllProjectsReadOnly(id).Select(p => new ProjectResponse(p));
        }

        [Route(HttpVerbs.Get, "/projects/{id}/targets")]
        public IEnumerable<TargetResponse> GetTargets(int id) {
            Project p = database.GetProjectReadOnly(id);

            if (p == null) {
                throw new HttpException(System.Net.HttpStatusCode.NotFound);
            }

            ExposureCompletionHelper ech = GetExposureCompletionHelper(p);
            return p.Targets.Select(t => new TargetResponse(t, ech));
        }

        [Route(HttpVerbs.Get, "/targets/{id}/statistics")]
        public IEnumerable<TargetStatisticsResponse> GetTargetStatistics(int id) {
            var t = database.GetTargetReadOnly(id);

            if (t == null) {
                throw new HttpException(System.Net.HttpStatusCode.NotFound);
            }

            var p = t.Project;

            var hfr = database.GetProfilePreference(p.ProfileId).AutoAcceptLevelHFR;
            var fwhm = database.GetProfilePreference(p.ProfileId).AutoAcceptLevelFWHM;
            var ecc = database.GetProfilePreference(p.ProfileId).AutoAcceptLevelEccentricity;

            var plans = t.ExposurePlans.Select(p => {
                // Already read-only by default
                var images = database.GetAcquiredImages(id, p.ExposureTemplate.FilterName);

                if (images == null || !images.Any()) {
                    return TargetStatisticsResponse.Empty(p);
                }

                return new TargetStatisticsResponse(p, images, hfr, fwhm, ecc);
            });

            if (plans == null || !plans.Any()) {
                return [];
            }

            return plans;
        }

        private List<IProject> MarkForPreview(List<IProject> projects) {
            if (Common.IsEmpty(projects)) return projects;

            projects.ForEach(p => {
                p.Targets.ForEach(t => { t.IsPreview = true; });
            });

            return projects;
        }

        [Route(HttpVerbs.Get, "/profiles/{id}/preview")]
        public IEnumerable<PreviewResponse> GetPreview(string id, [QueryField] DateTime? startTime = null) {
            IProfile profile = null;

            foreach (ProfileMeta profileMeta in profileService.Profiles) {
                if (profileMeta.Id.ToString() == id) {
                    profile = ProfileLoader.Load(profileService, profileMeta);
                }
            }

            if (profile == null) {
                throw new HttpException(System.Net.HttpStatusCode.NotFound);
            }

            var planner = new PreviewPlanner();
            SchedulerPlanLoader loader = new SchedulerPlanLoader(profile);
            List<IProject> projects = MarkForPreview(loader.LoadActiveProjects(databaseInteraction.GetContext()));
            ProfilePreference profilePreference = loader.GetProfilePreferences(databaseInteraction.GetContext());
            List<SchedulerPlan> preview =
                planner.GetPlanPreview(startTime ?? DateTime.Today.AddHours(13.0), profileService, profilePreference, projects);

            var response = new List<PreviewResponse>();
            var planItem = new PreviewResponse();
            var newItem = true;

            foreach (SchedulerPlan plan in preview) {
                if (plan.IsWait) {
                    var waitItem = new PreviewResponse {
                        WaitPeriod = true,
                        StartTime = plan.StartTime,
                        EndTime = (DateTime)plan.WaitForNextTargetTime,
                        TargetId = -1,
                        ExposurePlan = []
                    };

                    response.Add(waitItem);
                    planItem = new PreviewResponse();
                    newItem = true;

                    continue;
                }

                if (planItem.TargetId != plan.PlanTarget.DatabaseId) {
                    planItem = new PreviewResponse();
                    newItem = true;
                }

                if (newItem) {
                    planItem.StartTime = plan.StartTime;
                    planItem.EndTime = plan.EndTime;
                    planItem.TargetId = plan.PlanTarget.DatabaseId;
                    planItem.Name = plan.PlanTarget.Name;

                    response.Add(planItem);
                    newItem = false;
                } else {
                    planItem.EndTime = plan.EndTime;
                }

                var exposurePlanItem = planItem.ExposurePlan.LastOrDefault(new PreviewExposurePlanResponse());

                foreach (IInstruction instruction in plan.PlanInstructions) {
                    if (instruction is PlanMessage or PlanBeforeNewTargetContainer or PlanPostExposure or PlanSlew or PlanSetReadoutMode or PlanDither) {
                        continue;
                    }
                    
                    if (instruction is PlanSwitchFilter filter) {
                        string filterName = filter.exposure.FilterName;
                        if (filterName != exposurePlanItem.FilterName) {
                            if (exposurePlanItem.Count != 0) {
                                exposurePlanItem = new PreviewExposurePlanResponse();
                            }

                            exposurePlanItem.FilterName = filterName;
                            planItem.ExposurePlan.Add(exposurePlanItem);
                        }
                    } else if (instruction is PlanTakeExposure) {
                        if (exposurePlanItem.Count == 0) {
                            exposurePlanItem.Exposure = instruction.exposure.ExposureLength;
                        }

                        exposurePlanItem.Count++;
                    }
                }
            }

            return response;
        }
    }

    public class ProfileResponse {
        public ProfileResponse(ProfileMeta m) {
            ProfileId = m.Id.ToString();
            Name = m.Name;
            Active = m.IsActive;
        }

        public string ProfileId { get; private set; }
        public string Name { get; private set; }
        public bool Active { get; private set; }
    }

    public class ProjectResponse {
        public ProjectResponse(Project p) {
            ProfileId = p.ProfileId;
            ProjectId = p.Id;
            Name = p.Name;
            State = Enum.GetName(typeof(ProjectState), p.State);
            Priority = (int) p.Priority;
            Description = p.Description;
            CreateDate = p.CreateDate;
            ActiveDate = p.ActiveDate;
            InactiveDate = p.InactiveDate;
            MinimumTime = p.MinimumTime;
            UseCustomHorizon = p.UseCustomHorizon;
            HorizonOffset = p.HorizonOffset;
            MeridianWindow = p.MeridianWindow;
            FilterSwitchFrequency = p.FilterSwitchFrequency;
            DitherEvery = p.DitherEvery;
            EnableGrader = p.EnableGrader;
            Mosaic = p.IsMosaic;
            FlatsHandling = p.FlatsHandling;
            MinimumAltitude = p.MinimumAltitude;
            MaximumAltitude = p.MaximumAltitude;
            SmartExposureOrder = p.SmartExposureOrder;
        }

        public string ProfileId { get; private set; }
        public int ProjectId { get; private set; }
        public string Name { get; private set; }
        public string State { get; private set; }
        public int Priority { get; private set; }
        public string Description { get; private set; }
        public DateTime? CreateDate { get; private set; }
        public DateTime? ActiveDate { get; private set; }
        public DateTime? InactiveDate { get; private set; }
        public int MinimumTime { get; private set; }
        public bool UseCustomHorizon { get; private set; }
        public double HorizonOffset { get; private set; }
        public int MeridianWindow { get; private set; }
        public int FilterSwitchFrequency { get; private set; }
        public int DitherEvery { get; private set; }
        public bool EnableGrader { get; private set; }
        public bool Mosaic { get; private set; }
        public int FlatsHandling { get; private set; }
        public double MinimumAltitude { get; private set; }
        public double MaximumAltitude { get; private set; }
        public bool SmartExposureOrder { get; private set; }
    }

    public class TargetResponse {
        public TargetResponse(Target t, ExposureCompletionHelper ech) {
            ProjectId = t.ProjectId;
            TargetId = t.Id;
            Name = t.Name;
            Active = t.active;
            Ra = t.RA;
            Dec = t.Dec;
            Rotation = t.Rotation;
            Epoch = Enum.GetName(typeof(Epoch), t.Epoch);
            Roi = t.ROI;
            ExposurePlan = t.ExposurePlans.Select(p => new ExposurePlanResponse(p, ech.IsProvisionalPercentComplete(p)));
        }

        public int ProjectId { get; private set; }
        public int TargetId { get; private set; }
        public string Name { get; private set; }
        public bool Active { get; private set; }
        public double Ra { get; private set; }
        public double Dec { get; private set; }
        public double Rotation { get; private set; }
        public string Epoch { get; private set; }
        public double Roi { get; private set; }
        public IEnumerable<ExposurePlanResponse> ExposurePlan { get; private set; }
    }

    public class ExposurePlanResponse {
        public ExposurePlanResponse(ExposurePlan p, bool provisional) {
            TemplateName = p.ExposureTemplate.Name;
            Exposure = p.Exposure != -1 ? p.Exposure : p.ExposureTemplate.DefaultExposure;
            FilterName = p.ExposureTemplate.FilterName;
            Desired = p.Desired;
            Acquired = p.Acquired;
            Accepted = p.Accepted;
            Ungraded = provisional ? Acquired - Accepted : 0;
        }

        public string TemplateName { get; private set; }
        public double Exposure { get; private set; }
        public string FilterName { get; private set; }
        public int Desired { get; private set; }
        public int Acquired { get; private set; }
        public int Accepted { get; private set; }
        public int Ungraded { get; private set; }
    }

    public class TargetStatisticsResponse {
        private static double StdDev(IEnumerable<double> values) {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }

        public static TargetStatisticsResponse Empty(ExposurePlan p) {
            return new TargetStatisticsResponse(p);
        }

        private TargetStatisticsResponse(ExposurePlan p) {
            Exposure = p.Exposure != -1 ? p.Exposure : p.ExposureTemplate.DefaultExposure;
            FilterName = p.ExposureTemplate.FilterName;

            HFRMean = 0.0;
            HFRStdDev = 0.0;
            HFRBelowAutoAcceptLevel = -1;
            FWHMMean = 0.0;
            FWHMStdDev = 0.0;
            FWHMBelowAutoAcceptLevel = -1;
            EccentricityMean = 0;
            EccentricityStdDev = 0;
            EccentricityBelowAutoAcceptLevel = -1;
        }

        public TargetStatisticsResponse(ExposurePlan p, List<AcquiredImage> i, double hfrLimit, double fwhmLimit, double eccLimit) {
            Exposure = p.Exposure != -1 ? p.Exposure : p.ExposureTemplate.DefaultExposure;
            FilterName = p.ExposureTemplate.FilterName;

            var imageMetadatas = i.Select(i => i.Metadata);
            var hfrs = imageMetadatas.Where(m => !Double.IsNaN(m.HFR)).Select(m => m.HFR);
            var fwhms = imageMetadatas.Where(m => !Double.IsNaN(m.FWHM)).Select(i => i.FWHM);
            var eccs = imageMetadatas.Where(m => !Double.IsNaN(m.Eccentricity)).Select(i => i.Eccentricity);

            HFRMean = hfrs.Average();
            HFRStdDev = StdDev(hfrs);
            HFRBelowAutoAcceptLevel = hfrLimit > 0.0 ? hfrs.Count(t => t <= hfrLimit) : -1;
            FWHMMean = fwhms.Average();
            FWHMStdDev = StdDev(fwhms);
            FWHMBelowAutoAcceptLevel = fwhmLimit > 0.0 ? fwhms.Count(t => t <= fwhmLimit) : -1;
            EccentricityMean = eccs.Average();
            EccentricityStdDev = StdDev(eccs);
            EccentricityBelowAutoAcceptLevel = eccLimit > 0.0 ? eccs.Count(t => t <= eccLimit) : -1;
        }

        public double Exposure { get; private set; }
        public string FilterName { get; private set; }
        public double HFRMean { get; private set; }
        public double HFRStdDev { get; private set; }
        public int HFRBelowAutoAcceptLevel { get; private set; }
        public double FWHMMean { get; private set; }
        public double FWHMStdDev { get; private set; }
        public int FWHMBelowAutoAcceptLevel { get; private set; }
        public double EccentricityMean { get; private set; }
        public double EccentricityStdDev { get; private set; }
        public int EccentricityBelowAutoAcceptLevel { get; private set; }
    }


    public class PreviewExposurePlanResponse {
        public PreviewExposurePlanResponse() { }

        public string FilterName { get; set; }
        public double Exposure { get; set; }
        public int Count { get; set; }
    }

    public class PreviewResponse {
        public PreviewResponse() {
            ExposurePlan = new List<PreviewExposurePlanResponse>();
        }

        public int TargetId { get; set; }
        public string Name { get; set; }
        public bool WaitPeriod { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<PreviewExposurePlanResponse> ExposurePlan { get; set; }
    }
}