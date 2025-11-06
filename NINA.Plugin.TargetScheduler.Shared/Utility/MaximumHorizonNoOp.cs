using System;

namespace NINA.Plugin.TargetScheduler.Shared.Utility {

	public sealed class MaximumHorizonNoOp : IMaximumHorizonService {
		public static readonly MaximumHorizonNoOp Instance = new MaximumHorizonNoOp();

		private MaximumHorizonNoOp() { }

		public double? GetMaxAllowedAltitude(DateTime atTime, double azimuth, string targetName, long targetId) {
			return null; // No constraint
		}

		public string GetCurrentProfileName() {
			return null;
		}
	}
}


