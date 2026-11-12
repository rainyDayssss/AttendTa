using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// The facts an invalidation hook receives: the event, the retained attendance row, the trimmed
/// reason and the Management actor and UTC time that will be persisted (AD-2, AD-11).
/// </summary>
public sealed record AttendanceInvalidationContext(
    int EventId,
    int RegistrationId,
    int AttendanceId,
    string Reason,
    int InvalidatedBy,
    DateTime InvalidatedAt);
