using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// A hook an owning module implements to react to Event completion (AD-2). The completion
    /// use case invokes every registered implementation exactly once, inside its transaction and
    /// after the event row is saved, so a handler sees the Completed state and its exception rolls
    /// the whole completion back. An empty handler list is valid.
    /// </summary>
    public interface IEventCompletionHandler
    {
        Task HandleAsync(EventCompletionContext context, CancellationToken cancellationToken = default);
    }
}
