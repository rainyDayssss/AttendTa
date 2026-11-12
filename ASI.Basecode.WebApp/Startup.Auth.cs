using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp
{
    // Authorization configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configure cookie authentication and the named authorization policies.
        /// </summary>
        private void ConfigureAuthorization()
        {
            this._services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = AuthCookie.Name;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.ExpireTimeSpan = TimeSpan.FromHours(AuthCookie.LifetimeHours);
                    options.SlidingExpiration = false;
                    options.LoginPath = new PathString("/Account/Login");
                    options.AccessDeniedPath = new PathString("/Account/AccessDenied");
                    options.ReturnUrlParameter = "ReturnUrl";
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = ValidatePrincipalAsync
                    };
                });

            this._services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

            this._services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                options.AddPolicy(Policies.Management, policy =>
                    policy.RequireRole(RoleCodes.Admin, RoleCodes.Officer));

                options.AddPolicy(Policies.Administrator, policy =>
                    policy.RequireRole(RoleCodes.Admin));

                options.AddPolicy(Policies.Member, policy =>
                    policy.RequireRole(RoleCodes.Member));
            });
        }

        /// <summary>
        /// Reloads the account on every request and rejects an inactive or role-changed session.
        /// </summary>
        private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = principal?.FindFirst(ClaimTypes.Role)?.Value;

            if (!int.TryParse(userIdClaim, out var userId) || string.IsNullOrWhiteSpace(roleClaim))
            {
                await RejectPrincipalAsync(context);
                return;
            }

            var accountService = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
            var currentUser = await accountService.GetActiveUserAsync(userId, context.HttpContext.RequestAborted);

            if (currentUser is null || !string.Equals(currentUser.RoleCode, roleClaim, StringComparison.Ordinal))
            {
                await RejectPrincipalAsync(context);
            }
        }

        private static async Task RejectPrincipalAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
