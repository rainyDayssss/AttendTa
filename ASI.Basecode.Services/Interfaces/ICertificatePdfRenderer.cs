using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>Renders fixed certificate snapshots.</summary>
public interface ICertificatePdfRenderer
{
    /// <summary>Renders the fixed certificate template from captured values.</summary>
    Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default);
}
