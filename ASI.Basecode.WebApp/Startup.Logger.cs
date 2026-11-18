using System;
using System.IO;
using ASI.Basecode.Services;
using ASI.Basecode.WebApp.Configuration;
using ASI.Basecode.WebApp.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Filters;

namespace ASI.Basecode.WebApp
{
    // Logger configuration
    internal partial class StartupConfigurer
    {
        // Keep roughly a month of rolled log files instead of retaining them forever.
        private const int RetainedLogFileCount = 31;

        /// <summary>
        /// Configure the logger
        /// </summary>
        private void ConfigureLogger()
        {
            var loggerFactory = this._app.ApplicationServices.GetService<ILoggerFactory>();

            var setupRoot = SetupRootPath.Resolve(this.Configuration, this._environment);
            var logDir = Path.Combine(
                setupRoot, "logs",
                DateTime.Today.ToString("yyyyMM"));

            var defaultLogLevel = Configuration.GetLoggingLogLevel();
            var logFileSize = long.Parse(Configuration.GetLogFileSize());
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
                                retainedFileCountLimit: RetainedLogFileCount)
                            .WriteTo.Logger(lc => lc
                                            .Filter.ByIncludingOnly(le => le.Level == Serilog.Events.LogEventLevel.Debug)
                                            .WriteTo.File(Path.Combine(logDir, "Debug_.txt"),
                                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: RetainedLogFileCount))
                            .WriteTo.Logger(lc => lc
                                            .Filter.ByIncludingOnly(le => le.Level == Serilog.Events.LogEventLevel.Warning)
                                            .WriteTo.File(Path.Combine(logDir, "Warning_.txt"),
                                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: RetainedLogFileCount))
                            .WriteTo.Logger(lc => lc
                                            .Filter.ByIncludingOnly(le => le.Level == Serilog.Events.LogEventLevel.Error)
                                            .WriteTo.File(Path.Combine(logDir, "Error_.txt"),
                                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: RetainedLogFileCount))
                            .WriteTo.Logger(lc => lc
                                            .MinimumLevel.Debug()
                                            .Filter.ByIncludingOnly(Matching.FromSource<ServiceBase>())
                                            .WriteTo.File(Path.Combine(logDir, "Services_.txt"),
                                                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose,
                                                outputTemplate: "[{CorrelationId}] {Message}{NewLine}{Exception}",
                                                rollingInterval: RollingInterval.Day,
                                                fileSizeLimitBytes: logFileSize,
                                                rollOnFileSizeLimit: true,
                                                retainedFileCountLimit: RetainedLogFileCount))
                            .CreateLogger();

            loggerFactory.AddSerilog(serilogger);

            // Flush and close the file sinks while the host is stopping so buffered events written
            // during shutdown are not lost. AddSerilog registered the logger without ownership, so
            // disposing the logger factory alone would not dispose it.
            var applicationLifetime = this._app.ApplicationServices.GetService<IHostApplicationLifetime>();
            applicationLifetime?.ApplicationStopping.Register(() => serilogger.Dispose());

            var seqConfig = Configuration.GetSection("Seq");

            loggerFactory.AddSeq(seqConfig);
        }
    }
}
