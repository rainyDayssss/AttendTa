using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.Services.Services;

public sealed class ActivityLogWriter(AsiBasecodeDBContext context, ICurrentActor actor, TimeProvider clock) : IActivityLogWriter
{
    public void Append(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (context.Database.CurrentTransaction == null) throw new InvalidOperationException("Audit requires an active transaction.");
        if (!actor.IsAuthenticated || actor.UserId <= 0) throw new InvalidOperationException("Audit requires an authenticated user.");
        var description = AuditDescriptions.ForAction(entry.Action);
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.AccountRequestApproved || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.AccountRequestRejected) && entry.EntityType != "account_requests") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventUpdated || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventPublished || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventCancelled || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventCompleted || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventCheckInOpened || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventCheckInClosed) && entry.EntityType != "events") throw new ArgumentException("Unsupported audit entity type.");
        if (entry.Action == ASI.Basecode.Resources.Constants.AuditActions.RegistrationCancelled && entry.EntityType != "registrations") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.RegistrationApproved || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.RegistrationDeclined || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.RegistrationAdminCancelled) && entry.EntityType != "registrations") throw new ArgumentException("Unsupported audit entity type.");
        if (entry.Action == ASI.Basecode.Resources.Constants.AuditActions.RegistrationPromoted && entry.EntityType != "registrations") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventTypeCreated || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventTypeEdited || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.EventTypeStatusChanged) && entry.EntityType != "event_types") throw new ArgumentException("Unsupported audit entity type.");
        if (entry.Action == ASI.Basecode.Resources.Constants.AuditActions.UserRoleAssigned && entry.EntityType != "users") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.UserDeactivated || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.UserReactivated) && entry.EntityType != "users") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.VenueCreated || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.VenueEdited || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.VenueStatusChanged) && entry.EntityType != "venues") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.MemberCreated || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.MemberEdited || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.MemberStudentNumberCorrected || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.MemberMembershipDeactivated || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.MemberMembershipReactivated) && entry.EntityType != "members") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.AttendanceRecorded || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.AttendanceInvalidated) && entry.EntityType != "attendance") throw new ArgumentException("Unsupported audit entity type.");
        if ((entry.Action == ASI.Basecode.Resources.Constants.AuditActions.CertificateApproved || entry.Action == ASI.Basecode.Resources.Constants.AuditActions.CertificateDeclined) && entry.EntityType != "certificates") throw new ArgumentException("Unsupported audit entity type.");
        if (string.IsNullOrWhiteSpace(entry.EntityType) || entry.EntityId <= 0) throw new ArgumentException("Audit entity is required.");
        var oldValues = Serialize(entry.OldValues);
        var newValues = Serialize(entry.NewValues);
        context.ActivityLogs.Add(new ActivityLog
        {
            ActorTypeId = ActorTypeCode.User,
            ActorUserId = actor.UserId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Description = description,
            Reason = entry.Reason,
            OldValues = oldValues,
            NewValues = newValues,
            OccurredAt = clock.GetUtcNow().UtcDateTime
        });
    }

    private string Serialize(object value)
    {
        if (value == null) return null;
        Validate(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
        var json = JsonSerializer.Serialize(value, value.GetType());
        using var doc = JsonDocument.Parse(json);
        ValidateJson(doc.RootElement);
        return json;
    }

    private static bool Sensitive(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return normalized.Contains("password") || normalized.Contains("secret") || normalized.Contains("hash") || normalized.Contains("token") || normalized.Contains("path") || normalized.Contains("qr");
    }

    private void Validate(object value, HashSet<object> seen)
    {
        if (value == null) return;
        var type = value.GetType();
        if (context.Model.GetEntityTypes().Any(entity => entity.ClrType.IsAssignableFrom(type))) throw new ArgumentException("Entity payloads are not permitted.");
        if (type.IsPrimitive || type.IsEnum || value is string or decimal or DateTime or DateTimeOffset or Guid) return;
        if (value is JsonElement or JsonDocument or IDictionary) throw new ArgumentException("Only explicit field-subset payloads are permitted.");
        if (!type.IsValueType && !seen.Add(value)) throw new ArgumentException("Repeated or cyclic payloads are not permitted.");
        if (value is IEnumerable sequence) { foreach (var item in sequence) Validate(item, seen); return; }
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length != 0 || property.GetMethod == null) continue;
            if (Sensitive(property.Name) || Sensitive(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? ""))
                throw new ArgumentException("Sensitive audit fields are not permitted.");
            Validate(property.GetValue(value), seen);
        }
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Sensitive(field.Name) || Sensitive(field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? "")) throw new ArgumentException("Sensitive audit fields are not permitted.");
            Validate(field.GetValue(value), seen);
        }
    }

    private static void ValidateJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject()) { if (Sensitive(property.Name)) throw new ArgumentException("Sensitive audit fields are not permitted."); ValidateJson(property.Value); }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) ValidateJson(item);
    }
}
