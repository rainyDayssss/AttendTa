using System;
using System.Security.Claims;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ASI.Basecode.WebApp.Authentication
{
    /// <summary>
    /// Reads the authenticated actor from the current request's claims principal.
    /// </summary>
    public sealed class HttpCurrentActor : ICurrentActor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpCurrentActor"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        public HttpCurrentActor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <inheritdoc />
        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        /// <inheritdoc />
        public int UserId
        {
            get
            {
                var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(claimValue, out var userId) || userId <= 0)
                {
                    throw new InvalidOperationException("The current request has no authenticated user.");
                }

                return userId;
            }
        }

        /// <inheritdoc />
        public string Role => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
    }
}
