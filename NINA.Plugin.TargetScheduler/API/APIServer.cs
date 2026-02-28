using EmbedIO;
using NINA.Core.Utility.Notification;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.TargetScheduler.API {

    public class APIServer {
        private const string BASE_ROUTE = "/ts/v0";

        public WebServer WebServer;

        private Thread serverThread;
        private CancellationTokenSource apiToken;
        public readonly int Port;
        private readonly ISchedulerDatabaseInteraction database;
        private readonly IProfileService profileService;
        private static bool PrettyPrint;
        private readonly Func<WebServer> webServerFactory;

        public APIServer(int port, bool prettyPrint, IProfileService p, ISchedulerDatabaseInteraction i, Func<WebServer> webServerFactory = null) {
            Port = port;
            PrettyPrint = prettyPrint;
            database = i;
            profileService = p;

            this.webServerFactory = webServerFactory ?? (() =>
                new WebServer(o => o
                    .WithUrlPrefix($"http://*:{Port}")
                    .WithMode(HttpListenerMode.EmbedIO))
                    .WithModule(new PreprocessRequestModule())
                    .WithWebApi(BASE_ROUTE, SerializationCallback, m => m.RegisterController<APIController>(ControllerFactory))
            );
        }

        public static async Task SerializationCallback(IHttpContext context, object data) {
            context.Response.ContentType = "application/json";

            var jsonOptions = new JsonSerializerOptions { WriteIndented = PrettyPrint };
            using var textWriter = context.OpenResponseText(new UTF8Encoding(false));
            await textWriter.WriteAsync(System.Text.Json.JsonSerializer.Serialize(data, jsonOptions)).ConfigureAwait(false);
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

                Notification.ShowInformation($"Target Scheduler API started: http://localhost:{Port}/ts/v0/...");
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
                Notification.ShowInformation($"Target Scheduler API stopped");
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