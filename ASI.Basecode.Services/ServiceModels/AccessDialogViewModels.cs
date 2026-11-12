using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Account access dialog contract for the Administrator accounts list. The dialog lists the real
/// upcoming registrations a deactivation would cancel and blocks the last active Administrator.
/// Account access is separate from membership eligibility.
/// </summary>
public sealed class AccountAccessViewModel
{
    public int AccountId { get; init; }

    /// <summary>The linked <c>users.user_id</c>; the account id is the user id.</summary>
    public int UserId { get; init; }

    public string FullName { get; init; }

    public string Email { get; init; }

    public string RoleCode { get; init; }

    public string RoleLabel { get; init; }

    /// <summary>The account's real <c>users.is_active</c> state, set when the dialog is composed.</summary>
    public bool IsActive { get; init; }

    /// <summary>True when this active Administrator is the only active Administrator.</summary>
    public bool IsLastActiveAdministrator { get; init; }

    /// <summary>The real upcoming registrations a deactivation would cancel.</summary>
    public IReadOnlyList<UpcomingRegistrationPreview> AffectedRegistrations { get; init; } =
        Array.Empty<UpcomingRegistrationPreview>();

    public static AccountAccessViewModel For(AccountSummaryViewModel account) => new()
    {
        AccountId = account.AccountId,
        UserId = account.AccountId,
        FullName = account.FullName,
        Email = account.Email,
        RoleCode = account.RoleCode,
        RoleLabel = account.RoleLabel,
        IsActive = account.IsActive
    };
}

/// <summary>
/// Membership access dialog contract for the Management members list. The dialog previews the real
/// upcoming registrations through the member's linked account and keeps membership status separate
/// from account access.
/// </summary>
public sealed class MembershipAccessViewModel
{
    public int MemberId { get; init; }

    /// <summary>The member's linked <c>users.user_id</c>, used for the registration preview.</summary>
    public int UserId { get; init; }

    public string FullName { get; init; }

    public string StudentNumber { get; init; }

    /// <summary>The member's real membership status, set when the dialog is composed.</summary>
    public bool IsActive { get; init; }

    /// <summary>The real upcoming registrations a deactivation would affect.</summary>
    public IReadOnlyList<UpcomingRegistrationPreview> AffectedRegistrations { get; init; } =
        Array.Empty<UpcomingRegistrationPreview>();

    public static MembershipAccessViewModel For(MemberViewModel member) => new()
    {
        MemberId = member.MemberId,
        UserId = member.UserId,
        FullName = member.FullName,
        StudentNumber = member.StudentNumber,
        IsActive = string.Equals(member.Status, "active", StringComparison.OrdinalIgnoreCase)
    };
}
