using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.Serialization;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Controls.Util {

    public class ProfileLoader {
        private static readonly ProfileLoader Instance = new ProfileLoader();
        private static bool initialized = false;
        private ProfileCache cache;

        private static readonly ConcurrentDictionary<string, IProfile> lastLoaded = new ConcurrentDictionary<string, IProfile>();

        public static IProfile GetLastLoaded(string profileId) {
            return profileId != null && lastLoaded.TryGetValue(profileId, out IProfile profile) ? profile : null;
        }

        public static IProfile GetProfile(IProfileService profileService, string profileId) {
            ProfileMeta profileMeta = profileService.Profiles.Where(p => p.Id.ToString() == profileId).FirstOrDefault();
            if (profileMeta != null) {
                return Load(profileService, profileMeta);
            }

            TSLogger.Error($"failed to load profile, id = {profileId}");
            return null;
        }

        public static IProfile Load(IProfileService profileService, ProfileMeta profileMeta) {
            if (!initialized) {
                profileService.ProfileChanged += Instance.ProfileService_ProfileChanged;
                profileService.Profiles.CollectionChanged += Instance.ProfileService_ProfileChanged;
                initialized = true;
            }

            if (profileService.ActiveProfile.Id.ToString() == profileMeta.Id.ToString()) {
                return profileService.ActiveProfile;
            }

            string cacheKey = Instance.GetCacheKey(profileMeta.Location);
            IProfile profile = Instance.cache.Get(cacheKey);
            if (profile != null) {
                lastLoaded[profileMeta.Id.ToString()] = profile;
                return profile;
            }

            try {
                using FileStream fs = new FileStream(profileMeta.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var serializer = new DataContractSerializer(typeof(Profile.Profile));
                profile = (Profile.Profile)serializer.ReadObject(fs);
                Instance.cache.Put(profile, cacheKey);
                lastLoaded[profileMeta.Id.ToString()] = profile;
                return profile;
            } catch (Exception e) {
                TSLogger.Error($"failed to read profile at {profileMeta.Location}: {e.Message} {e.StackTrace}");
                throw;
            }
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            cache.Clear();
        }

        private string GetCacheKey(string path) {
            DateTime lastWriteTime = File.GetLastWriteTime(path);
            StringBuilder sb = new StringBuilder();
            sb.Append($"{path}_");
            sb.Append($"{lastWriteTime:yyyy-MM-dd-HH-mm-ss}");
            return sb.ToString();
        }

        private ProfileLoader() {
            cache = new ProfileCache();
        }
    }

    internal class ProfileCache {
        private static readonly TimeSpan ITEM_TIMEOUT = TimeSpan.FromHours(2);
        private static MemoryCache _cache = new MemoryCache("Scheduler Profile");

        public IProfile Get(string cacheKey) {
            return (IProfile)_cache.Get(cacheKey);
        }

        public void Put(IProfile profile, string cacheKey) {
            if (!_cache.Add(cacheKey, profile, DateTime.Now.Add(ITEM_TIMEOUT))) {
                _cache.Remove(cacheKey);
                _cache.Add(cacheKey, profile, DateTime.Now.Add(ITEM_TIMEOUT));
            }
        }

        public void Clear() {
            _cache.Dispose();
            _cache = new MemoryCache("Scheduler Profile");
        }

        internal ProfileCache() {
        }
    }
}