using System.IO;
using ASI.Basecode.WebApp.Extensions.Configuration;
using ASI.Basecode.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Filters;

namespace ASI.Basecode.WebApp
{
    // Logger configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configure the logger
        /// </summary>
        private void ConfigureLogger()
        {
            var loggerFactory = this._app.ApplicationServices.GetService<ILoggerFactory>();

            var logDir = Path.Combine(
                Configuration.GetSetupRootDirectoryPath(), "logs",
                System.DateTime.Today.ToString("yyyyMM"));

            var defaultLogLevel = Configuration.GetLoggingLogLevel();
            var logFileSize = long.Parse(Configuration.GetLogFileSize());
            int? retained = null; //Retain all files
            var logEventLevel = (Serilog.Events.LogEventLevel)defaultLogLevel;
            var serilogger = new LoggerConfiguration()
                            .MinimumLevel.Is(logEventLevel)
                            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
                            .Enrich.FromLogContext()
                            .WriteTo.File(Path.Combine(logDir, "log-.txt"),
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                                rollingInterval: RollingInterval.Day,
                                fileSizeLimitBytes: logFileSize,
                                rollOnFileSizeLimit: true,
                                retainedFileCountLimit: retained)
                            .WriteTo.Logger(lc => lc
                                            .Filter.ByIncludingOnly(le => le.Level == Serilog.Events.LogEventLevel.Debug)
                                            .WriteTo.File(Path.Combine(logDir, "Debug_.txt"),
                                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: retained))
                            .WriteTo.Logger(lc => lc
                                            .Filter.ByIncludingOnly(le => le.Level == Serilog.Events.LogEventLevel.Warning)
                                            .WriteTo.File(Path.Combine(logDir, "Warning_.txt"),
                                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: retained))
                            .WriteTo.Logger(lc => lc
                                            .Filter.ByIncludingOnly(le => le.Level == Serilog.Events.LogEventLevel.Error)
                                            .WriteTo.File(Path.Combine(logDir, "Error_.txt"),
                                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: retained))
                            .WriteTo.Logger(lc => lc
                                            .MinimumLevel.Debug()
                                            .Filter.ByIncludingOnly(Matching.FromSource<ServiceBase>())
                                            .WriteTo.File(Path.Combine(logDir, "Services_.txt"),
                                                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose,
                                                outputTemplate: "[{CorrelationId}] {Message}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: retained))
                            .CreateLogger();

            loggerFactory.AddSerilog(serilogger);

            var seqConfig = Configuration.GetSection("Seq");

            loggerFactory.AddSeq(seqConfig);
        }
    }
}
