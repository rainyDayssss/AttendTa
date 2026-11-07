using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

// M-11b/M-12: one document path for ledger and owner reads. Callers authorize before invoking it.
public sealed class DemoCertificateDocuments(DemoStore store, ICertificatePdfRenderer renderer)
{
    public async Task<CertificateDocument> GetPdfAsync(int registrationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!store.TryGetIssuedCertificate(registrationId, out var issue) || issue.Snapshot == null)
            throw new DomainException(OutcomeCode.NotFound, "Certificate not found.");
        if (issue.Pdf == null)
        {
            var bytes = await RenderAsync(issue.Snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            issue = store.AttachCertificateDocument(registrationId, issue.Snapshot, bytes);
        }
        return new CertificateDocument((byte[])issue.Pdf.Clone(), issue.CertificateNumber);
    }

    public async Task<byte[]> RenderAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await renderer.RenderSnapshotAsync(snapshot, cancellationToken);
            if (bytes == null || bytes.Length < 5 || bytes[0] != '%' || bytes[1] != 'P' || bytes[2] != 'D' || bytes[3] != 'F' || bytes[4] != '-')
                throw new InvalidOperationException("The renderer did not return a PDF.");
            return bytes;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            throw new DomainException(OutcomeCode.Invalid, "The certificate could not be rendered. Please try again.");
        }
    }
}
