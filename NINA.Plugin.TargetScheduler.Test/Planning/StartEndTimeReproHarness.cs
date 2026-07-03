using FluentAssertions;
using Moq;
using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyWeatherData;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.SQLite;
using System.Data.SQLite.EF6;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    /// <summary>
    /// Repro harness for the intermittent preview crash:
    ///
    ///   exception generating plan: startTime must be before endTime
    ///     at TimeInterval..ctor
    ///     at SchedulerPlan..ctor(planTime, projects, nextTarget, logPlan)   // the WAIT ctor
    ///     at Planner.GetPlan
    ///
    /// Hypothesis: GetNextPossibleTarget -> CheckFuture can return a target whose StartTime is
    /// BEFORE the current planning time (a currently-visible target that is moon/max-alt rejected
    /// NOW but was acceptable earlier in its visibility window). The WAIT SchedulerPlan ctor then
    /// builds TimeInterval(atTime, nextTarget.StartTime) and throws because atTime > StartTime.
    ///
    /// This loads a user's real schedulerdb.sqlite from the standard plugin home
    /// (%LOCALAPPDATA%\NINA\SchedulerPlugin\schedulerdb.sqlite) and replays the preview planner's
    /// chaining loop across a range of nights at the user's coordinates. Unlike PreviewPlanner
    /// (which swallows the exception), this records the exact time and offending target.
    ///
    /// Marked [Explicit] so it never runs in CI - it depends on a specific external database file.
    /// </summary>
    [TestFixture]
    [Explicit("Loads a user-specific schedulerdb.sqlite from the plugin home; run manually to repro")]
    public class StartEndTimeReproHarness {

        // The reporting user's location: lat 31.546944 N, lon 99.381667 W.
        private static readonly ObserverInfo Location = new ObserverInfo {
            Latitude = 31.546944,
            Longitude = -99.381667,
            Elevation = 500,
        };

        private const string TargetProjectName = "LaCerta9";

        // LaCerta9 (RA ~22h) is a prime EVENING target in autumn, when the moon-down/moon-up crossing
        // within its long visible window is most likely - the classic case for the crash. (The reported
        // 2026-07-02 timestamp is only when the user ran the preview, not necessarily the plan date.)
        private static readonly DateTime SweepStart = new DateTime(2026, 9, 1);
        private static readonly int SweepDays = 60;

        [OneTimeSetUp]
        public void OneTimeSetUp() {
            DllLoader.LoadDll(Path.Combine("SQLite", "SQLite.Interop.dll"));

            // Under 'dotnet test' on .NET (Core), the app.config <system.data><DbProviderFactories>
            // section is ignored (it's a .NET Framework mechanism), so EF6 cannot reverse-map the
            // SQLiteConnection's factory to its invariant name. Register it programmatically. (Do NOT
            // set a global DbConfiguration - that would also capture NINA's own NINADbContext, which
            // lives in a different assembly and would then throw.)
            try { DbProviderFactories.RegisterFactory("System.Data.SQLite", SQLiteFactory.Instance); } catch { }
            try { DbProviderFactories.RegisterFactory("System.Data.SQLite.EF6", SQLiteProviderFactory.Instance); } catch { }

            // Both the plugin's SchedulerDatabaseContext and NINA's own NINADbContext (used by
            // astrometry for UT1-UTC) need the EF6 SQLite provider services registered. NINA already
            // ships a DbConfiguration for exactly this; set it globally at start so it applies to
            // every context in the app domain. (Setting our own config would conflict with NINA's,
            // which EF auto-discovers in the NINADbContext assembly.)
            try {
                DbConfiguration.SetConfiguration(new NINA.Core.Database.SQLiteConfiguration());
            } catch (InvalidOperationException) {
                // Already set/locked earlier in this app domain - fine.
            }
        }

        // NINA meridian-flip settings. The crash requires PauseTimeBeforeMeridian > 0 (that is the only
        // rejection path in this DB - max altitude and meridian window are both off - that leaves a
        // target's StartTime un-refreshed at a stale, in-the-past value). We don't have the user's exact
        // values, so try a few plausible pairs (pause, minutesAfter) until one reproduces.
        private static readonly (double Pause, double After)[] MFConfigs = {
            (1, 15),
            (5, 15),
            (2, 30),
            (5, 60),
        };

        [Test]
        public void ReproduceStartEndTimeCrash() {
            SchedulerDatabaseInteraction db = new SchedulerDatabaseInteraction(); // default ctor -> plugin home DB

            string profileId = FindProfileId(db, TargetProjectName);
            TestContext.Progress.WriteLine($"Using profileId '{profileId}' (owns project '{TargetProjectName}')");
            profileId.Should().NotBeNull($"expected to find a project named '{TargetProjectName}' in the database");

            // Diagnostics dump once.
            DumpProjectDiagnostics(LoadFreshProjects(db, BuildProfileService(profileId, 0, 0).ActiveProfile));

            List<CrashReport> crashes = new List<CrashReport>();

            foreach ((double pause, double after) in MFConfigs) {
                IProfileService profileService = BuildProfileService(profileId, pause, after);
                IProfile profile = profileService.ActiveProfile;
                IWeatherDataMediator weather = new DisconnectedWeather();
                ProfilePreference prefs = new SchedulerPlanLoader(profile).GetProfilePreferences(db.GetContext());

                int nightsCrashed = 0;
                for (int day = 0; day < SweepDays; day++) {
                    DateTime date = SweepStart.AddDays(day);

                    // Fresh DB state per night (chaining mutates acquired counts + StartTime in-memory).
                    List<IProject> projects = LoadFreshProjects(db, profile);
                    if (projects == null) {
                        continue;
                    }

                    CrashReport crash = ChainNight(date.AddHours(16), profile, prefs, weather, projects);
                    if (crash != null) {
                        crash.MeridianPause = pause;
                        crash.MeridianAfter = after;
                        crashes.Add(crash);
                        nightsCrashed++;
                        TestContext.Progress.WriteLine($"*** CRASH: {crash}");
                    }
                }

                TestContext.Progress.WriteLine($"MF(pause={pause}, after={after}): {nightsCrashed}/{SweepDays} nights crashed");
                if (nightsCrashed > 0) {
                    break; // reproduced - no need to try wider settings
                }
            }

            TestContext.Progress.WriteLine($"\nTotal crashes reproduced: {crashes.Count}");
            crashes.Should().NotBeEmpty("the harness should reproduce the 'startTime must be before endTime' crash");
        }

        /// <summary>
        /// Faithful replay of PreviewPlanner.GetPlanPreview for one night: chain planner runs using the
        /// previous plan's end/wait time as the next start, and (critically) do NOT reset target StartTime
        /// between runs - exactly what PreviewPlanner.PrepForNextRun does. Surfaces the start/end crash
        /// instead of swallowing it.
        /// </summary>
        private CrashReport ChainNight(DateTime atTime, IProfile profile, ProfilePreference prefs,
            IWeatherDataMediator weather, List<IProject> projects) {

            DitherManagerCache.Clear();
            ITarget previousTarget = null;
            DateTime currentTime = atTime;
            DateTime hardStop = atTime.AddHours(30);

            for (int i = 0; i < 5000 && currentTime < hardStop; i++) {
                SchedulerPlan plan;
                try {
                    plan = new Planner(currentTime, profile, prefs, weather, false, true, projects).GetPlan(previousTarget);
                } catch (Exception ex) {
                    if (!IsStartEndTimeCrash(ex)) {
                        throw;
                    }

                    // The failing GetPlan already left the culprit's StartTime set (by CheckFuture) to
                    // the stale, in-the-past value. Scan the current state for it rather than re-running
                    // the filters (which would refresh StartTime and hide the culprit).
                    // GetNextPossibleTarget returns the soonest IMAGABLE target - i.e. one CheckFuture
                    // un-rejected but left with a stale, in-the-past StartTime.
                    ITarget culprit = projects
                        .SelectMany(p => p.Targets)
                        .Where(t => !t.Rejected && t.StartTime != DateTime.MinValue && t.StartTime < currentTime)
                        .OrderBy(t => t.StartTime)
                        .FirstOrDefault();

                    return new CrashReport {
                        AtTime = currentTime,
                        TargetName = culprit != null ? $"{culprit.Project?.Name}/{culprit.Name}" : "(unknown)",
                        TargetStartTime = culprit?.StartTime,
                        RejectedReason = culprit?.RejectedReason,
                        WouldCrashPreview = true,
                    };
                }

                if (plan == null) {
                    return null;
                }

                currentTime = plan.IsWait ? (DateTime)plan.WaitForNextTargetTime : plan.EndTime;
                PrepForNextRun(projects, plan, ref previousTarget);
            }

            return null;
        }

        // Mirror of PreviewPlanner.PrepForNextRun (private in production). NOTE: like the original, this
        // does NOT reset target.StartTime - the stale-StartTime carryover is essential to the bug.
        private void PrepForNextRun(List<IProject> projects, SchedulerPlan plan, ref ITarget previousTarget) {
            if (!plan.IsWait) {
                if (previousTarget != null && plan.PlanTarget != previousTarget) {
                    plan.PlanTarget.ExposureSelector.TargetReset();
                }

                plan.PlanTarget.ExposureSelector.ExposureTaken(plan.PlanTarget.SelectedExposure);
                plan.PlanTarget.SelectedExposure.Acquired++;
                if (plan.PlanTarget.Project.EnableGrader) {
                    plan.PlanTarget.SelectedExposure.Accepted++;
                }

                previousTarget = plan.PlanTarget;
            } else {
                previousTarget = null;
            }

            ClearRejectionsOnly(projects);
        }

        // Clear rejection/scoring state between runs, but preserve StartTime (as PreviewPlanner does).
        private void ClearRejectionsOnly(List<IProject> projects) {
            foreach (IProject project in projects) {
                project.Rejected = false;
                project.RejectedReason = null;
                foreach (ITarget target in project.Targets) {
                    target.ScoringResults = null;
                    target.Rejected = false;
                    target.RejectedReason = null;

                    foreach (IExposure exposure in target.ExposurePlans) {
                        exposure.Rejected = false;
                        exposure.RejectedReason = null;
                        exposure.MoonAvoidanceScore = MoonAvoidanceExpert.SCORE_OFF;
                    }
                }
            }
        }

        private List<IProject> LoadFreshProjects(SchedulerDatabaseInteraction db, IProfile profile) {
            SchedulerPlanLoader loader = new SchedulerPlanLoader(profile);
            List<IProject> projects = loader.LoadActiveProjects(db.GetContext());
            projects?.ForEach(p => p.Targets.ForEach(t => t.IsPreview = true));
            return projects;
        }

        private void DumpProjectDiagnostics(List<IProject> projects) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\nLoaded {projects.Count} active project(s):");
            foreach (IProject p in projects) {
                sb.AppendLine($"  Project '{p.Name}': MinTime={p.MinimumTime}min, MinAlt={p.MinimumAltitude:0.#}, " +
                              $"MaxAlt={p.MaximumAltitude:0.#}, MeridianWindow={p.MeridianWindow}, targets={p.Targets.Count}");
                foreach (ITarget t in p.Targets) {
                    int incomplete = t.ExposurePlans.Count(e => e.IsIncomplete());
                    sb.AppendLine($"      Target '{t.Name}' @ {t.Coordinates?.RAString}/{t.Coordinates?.DecString}: " +
                                  $"{t.ExposurePlans.Count} exposure plan(s), {incomplete} incomplete");
                    foreach (IExposure e in t.ExposurePlans.Where(e => e.IsIncomplete())) {
                        sb.AppendLine($"          exp '{e.FilterName}': twilight={e.TwilightLevel}, offset={e.MinutesOffset}, " +
                                      $"moonAvoid={e.MoonAvoidanceEnabled} (sep={e.MoonAvoidanceSeparation:0.#}, width={e.MoonAvoidanceWidth}), " +
                                      $"moonDown={e.MoonDownEnabled}");
                    }
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }

        private static bool IsStartEndTimeCrash(Exception ex) {
            for (Exception e = ex; e != null; e = e.InnerException) {
                if (e is ArgumentException && e.Message.Contains("startTime must be before endTime")) {
                    return true;
                }
            }

            return false;
        }

        private static string FindProfileId(SchedulerDatabaseInteraction db, string projectName) {
            using (ISchedulerDatabaseContext context = db.GetContext()) {
                List<Project> all = context.GetAllProjects();
                Project match = all.FirstOrDefault(p =>
                    string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
                return match?.ProfileId;
            }
        }

        private static IProfileService BuildProfileService(string profileId, double meridianPause, double minutesAfter) {
            Mock<IProfileService> mock = new Mock<IProfileService>();
            mock.SetupProperty(m => m.ActiveProfile.Id, Guid.Parse(profileId));
            mock.SetupProperty(m => m.ActiveProfile.AstrometrySettings.Latitude, Location.Latitude);
            mock.SetupProperty(m => m.ActiveProfile.AstrometrySettings.Longitude, Location.Longitude);
            mock.SetupProperty(m => m.ActiveProfile.AstrometrySettings.Elevation, Location.Elevation);
            mock.SetupProperty(m => m.ActiveProfile.MeridianFlipSettings.PauseTimeBeforeMeridian, meridianPause);
            mock.SetupProperty(m => m.ActiveProfile.MeridianFlipSettings.MinutesAfterMeridian, minutesAfter);
            return mock.Object;
        }

        private class CrashReport {
            public DateTime AtTime;
            public string TargetName;
            public DateTime? TargetStartTime;
            public string RejectedReason;
            public bool WouldCrashPreview;
            public double MeridianPause;
            public double MeridianAfter;

            public override string ToString() {
                string delta = TargetStartTime.HasValue
                    ? $" (StartTime is {(AtTime - TargetStartTime.Value).TotalMinutes:0.#} min BEFORE atTime)"
                    : "";
                return $"MF(pause={MeridianPause},after={MeridianAfter}) atTime={AtTime:yyyy-MM-dd HH:mm:ss}, " +
                       $"nextTarget={TargetName}, nextTarget.StartTime={TargetStartTime:yyyy-MM-dd HH:mm:ss}{delta}, " +
                       $"reason={RejectedReason}";
            }
        }

        private class DisconnectedWeather : IWeatherDataMediator {
            private readonly WeatherDataInfo info = new WeatherDataInfo { Connected = false };

            public event Func<object, EventArgs, Task> Connected;

            public event Func<object, EventArgs, Task> Disconnected;

            public WeatherDataInfo GetInfo() => info;

            public string Action(string actionName, string actionParameters) => throw new NotImplementedException();

            public void Broadcast(WeatherDataInfo deviceInfo) => throw new NotImplementedException();

            public Task<bool> Connect() => throw new NotImplementedException();

            public Task Disconnect() => throw new NotImplementedException();

            public IDevice GetDevice() => throw new NotImplementedException();

            public void RegisterConsumer(IWeatherDataConsumer consumer) => throw new NotImplementedException();

            public void RegisterHandler(IWeatherDataVM handler) => throw new NotImplementedException();

            public void RemoveConsumer(IWeatherDataConsumer consumer) => throw new NotImplementedException();

            public Task<IList<string>> Rescan() => throw new NotImplementedException();

            public void SendCommandBlind(string command, bool raw = true) => throw new NotImplementedException();

            public bool SendCommandBool(string command, bool raw = true) => throw new NotImplementedException();

            public string SendCommandString(string command, bool raw = true) => throw new NotImplementedException();
        }
    }
}
