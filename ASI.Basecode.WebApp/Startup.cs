using ASI.Basecode.Data;
using ASI.Basecode.WebApp.Extensions.Configuration;
using ASI.Basecode.WebApp.Hotspot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;
using Serilog.Context;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp
{
    /// <summary>
    /// For configuring services on application startup.
    /// </summary>
    /// <remarks>
    /// <para>Method call sequence for instances of this class:</para>
    /// <para>1. constructor</para>
    /// <para>2. <see cref="ConfigureServices(IServiceCollection)"/></para>
    /// <para>3. (create <see cref="IApplicationBuilder"/> instance)</para>
    /// <para>4. <see cref="ConfigureApp(IApplicationBuilder, IWebHostEnvironment)"/></para>
    /// </remarks>
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        private IConfiguration Configuration { get; }

        private IApplicationBuilder _app;

        private IWebHostEnvironment _environment;

        private IServiceCollection _services;

        /// <summary>
        /// Initialize new <see cref="StartupConfigurer"/> instance using <paramref name="configuration"/>
        /// </summary>
        /// <param name="configuration"></param>
        public StartupConfigurer(IConfiguration configuration)
        {
            this.Configuration = configuration;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Use this method to add services to the container.
        /// </summary>
        /// <param name="services">Services</param>
        public void ConfigureServices(IServiceCollection services)
        {
            this._services = services;

            services.AddMemoryCache();

            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not configured. Set the "
                    + "ConnectionStrings__DefaultConnection environment variable (or a user secret) "
                    + "before starting AttendTa.");
            }

            // Register SQL database configuration context as services.
            services.AddDbContext<AsiBasecodeDBContext>(options =>
            {
                options.UseSqlServer(
                    connectionString,
                    sqlServerOptions => sqlServerOptions.CommandTimeout(120));
            });

            services.AddControllersWithViews();
            services.AddRazorPages().AddRazorRuntimeCompilation();

            this.ConfigureDataProtection();

            // DI Services AutoMapper(Add Profile)
            this.ConfigureAutoMapper();

            // DI Services
            this.ConfigureOtherServices();

            // Authorization (cookie authentication and named policies)
            this.ConfigureAuthorization();

            services.Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = 1024 * 1024 * 100;
            });

            services.AddSingleton<IFileProvider>(serviceProvider =>
            {
                var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
                var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
                return new PhysicalFileProvider(webRoot);
            });
        }

        /// <summary>
        /// Configure application
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void ConfigureApp(IApplicationBuilder app, IWebHostEnvironment env)
        {
            this._app = app;
            this._environment = env;

            if (!this._environment.IsDevelopment())
            {
                this._app.UseHsts();
            }

            this.ConfigureLogger();

            this._app.Use(async (context, next) =>
            {
                var correlationId = Guid.NewGuid().ToString("N");
                context.Response.Headers["X-Correlation-ID"] = correlationId;
                using (LogContext.PushProperty("CorrelationId", correlationId))
                {
                    try { await next(); }
                    finally
                    {
                        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AttendTa.Request");
                        logger.LogInformation("Request completed with status {StatusCode}", context.Response.StatusCode);
                    }
                }
            });

            // PLAIN ENGLISH: In hotspot mode the phone must use HTTPS, but the laptop's own
            // readiness check still uses HTTP. We skip the automatic HTTP-to-HTTPS redirect so that
            // check keeps working.
            // Hotspot mode serves HTTPS directly on the hotspot adapter IP. Redirecting the loopback
            // HTTP readiness endpoint would present a certificate that may not name 127.0.0.1, so
            // the redirect stays off while hotspot HTTPS is on.
            if (!HotspotHttpsOptions.FromConfiguration(this.Configuration).Enabled)
            {
                this._app.UseHttpsRedirection();
            }

            this._app.UseStaticFiles();

            // Localization
            var options = this._app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            this._app.UseRequestLocalization(options.Value);

            this._app.UseRouting();

            this._app.UseAuthentication();
            this._app.UseAuthorization();
        }

        /// <summary>
        /// Persists Data Protection keys under <c>Common:SetupRoot/keys</c> so cookies survive restarts.
        /// </summary>
        private void ConfigureDataProtection()
        {
            var setupRoot = Configuration.GetSetupRootDirectoryPath();
            if (string.IsNullOrWhiteSpace(setupRoot))
            {
                throw new InvalidOperationException(
                    "Common:SetupRoot must be configured so Data Protection keys can be stored under its keys folder.");
            }

            var keyDirectory = new DirectoryInfo(Path.Combine(setupRoot, "keys"));
            keyDirectory.Create();

            this._services.AddDataProtection()
                .PersistKeysToFileSystem(keyDirectory)
                .SetApplicationName("AttendTa");
        }
    }
}
