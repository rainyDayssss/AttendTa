using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Certificates;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.WebApp.DemoData;
using ASI.Basecode.WebApp.Rendering;
using ASI.Basecode.WebApp.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace ASI.Basecode.WebApp
{
    // Other services configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configures the other services.
        /// </summary>
        private void ConfigureOtherServices()
        {
            // Framework
            this._services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Common
            this._services.AddScoped<IUnitOfWork, UnitOfWork>();
            this._services.TryAddSingleton(TimeProvider.System);
            this._services.AddScoped<IActivityLogWriter, ActivityLogWriter>();

            // Services
            this._services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
            this._services.AddScoped<IAccountService, AccountService>();
            this._services.AddScoped<IAccountRequestQueueService, AccountRequestQueueService>();
            this._services.AddScoped<IActivityLogReaderService, ActivityLogReaderService>();
            this._services.AddScoped<IAccountRequestService, AccountRequestService>();
            this._services.AddScoped<IAccountAdministrationService, AccountAdministrationService>();
            this._services.AddScoped<IAccountRoleService, AccountRoleService>();
            this._services.AddScoped<IAccessDialogService, AccessDialogService>();
            this._services.AddScoped<AccountAccessService>();
            this._services.AddScoped<IAccountAccessCommands, AccountAccessService>();
            this._services.AddScoped<MembershipAccessService>();
            this._services.AddScoped<IMembershipAccessCommands, MembershipAccessService>();
            this._services.AddScoped<IMemberService, MemberService>();
            this._services.AddScoped<IEventTypeService, EventTypeService>();
            this._services.AddScoped<IVenueService, VenueService>();
            this._services.AddScoped<IEventLifecycleService, EventLifecycleService>();
            this._services.AddScoped<IMemberEventService, MemberEventService>();
            this._services.AddScoped<IRegistrationCancellationService, RegistrationCancellationService>();
            this._services.AddScoped<IRegistrationReviewService, RegistrationReviewService>();
            this._services.AddScoped<IAttendanceRecordingService, AttendanceRecordingService>();
            this._services.AddScoped<ICheckInService, CheckInService>();
            this._services.AddScoped<ICheckInLookupService, CheckInLookupService>();
            this._services.AddScoped<IAttendanceInvalidationService, AttendanceInvalidationService>();
            this._services.AddScoped<ICertificateTracerService, CertificateTracerService>();
            // The real certificate ledger serves both demo switch states; the demo registration
            // replaces the interface with its issued-state overlay when the switch is on.
            this._services.AddScoped<CertificateLedgerService>();
            this._services.AddScoped<ICertificateLedgerService>(services => services.GetRequiredService<CertificateLedgerService>());
            this._services.AddScoped<IMyEventsService, MyEventsService>();
            this._services.AddScoped<IPassService, PassService>();
            this._services.AddScoped<ParticipationReportService>();
            this._services.AddScoped<IParticipationReportService>(services => services.GetRequiredService<ParticipationReportService>());

            // Event completion hooks (AD-2). The completion use case invokes every registration inside
            // its transaction; each handler shares the caller's unit of work.
            this._services.AddScoped<IEventCompletionHandler, CertificateReviewCompletionHandler>();

            // Attendance invalidation hooks (AD-2). The invalidation use case invokes every
            // registered handler inside its transaction after the event lock is held; each handler
            // shares the caller's unit of work.
            this._services.AddScoped<IAttendanceInvalidationHandler, CertificateReviewInvalidationHandler>();

            // Repositories
            this._services.AddScoped<IUserRepository, UserRepository>();
            this._services.AddScoped<IAccountRequestRepository, AccountRequestRepository>();
            this._services.AddScoped<IActivityLogReaderRepository, ActivityLogReaderRepository>();
            this._services.AddScoped<IAccountAdministrationRepository, AccountAdministrationRepository>();
            this._services.AddScoped<IMemberRepository, MemberRepository>();
            this._services.AddScoped<IEventTypeRepository, EventTypeRepository>();
            this._services.AddScoped<IVenueRepository, VenueRepository>();
            this._services.AddScoped<IEventRepository, EventRepository>();
            this._services.AddScoped<IRegistrationRepository, RegistrationRepository>();
            this._services.AddScoped<IAttendanceRepository, AttendanceRepository>();
            this._services.AddScoped<IParticipationReportRepository, ParticipationReportRepository>();
            this._services.AddScoped<ICertificateRepository, CertificateRepository>();

            // Shared server-local storage for private images, certificates and renderer files.
            this._services.AddScoped<IPrivateFileStore, PrivateFileStore>();
            this._services.AddScoped<ICertificatePdfRenderer, PlaywrightCertificatePdfRenderer>();
            this._services.AddSingleton<IQrPassPngRenderer, QrPassPngRenderer>();

            this._services.AddOptions<CertificateSignatoryOptions>()
                .Bind(this.Configuration.GetSection(CertificateSignatoryOptions.SectionName))
                .Validate(
                    options => string.Equals(options.Name, CertificateSignatoryOptions.ApprovedName, StringComparison.Ordinal),
                    $"{CertificateSignatoryOptions.SectionName}:Name must match the approved certificate signatory.")
                .Validate(
                    options => string.Equals(options.Title, CertificateSignatoryOptions.ApprovedTitle, StringComparison.Ordinal),
                    $"{CertificateSignatoryOptions.SectionName}:Title must match the approved certificate signatory title.")
                .Validate(
                    options => string.Equals(options.PackagedSignatureAsset, CertificateSignatoryOptions.SignatureAssetPath, StringComparison.Ordinal),
                    $"{CertificateSignatoryOptions.SectionName}:PackagedSignatureAsset must name the packaged approved signature asset.");

            // Current request actor for service-layer authorization and audit.
            this._services.AddScoped<ICurrentActor, HttpCurrentActor>();

            // Demo data seam. The switch is off in committed settings; the startup guard has already
            // refused a wrong database before this runs.
            this._services.AddDemoData(this.Configuration);

            this._services.AddHttpClient();
        }
    }
}
