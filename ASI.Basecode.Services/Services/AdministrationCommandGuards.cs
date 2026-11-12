using System;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The reason and actor guards shared by the Administration access and role commands. Each guard
/// throws the same <see cref="DomainException"/> message, outcome and field key it always has;
/// callers pass the field key their form binds because the role form binds a model property
/// (<c>Reason</c>) while the access dialogs post a raw <c>reason</c> field.
/// </summary>
internal static class AdministrationCommandGuards
{
    internal const int ReasonMaxLength = 1000;

    internal static string RequiredReason(string reason, string fieldName)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException(OutcomeCode.Invalid, "A reason is required.", fieldName);
        }

        if (normalized.Length > ReasonMaxLength)
        {
            throw new DomainException(OutcomeCode.Invalid, "The reason must be 1000 characters or fewer.", fieldName);
        }

        return normalized;
    }

    internal static int RequireAdministrator(ICurrentActor currentActor)
    {
        var userId = RequireAuthenticatedActor(currentActor);
        if (!string.Equals(currentActor.Role, RoleCodes.Admin, StringComparison.Ordinal))
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }

    internal static int RequireManagement(ICurrentActor currentActor)
    {
        var userId = RequireAuthenticatedActor(currentActor);
        if (!string.Equals(currentActor.Role, RoleCodes.Admin, StringComparison.Ordinal)
            && !string.Equals(currentActor.Role, RoleCodes.Officer, StringComparison.Ordinal))
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }

    private static int RequireAuthenticatedActor(ICurrentActor currentActor)
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

        if (userId <= 0)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }
}
