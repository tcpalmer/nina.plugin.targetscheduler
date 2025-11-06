using System;

namespace NINA.Plugin.TargetScheduler.Shared.Utility {

	public interface IMaximumHorizonService {
		/// <summary>
		/// Returns the maximum allowed altitude at the specified time/azimuth for the target.
		/// Return null when there is no maximum constraint (plugin unavailable or not applicable).
		/// </summary>
		/// <param name="atTime">The time to evaluate.</param>
		/// <param name="azimuth">Azimuth in degrees (0-360).</param>
		/// <param name="targetName">Target name (for context if needed by provider).</param>
		/// <param name="targetId">Target id (for context if needed by provider).</param>
		/// <returns>Maximum allowed altitude in degrees, or null.</returns>
		double? GetMaxAllowedAltitude(DateTime atTime, double azimuth, string targetName, long targetId);

		/// <summary>
		/// Gets the currently selected profile name for cache key generation.
		/// Returns null if no profile is selected or Maximum Horizon is not available.
		/// </summary>
		string GetCurrentProfileName();
	}
}


