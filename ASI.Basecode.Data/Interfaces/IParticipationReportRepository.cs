using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Read-only participation report facts for one event. One snapshot read returns every registration
/// of the event with its member, signup snapshots and attendance; the summary, no-shows, distribution
/// and detail rows are all derived from those rows, so the panels cannot disagree while attendance
/// changes. The report never mutates participation records.
/// </summary>
public interface IParticipationReportRepository
{
    /// <summary>
    /// Returns every registration of one event in every status, joined to the participant's name and
    /// student number, with the course and year level captured at signup and the attendance row when
    /// one exists. Ordered by participant name and then <c>registration_id</c> so page boundaries are
    /// stable. A null <see cref="ParticipationReportDetailRow.CheckedInAt"/> means the registration has
    /// no attendance row.
    /// </summary>
    Task<IReadOnlyList<ParticipationReportDetailRow>> GetSnapshotAsync(int eventId, CancellationToken cancellationToken = default);
}

/// <summary>
/// One participant row of the event snapshot, before presentation labels. <see cref="CheckedInAt"/> is
/// null when the registration has no attendance row; <see cref="InvalidatedAt"/> separates an
/// invalidated check-in from a valid one.
/// </summary>
public sealed record ParticipationReportDetailRow(
    int RegistrationId,
    string MemberName,
    string StudentNumber,
    string Course,
    int YearLevel,
    RegistrationStatusCode Status,
    DateTime? CheckedInAt,
    DateTime? InvalidatedAt);
