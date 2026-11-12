using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Email and password sign-in against the canonical <c>users</c> table.
    /// </summary>
    public class AccountService : IAccountService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountService"/> class.
        /// </summary>
        /// <param name="userRepository">The user repository.</param>
        /// <param name="passwordHasher">The single password hasher.</param>
        public AccountService(IUserRepository userRepository, IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }

        /// <inheritdoc />
        public async Task<AuthenticatedUser> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            var user = await _userRepository.GetByEmailAsync(email.Trim(), cancellationToken);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            if (!_passwordHasher.Verify(password, user.Password))
            {
                return null;
            }

            return await ToAuthenticatedUserAsync(user, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<AuthenticatedUser> GetActiveUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return null;
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            return await ToAuthenticatedUserAsync(user, cancellationToken);
        }

        private async Task<AuthenticatedUser> ToAuthenticatedUserAsync(User user, CancellationToken cancellationToken)
        {
            var roleCode = await _userRepository.GetRoleCodeAsync(user.RoleId, cancellationToken);
            if (string.IsNullOrWhiteSpace(roleCode))
            {
                return null;
            }

            return new AuthenticatedUser
            {
                UserId = user.Id,
                Email = user.UserId,
                FullName = user.Name,
                RoleCode = roleCode
            };
        }
    }
}
