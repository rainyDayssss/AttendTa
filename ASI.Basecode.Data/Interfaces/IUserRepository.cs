using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IUserRepository
    {
        /// <summary>Reads every user row, tracked for the caller's unit of work.</summary>
        Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a user by the login identifier (<c>users.email</c>).
        /// </summary>
        Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a user by <c>users.user_id</c>.
        /// </summary>
        Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new user for insertion. The caller saves through the unit of work.
        /// </summary>
        Task AddAsync(User user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the lowercase lookup code for a role id.
        /// </summary>
        Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default);
    }
}
