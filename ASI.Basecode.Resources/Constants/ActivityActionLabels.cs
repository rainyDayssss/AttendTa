using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Resources.Constants;

/// <summary>Shared display vocabulary for audit actions and server-side search.</summary>
public static class ActivityActionLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [AuditActions.AccountRequestApproved] = "Account request approved",
        [AuditActions.AccountRequestRejected] = "Account request rejected",
        [AuditActions.EventCreated] = "Event created",
        [AuditActions.EventUpdated] = "Event updated",
        [AuditActions.EventPublished] = "Event published",
        [AuditActions.EventCancelled] = "Event cancelled",
        [AuditActions.EventCompleted] = "Event completed",
        [AuditActions.EventCheckInOpened] = "Check-in opened",
        [AuditActions.EventCheckInClosed] = "Check-in closed",
        [AuditActions.RegistrationCancelled] = "Registration cancelled",
        [AuditActions.RegistrationApproved] = "Registration approved",
        [AuditActions.RegistrationPromoted] = "Registration promoted from waitlist",
        [AuditActions.RegistrationDeclined] = "Registration declined",
        [AuditActions.RegistrationAdminCancelled] = "Registration cancelled by staff",
        [AuditActions.EventTypeCreated] = "Event type created",
        [AuditActions.EventTypeEdited] = "Event type updated",
        [AuditActions.EventTypeStatusChanged] = "Event type status changed",
        [AuditActions.UserRoleAssigned] = "Role changed",
        ["user.provisioned"] = "Initial administrator provisioned",
        [AuditActions.UserDeactivated] = "Account deactivated",
        [AuditActions.UserReactivated] = "Account reactivated",
        [AuditActions.VenueCreated] = "Venue created",
        [AuditActions.VenueEdited] = "Venue updated",
        [AuditActions.VenueStatusChanged] = "Venue status changed",
        [AuditActions.MemberCreated] = "Member added",
        [AuditActions.MemberEdited] = "Member updated",
        [AuditActions.MemberStudentNumberCorrected] = "Student number corrected",
        [AuditActions.MemberMembershipDeactivated] = "Membership deactivated",
        [AuditActions.MemberMembershipReactivated] = "Membership reactivated",
        [AuditActions.AttendanceRecorded] = "Attendance recorded",
        [AuditActions.AttendanceInvalidated] = "Attendance invalidated",
        [AuditActions.CertificateApproved] = "Certificate approved",
        [AuditActions.CertificateDeclined] = "Certificate declined",
        [AuditActions.CertificateIssued] = "Certificate issued"
    };

    public static string For(string action) => string.IsNullOrWhiteSpace(action)
        ? "Unknown action"
        : Labels.TryGetValue(action, out var label) ? label : action;

    public static string[] MatchingCodes(string term) => Labels
        .Where(pair => pair.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
        .Select(pair => pair.Key).ToArray();
}
