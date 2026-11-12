using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ASI.Basecode.Data.Repositories;

public sealed class AccountAdministrationRepository(IUnitOfWork unitOfWork) : BaseRepository(unitOfWork), IAccountAdministrationRepository
{
    private const string LastAdministratorLockResource = "attendta:last-admin";
    private const int LastAdministratorLockTimeoutMilliseconds = 5000;

    public Task<int> CountAsync(string search, CancellationToken cancellationToken = default) =>
        Filter(search).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountListItem>> GetPageAsync(
        string search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));

        return await (from user in Filter(search)
                      join role in GetDbSet<RoleLookup>().AsNoTracking() on user.RoleId equals role.RoleId
                      orderby user.Name, user.Id
                      select new AccountListItem
                      {
                          AccountId = user.Id,
                          FullName = user.Name,
                          Email = user.UserId,
                          RoleCode = role.Code,
                          RoleLabel = role.Label,
                          IsActive = user.IsActive
                      })
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountListItem> GetAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        if (accountId <= 0)
        {
            return null;
        }

        return await (from user in GetDbSet<User>().AsNoTracking()
                      join role in GetDbSet<RoleLookup>().AsNoTracking() on user.RoleId equals role.RoleId
                      where user.Id == accountId
                      select new AccountListItem
                      {
                          AccountId = user.Id,
                          FullName = user.Name,
                          Email = user.UserId,
                          RoleCode = role.Code,
                          RoleLabel = role.Label,
                          IsActive = user.IsActive
                      })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleOption>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        await GetDbSet<RoleLookup>()
            .AsNoTracking()
            .OrderBy(role => role.RoleId)
            .Select(role => new RoleOption
            {
                RoleId = role.RoleId,
                Code = role.Code,
                Label = role.Label
            })
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        GetDbSet<User>()
            .AsNoTracking()
            .CountAsync(user => user.RoleId == RoleCode.Admin && user.IsActive, cancellationToken);

    public async Task AcquireLastAdministratorLockAsync(CancellationToken cancellationToken = default)
    {
        var transaction = Context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A transaction is required to take the last-administrator lock.");

        var command = Context.Database.GetDbConnection().CreateCommand();
        await using (command)
        {
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Transaction = transaction.GetDbTransaction();

            AddParameter(command, "@Resource", DbType.String, 255, LastAdministratorLockResource);
            AddParameter(command, "@LockMode", DbType.String, 32, "Exclusive");
            AddParameter(command, "@LockOwner", DbType.String, 32, "Transaction");
            AddParameter(command, "@LockTimeout", DbType.Int32, null, LastAdministratorLockTimeoutMilliseconds);

            var result = command.CreateParameter();
            result.ParameterName = "@result";
            result.DbType = DbType.Int32;
            result.Direction = ParameterDirection.ReturnValue;
            command.Parameters.Add(result);

            await command.ExecuteNonQueryAsync(cancellationToken);

            if (Convert.ToInt32(result.Value) < 0)
            {
                throw new DomainException(OutcomeCode.Stale, "Another role change is in progress. Try again.");
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, int? size, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private IQueryable<User> Filter(string search)
    {
        var query = GetDbSet<User>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.Name.Contains(term) || user.UserId.Contains(term));
        }

        return query;
    }
}
