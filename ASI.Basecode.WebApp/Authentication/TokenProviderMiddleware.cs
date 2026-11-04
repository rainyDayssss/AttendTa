using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using ASI.Basecode.Resources.Constants;
using System.Threading.RateLimiting;

namespace ASI.Basecode.WebApp.Authentication
{
    /// <summary>
    /// Token Provider Middleware
    /// </summary>
    public class TokenProviderMiddleware
    {
        private static readonly PartitionedRateLimiter<HttpContext> RequestLimiter =
            PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        private readonly RequestDelegate _next;
        private readonly TokenProviderOptions _options;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly TokenProvider _tokenProvider;
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// Initializes a new instance of the TokenProviderMiddleware class.
        /// </summary>
        /// <param name="next">Request</param>
        /// <param name="options">Options</param>
        public TokenProviderMiddleware(RequestDelegate next, IOptions<TokenProviderOptions> options, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _options = options.Value;
            _scopeFactory = scopeFactory;
            _tokenProvider = new TokenProvider(Options.Create(_options));

            ThrowIfInvalidOptions(_options);
            _serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
        }

        /// <summary>
        /// Invokes the specified context.
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <returns>Task</returns>
        public async Task Invoke(HttpContext context)
        {
            // If the request path doesn't match, skip
            if (!context.Request.Path.Equals(_options.Path, StringComparison.Ordinal))
            {
                await _next(context);
                return;
            }

            // Request must be POST with Content-Type: application/x-www-form-urlencoded
            if (!context.Request.Method.Equals("POST"))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Bad request.");
                return;
            }

            using var lease = await RequestLimiter.AcquireAsync(context, 1);
            if (!lease.IsAcquired)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Too many authentication attempts. Try again later.");
                return;
            }

            await GenerateTokenUser(context);
        }

        /// <summary>
        /// Generates the user token
        /// </summary>
        /// <param name="context">HttpContext</param>
        private async Task GenerateTokenUser(HttpContext context)
        {
            string encodedJwt;
            var now = DateTime.UtcNow;
            if (!context.Request.HasFormContentType)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Form content is required.");
                return;
            }
            var email = context.Request.Form["email"].ToString();
            if (string.IsNullOrWhiteSpace(email)) email = context.Request.Form["username"].ToString();
            var password = context.Request.Form["password"];
            using var scope = _scopeFactory.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserService>();
            var user = users.AuthenticateUser(email, password);
            if (user == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid username or password.");
                return;
            }

            var identity = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString(), ClaimValueTypes.Integer32, Const.Issuer),
                new Claim(ClaimTypes.Name, user.FullName, ClaimValueTypes.String, Const.Issuer),
                new Claim(ClaimTypes.Email, user.Email, ClaimValueTypes.String, Const.Issuer),
                new Claim(ClaimTypes.Role, user.Role, ClaimValueTypes.String, Const.Issuer),
                new Claim("user_id", user.UserId.ToString(), ClaimValueTypes.Integer32, Const.Issuer),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, await _options.NonceGenerator()),
                new Claim(JwtRegisteredClaimNames.Iat, ToUnixEpochDate(now).ToString(), ClaimValueTypes.Integer64)
            }, Const.AuthenticationScheme);
            encodedJwt = _tokenProvider.GetJwtSecurityToken(identity, _options);

            var response = new
            {
                access_token = encodedJwt,
                expires_in = (int)_options.Expiration.TotalSeconds
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(response, _serializerSettings));
        }

        /// <summary>
        /// Throws if invalid options.
        /// </summary>
        /// <param name="options">The options.</param>
        private static void ThrowIfInvalidOptions(TokenProviderOptions options)
        {
            if (string.IsNullOrEmpty(options.Path))
            {
                throw new ArgumentNullException(nameof(TokenProviderOptions.Path));
            }

            if (string.IsNullOrEmpty(options.Issuer))
            {
                throw new ArgumentNullException(nameof(TokenProviderOptions.Issuer));
            }

            if (string.IsNullOrEmpty(options.Audience))
            {
                throw new ArgumentNullException(nameof(TokenProviderOptions.Audience));
            }

            if (options.Expiration == TimeSpan.Zero)
            {
                throw new ArgumentException("Must be a non-zero TimeSpan.", nameof(TokenProviderOptions.Expiration));
            }

            if (options.SigningCredentials == null)
            {
                throw new ArgumentNullException(nameof(TokenProviderOptions.SigningCredentials));
            }

            if (options.NonceGenerator == null)
            {
                throw new ArgumentNullException(nameof(TokenProviderOptions.NonceGenerator));
            }
        }

        /// <summary>
        /// Converts to unix epoch date.
        /// </summary>
        /// <param name="date">Date</param>
        /// <returns>Parsed date</returns>
        public static long ToUnixEpochDate(DateTime date) => new DateTimeOffset(date).ToUniversalTime().ToUnixTimeSeconds();
    }
}
