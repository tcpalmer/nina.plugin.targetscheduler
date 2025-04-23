using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;

namespace NINA.Plugin.TargetScheduler.Planning.Exposures {

    /// <summary>
    /// Manage the dither state for a target.  We maintain a stack of exposures since the last
    /// dither.  When a new exposure is selected, we need to dither if the count of that
    /// exposure filter in the stack is equal to the 'dither every' value.  The stack should be
    /// reset (cleared) after a successful dither/exposure.
    ///
    /// Note that the comparison is on the filter name - not the exposure plan itself.  Since you
    /// could have different exposure plans using the same filter, we want to ensure that when that
    /// filter is selected again, we dither appropriately.
    ///
    /// Also, we don't reset the stack here if a dither is required since we don't know at that
    /// point whether the exposure will actually be taken and pre-dither performed.
    /// </summary>
    public class DitherManager {
        private Stack<IExposure> exposureStack;
        private int ditherEvery;

        public DitherManager(int ditherEvery) {
            this.ditherEvery = ditherEvery;
            exposureStack = new Stack<IExposure>();
        }

        public void AddExposure(IExposure exposure) {
            exposureStack.Push(exposure);
        }

        public bool DitherRequired(IExposure nextExposure) {
            int? ditherOverride = GetExposureDitherOverride(nextExposure);
            int dither = ditherOverride.HasValue ? ditherOverride.Value : ditherEvery;

            if (dither == 0) { return false; }
            int count = exposureStack.Count(item => item.FilterName == nextExposure.FilterName);
            return count >= dither;
        }

        public void Reset() {
            exposureStack.Clear();
        }

        private int? GetExposureDitherOverride(IExposure exposure) {
            return exposure.DitherEvery >= 0 ? exposure.DitherEvery : null;
        }
    }

    /// <summary>
    /// Support an in-memory cache of DitherManagers.  This is needed so that some exposure
    /// selectors can maintain dither state over the course of an imaging session.
    ///
    /// Note that when the planner determines that it is switching from a previous target to
    /// a new one, it will remove the cache entry for the previous so it starts fresh if
    /// selected again (in which case it will necessarily have a slew/center to begin).
    /// </summary>
    public class DitherManagerCache {
        private static readonly TimeSpan ITEM_TIMEOUT = TimeSpan.FromHours(18);
        private static MemoryCache _cache = Create();
        private static object lockObj = new object();

        public static string GetCacheKey(Target target) {
            return $"{target.Id}";
        }

        public static string GetCacheKey(ITarget target) {
            return $"{target.DatabaseId}";
        }

        public static DitherManager Get(string cacheKey) {
            lock (lockObj) {
                return (DitherManager)_cache.Get(cacheKey);
            }
        }

        public static void Put(DitherManager ditherManager, string cacheKey) {
            lock (lockObj) {
                _cache.Add(cacheKey, ditherManager, DateTime.Now.Add(ITEM_TIMEOUT));
            }
        }

        public static void Remove(ITarget target) {
            Remove(GetCacheKey(target));
        }

        public static void Remove(Target target) {
            Remove(GetCacheKey(target));
        }

        public static void Remove(List<Target> targets) {
            if (Common.IsEmpty(targets)) return;
            foreach (Target target in targets) {
                Remove(target);
            }
        }

        public static void Remove(string cacheKey) {
            lock (lockObj) {
                _cache.Remove(cacheKey);
            }
        }

        public static void Clear() {
            lock (lockObj) {
                _cache.Dispose();
                _cache = Create();
            }
        }

        private static MemoryCache Create() {
            return new MemoryCache("Scheduler DitherManager");
        }

        private DitherManagerCache() {
        }
    }
}