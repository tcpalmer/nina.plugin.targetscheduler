using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.Shared.Utility {

	public static class MaximumHorizonClient {
		private static IMaximumHorizonService cachedService;
		private static bool resolved = false;
		private static bool canReloadSettings = false;
		private static string lastKnownProfile = null; // Track profile across service recreations

		/// <summary>
		/// Try to resolve the Maximum Horizon plugin at runtime. If unavailable, returns a no-op service.
		/// This uses reflection to access MaximumHorizonServiceAccessor.GetShared() without build-time dependency.
		/// </summary>
		public static IMaximumHorizonService Resolve() {
			// If we can't reload settings, recreate the service instance to pick up profile changes
			// This ensures we get fresh settings when the user changes the profile in the UI
			if (resolved && cachedService != null && cachedService != MaximumHorizonNoOp.Instance && !canReloadSettings) {
				resolved = false;
				cachedService = null;
			}

			// Always check if profile changed (if we have a cached service)
			// This ensures profile changes are detected even on subsequent planning runs
			if (resolved && cachedService != null && cachedService != MaximumHorizonNoOp.Instance) {
				// Force profile change check by calling GetCurrentProfileName
				// This will log and clear caches if the profile changed
				try {
					string currentProfile = cachedService.GetCurrentProfileName();
					// Profile change detection and logging happens inside GetCurrentProfileName()
					// The INFO level log will appear if profile changed
				}
				catch (Exception ex) {
					TSLogger.Warning($"Error checking Maximum Horizon profile in Resolve(): {ex.Message}");
				}
				return cachedService;
			}

			try {
				// Try to find the Maximum Horizon plugin assembly
				AppDomain currentDomain = AppDomain.CurrentDomain;
				Assembly[] assemblies = currentDomain.GetAssemblies();

				// Look for Maximum Horizon plugin assembly
				Assembly maxHorizonAssembly = assemblies.FirstOrDefault(a =>
					a.FullName != null && (
						a.FullName.Contains("MaximumHorizon", StringComparison.OrdinalIgnoreCase) ||
						a.FullName.Contains("Maximum-Horizon", StringComparison.OrdinalIgnoreCase) ||
						a.GetName().Name != null && (
							a.GetName().Name.Contains("MaximumHorizon", StringComparison.OrdinalIgnoreCase) ||
							a.GetName().Name.Contains("Maximum-Horizon", StringComparison.OrdinalIgnoreCase)
						)
					)
				);

				if (maxHorizonAssembly != null) {
					// Try to get MaximumHorizonService directly
					// The service is exported via MEF, so we'll try to access it through the service class
					Type serviceType = maxHorizonAssembly.GetTypes().FirstOrDefault(t =>
						t.FullName == "NINA.Plugin.MaximumHorizon.Services.MaximumHorizonService" && (t.IsPublic || t.IsNestedPublic)
					);

					if (serviceType != null) {
						object serviceInstance = null;

						// Try 1: Look for static GetShared() method
						MethodInfo getSharedMethod = serviceType.GetMethod("GetShared", BindingFlags.Public | BindingFlags.Static);
						if (getSharedMethod != null) {
							try {
								serviceInstance = getSharedMethod.Invoke(null, null);
							}
							catch (Exception ex) {
								TSLogger.Warning($"Failed to call GetShared() on MaximumHorizonService: {ex.Message}");
							}
						}

						// Try 2: Look for static Instance property
						if (serviceInstance == null) {
							PropertyInfo instanceProperty = serviceType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
							if (instanceProperty != null && instanceProperty.CanRead) {
								try {
									serviceInstance = instanceProperty.GetValue(null);
								}
								catch (Exception ex) {
									TSLogger.Warning($"Failed to access Instance property on MaximumHorizonService: {ex.Message}");
								}
							}
						}

						// Try 3: Try to get via MEF CompositionContainer (if available in NINA context)
						if (serviceInstance == null) {
							try {
								// Check if we can access NINA's composition container
								Type interfaceType = maxHorizonAssembly.GetType("NINA.Plugin.MaximumHorizon.Services.IMaximumHorizonService");
								if (interfaceType != null) {
									// Try to use System.ComponentModel.Composition
									// Note: This might not work if MEF container isn't accessible from here
									// But we'll try anyway
								}
							}
							catch (Exception ex) {
								// MEF resolution attempt failed (expected)
							}
						}

						// Try 4: Look for Instance property on the plugin class itself
						if (serviceInstance == null) {
							try {
								Type pluginType = maxHorizonAssembly.GetType("NINA.Plugin.MaximumHorizon.MaximumHorizonPlugin");
								if (pluginType != null) {
									// Try to get a static Instance property or similar
									PropertyInfo pluginInstanceProp = pluginType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
									if (pluginInstanceProp != null) {
										object pluginInstance = pluginInstanceProp.GetValue(null);
										if (pluginInstance != null) {
											// Try to get the service from the plugin instance
											PropertyInfo serviceProp = pluginInstance.GetType().GetProperty("Service", BindingFlags.Public | BindingFlags.Instance);
											if (serviceProp != null) {
												serviceInstance = serviceProp.GetValue(pluginInstance);
											}
										}
									}
								}
							}
							catch (Exception ex) {
								// Failed to get service from plugin
							}
						}

						// Try 5: Create instance directly (if it has a parameterless constructor)
						if (serviceInstance == null) {
							try {
								ConstructorInfo ctor = serviceType.GetConstructor(Type.EmptyTypes);
								if (ctor != null) {
									serviceInstance = ctor.Invoke(null);
									TSLogger.Warning("MaximumHorizonService created directly - profile changes may not be detected correctly. Consider using plugin's shared instance.");
								}
							}
							catch (Exception ex) {
								// Failed to create MaximumHorizonService instance
							}
						}

						// If we got a service instance, wrap it
						if (serviceInstance != null) {
							var wrapper = new MaximumHorizonServiceWrapper(serviceInstance);
							canReloadSettings = wrapper.HasLoadSettingsMethod();
							cachedService = wrapper;
							resolved = true;
							return cachedService;
						}
						else {
							TSLogger.Warning("Could not obtain MaximumHorizonService instance - tried GetShared(), Instance property, MEF, and direct construction");
						}
					}
					else {
						TSLogger.Warning("Could not find MaximumHorizonService type in Maximum Horizon assembly");
					}
				}
			}
			catch (Exception ex) {
				TSLogger.Warning($"Error while attempting to resolve Maximum Horizon plugin: {ex.Message}");
			}

			// No Maximum Horizon plugin found or failed to access - use no-op
			cachedService = MaximumHorizonNoOp.Instance;
			resolved = true;
			return cachedService;
		}

		/// <summary>
		/// Wrapper that bridges Maximum Horizon's async API to our synchronous interface.
		/// Uses GetAwaiter().GetResult() to call async methods synchronously.
		/// </summary>
		private class MaximumHorizonServiceWrapper : IMaximumHorizonService {
			private readonly object maximumHorizonService;
			private readonly MethodInfo getMaxAltitudeMethod;
			private readonly PropertyInfo selectedProfileProperty;
			private readonly PropertyInfo globalMarginBufferProperty;
			private readonly MethodInfo loadSettingsMethod;
			private readonly System.Collections.Generic.Dictionary<(int azimuth, string profile), double> altitudeCache = new System.Collections.Generic.Dictionary<(int, string), double>();
			private DateTime lastCacheClear = DateTime.Now;
			private const int CACHE_DURATION_MINUTES = 5;
			private int callCount = 0;

			public MaximumHorizonServiceWrapper(object serviceInstance) {
				this.maximumHorizonService = serviceInstance ?? throw new ArgumentNullException(nameof(serviceInstance));
				Type serviceType = serviceInstance.GetType();

				// Find GetMaximumAltitudeAsync method
				// According to docs: GetMaximumAltitudeAsync(int azimuth, string profileName)
				getMaxAltitudeMethod = serviceType.GetMethod("GetMaximumAltitudeAsync", 
					BindingFlags.Public | BindingFlags.Instance,
					null,
					new Type[] { typeof(int), typeof(string) },
					null);

				// Find SelectedProfileName property (to use as default if profileName is null)
				selectedProfileProperty = serviceType.GetProperty("SelectedProfileName", BindingFlags.Public | BindingFlags.Instance);

				// Find GlobalMarginBuffer property
				globalMarginBufferProperty = serviceType.GetProperty("GlobalMarginBuffer", BindingFlags.Public | BindingFlags.Instance);

				// Find LoadSettings method to refresh settings before reading profile
				loadSettingsMethod = serviceType.GetMethod("LoadSettings", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (loadSettingsMethod == null) {
					// Try async version
					loadSettingsMethod = serviceType.GetMethod("LoadSettingsAsync", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				}

				if (getMaxAltitudeMethod == null) {
					TSLogger.Warning("GetMaximumAltitudeAsync method not found on Maximum Horizon service");
				}
			}

			public double? GetMaxAllowedAltitude(DateTime atTime, double azimuth, string targetName, long targetId) {
				if (getMaxAltitudeMethod == null || maximumHorizonService == null) {
					return null;
				}

				try {
					// Get selected profile if no profile specified
					// Profile change detection happens in GetCurrentProfileName() which is called during cache key generation
					string profileName = GetCurrentProfileName();

					// Clear cache periodically
					if ((DateTime.Now - lastCacheClear).TotalMinutes > CACHE_DURATION_MINUTES) {
						altitudeCache.Clear();
						lastCacheClear = DateTime.Now;
					}

					// Round azimuth to integer for caching and API call
					int azimuthInt = (int)Math.Round(azimuth);
					if (azimuthInt < 0) azimuthInt = 0;
					if (azimuthInt > 359) azimuthInt = 359;

					// Check cache
					var cacheKey = (azimuthInt, profileName ?? "");
					if (altitudeCache.TryGetValue(cacheKey, out double cachedAlt)) {
						return cachedAlt;
					}

					// Call async method synchronously
					Task<double> task = (Task<double>)getMaxAltitudeMethod.Invoke(maximumHorizonService, new object[] { azimuthInt, profileName });
					double maxAltitude = task.GetAwaiter().GetResult();

					// Apply global margin buffer if available
					double? margin = null;
					if (globalMarginBufferProperty != null) {
						margin = globalMarginBufferProperty.GetValue(maximumHorizonService) as double?;
						if (margin.HasValue && margin.Value > 0) {
							maxAltitude = Math.Max(0, maxAltitude - margin.Value);
						}
					}

					// Cache result
					altitudeCache[cacheKey] = maxAltitude;

					return maxAltitude;
				}
				catch (Exception ex) {
					TSLogger.Warning($"Error calling Maximum Horizon GetMaximumAltitudeAsync: {ex.Message}");
					return null;
				}
			}

			public bool HasLoadSettingsMethod() {
				return loadSettingsMethod != null;
			}

			public string GetCurrentProfileName() {
				if (selectedProfileProperty != null && maximumHorizonService != null) {
					try {
						// Reload settings before reading profile to ensure we get the latest value
						// This is critical when the user changes the profile in the Maximum Horizon plugin UI
						if (loadSettingsMethod != null) {
							try {
								if (loadSettingsMethod.Name == "LoadSettingsAsync") {
									// Async version - call synchronously
									Task task = loadSettingsMethod.Invoke(maximumHorizonService, null) as Task;
									if (task != null) {
										task.GetAwaiter().GetResult();
									}
								}
								else {
									// Sync version
									loadSettingsMethod.Invoke(maximumHorizonService, null);
								}
							}
							catch (Exception ex) {
								// Failed to reload settings - continue anyway
							}
						}

						string currentProfile = selectedProfileProperty.GetValue(maximumHorizonService) as string;
						
						// Use static lastKnownProfile to track across service recreations
						// Detect profile change when getting profile name (used for cache key generation)
						if (currentProfile != MaximumHorizonClient.lastKnownProfile) {
							if (MaximumHorizonClient.lastKnownProfile != null) {
								// Profile changed - clear cache
								altitudeCache.Clear();
								lastCacheClear = DateTime.Now;
								
								// Note: TargetVisibility cache will automatically use different entries
								// because the cache key includes the profile name (see TargetVisibility.GetCacheKey)
								// So switching profiles will create a new cache entry with the new profile name
							}
							MaximumHorizonClient.lastKnownProfile = currentProfile;
						}
						
						return currentProfile;
					}
					catch (Exception ex) {
						TSLogger.Warning($"Error getting Maximum Horizon profile name: {ex.Message}");
						return null;
					}
				}
				return null;
			}
		}
	}
}


