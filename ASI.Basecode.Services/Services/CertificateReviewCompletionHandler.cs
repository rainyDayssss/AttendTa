using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// Creates one pending certificate review per registration with Valid Attendance when an event is
/// completed (AD-2). It runs inside the caller's completion transaction and saves through the
/// caller's unit of work, so a later handler failure rolls every review back with the completion.
/// It never opens a transaction of its own and never writes an audit entry: completion owns the
/// single <c>event.completed</c> row.
/// </summary>
public sealed class CertificateReviewCompletionHandler : IEventCompletionHandler
{
    private readonly ICertificateRepository _certificates;
    private readonly IUnitOfWork _unitOfWork;

    public CertificateReviewCompletionHandler(ICertificateRepository certificates, IUnitOfWork unitOfWork)
    {
        _certificates = certificates;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(EventCompletionContext context, CancellationToken cancellationToken = default)
    {
        var registrationIds = await _certificates.GetRegistrationsAwaitingReviewAsync(context.EventId, cancellationToken);
        if (registrationIds.Count == 0)
        {
            return;
        }

        var certificates = new List<Certificate>(registrationIds.Count);
        foreach (var registrationId in registrationIds)
        {
            certificates.Add(new Certificate
            {
                RegistrationId = registrationId,
                CertificateStatusId = CertificateStatusCode.PendingApproval,
                CreatedBy = context.CompletedBy,
                CreatedAt = context.CompletedAt
            });
        }

        await _certificates.AddRangeAsync(certificates, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
