using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories;

public sealed class AccountRequestRepository(IUnitOfWork unitOfWork) : BaseRepository(unitOfWork), IAccountRequestRepository
{
    public Task<int> CountByStatusAsync(AccountRequestStatusCode? status, string search, CancellationToken cancellationToken = default) =>
        FilterByStatus(status, search).CountAsync(cancellationToken);

    public async Task<AccountRequestStatusCounts> GetStatusCountsAsync(string search, CancellationToken cancellationToken = default)
    {
        var counts = await FilterByStatus(null, search)
            .GroupBy(request => request.AccountRequestStatusId)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return new AccountRequestStatusCounts(
            counts.Where(item => item.Status == AccountRequestStatusCode.Pending).Sum(item => item.Count),
            counts.Where(item => item.Status == AccountRequestStatusCode.Approved).Sum(item => item.Count),
            counts.Where(item => item.Status == AccountRequestStatusCode.Rejected).Sum(item => item.Count));
    }

    public async Task<IReadOnlyList<AccountRequestQueueItem>> GetPageByStatusAsync(
        AccountRequestStatusCode? status,
        string search,
        bool oldestFirst,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));

        var query = FilterByStatus(status, search);
        query = oldestFirst
            ? query.OrderBy(request => request.RequestedAt).ThenBy(request => request.RequestId)
            : query.OrderByDescending(request => request.RequestedAt).ThenByDescending(request => request.RequestId);

        return await query
            .Skip(skip)
            .Take(take)
            .Select(request => new AccountRequestQueueItem
            {
                RequestId = request.RequestId,
                StudentNumber = request.StudentNumber,
                FullName = request.FullName,
                Email = request.Email,
                Course = request.Course,
                YearLevel = request.YearLevel,
                Section = request.Section,
                MobileNumber = request.MobileNumber,
                Status = request.AccountRequestStatusId,
                RequestedAt = request.RequestedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountRequest> LockAccountRequestAsync(int requestId, CancellationToken cancellationToken = default)
    {
        if (Context.Database.CurrentTransaction == null) throw new InvalidOperationException("A transaction is required to lock an account request.");
        var row = await GetDbSet<AccountRequest>().FromSqlInterpolated($"SELECT * FROM dbo.account_requests WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE request_id = {requestId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var tracked = GetDbSet<AccountRequest>().Local.FirstOrDefault(x => x.RequestId == requestId);
        if (row == null) { if (tracked != null) Context.Entry(tracked).State = EntityState.Detached; return null; }
        if (tracked == null) { GetDbSet<AccountRequest>().Attach(row); return row; }
        Context.Entry(tracked).CurrentValues.SetValues(row);
        Context.Entry(tracked).OriginalValues.SetValues(row);
        Context.Entry(tracked).State = EntityState.Unchanged;
        return tracked;
    }

    public Task AddAsync(AccountRequest request, CancellationToken cancellationToken = default)
    {
        GetDbSet<AccountRequest>().Add(request);
        return Task.CompletedTask;
    }

    public Task<bool> PendingEmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(false);
        }

        return GetDbSet<AccountRequest>()
            .AsNoTracking()
            .AnyAsync(
                request => request.AccountRequestStatusId == AccountRequestStatusCode.Pending
                    && request.Email == email,
                cancellationToken);
    }

    private IQueryable<AccountRequest> FilterByStatus(AccountRequestStatusCode? status, string search)
    {
        var query = GetDbSet<AccountRequest>().AsNoTracking();
        if (status is not null)
        {
            query = query.Where(request => request.AccountRequestStatusId == status);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(request =>
                request.FullName.Contains(term)
                || request.Email.Contains(term)
                || request.StudentNumber.Contains(term));
        }

        return query;
    }

    public Task<bool> PendingStudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentNumber))
        {
            return Task.FromResult(false);
        }

        return GetDbSet<AccountRequest>()
            .AsNoTracking()
            .AnyAsync(
                request => request.AccountRequestStatusId == AccountRequestStatusCode.Pending
                    && request.StudentNumber == studentNumber,
                cancellationToken);
    }
}
