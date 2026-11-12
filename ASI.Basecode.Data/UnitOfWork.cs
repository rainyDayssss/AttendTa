using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    public DbContext Database { get; }
    private int depth;
    private ExceptionDispatchInfo failure;

    public UnitOfWork(AsiBasecodeDBContext serviceContext) => Database = serviceContext;

    private Exception Translate(Exception exception)
    {
        for (var e = exception; e != null; e = e.InnerException)
            if (e is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                var studentNumberUpdate = Database.ChangeTracker.Entries<Member>().Any(entry =>
                    entry.State == EntityState.Modified && entry.Property(member => member.StudentNumber).IsModified);
                return studentNumberUpdate
                    ? new DomainException(OutcomeCode.Duplicate, "Student number already exists.", nameof(Member.StudentNumber))
                    : new DomainException(OutcomeCode.Duplicate, "A record with these values already exists.");
            }
        return exception;
    }

    public void SaveChanges()
    {
        try { Database.SaveChanges(); }
        catch (Exception e) { var translated = Translate(e); if (translated == e) throw; throw translated; }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await Database.SaveChangesAsync(cancellationToken); }
        catch (Exception e) { var translated = Translate(e); if (translated == e) throw; throw translated; }
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync<object>(async ct => { await work(ct); return null; }, cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        var outer = depth == 0;
        var owned = outer && Database.Database.CurrentTransaction == null;
        if (outer) failure = null;
        var transaction = Database.Database.CurrentTransaction;
        if (owned) transaction = await Database.Database.BeginTransactionAsync(cancellationToken);
        depth++;
        try
        {
            var result = await work(cancellationToken);
            failure?.Throw();
            if (owned) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception e)
        {
            var translated = Translate(e);
            failure ??= ExceptionDispatchInfo.Capture(translated);
            if (outer || !owned && depth > 1)
            {
                try { if (Database.Database.CurrentTransaction != null) await transaction.RollbackAsync(CancellationToken.None); }
                catch { /* Preserve the original failure. */ }
                Database.ChangeTracker.Clear();
            }
            failure.Throw();
            throw;
        }
        finally
        {
            depth--;
            if (owned) await transaction.DisposeAsync();
            if (outer) failure = null;
        }
    }

    public void Dispose() => Database.Dispose();
}
