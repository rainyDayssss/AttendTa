using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// A hook an owning module implements to react to Attendance invalidation (AD-2). The
    /// invalidation use case invokes every registered implementation exactly once, inside its
    /// transaction, after the attendance row is locked and before the stamp is saved, so a handler
    /// can change related state in the same unit of work and a thrown <c>Forbidden</c> rolls the
    /// whole invalidation back. An empty handler list is valid.
    /// </summary>
    public interface IAttendanceInvalidationHandler
    {
        Task HandleAsync(AttendanceInvalidationContext context, CancellationToken cancellationToken = default);
    }
}
