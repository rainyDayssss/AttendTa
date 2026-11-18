using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Unit of Work Interface
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Gets the database context
        /// </summary>
        /// <value>
        /// The database.
        /// </value>
        DbContext Database { get; }

        /// <summary>
        /// Gets a value indicating whether this unit of work is currently inside a database
        /// transaction, whether it owns the transaction or joined an ambient one.
        /// </summary>
        bool HasActiveTransaction => Database is not null && Database.Database.CurrentTransaction is not null;

        /// <summary>
        /// Saves the changes to database
        /// </summary>
        void SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
        Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
    }
}
