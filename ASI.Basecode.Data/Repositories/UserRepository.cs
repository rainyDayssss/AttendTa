using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {

        }

        public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
        {
            return await this.GetDbSet<User>().ToListAsync(cancellationToken);
        }

        public Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Task.FromResult<User>(null);
            }

            return this.GetDbSet<User>()
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.UserId == email, cancellationToken);
        }

        public Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return Task.FromResult<User>(null);
            }

            return this.GetDbSet<User>()
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            this.GetDbSet<User>().Add(user);
            return Task.CompletedTask;
        }

        public async Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default)
        {
            var role = await this.GetDbSet<RoleLookup>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.RoleId == roleId, cancellationToken);

            return role?.Code;
        }

        public async Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (Context.Database.CurrentTransaction == null) throw new InvalidOperationException("A transaction is required to lock a user.");
            var row = await GetDbSet<User>().FromSqlInterpolated($"SELECT * FROM dbo.users WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE user_id = {userId}")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var tracked = GetDbSet<User>().Local.FirstOrDefault(x => x.Id == userId);
            if (row == null) { if (tracked != null) Context.Entry(tracked).State = EntityState.Detached; return null; }
            if (tracked == null) { GetDbSet<User>().Attach(row); return row; }
            Context.Entry(tracked).CurrentValues.SetValues(row);
            Context.Entry(tracked).OriginalValues.SetValues(row);
            Context.Entry(tracked).State = EntityState.Unchanged;
            return tracked;
        }
    }
}
