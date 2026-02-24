using EmbedIO;
using EmbedIO.WebApi;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Profile.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Utility.Notification;

namespace NINA.Plugin.TargetScheduler.API {
    public class Server {
        public WebServer WebServer;

        private Thread serverThread;
        private CancellationTokenSource apiToken;
        public readonly int Port;
        private readonly ISchedulerDatabaseInteraction database;
        private readonly IProfileService profileService;
        private readonly Func<WebServer> webServerFactory;

        // Added optional webServerFactory injection for testing.
        public Server(int port, IProfileService p, ISchedulerDatabaseInteraction i, Func<WebServer> webServerFactory = null) {
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
                    .WithWebApi("/v1", m => m.RegisterController<Controller>(ControllerFactory))
            );
        }

        public Controller ControllerFactory() {
            return new Controller(database, profileService);
        }

        public void CreateServer() {
            WebServer = webServerFactory();
        }

        public void Start() {
            try {
                TSLogger.Trace("Creating embedio server");
                CreateServer();
                TSLogger.Trace("Starting embedio server");
                if (WebServer != null) {
                    serverThread = new Thread(() => APITask(WebServer));
                    serverThread.Name = "API Thread";
                    serverThread.SetApartmentState(ApartmentState.STA);
                    serverThread.Start();
                }
            } catch (Exception e) {
                TSLogger.Error($"failed to start embedio server: {e}");
            }
        }

        public void Stop() {
            try {
                apiToken?.Cancel();
                WebServer?.Dispose();
                WebServer = null;
            } catch (Exception e) {
                TSLogger.Error($"failed to stop embedio server: {e}");
            }
        }

        [STAThread]
        private void APITask(WebServer server) {
            TSLogger.Info($"starting embedio server, listening on port {Port}");

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
        public PreprocessRequestModule() : base("/") { }

        protected override Task OnRequestAsync(IHttpContext context) {
            TSLogger.Trace($"Request: {context.Request.Url.OriginalString}");
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return Task.CompletedTask;
        }

        public override bool IsFinalHandler => false;
    }
}
