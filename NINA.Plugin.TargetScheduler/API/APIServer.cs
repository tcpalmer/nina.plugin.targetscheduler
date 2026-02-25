using EmbedIO;
using NINA.Core.Utility.Notification;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.API {

    public class APIServer {
        public WebServer WebServer;

        private Thread serverThread;
        private CancellationTokenSource apiToken;
        public readonly int Port;
        private readonly ISchedulerDatabaseInteraction database;
        private readonly IProfileService profileService;
        private readonly Func<WebServer> webServerFactory;

        // Added optional webServerFactory injection for testing.
        public APIServer(int port, IProfileService p, ISchedulerDatabaseInteraction i, Func<WebServer> webServerFactory = null) {
            Port = port;
            // In production, use the default constructor.
            database = i;
            profileService = p;
            // If no factory is provided, build the default WebServer.
            this.webServerFactory = webServerFactory ?? (() =>
                new WebServer(o => o
                    .WithUrlPrefix($"http://*:{Port}")
                    .WithMode(HttpListenerMode.EmbedIO))
                    .WithModule(new PreprocessRequestModule())
                    .WithWebApi("/v1", m => m.RegisterController<APIController>(ControllerFactory))
            );
        }

        public APIController ControllerFactory() {
            return new APIController(database, profileService);
        }

        public void CreateServer() {
            WebServer = webServerFactory();
        }

        public void Start() {
            try {
                TSLogger.Trace("creating embedio server");
                CreateServer();
                TSLogger.Trace("starting embedio server");
                if (WebServer != null) {
                    serverThread = new Thread(() => APITask(WebServer));
                    serverThread.Name = "Target Scheduler API Thread";
                    serverThread.SetApartmentState(ApartmentState.STA);
                    serverThread.Start();
                }
            } catch (Exception e) {
                TSLogger.Error($"failed to start embedio server: {e}");
            }
        }

        public void Stop() {
            try {
                TSLogger.Debug("stopping embedio server");
                apiToken?.Cancel();
                WebServer?.Dispose();
                WebServer = null;
                Thread.Sleep(200);
            } catch (Exception e) {
                TSLogger.Error($"failed to stop embedio server: {e}");
            }
        }

        [STAThread]
        private void APITask(WebServer server) {
            TSLogger.Info($"starting embedio server for TS API, listening on port {Port}");

            try {
                apiToken = new CancellationTokenSource();
                server.RunAsync(apiToken.Token).Wait();
            } catch (Exception e) {
                TSLogger.Error($"failed to start embedio server: {e}");
                Notification.ShowError($"Failed to start Target Scheduler API server, see Target Scheduler log for details");
                TSLogger.Debug("aborting web server thread");
            }
        }
    }

    public class PreprocessRequestModule : WebModuleBase {

        public PreprocessRequestModule() : base("/") {
        }

        protected override Task OnRequestAsync(IHttpContext context) {
            TSLogger.Debug($"Request: {context.Request.Url.OriginalString}");
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return Task.CompletedTask;
        }

        public override bool IsFinalHandler => false;
    }
}