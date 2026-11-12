using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Email and password sign-in against the canonical <c>users</c> table.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Authenticates an active account by email and password.
        /// </summary>
        /// <param name="email">The account email.</param>
        /// <param name="password">The plaintext password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The authenticated account, or <c>null</c> for an unknown email, a wrong password or an
        /// inactive account. Callers must show one generic failure message.
        /// </returns>
        Task<AuthenticatedUser> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reloads an account to re-check its current active state and role.
        /// </summary>
        /// <param name="userId">The <c>users.user_id</c>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current account, or <c>null</c> when it is missing or inactive.</returns>
        Task<AuthenticatedUser> GetActiveUserAsync(int userId, CancellationToken cancellationToken = default);
    }
}
