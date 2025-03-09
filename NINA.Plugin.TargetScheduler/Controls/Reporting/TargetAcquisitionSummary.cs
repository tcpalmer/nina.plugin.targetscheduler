using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Grading;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Controls.Reporting {

    public class TargetAcquisitionSummary {
        public static readonly string TOTAL_LBL = "Total";
        public static readonly int EXPOSURES = 0;
        public static readonly int TOTAL = 1;
        public static readonly int ACCEPTED = 2;
        public static readonly int REJECTED = 3;
        public static readonly int PENDING = 4;

        public List<TargetAcquisitionSummaryRow> Rows { get; private set; }

        public TargetAcquisitionSummary(List<AcquiredImage> acquiredImages) {
            Rows = new List<TargetAcquisitionSummaryRow>();
            if (Common.IsEmpty(acquiredImages)) return;

            Dictionary<string, int[]> scratch = new Dictionary<string, int[]>();
            scratch[TOTAL_LBL] = new int[5];

            acquiredImages.ForEach(ai => {
                int duration = (int)ai.Metadata.ExposureDuration;
                int[] times;
                scratch.TryGetValue("Total", out times);
                times[EXPOSURES]++;
                times[TOTAL] += duration;

                switch (ai.GradingStatus) {
                    case GradingStatus.Accepted: times[ACCEPTED] += duration; break;
                    case GradingStatus.Rejected: times[REJECTED] += duration; break;
                    case GradingStatus.Pending: times[PENDING] += duration; break;
                }

                string filter = ai.FilterName;
                if (!scratch.TryGetValue(filter, out times)) {
                    times = new int[5];
                    scratch[filter] = times;
                }

                times[EXPOSURES]++;
                times[TOTAL] += duration;
                switch (ai.GradingStatus) {
                    case GradingStatus.Accepted: times[ACCEPTED] += duration; break;
                    case GradingStatus.Rejected: times[REJECTED] += duration; break;
                    case GradingStatus.Pending: times[PENDING] += duration; break;
                }
            });

            foreach (var (key, times) in scratch) {
                Rows.Add(new TargetAcquisitionSummaryRow(key, times));
            }

            // Move total row to end for display
            var total = Rows[0];
            Rows.RemoveAt(0);
            Rows.Add(total);
        }
    }

    public class TargetAcquisitionSummaryRow {
        public string Key { get; private set; }
        public int Exposures { get; private set; }
        public int TotalTime { get; private set; }
        public int AcceptedTime { get; private set; }
        public int RejectedTime { get; private set; }
        public int PendingTime { get; private set; }

        public TargetAcquisitionSummaryRow(string key, int[] times) {
            if (string.IsNullOrEmpty(key) || Common.IsEmpty(times)) return;

            Key = key;

            Exposures = times[TargetAcquisitionSummary.EXPOSURES];
            TotalTime = times[TargetAcquisitionSummary.TOTAL];
            AcceptedTime = times[TargetAcquisitionSummary.ACCEPTED];
            RejectedTime = times[TargetAcquisitionSummary.REJECTED];
            PendingTime = times[TargetAcquisitionSummary.PENDING];
        }
    }
}