using NINA.Core.Enum;
using NINA.Core.Utility;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.File;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Shared.Utility {

    /// <summary>
    /// Cribbed from NINA.Core.Logger
    /// </summary>
    public static class TSLogger {
        private static ILogger TSLog;
        private static LoggingLevelSwitch levelSwitch;

        static TSLogger() {
            var logDate = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var logDir = Path.Combine(Common.PLUGIN_HOME, "Logs");
            var processId = Environment.ProcessId;
            var logFilePath = Path.Combine(logDir, $"TS-{logDate}-{CoreUtil.Version}.{processId}.log");

            levelSwitch = new LoggingLevelSwitch();
            levelSwitch.MinimumLevel = LogEventLevel.Information;

            if (!Directory.Exists(logDir)) {
                Directory.CreateDirectory(logDir);
            } else {
                CoreUtil.DirectoryCleanup(logDir, TimeSpan.FromDays(-90));
            }

            TSLog = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .Enrich.With<LegacyLogLevelMappingEnricher>()
                .WriteTo.File(logFilePath,
                    rollingInterval: RollingInterval.Infinite,
                    outputTemplate: "{Timestamp:yyyy-MM-ddTHH:mm:ss.ffff}|{LegacyLogLevel}|{Message:lj}{NewLine}{Exception}",
                    shared: false,
                    buffered: false,
                    hooks: new HeaderWriter(GenerateHeader),
                    flushToDiskInterval: TimeSpan.FromSeconds(1),
                    retainedFileCountLimit: null)
                .CreateLogger();

            // Default but should be set from TS profile preferences based on current NINA profile
            SetLogLevel(LogLevelEnum.DEBUG);
        }

        private static string GenerateHeader() {
            var sb = new StringBuilder();
            sb.AppendLine("----------------------------------------------------------------------");
            sb.AppendLine($"NINA Target Scheduler Plugin {GetPluginVersion()}");
            sb.AppendLine("----------------------------------------------------------------------");
            sb.Append("DATE|LEVEL|SOURCE|MEMBER|LINE|MESSAGE");

            return sb.ToString();
        }

        private static string GetPluginVersion() {
            string? version = null;

            // We have to check all assemblies since this class no longer lives in the main DLL
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies) {
                AssemblyName name = assembly.GetName();
                if (name.Name == "NINA.Plugin.TargetScheduler") {
                    version = name.Version?.ToString();
                    break;
                }
            }

            return version ?? "<version?>";
        }

        public static bool IsEnabled(LogLevelEnum logLevel) {
            switch (logLevel) {
                case LogLevelEnum.TRACE: return levelSwitch.MinimumLevel <= LogEventLevel.Verbose;
                case LogLevelEnum.DEBUG: return levelSwitch.MinimumLevel <= LogEventLevel.Debug;
                case LogLevelEnum.INFO: return levelSwitch.MinimumLevel <= LogEventLevel.Information;
                case LogLevelEnum.WARNING: return levelSwitch.MinimumLevel <= LogEventLevel.Warning;
                case LogLevelEnum.ERROR: return levelSwitch.MinimumLevel <= LogEventLevel.Error;
                default: return false;
            }
        }

        public static void SetLogLevel(LogLevelEnum logLevel) {
            TSLogger.Warning($"setting TS log level to {logLevel}");
            switch (logLevel) {
                case LogLevelEnum.TRACE: levelSwitch.MinimumLevel = LogEventLevel.Verbose; break;
                case LogLevelEnum.DEBUG: levelSwitch.MinimumLevel = LogEventLevel.Debug; break;
                case LogLevelEnum.INFO: levelSwitch.MinimumLevel = LogEventLevel.Information; break;
                case LogLevelEnum.WARNING: levelSwitch.MinimumLevel = LogEventLevel.Warning; break;
                case LogLevelEnum.ERROR: levelSwitch.MinimumLevel = LogEventLevel.Error; break;
                default: levelSwitch.MinimumLevel = LogEventLevel.Information; break;
            }
        }

        public static void CloseAndFlush() {
            (TSLog as IDisposable)?.Dispose();
        }

        public static void Error(
                Exception ex,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {
            TSLog.Error(ex, "{source}|{member}|{line}", ExtractFileName(sourceFilePath), memberName, lineNumber);
        }

        public static void Error(
                string customMsg,
                Exception ex,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {
            TSLog.Error(ex, "{source}|{member}|{line}|{message}", ExtractFileName(sourceFilePath), memberName, lineNumber, customMsg);
        }

        public static void Error(string message,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {
            TSLog.Error("{source}|{member}|{line}|{message}", ExtractFileName(sourceFilePath), memberName, lineNumber, message);
        }

        public static void Warning(string message,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {
            TSLog.Warning("{source}|{member}|{line}|{message}", ExtractFileName(sourceFilePath), memberName, lineNumber, message);
        }

        public static void Info(string message,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {
            TSLog.Information("{source}|{member}|{line}|{message}", ExtractFileName(sourceFilePath), memberName, lineNumber, message);
        }

        public static void Debug(string message,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {
            TSLog.Debug("{source}|{member}|{line}|{message}", ExtractFileName(sourceFilePath), memberName, lineNumber, message);
        }

        public static void Trace(string message,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {
            TSLog.Verbose("{source}|{member}|{line}|{message}", ExtractFileName(sourceFilePath), memberName, lineNumber, message);
        }

        private static string ExtractFileName(string sourceFilePath) {
            string file = string.Empty;
            try { file = Path.GetFileName(sourceFilePath); } catch (Exception) { }
            return file;
        }

        private class HeaderWriter : FileLifecycleHooks {

            // Factory method to generate the file header
            private readonly Func<string> headerFactory;

            public HeaderWriter(Func<string> headerFactory) {
                this.headerFactory = headerFactory;
            }

            public override Stream OnFileOpened(Stream underlyingStream, Encoding encoding) {
                using (var writer = new StreamWriter(underlyingStream, encoding, 1024, true)) {
                    var header = this.headerFactory();

                    writer.WriteLine(header);
                    writer.Flush();
                    underlyingStream.Flush();
                }

                return base.OnFileOpened(underlyingStream, encoding);
            }
        }

        private class LegacyLogLevelMappingEnricher : ILogEventEnricher {

            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) {
                string LegacyLogLevel = string.Empty;

                switch (logEvent.Level) {
                    case LogEventLevel.Verbose:
                        LegacyLogLevel = LogLevelEnum.TRACE.ToString();
                        break;

                    case LogEventLevel.Debug:
                        LegacyLogLevel = LogLevelEnum.DEBUG.ToString();
                        break;

                    case LogEventLevel.Information:
                        LegacyLogLevel = LogLevelEnum.INFO.ToString();
                        break;

                    case LogEventLevel.Warning:
                        LegacyLogLevel = LogLevelEnum.WARNING.ToString();
                        break;

                    case LogEventLevel.Error:
                        LegacyLogLevel = LogLevelEnum.ERROR.ToString();
                        break;

                    case LogEventLevel.Fatal:
                        LegacyLogLevel = "FATAL";
                        break;

                    default:
                        LegacyLogLevel = "UNKNOWN";
                        break;
                }

                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LegacyLogLevel", LegacyLogLevel));
            }
        }
    }
}