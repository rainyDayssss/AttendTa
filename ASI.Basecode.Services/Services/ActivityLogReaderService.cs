using ASI.Basecode.Data.Queries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Formatting;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// Turns paged audit rows into administrator-readable history. Only names and audited fields
/// from a per-action allowlist are shown; raw JSON never reaches the view.
/// </summary>
public sealed class ActivityLogReaderService(IActivityLogReaderRepository activityLogRepository, ListReadOptions readOptions = null) : IActivityLogReaderService
{
    public async Task<PagedResult<ActivityLogItemViewModel>> GetPageAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await activityLogRepository.CountAsync(cancellationToken);
        var pageRequest = new PageRequest(pageNumber, readOptions?.PageSize);
        var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
        var records = await activityLogRepository.GetPageAsync(
            pageRequest.GetOffset(normalizedPage),
            PageRequest.NormalizeSize(readOptions?.PageSize),
            cancellationToken);

        var items = records.Select(ToViewModel).ToList();

        return new PagedResult<ActivityLogItemViewModel>(items, normalizedPage, totalCount, PageRequest.NormalizeSize(readOptions?.PageSize));
    }

    private static ActivityLogItemViewModel ToViewModel(ActivityLogListItem record)
    {
        var actorName = ActorName(record);
        var actionLabel = ActionLabel(record.Action);
        var (changes, changesUnavailable) = ReadChanges(record.Action, record.OldValues, record.NewValues);

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Occurred"] = ManilaTimeFormatter.Format(record.OccurredAt),
            ["Actor"] = actorName,
            ["Affected record"] = record.TargetName is null
                ? $"Unavailable (record {record.EntityId})"
                : record.TargetName,
            ["Record type"] = EntityTypeLabel(record.EntityType),
            ["Record ID"] = record.EntityId.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(record.TargetContext))
        {
            details["Related event"] = record.TargetContext;
        }
        else if (HasRelatedEvent(record.EntityType) && record.TargetName is not null)
        {
            details["Related event"] = "Unavailable";
        }

        details["Reason"] = record.Reason;

        if (changesUnavailable)
        {
            details["Recorded changes"] = changes.Count > 0 ? "Some change details are unavailable" : "Unavailable";
        }

        foreach (var change in changes)
        {
            details[change.Key] = change.Value;
        }

        return new ActivityLogItemViewModel
        {
            LogId = record.LogId,
            ActorName = actorName,
            ActionLabel = actionLabel,
            AffectedRecord = record.TargetName,
            AffectedRecordContext = record.TargetContext,
            EntityId = record.EntityId,
            OccurredAt = record.OccurredAt,
            Details = details
        };
    }

    /// <summary>
    /// Friendly label for an audit action, shared with the list filter so the dropdown and the
    /// rows use one vocabulary. Unknown codes are returned unchanged.
    /// </summary>
    public static string LabelForAction(string action) => ActionLabel(action);

    private static string ActorName(ActivityLogListItem record) =>
        record.ActorType == ActorTypeCode.System
            ? "System process"
            : string.IsNullOrWhiteSpace(record.ActorName) ? "Unknown user" : record.ActorName;

    private static bool HasRelatedEvent(string entityType) =>
        NormalizeTable(entityType) is "registrations" or "attendance" or "certificates";

    private static string NormalizeTable(string entityType) =>
        (entityType ?? string.Empty).Trim().ToLowerInvariant();

    private static string ActionLabel(string action) => ActivityActionLabels.For(action);

    private static string EntityTypeLabel(string entityType)
    {
        var normalized = NormalizeTable(entityType);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Unknown record type";
        }

        return EntityTypeLabels.TryGetValue(normalized, out var label) ? label : entityType.Trim();
    }

    /// <summary>
    /// Reads only the allowlisted fields for a known action from the recorded snapshots.
    /// Malformed or missing snapshots report <c>Unavailable</c> instead of a guessed state.
    /// </summary>
    private static (IReadOnlyList<KeyValuePair<string, string>> Changes, bool Unavailable) ReadChanges(
        string action,
        string oldValues,
        string newValues)
    {
        var changes = new List<KeyValuePair<string, string>>();
        if (!FieldsByAction.TryGetValue(action ?? string.Empty, out var fields))
        {
            return (changes, false);
        }

        var hasOld = !string.IsNullOrWhiteSpace(oldValues);
        var hasNew = !string.IsNullOrWhiteSpace(newValues);
        JsonDocument oldDocument = null;
        JsonDocument newDocument = null;
        try
        {
            var oldRead = TryReadObject(oldValues, out oldDocument);
            var newRead = TryReadObject(newValues, out newDocument);

            // A snapshot that exists but cannot be read is reported, never guessed around.
            var unavailable = (hasOld && !oldRead) || (hasNew && !newRead);
            var expectsBothSnapshots = !NewOnlyActions.Contains(action);
            if (!newRead || (expectsBothSnapshots && !oldRead))
            {
                unavailable = true;
            }
            // EventUpdated has two writer shapes: a full EventAuditValues snapshot and a
            // cover-only EventCoverAuditValues snapshot. Cover fields on the full shape are
            // deliberately omitted when no cover was changed.
            var fullEventSnapshot = action != AuditActions.EventUpdated ||
                EventSnapshotFields().Any(field => field.Name is not ("HasCover" or "CoverChanged") &&
                    ((oldRead && oldDocument.RootElement.TryGetProperty(field.Name, out _)) ||
                     (newRead && newDocument.RootElement.TryGetProperty(field.Name, out _))));
            // Full event snapshots omit both cover properties when there was no cover change;
            // the separate cover-only writer always records both properties on both sides.
            var coverFieldsPresent = (oldRead && (oldDocument.RootElement.TryGetProperty("HasCover", out _) || oldDocument.RootElement.TryGetProperty("CoverChanged", out _))) ||
                (newRead && (newDocument.RootElement.TryGetProperty("HasCover", out _) || newDocument.RootElement.TryGetProperty("CoverChanged", out _)));
            var foundAny = false;

            foreach (var field in fields)
            {
                var oldText = oldRead ? FormatField(oldDocument.RootElement, field) : null;
                var newText = newRead ? FormatField(newDocument.RootElement, field) : null;
                var coverField = field.Name is "HasCover" or "CoverChanged";
                var required = !(field.Name == "Cause" && action != AuditActions.RegistrationCancelled)
                    && !(action == AuditActions.EventUpdated && !fullEventSnapshot && !coverField)
                    && !(coverField && fullEventSnapshot && !coverFieldsPresent);
                if (required && (newText is null || (expectsBothSnapshots && oldText is null)))
                {
                    unavailable = true;
                }
                // These are action indicators, not historical states. Some writers intentionally
                // omit them on the old side, and cover-only writers set true on both sides.
                if (field.Name is "CoverChanged" or "PhotoChanged")
                {
                    if (newText == "Yes")
                    {
                        changes.Add(new KeyValuePair<string, string>(field.Label, newText));
                        foundAny = true;
                    }
                    else if (oldText is not null || newText is not null)
                    {
                        foundAny = true;
                    }
                    continue;
                }

                if (oldText is null && newText is null)
                {
                    continue;
                }

                foundAny = true;
                if (field.Name == "Cause" && action == AuditActions.RegistrationCancelled && newText is not null)
                {
                    changes.Add(new KeyValuePair<string, string>(field.Label, newText));
                    continue;
                }
                if (oldText is not null && newText is not null && string.Equals(oldText, newText, StringComparison.Ordinal))
                {
                    if (field.Format != AuditValueFormat.Timestamp ||
                        !TryFormatDistinctTimestamps(oldDocument.RootElement, newDocument.RootElement, field.Name, out oldText, out newText))
                    {
                        continue;
                    }
                }

                changes.Add(new KeyValuePair<string, string>(
                    oldText is not null && newText is null ? $"{field.Label} (previous)" :
                    expectsBothSnapshots && oldText is null && newText is not null ? $"{field.Label} (recorded new)" : field.Label,
                    ComposeValue(oldText, newText)));
            }

            if (!foundAny && !unavailable)
            {
                unavailable = true;
            }

            return (changes, unavailable);
        }
        finally
        {
            oldDocument?.Dispose();
            newDocument?.Dispose();
        }
    }

    private static bool TryFormatDistinctTimestamps(JsonElement oldValues, JsonElement newValues, string field,
        out string oldText, out string newText)
    {
        oldText = newText = null;
        if (!oldValues.TryGetProperty(field, out var oldElement) || oldElement.ValueKind != JsonValueKind.String ||
            !newValues.TryGetProperty(field, out var newElement) || newElement.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(oldElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var oldTime) ||
            !DateTimeOffset.TryParse(newElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var newTime) ||
            oldTime == newTime)
        {
            return false;
        }

        var manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        const string format = "yyyy-MM-dd hh:mm:ss.FFFFFFF tt";
        oldText = TimeZoneInfo.ConvertTime(oldTime, manila).ToString(format, CultureInfo.InvariantCulture);
        newText = TimeZoneInfo.ConvertTime(newTime, manila).ToString(format, CultureInfo.InvariantCulture);
        return true;
    }

    private static string ComposeValue(string oldText, string newText) => (oldText, newText) switch
    {
        (not null, not null) => $"{oldText} → {newText}",
        (null, not null) => newText,
        (not null, null) => oldText,
        _ => null
    };

    private static bool TryReadObject(string json, out JsonDocument document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                parsed.Dispose();
                return false;
            }

            document = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string FormatField(JsonElement values, AuditField field)
    {
        if (!values.TryGetProperty(field.Name, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            // These are boolean action indicators, never nullable historical values. Event
            // writers omit an absent nullable cover indicator rather than writing JSON null.
            return field.Name is "PhotoChanged" or "CoverChanged" ? null : "None";
        }

        return field.Format switch
        {
            AuditValueFormat.Text => element.ValueKind == JsonValueKind.String
                ? string.IsNullOrWhiteSpace(element.GetString()) ? "(empty)" : element.GetString()
                : null,
            AuditValueFormat.Number => FormatNumber(element),
            AuditValueFormat.Role => element.ValueKind == JsonValueKind.String ? RoleLabel(element.GetString()) : null,
            AuditValueFormat.EventStatus => FormatStatus(element, EventStatusLabel),
            AuditValueFormat.RegistrationStatus => FormatStatus(element, RegistrationStatusLabel),
            AuditValueFormat.MembershipStatus => FormatStatus(element, MembershipStatusLabel),
            AuditValueFormat.RequestStatus => FormatStatus(element, RequestStatusLabel),
            AuditValueFormat.CertificateStatus => FormatStatus(element, CertificateStatusLabel),
            AuditValueFormat.CheckInMethod => FormatStatus(element, CheckInMethodLabel),
            AuditValueFormat.Active => element.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? element.GetBoolean() ? "Active" : "Inactive"
                : null,
            AuditValueFormat.YesNo => element.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? element.GetBoolean() ? "Yes" : "No"
                : null,
            AuditValueFormat.Timestamp => FormatTimestamp(element),
            _ => null
        };
    }

    private static string FormatNumber(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : null;

    private static string FormatStatus(JsonElement element, Func<string, string> label)
    {
        var code = element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => element.GetString(),
            _ => null
        };

        return string.IsNullOrWhiteSpace(code) ? null : label(code);
    }

    private static string FormatTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = element.GetString();
        if (string.IsNullOrWhiteSpace(raw)
            || !DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
        {
            return raw;
        }

        try
        {
            return ManilaTimeFormatter.Format(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value);
        }
        catch (ArgumentException)
        {
            // An unreadable value stays as recorded rather than being reformatted into a guess.
            return raw;
        }
    }

    private static string EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string RoleLabel(string code) => code?.Trim().ToLowerInvariant() switch
    {
        "admin" => "Administrator",
        "officer" => "Officer",
        "member" => "Member",
        _ => EmptyToNull(code)
    };

    // Audit snapshots are serialized with the default options, so typed enums arrive as numbers
    // while statuses written as strings arrive as codes. Each label helper reads both forms.

    private static string EventStatusLabel(string code) => Numeric(code) switch
    {
        1 => "Draft",
        2 => "Published",
        3 => "Cancelled",
        4 => "Completed",
        _ => CodeOrNull(code) switch
        {
            "draft" => "Draft",
            "published" => "Published",
            "cancelled" => "Cancelled",
            "completed" => "Completed",
            _ => EmptyToNull(code)
        }
    };

    private static string RegistrationStatusLabel(string code) => Numeric(code) switch
    {
        1 => "Pending",
        2 => "Confirmed",
        3 => "Waitlisted",
        4 => "Cancelled",
        _ => CodeOrNull(code) switch
        {
            "pending" => "Pending",
            "confirmed" => "Confirmed",
            "waitlisted" => "Waitlisted",
            "cancelled" => "Cancelled",
            _ => EmptyToNull(code)
        }
    };

    private static string MembershipStatusLabel(string code) => Numeric(code) switch
    {
        1 => "Active",
        2 => "Inactive",
        _ => CodeOrNull(code) switch
        {
            "active" => "Active",
            "inactive" => "Inactive",
            _ => EmptyToNull(code)
        }
    };

    private static string RequestStatusLabel(string code) => Numeric(code) switch
    {
        1 => "Pending",
        2 => "Approved",
        3 => "Rejected",
        _ => CodeOrNull(code) switch
        {
            "pending" => "Pending",
            "approved" => "Approved",
            "rejected" => "Rejected",
            _ => EmptyToNull(code)
        }
    };

    private static string CertificateStatusLabel(string code) => Numeric(code) switch
    {
        1 => "Pending approval",
        2 => "Eligible",
        3 => "Not eligible",
        4 => "Issued",
        _ => CodeOrNull(code) switch
        {
            "pendingapproval" => "Pending approval",
            "eligible" => "Eligible",
            "noteligible" or "not_eligible" => "Not eligible",
            "issued" => "Issued",
            _ => EmptyToNull(code)
        }
    };

    private static string CheckInMethodLabel(string code) => Numeric(code) switch
    {
        1 => "QR scan",
        2 => "Manual lookup",
        3 => "Student number",
        _ => CodeOrNull(code) switch
        {
            "qrcode" or "qr" => "QR scan",
            "manualsearch" or "manual" => "Manual lookup",
            "studentnumber" => "Student number",
            _ => EmptyToNull(code)
        }
    };

    private static int? Numeric(string code) =>
        int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static string CodeOrNull(string code) =>
        string.IsNullOrWhiteSpace(code) || Numeric(code) is not null
            ? null
            : code.Replace(" ", string.Empty).Trim().ToLowerInvariant();

    private readonly record struct AuditField(string Name, string Label, AuditValueFormat Format);

    private enum AuditValueFormat
    {
        Text,
        Number,
        Role,
        EventStatus,
        RegistrationStatus,
        MembershipStatus,
        RequestStatus,
        CertificateStatus,
        CheckInMethod,
        Active,
        YesNo,
        Timestamp
    }

    private static AuditField F(string name, string label, AuditValueFormat format) => new(name, label, format);

    // These writers record only the new state; all other known actions record both sides.
    private static readonly HashSet<string> NewOnlyActions = new(StringComparer.Ordinal)
    {
        AuditActions.EventCreated, AuditActions.EventTypeCreated, AuditActions.VenueCreated,
        AuditActions.MemberCreated, AuditActions.AttendanceRecorded, AuditActions.AttendanceInvalidated,
        AuditActions.CertificateApproved, AuditActions.CertificateDeclined, AuditActions.CertificateIssued
    };

    private static readonly IReadOnlyDictionary<string, string> EntityTypeLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["users"] = "Account",
        ["account_requests"] = "Account request",
        ["members"] = "Member",
        ["events"] = "Event",
        ["event_types"] = "Event type",
        ["venues"] = "Venue",
        ["registrations"] = "Registration",
        ["attendance"] = "Attendance",
        ["certificates"] = "Certificate"
    };

    /// <summary>
    /// Per-action allowlist. Property names match the audit writer's snapshot records; fields
    /// outside these lists, including contact data and technical identifiers, are never shown.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, AuditField[]> FieldsByAction = new Dictionary<string, AuditField[]>(StringComparer.Ordinal)
    {
        [AuditActions.UserRoleAssigned] = new[]
        {
            F("RoleCode", "Role", AuditValueFormat.Role)
        },
        [AuditActions.UserDeactivated] = new[]
        {
            F("IsActive", "Account access", AuditValueFormat.Active)
        },
        [AuditActions.UserReactivated] = new[]
        {
            F("IsActive", "Account access", AuditValueFormat.Active)
        },
        [AuditActions.AccountRequestApproved] = new[]
        {
            F("Status", "Request status", AuditValueFormat.RequestStatus)
        },
        [AuditActions.AccountRequestRejected] = new[]
        {
            F("Status", "Request status", AuditValueFormat.RequestStatus)
        },
        [AuditActions.EventCreated] = EventSnapshotFields(),
        [AuditActions.EventUpdated] = EventSnapshotFields(),
        [AuditActions.EventPublished] = new[]
        {
            F("Status", "Status", AuditValueFormat.EventStatus),
            F("PublishedAt", "Published at", AuditValueFormat.Timestamp)
        },
        [AuditActions.EventCancelled] = new[]
        {
            F("Status", "Status", AuditValueFormat.EventStatus),
            F("IsCheckInOpen", "Check-in open", AuditValueFormat.YesNo),
            F("CancelledAt", "Cancelled at", AuditValueFormat.Timestamp),
            F("CancellationReason", "Cancellation reason", AuditValueFormat.Text)
        },
        [AuditActions.EventCompleted] = new[]
        {
            F("Status", "Status", AuditValueFormat.EventStatus),
            F("IsCheckInOpen", "Check-in open", AuditValueFormat.YesNo),
            F("CompletedAt", "Completed at", AuditValueFormat.Timestamp)
        },
        [AuditActions.EventCheckInOpened] = new[]
        {
            F("Status", "Status", AuditValueFormat.EventStatus),
            F("IsCheckInOpen", "Check-in open", AuditValueFormat.YesNo),
            F("CheckInOpenedAt", "Check-in opened at", AuditValueFormat.Timestamp),
            F("CheckInClosedAt", "Check-in closed at", AuditValueFormat.Timestamp)
        },
        [AuditActions.EventCheckInClosed] = new[]
        {
            F("Status", "Status", AuditValueFormat.EventStatus),
            F("IsCheckInOpen", "Check-in open", AuditValueFormat.YesNo),
            F("CheckInOpenedAt", "Check-in opened at", AuditValueFormat.Timestamp),
            F("CheckInClosedAt", "Check-in closed at", AuditValueFormat.Timestamp)
        },
        [AuditActions.EventTypeCreated] = new[]
        {
            F("TypeName", "Classification", AuditValueFormat.Text),
            F("Description", "Description", AuditValueFormat.Text),
            F("IsActive", "Active", AuditValueFormat.Active)
        },
        [AuditActions.EventTypeEdited] = new[]
        {
            F("TypeName", "Classification", AuditValueFormat.Text),
            F("Description", "Description", AuditValueFormat.Text),
            F("IsActive", "Active", AuditValueFormat.Active)
        },
        [AuditActions.EventTypeStatusChanged] = new[]
        {
            F("IsActive", "Active", AuditValueFormat.Active),
            F("LinkedEventCount", "Linked events", AuditValueFormat.Number)
        },
        [AuditActions.VenueCreated] = VenueFields(),
        [AuditActions.VenueEdited] = VenueFields(),
        [AuditActions.VenueStatusChanged] = new[]
        {
            F("IsActive", "Active", AuditValueFormat.Active),
            F("LinkedEventCount", "Linked events", AuditValueFormat.Number)
        },
        [AuditActions.MemberCreated] = MemberFields(),
        [AuditActions.MemberEdited] = MemberFields(),
        [AuditActions.MemberStudentNumberCorrected] = MemberFields(),
        [AuditActions.MemberMembershipDeactivated] = new[]
        {
            F("Status", "Membership status", AuditValueFormat.MembershipStatus)
        },
        [AuditActions.MemberMembershipReactivated] = new[]
        {
            F("Status", "Membership status", AuditValueFormat.MembershipStatus)
        },
        [AuditActions.RegistrationCancelled] = RegistrationFields(),
        [AuditActions.RegistrationApproved] = RegistrationFields(),
        [AuditActions.RegistrationPromoted] = RegistrationFields(),
        [AuditActions.RegistrationDeclined] = RegistrationFields(),
        [AuditActions.RegistrationAdminCancelled] = RegistrationFields(),
        [AuditActions.AttendanceRecorded] = new[]
        {
            F("CheckInMethodId", "Check-in method", AuditValueFormat.CheckInMethod),
            F("RecordedAt", "Recorded at", AuditValueFormat.Timestamp)
        },
        [AuditActions.AttendanceInvalidated] = new[]
        {
            F("InvalidatedAt", "Invalidated at", AuditValueFormat.Timestamp),
            F("InvalidationReason", "Invalidation reason", AuditValueFormat.Text)
        },
        [AuditActions.CertificateApproved] = new[]
        {
            F("Status", "Decision", AuditValueFormat.CertificateStatus),
            F("ReviewedAt", "Reviewed at", AuditValueFormat.Timestamp)
        },
        [AuditActions.CertificateDeclined] = new[]
        {
            F("Status", "Decision", AuditValueFormat.CertificateStatus),
            F("ReviewedAt", "Reviewed at", AuditValueFormat.Timestamp)
        },
        [AuditActions.CertificateIssued] = new[]
        {
            F("CertificateNumber", "Certificate number", AuditValueFormat.Text),
            F("IssuedAt", "Issued at", AuditValueFormat.Timestamp)
        }
    };

    private static AuditField[] EventSnapshotFields() => new[]
    {
        F("Title", "Title", AuditValueFormat.Text),
        F("Description", "Description", AuditValueFormat.Text),
        F("TypeName", "Event type", AuditValueFormat.Text),
        F("VenueName", "Venue", AuditValueFormat.Text),
        F("StartsAt", "Starts at", AuditValueFormat.Timestamp),
        F("RegistrationDeadline", "Registration deadline", AuditValueFormat.Timestamp),
        F("ParticipantLimit", "Participant limit", AuditValueFormat.Number),
        F("Status", "Status", AuditValueFormat.EventStatus),
        F("HasCover", "Cover attached", AuditValueFormat.YesNo),
        F("CoverChanged", "Cover changed", AuditValueFormat.YesNo)
    };

    private static AuditField[] VenueFields() => new[]
    {
        F("RoomName", "Room", AuditValueFormat.Text),
        F("Capacity", "Capacity", AuditValueFormat.Number),
        F("Location", "Location", AuditValueFormat.Text),
        F("AvailabilityNotes", "Availability notes", AuditValueFormat.Text),
        F("ContactPerson", "Contact person", AuditValueFormat.Text),
        F("PhotoChanged", "Photo changed", AuditValueFormat.YesNo)
    };

    private static AuditField[] MemberFields() => new[]
    {
        F("FullName", "Full name", AuditValueFormat.Text),
        F("StudentNumber", "Student number", AuditValueFormat.Text),
        F("Course", "Course", AuditValueFormat.Text),
        F("YearLevel", "Year level", AuditValueFormat.Number),
        F("Section", "Section", AuditValueFormat.Text),
        F("MembershipType", "Membership type", AuditValueFormat.Text),
        F("Status", "Membership status", AuditValueFormat.MembershipStatus)
    };

    private static AuditField[] RegistrationFields() => new[]
    {
        F("Status", "Status", AuditValueFormat.RegistrationStatus),
        F("Cause", "Cause", AuditValueFormat.Text)
    };
}
