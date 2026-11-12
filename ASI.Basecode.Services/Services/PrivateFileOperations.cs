using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services;

public static class PrivateFileOperations
{
    public static async Task<string> SaveImageAsync(this IPrivateFileStore store, PrivateFileArea area, Stream content, string extension, string field, CancellationToken cancellationToken)
    {
        try { return await store.SaveAsync(area, content, extension, cancellationToken); }
        catch (InvalidDataException exception) { throw new DomainException(OutcomeCode.Invalid, exception.Message, field); }
    }

    public static async Task<Stream> OpenSafelyAsync(this IPrivateFileStore store, string key, CancellationToken cancellationToken)
    {
        try { return await store.OpenReadAsync(key, cancellationToken); }
        catch (InvalidDataException) { throw new DomainException(OutcomeCode.NotFound, "File not found."); }
    }
}
