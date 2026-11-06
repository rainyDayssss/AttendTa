using ASI.Basecode.Resources.Constants;
using System;
using System.Resources;

namespace ASI.Basecode.Resources.Messages;

public static class AuditDescriptions
{
    private static readonly ResourceManager Templates = new("ASI.Basecode.Resources.Messages.Audit", typeof(AuditActions).Assembly);
    public static string ForAction(string action) => action switch
    {
        AuditActions.AccountRequestApproved => Templates.GetString("AccountRequestApproved") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.AccountRequestRejected => Templates.GetString("AccountRequestRejected") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.EventUpdated => Templates.GetString("EventUpdated") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.EventPublished => Templates.GetString("EventPublished") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.EventCancelled => Templates.GetString("EventCancelled") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.RegistrationCancelled => Templates.GetString("RegistrationCancelled") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.RegistrationApproved => Templates.GetString("RegistrationApproved") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.RegistrationPromoted => Templates.GetString("RegistrationPromoted") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.RegistrationDeclined => Templates.GetString("RegistrationDeclined") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.RegistrationAdminCancelled => Templates.GetString("RegistrationAdminCancelled") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.EventTypeCreated => Templates.GetString("EventTypeCreated") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.EventTypeEdited => Templates.GetString("EventTypeEdited") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.EventTypeStatusChanged => Templates.GetString("EventTypeStatusChanged") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.UserRoleAssigned => Templates.GetString("UserRoleAssigned") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.VenueCreated => Templates.GetString("VenueCreated") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.VenueEdited => Templates.GetString("VenueEdited") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.VenueStatusChanged => Templates.GetString("VenueStatusChanged") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.MemberCreated => Templates.GetString("MemberCreated") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.MemberEdited => Templates.GetString("MemberEdited") ?? throw new InvalidOperationException("Missing audit template."),
        AuditActions.MemberStudentNumberCorrected => Templates.GetString("MemberStudentNumberCorrected") ?? throw new InvalidOperationException("Missing audit template."),
        _ => throw new ArgumentException("Unsupported audit action.", nameof(action))
    };
}
