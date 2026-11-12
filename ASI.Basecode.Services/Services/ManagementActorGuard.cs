using System;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The management actor guard shared by the check-in commands. Every refusal is the same
/// <see cref="DomainException"/> with the same message and outcome code, so the scan, manual
/// check-in and invalidation paths cannot drift.
/// </summary>
internal static class ManagementActorGuard
{
    internal static int RequireUserId(ICurrentActor currentActor)
    {
        if (!currentActor.IsAuthenticated)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        int userId;
        try
        {
            userId = currentActor.UserId;
        }
        catch (InvalidOperationException)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        if (userId <= 0
            || (!string.Equals(currentActor.Role, RoleCodes.Admin, StringComparison.Ordinal)
                && !string.Equals(currentActor.Role, RoleCodes.Officer, StringComparison.Ordinal)))
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }
}
