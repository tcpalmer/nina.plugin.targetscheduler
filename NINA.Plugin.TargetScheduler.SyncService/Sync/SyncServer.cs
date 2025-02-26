using Grpc.Core;
using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using Scheduler.SyncService;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace NINA.Plugin.TargetScheduler.SyncService.Sync {

    public enum ServerState {
        Ready,
        Waiting,
        WaitComplete,
        PlanWait,
        ExposureReady,
        SolveRotateReady,
        EndSyncContainers,
        EventContainerReady
    }

    public class SyncServer : SchedulerSync.SchedulerSyncBase {
        private object lockObj = new object();

        private static readonly Lazy<SyncServer> lazy = new Lazy<SyncServer>(() => new SyncServer());
        public static SyncServer Instance { get => lazy.Value; }

        private Dictionary<string, SyncClientInstance> registeredClients;
        private ClientActiveState clientActiveExposures = new ClientActiveState();
        private ClientActiveState clientActiveSolveRotates = new ClientActiveState();
        private ClientActiveState clientActiveEventContainers = new ClientActiveState();

        public ServerState State { get; set; }
        public string ProfileId { get; internal set; }

        private ActionResponse activeActionResponse;
        private CancellationTokenSource staleClientPurgeCts;

        public SyncServer() {
            registeredClients = new Dictionary<string, SyncClientInstance>();
            SetServerState(ServerState.Ready);
            StartStaleClientPurge();
        }

        private void SetServerState(ServerState state) {
            lock (lockObj) {
                TSLogger.Info($"SYNC server setting state {State} -> {state}");
                State = state;
            }
        }

        public override Task<RegistrationResponse> Register(RegistrationRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server received client registration request {request.Guid} {request.Pid} {request.Timestamp.ToDateTime().ToLocalTime()}");

            lock (lockObj) {
                if (registeredClients.ContainsKey(request.Guid)) {
                    TSLogger.Warning($"SYNC warning: client {request.Guid} is already registered");
                    return Task.FromResult(new RegistrationResponse { Success = false, Message = "client is already registered" });
                } else {
                    TSLogger.Info($"SYNC registering client: {request.Guid}");
                    registeredClients[request.Guid] = new SyncClientInstance(request);
                    LogRegisteredClients();
                    return Task.FromResult(new RegistrationResponse { Success = true, ServerProfileId = ProfileId, Message = "" });
                }
            }
        }

        public override Task<StatusResponse> Unregister(ClientIdRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server received client unregister request {request.Guid}");

            lock (lockObj) {
                if (registeredClients.ContainsKey(request.Guid)) {
                    TSLogger.Info($"SYNC unregistering client: {request.Guid}");
                    registeredClients.Remove(request.Guid);
                    LogRegisteredClients();
                    return Task.FromResult(new StatusResponse { Success = true, Message = "" });
                } else {
                    TSLogger.Warning($"SYNC client does not exist: {request.Guid}");
                    return Task.FromResult(new StatusResponse { Success = false, Message = "client is not registered" });
                }
            }
        }

        public override Task<SyncWaitResponse> SyncWait(ClientIdRequest request, ServerCallContext context) {
            lock (lockObj) {
                if (registeredClients.ContainsKey(request.Guid)) {
                    TSLogger.Info($"SYNC client sync wait: {request.Guid}");
                    bool willContinue = State != ServerState.WaitComplete;
                    registeredClients[request.Guid].SetState(willContinue ? ClientState.Waiting : ClientState.Ready);

                    LogRegisteredClients();
                    return Task.FromResult(new SyncWaitResponse { Success = true, Continue = willContinue });
                } else {
                    TSLogger.Warning($"SYNC client does not exist: {request.Guid}");
                    return Task.FromResult(new SyncWaitResponse { Success = false, Continue = false });
                }
            }
        }

        public override Task<ActionResponse> RequestAction(ClientIdRequest request, ServerCallContext context) {
            lock (lockObj) {
                if (registeredClients.ContainsKey(request.Guid)) {
                    if (State == ServerState.EndSyncContainers) {
                        TSLogger.Info($"SYNC server ending sync container {request.Guid}");
                        return Task.FromResult(new ActionResponse { Success = true, Terminate = true });
                    }

                    ActionResponse response = (
                        State == ServerState.ExposureReady ||
                        State == ServerState.SolveRotateReady ||
                        State == ServerState.EventContainerReady) ?
                                                activeActionResponse :
                                                new ActionResponse { Success = true, ExposureReady = false, Terminate = false, EventContainer = false };

                    LogRegisteredClients();
                    return Task.FromResult(response);
                } else {
                    TSLogger.Warning($"SYNC client does not exist: {request.Guid}");
                    return Task.FromResult(new ActionResponse { Success = false, Terminate = true });
                }
            }
        }

        public override Task<StatusResponse> Keepalive(ClientIdRequest request, ServerCallContext context) {
            TSLogger.Debug($"SYNC keepalive {request.Guid} {request.ClientState}");

            lock (lockObj) {
                if (registeredClients.ContainsKey(request.Guid)) {
                    registeredClients[request.Guid].SetState(request.ClientState);
                    registeredClients[request.Guid].SetLastAliveDate(request);
                    //LogRegisteredClients();
                    return Task.FromResult(new StatusResponse { Success = true, Message = "" });
                } else {
                    TSLogger.Warning($"keepalive: client not registered {request.Guid}");
                    return Task.FromResult(new StatusResponse { Success = false, Message = "client is not registered" });
                }
            }
        }

        public async Task SyncExposure(string exposureId, InputTarget target, int targetDatabaseId, int exposurePlanDatabaseId, double exposureLength, int syncExposureTimeout, CancellationToken token) {
            activeActionResponse = new ActionResponse {
                Success = true,
                ExposureReady = true,
                SolveRotateReady = false,
                Terminate = false,
                EventContainer = false,
                ExposureId = exposureId,
                TargetName = target.DeepSkyObject.NameAsAscii,
                TargetRa = target.InputCoordinates.Coordinates.RAString,
                TargetDec = target.InputCoordinates.Coordinates.DecString,
                TargetPositionAngle = target.PositionAngle,
                TargetDatabaseId = targetDatabaseId,
                ExposurePlanDatabaseId = exposurePlanDatabaseId,
                ExposureLength = exposureLength,
            };

            SetServerState(ServerState.ExposureReady);
            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(syncExposureTimeout);

            TSLogger.Info($"SYNC server informing clients of available exposure ({exposureId}), timeout {timeout.TotalSeconds}s");

            while (NotTimedOut(stopwatch, timeout)) {
                if (AllClientsInState(ClientState.Exposing)) {
                    TSLogger.Info("SYNC server all clients now have the exposure, continuing");
                    SetServerState(ServerState.Ready);
                    activeActionResponse = new ActionResponse { Success = true, ExposureReady = false, SolveRotateReady = false, Terminate = false, EventContainer = false };
                    SetClientActiveExposureList(exposureId);
                    return;
                }

                await Task.Delay(SyncManager.SERVER_AWAIT_EXPOSURE_POLL_PERIOD, token);
            }

            TSLogger.Warning($"SYNC server timed out waiting for one or more clients to accept exposure ({exposureId}), continuing");
        }

        public override Task<StatusResponse> AcceptExposure(ExposureRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server accepted exposure ({request.ExposureId}) from client ({request.Guid})");
            registeredClients[request.Guid].SetState(ClientState.Exposing);
            return Task.FromResult(new StatusResponse { Success = true, Message = "" });
        }

        public override Task<StatusResponse> CompleteExposure(ExposureRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server received completed exposure ({request.ExposureId}) from client ({request.Guid})");

            if (RemoveClientFromActiveExposureList(request.Guid, request.ExposureId)) {
                registeredClients[request.Guid].SetState(ClientState.Actionready);
                return Task.FromResult(new StatusResponse { Success = true, Message = "" });
            } else {
                TSLogger.Warning($"SYNC server client not found in active exposure list: {request.Guid}");
                return Task.FromResult(new StatusResponse { Success = false, Message = "client not found in active exposure list" });
            }
        }

        public async Task SyncSolveRotate(string solveRotateId, InputTarget target, int targetDatabaseId, int syncActionTimeout, CancellationToken token) {
            activeActionResponse = new ActionResponse {
                Success = true,
                ExposureReady = false,
                SolveRotateReady = true,
                Terminate = false,
                EventContainer = false,
                SolveRotateId = solveRotateId,
                TargetName = target.DeepSkyObject.NameAsAscii,
                TargetRa = target.InputCoordinates.Coordinates.RAString,
                TargetDec = target.InputCoordinates.Coordinates.DecString,
                TargetPositionAngle = target.PositionAngle,
                TargetDatabaseId = targetDatabaseId,
                PierSide = 0
            };

            SetServerState(ServerState.SolveRotateReady);
            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(syncActionTimeout);

            TSLogger.Info($"SYNC server informing clients of solve/rotate ({solveRotateId}), timeout {timeout.TotalSeconds}s");

            while (NotTimedOut(stopwatch, timeout)) {
                if (AllClientsInState(ClientState.Solving)) {
                    TSLogger.Info("SYNC server all clients now solving, continuing");
                    SetServerState(ServerState.Ready);
                    activeActionResponse = new ActionResponse { Success = true, ExposureReady = false, SolveRotateReady = false, Terminate = false, EventContainer = false };
                    SetClientActiveSolveRotateList(solveRotateId);
                    return;
                }

                await Task.Delay(SyncManager.SERVER_AWAIT_SOLVEROTATE_POLL_PERIOD, token);
            }

            TSLogger.Warning($"SYNC server timed out waiting for one or more clients to accept solve/rotate ({solveRotateId}), continuing");
        }

        public override Task<StatusResponse> AcceptSolveRotate(SolveRotateRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server accepted solve/rotate ({request.SolveRotateId}) from client ({request.Guid})");
            registeredClients[request.Guid].SetState(ClientState.Solving);
            return Task.FromResult(new StatusResponse { Success = true, Message = "" });
        }

        public override Task<StatusResponse> CompleteSolveRotate(SolveRotateRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server received completed solve/rotate ({request.SolveRotateId}) from client ({request.Guid})");

            if (RemoveClientFromActiveSolveRotateList(request.Guid, request.SolveRotateId)) {
                registeredClients[request.Guid].SetState(ClientState.Actionready);
                return Task.FromResult(new StatusResponse { Success = true, Message = "" });
            } else {
                TSLogger.Warning($"SYNC server client not found in active solve/rotate list: {request.Guid}");
                return Task.FromResult(new StatusResponse { Success = false, Message = "client not found in active solve/rotate list" });
            }
        }

        public async Task SyncEventContainer(string eventContainerId, EventContainerType eventContainerType, int syncActionTimeout, CancellationToken token) {
            string eventTypeName = eventContainerType.ToString();
            ServerState serverState = ServerState.EventContainerReady;
            ClientState clientReadyState = ClientState.Eventcontainer;

            activeActionResponse = new ActionResponse {
                Success = true,
                ExposureReady = false,
                SolveRotateReady = false,
                Terminate = false,
                EventContainer = true,
                EventContainerId = eventContainerId,
                EventContainerType = eventTypeName
            };

            SetServerState(serverState);
            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(syncActionTimeout);

            TSLogger.Info($"SYNC server informing clients of {eventContainerType}, timeout {timeout.TotalSeconds}s");

            while (NotTimedOut(stopwatch, timeout)) {
                if (AllClientsInState(clientReadyState)) {
                    TSLogger.Info($"SYNC server all clients now in {eventContainerType}, continuing");
                    SetServerState(ServerState.Ready);
                    activeActionResponse = new ActionResponse { Success = true, ExposureReady = false, SolveRotateReady = false, Terminate = false, EventContainer = false };
                    SetClientActiveEventContainerList(eventContainerId);
                    return;
                }

                await Task.Delay(SyncManager.SERVER_AWAIT_EVENTCONTAINER_POLL_PERIOD, token);
            }

            TSLogger.Warning($"SYNC server timed out waiting for one or more clients to accept {eventContainerType}, continuing");
        }

        public override Task<StatusResponse> AcceptEventContainer(EventContainerRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server accepted event container ({request.EventContainerId}) from client ({request.Guid})");
            registeredClients[request.Guid].SetState(ClientState.Eventcontainer);
            return Task.FromResult(new StatusResponse { Success = true, Message = "" });
        }

        public override Task<StatusResponse> CompleteEventContainer(EventContainerRequest request, ServerCallContext context) {
            TSLogger.Info($"SYNC server received completed event container ({request.EventContainerId}) from client ({request.Guid})");

            if (RemoveClientFromActiveEventContainerList(request.Guid, request.EventContainerId)) {
                registeredClients[request.Guid].SetState(ClientState.Actionready);
                return Task.FromResult(new StatusResponse { Success = true, Message = "" });
            } else {
                TSLogger.Warning($"SYNC server client not found in active event container list: {request.Guid}");
                return Task.FromResult(new StatusResponse { Success = false, Message = "client not found in active event container list" });
            }
        }

        public bool AllClientsInState(ClientState state) {
            foreach (SyncClientInstance client in registeredClients.Values) {
                if (client.ClientState != state) {
                    return false;
                }
            }

            return true;
        }

        public async Task WaitForClientExposureCompletion(string exposureId, CancellationToken token) {
            if (clientActiveExposures.IsEmpty()) {
                TSLogger.Warning("SYNC server not waiting on any clients for completed exposures");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(SyncManager.SERVER_AWAIT_EXPOSURE_COMPLETE_TIMEOUT);

            TSLogger.Info($"SYNC server waiting for all clients to complete exposure: {exposureId}, timeout {SyncManager.SERVER_AWAIT_EXPOSURE_COMPLETE_TIMEOUT}s");

            while (NotTimedOut(stopwatch, timeout)) {
                if (clientActiveExposures.IsEmpty()) {
                    TSLogger.Info($"SYNC server all clients have completed exposure: {exposureId}");
                    return;
                }

                await Task.Delay(SyncManager.SERVER_AWAIT_EXPOSURE_COMPLETE_POLL_PERIOD, token);
            }

            // If we timed out waiting for client exposures, then we need clear the list and let the server continue
            TSLogger.Warning($"SYNC server timed out waiting for all clients to complete exposure: {exposureId}, clearing wait list and continuing.  Remaining was:");
            clientActiveExposures.Log();
            clientActiveExposures.Clear();
        }

        private void SetClientActiveExposureList(string exposureId) {
            lock (lockObj) {
                clientActiveExposures.Clear();
                foreach (SyncClientInstance client in registeredClients.Values) {
                    if (client.ClientState == ClientState.Exposing) {
                        clientActiveExposures.Add(client.Guid, exposureId);
                    }
                }

                clientActiveExposures.Log();
            }
        }

        private bool RemoveClientFromActiveExposureList(string clientId, string exposureId) {
            lock (lockObj) {
                if (clientActiveExposures.ContainsKey(clientId)) {
                    string eid;
                    clientActiveExposures.Remove(clientId, exposureId);
                    clientActiveExposures.Log();
                    return true;
                }

                return false;
            }
        }

        public async Task WaitForClientSolveRotateCompletion(string solveRotateId, int syncSolveRotateTimeout, CancellationToken token) {
            if (clientActiveSolveRotates.IsEmpty()) {
                TSLogger.Warning("SYNC server not waiting on any clients for completed solve/rotates");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(syncSolveRotateTimeout);
            TSLogger.Info($"SYNC server waiting for all clients to complete solve/rotate: {solveRotateId}, timeout {syncSolveRotateTimeout}s");

            while (NotTimedOut(stopwatch, timeout)) {
                if (clientActiveSolveRotates.IsEmpty()) {
                    TSLogger.Info($"SYNC server all clients have completed solve/rotate: {solveRotateId}");
                    return;
                }

                await Task.Delay(SyncManager.SERVER_AWAIT_SOLVEROTATE_COMPLETE_POLL_PERIOD, token);
            }

            // If we timed out waiting for client solve/rotate, then we need clear the list and let the server continue
            TSLogger.Warning($"SYNC server timed out waiting for all clients to complete solve/rotate: {solveRotateId}, clearing wait list and continuing.  Remaining was:");
            clientActiveSolveRotates.Log();
            clientActiveSolveRotates.Clear();
        }

        private void SetClientActiveSolveRotateList(string solveRotateId) {
            lock (lockObj) {
                clientActiveSolveRotates.Clear();
                foreach (SyncClientInstance client in registeredClients.Values) {
                    if (client.ClientState == ClientState.Solving) {
                        clientActiveSolveRotates.Add(client.Guid, solveRotateId);
                    }
                }

                clientActiveSolveRotates.Log();
            }
        }

        private bool RemoveClientFromActiveSolveRotateList(string clientId, string solveRotateId) {
            lock (lockObj) {
                if (clientActiveSolveRotates.ContainsKey(clientId)) {
                    clientActiveSolveRotates.Remove(clientId, solveRotateId);
                    clientActiveSolveRotates.Log();
                    return true;
                }

                return false;
            }
        }

        public async Task WaitForClientEventContainerCompletion(EventContainerType eventContainerType, string eventContainerId, int syncEventContainerTimeout, CancellationToken token) {
            if (clientActiveEventContainers.IsEmpty()) {
                TSLogger.Warning("SYNC server not waiting on any clients for completed event container");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(syncEventContainerTimeout);
            TSLogger.Info($"SYNC server waiting for all clients to complete event container {eventContainerType}: {eventContainerId}, timeout {syncEventContainerTimeout}s");

            while (NotTimedOut(stopwatch, timeout)) {
                if (clientActiveEventContainers.IsEmpty()) {
                    TSLogger.Info($"SYNC server all clients have completed event container: {eventContainerId}");
                    return;
                }

                await Task.Delay(SyncManager.SERVER_AWAIT_EVENTCONTAINER_COMPLETE_POLL_PERIOD, token);
            }

            // If we timed out waiting for client event container, then we need clear the list and let the server continue
            TSLogger.Warning($"SYNC server timed out waiting for all clients to complete event container {eventContainerType}: {eventContainerId}, clearing wait list and continuing.  Remaining was:");
            clientActiveEventContainers.Log();
            clientActiveEventContainers.Clear();
        }

        private void SetClientActiveEventContainerList(string eventContainerId) {
            lock (lockObj) {
                clientActiveEventContainers.Clear();
                foreach (SyncClientInstance client in registeredClients.Values) {
                    if (client.ClientState == ClientState.Eventcontainer) {
                        clientActiveEventContainers.Add(client.Guid, eventContainerId);
                    }
                }

                clientActiveEventContainers.Log();
            }
        }

        private bool RemoveClientFromActiveEventContainerList(string clientId, string eventContainerId) {
            lock (lockObj) {
                if (clientActiveEventContainers.ContainsKey(clientId)) {
                    clientActiveEventContainers.Remove(clientId, eventContainerId);
                    clientActiveEventContainers.Log();
                    return true;
                }

                return false;
            }
        }

        private void LogRegisteredClients() {
            if (registeredClients.Count == 0) {
                return;
            }

            if (registeredClients.Count == 1) {
                SyncClientInstance client = registeredClients.First().Value;
                TSLogger.Debug($"SYNC CLIENT: {client.Guid} state={client.ClientState} lastAlive={client.LastAliveDate}");
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (SyncClientInstance client in registeredClients.Values) {
                sb.AppendLine($"    {client.Guid} state={client.ClientState} lastAlive={client.LastAliveDate}");
            }

            TSLogger.Debug($"SYNC CLIENTS:\n{sb}");
        }

        private bool NotTimedOut(Stopwatch stopwatch, TimeSpan timeout) {
            if (stopwatch.Elapsed > timeout) {
                return false;
            }

            return true;
        }

        public void Shutdown() {
            try {
                staleClientPurgeCts?.Cancel();
            } catch (Exception ex) {
                TSLogger.Error("exception stopping stale client purge thread", ex);
            }
        }

        private Task StartStaleClientPurge() {
            TSLogger.Info($"SYNC starting stale client purge thread");

            return Task.Run(async () => {
                using (staleClientPurgeCts = new CancellationTokenSource()) {
                    var token = staleClientPurgeCts.Token;
                    while (!token.IsCancellationRequested) {
                        try {
                            await Task.Delay(SyncManager.SERVER_STALE_CLIENT_PURGE_CHECK_PERIOD, token);

                            lock (lockObj) {
                                List<String> purgeList = new List<String>();
                                foreach (SyncClientInstance client in registeredClients.Values) {
                                    TimeSpan timeSpan = DateTime.Now - client.LastAliveDate;
                                    if (timeSpan.TotalSeconds > SyncManager.SERVER_STALE_CLIENT_PURGE_TIMEOUT) {
                                        purgeList.Add(client.Guid);
                                    }
                                }

                                for (int i = 0; i < purgeList.Count; i++) {
                                    TSLogger.Warning($"SYNC detected stale client, purging {purgeList[i]}");
                                    registeredClients.Remove(purgeList[i]);
                                }
                            }
                        } catch (OperationCanceledException) {
                            TSLogger.Info($"SYNC stopping stale client purge thread");
                        } catch (Exception ex) {
                            TSLogger.Error($"SYNC an error occurred during stale client purge", ex);
                        }
                    }
                }
            });
        }
    }

    public class ClientActiveState {
        private ConcurrentDictionary<string, string> dictionary;

        public ClientActiveState() {
            dictionary = new ConcurrentDictionary<string, string>(Environment.ProcessorCount * 2, 31);
        }

        public bool IsEmpty() {
            return dictionary.IsEmpty;
        }

        public void Clear() {
            dictionary.Clear();
        }

        public bool Add(string clientId, string itemId) {
            return dictionary.TryAdd(clientId, itemId);
        }

        public bool ContainsKey(string clientId) {
            return dictionary.ContainsKey(clientId);
        }

        public bool Remove(string clientId, string expectedId) {
            string itemId;
            bool success = dictionary.TryRemove(clientId, out itemId);
            if (success && itemId != expectedId) {
                TSLogger.Warning($"SYNC server unexpected client active ID for client ({clientId}: found {itemId} but expected {expectedId})");
                return false;
            }

            return success;
        }

        public string Log() {
            StringBuilder sb = new StringBuilder();
            foreach (var item in dictionary) {
                sb.Append($"{item.Key}:{item.Value} ");
            }

            return sb.ToString();
        }
    }

    public class SyncClientExposureResults {
        public readonly string ClientId;
        public readonly string ExposureId;

        public SyncClientExposureResults(ExposureRequest request) {
            ClientId = request.Guid;
            ExposureId = request.ExposureId;
        }
    }
}