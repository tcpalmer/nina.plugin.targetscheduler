using LinqKit;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Planning.Exposures {

    public abstract class BaseExposureSelector {
        protected ITarget Target;
        protected FilterCadence FilterCadence;
        protected DitherManager DitherManager;

        public BaseExposureSelector(ITarget target) {
            Target = target;
        }

        /// <summary>
        /// Some exposure selectors need to remember the previous dither state - typically those
        /// that don't rely on a persisted FilterCadence.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public DitherManager GetDitherManager(IProject project, ITarget target) {
            string cacheKey = DitherManagerCache.GetCacheKey(target);
            DitherManager dm = DitherManagerCache.Get(cacheKey);
            if (dm != null) {
                return dm;
            } else {
                dm = new DitherManager(project.DitherEvery);
                DitherManagerCache.Put(dm, cacheKey);
                return dm;
            }
        }

        /// <summary>
        /// Return true if all exposure plans were rejected, otherwise false.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool AllExposurePlansRejected(ITarget target) {
            bool atLeastOneAccepted = false;
            target.ExposurePlans.ForEach(e => { if (!e.Rejected) atLeastOneAccepted = true; });
            return !atLeastOneAccepted;
        }

        /// <summary>
        /// Update the target's filter cadence list, typically after an exposure is taken
        /// and the cadence is advanced.
        /// </summary>
        /// <param name="filterCadence"></param>
        public void UpdateFilterCadences(FilterCadence filterCadence) {
            List<FilterCadenceItem> items = new List<FilterCadenceItem>(filterCadence.Count);
            filterCadence.List.ForEach(fci => {
                items.Add(new FilterCadenceItem {
                    TargetId = Target.DatabaseId,
                    Order = fci.Order,
                    Next = fci.Next,
                    Action = fci.Action,
                    ReferenceIdx = fci.ReferenceIdx,
                });
            });

            using (var context = GetSchedulerDatabaseContext()) {
                context.ReplaceFilterCadences(Target.DatabaseId, items, false);
            }
        }

        public virtual ISchedulerDatabaseContext GetSchedulerDatabaseContext() {
            return new SchedulerDatabaseInteraction().GetContext();
        }
    }
}