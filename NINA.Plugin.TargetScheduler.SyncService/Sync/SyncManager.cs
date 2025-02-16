using GrpcDotNetNamedPipes;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using Scheduler.SyncService;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NINA.Plugin.TargetScheduler.SyncService.Sync {

    public class SyncManager {
        private static readonly Lazy<SyncManager> lazy = new Lazy<SyncManager>(() => new SyncManager());
        public static SyncManager Instance { get => lazy.Value; }

        public static readonly string PIPE_NAME = "TargetScheduler.Sync";

        public static readonly int SERVER_WAIT_POLL_PERIOD = 500;
        public static readonly int SERVER_STALE_CLIENT_PURGE_CHECK_PERIOD = 3000;
        public static readonly int SERVER_STALE_CLIENT_PURGE_TIMEOUT = 120;
        public static readonly int SERVER_AWAIT_EXPOSURE_POLL_PERIOD = 1000;
        public static readonly int SERVER_AWAIT_EXPOSURE_COMPLETE_POLL_PERIOD = 1000;
        public static readonly int SERVER_AWAIT_EXPOSURE_COMPLETE_TIMEOUT = 30;
        public static readonly int SERVER_AWAIT_SOLVEROTATE_POLL_PERIOD = 1000;
        public static readonly int SERVER_AWAIT_SOLVEROTATE_COMPLETE_POLL_PERIOD = 1000;
        public static readonly int SERVER_AWAIT_EVENTCONTAINER_POLL_PERIOD = 1000;
        public static readonly int SERVER_AWAIT_EVENTCONTAINER_COMPLETE_POLL_PERIOD = 1000;
        public static readonly int CLIENT_KEEPALIVE_PERIOD = 3000;
        public static readonly int CLIENT_WAIT_POLL_PERIOD = 1000;
        public static readonly int CLIENT_ACTION_READY_POLL_PERIOD = 3000;
        public static readonly int CLIENT_REGISTRATION_DEADLINE = 60;
        public static readonly int CLIENT_BASIC_DEADLINE = 20;
        public static readonly int DEFAULT_SYNC_WAIT_TIMEOUT = 300;
        public static readonly int DEFAULT_SYNC_ACTION_TIMEOUT = 300;
        public static readonly int DEFAULT_SYNC_SOLVEROTATE_TIMEOUT = 300;

        private NamedPipeServer? pipe;
        private string? mutexid;

        private bool isServer = false;
        public bool IsServer { get => isServer; private set { isServer = value; } }

        private bool isRunning = false;
        public bool IsRunning { get => isRunning; private set { isRunning = value; } }

        public bool RunningServer { get => isRunning && isServer; }
        public bool RunningClient { get => isRunning && !isServer; }

        private SyncManager() {
        }

        public void Start(IProfileService profileService) {
            string profileId = profileService.ActiveProfile.Id.ToString();
            try {
                TryStartServer();

                if (IsServer) {
                    SyncServer.Instance.ProfileId = profileId;
                } else {
                    SyncClient.Instance.Register(profileId);
                }
            } catch (Exception ex) {
                TSLogger.Error("SYNC failed to start synchronization server ", ex);
            }
        }

        public void Shutdown() {
            if (isServer) {
                SyncServer.Instance.Shutdown();
            }

            if (!IsServer) {
                SyncClient.Instance.Unregister();
            }

            try {
                if (pipe != null) {
                    TSLogger.Info("SYNC shutting down server");
                    pipe.Kill();
                    pipe.Dispose();
                    pipe = null;

                    IsRunning = false;
                    IsServer = false;
                }
            } catch (Exception ex) {
                TSLogger.Error("SYNC failed to shutdown pipe", ex);
            }
        }

        private void TryStartServer() {
            TSLogger.Info($"SYNC trying to start server for PID {Environment.ProcessId}");

            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);

            // Ensure that only one server will be spawned when multiple application instances are started
            // Ref: https://docs.microsoft.com/en-us/dotnet/api/system.threading.mutex?redirectedfrom=MSDN&view=net-5.0

            using (var mutex = new Mutex(false, mutexid, out var createNew)) {
                var hasHandle = false;
                try {
                    try {
                        // Wait for 5 seconds to receive the mutex
                        hasHandle = mutex.WaitOne(5000, false);
                        if (hasHandle == false) {
                            throw new TimeoutException("SYNC timeout waiting for exclusive access");
                        }

                        try {
                            var pipeName = PIPE_NAME;

                            if (!NamedPipeExist(pipeName)) {
                                var user = WindowsIdentity.GetCurrent().User;
                                var security = new PipeSecurity();
                                security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
                                security.SetOwner(user);
                                security.SetGroup(user);

                                pipe = new NamedPipeServer(pipeName, new NamedPipeServerOptions() { PipeSecurity = security });
                                SchedulerSync.BindService(pipe.ServiceBinder, SyncServer.Instance);
                                pipe.Start();
                                IsRunning = true;
                                IsServer = true;

                                TSLogger.Debug($"SYNC started synchronization server on pipe {pipeName}");
                                TSLogger.Info("SYNC running as sync server");
                            } else {
                                IsRunning = true;
                                TSLogger.Debug($"SYNC named pipe already exists: {pipeName}");
                                TSLogger.Info("SYNC running as sync client");
                            }
                        } catch (Exception ex) {
                            TSLogger.Error("SYNC failed to start synchronization server ", ex);
                        }
                    } catch (AbandonedMutexException) {
                        hasHandle = true;
                    }
                } finally {
                    if (hasHandle) {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool WaitNamedPipe(string name, int timeout);

        public static bool NamedPipeExist(string pipeName) {
            try {
                int timeout = 0;
                string normalizedPath = Path.GetFullPath(string.Format(@"\\.\pipe\{0}", pipeName));
                bool exists = WaitNamedPipe(normalizedPath, timeout);
                if (!exists) {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 0) {
                        return false; // pipe does not exist
                    } else if (error == 2) {
                        return false; // win32 error code for file not found
                                      // all other errors indicate other issues
                    }
                }

                return true;
            } catch (Exception) {
                return false; // assume it doesn't exist
            }
        }
    }
}