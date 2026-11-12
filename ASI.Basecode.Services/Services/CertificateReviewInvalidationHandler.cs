using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// Keeps the certificate review honest when attendance is invalidated (AD-2, AD-4). It runs inside
/// the caller's invalidation transaction and under the event lock the caller already holds, and it
/// saves through the caller's unit of work, so the review change, the attendance stamp and the
/// single <c>attendance.invalidated</c> audit entry commit or roll back together. An issued
/// certificate refuses the invalidation; a review that is absent or already not eligible is left
/// untouched. It never opens a transaction and never writes an audit entry.
/// </summary>
public sealed class CertificateReviewInvalidationHandler : IAttendanceInvalidationHandler
{
    private const string IssuedMessage = "A certificate has already been issued for this attendance.";

    private readonly ICertificateRepository _certificates;
    private readonly IUnitOfWork _unitOfWork;

    public CertificateReviewInvalidationHandler(ICertificateRepository certificates, IUnitOfWork unitOfWork)
    {
        _certificates = certificates;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(AttendanceInvalidationContext context, CancellationToken cancellationToken = default)
    {
        var certificate = await _certificates.GetByRegistrationIdAsync(context.RegistrationId, cancellationToken);
        if (certificate is null)
        {
            return;
        }

        // An issued certificate is the one state invalidation may not overturn.
        if (certificate.CertificateStatusId == CertificateStatusCode.Issued)
        {
            throw new DomainException(OutcomeCode.Forbidden, IssuedMessage);
        }

        // A declined review keeps its original reason; only an undecided or approved review flips.
        if (certificate.CertificateStatusId is not (CertificateStatusCode.PendingApproval or CertificateStatusCode.Eligible))
        {
            return;
        }

        certificate.CertificateStatusId = CertificateStatusCode.NotEligible;
        certificate.ReviewNotes = context.Reason;
        certificate.UpdatedBy = context.InvalidatedBy;
        certificate.UpdatedAt = context.InvalidatedAt;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
