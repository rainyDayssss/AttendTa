using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo membership access commands (register M-15a and M-15b). Every command reloads the real member
/// and applies the same refusal rules 5.6 and 5.7 will, then changes only the in-memory store: no
/// SQL row, no stored file and no Activity Log entry (register rules 4 and 8). Account access is
/// never touched.
/// </summary>
public sealed class DemoMembershipAccessCommands : IMembershipAccessCommands
{
    private readonly IMemberRepository _members;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;

    public DemoMembershipAccessCommands(
        IMemberRepository members,
        DemoStore store,
        TimeProvider timeProvider)
    {
        _members = members;
        _store = store;
        _timeProvider = timeProvider;
    }

    public Task DeactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default) =>
        SetStatusAsync(memberId, MembershipStatusCode.Inactive, reason, cancellationToken);

    public Task ReactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default) =>
        SetStatusAsync(memberId, MembershipStatusCode.Active, reason, cancellationToken);

    private Task SetStatusAsync(int memberId, MembershipStatusCode status, string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Refuse a blank reason before the state check, so the reason refusal wins over the
        // already-active and already-inactive refusals.
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(OutcomeCode.Invalid, "A reason is required.", "reason");
        }

        var member = _members.GetMemberRecord(memberId);
        if (member is null)
        {
            throw new DomainException(OutcomeCode.NotFound, "Member not found.");
        }

        // The state the screen shows: the real row, overlaid by the store when a demo command already
        // changed it. The store never writes SQL, so the real row alone would refuse the second step.
        var isActive = _store.TryGetMembershipStatus(memberId, out var stored)
            ? stored.Status == MembershipStatusCode.Active
            : string.Equals(member.Status, "active", StringComparison.OrdinalIgnoreCase);

        if (isActive == (status == MembershipStatusCode.Active))
        {
            throw new DomainException(
                OutcomeCode.Invalid,
                status == MembershipStatusCode.Active
                    ? "This membership is already active."
                    : "This membership is already inactive.");
        }

        _store.SetMembershipStatus(memberId, status, reason, _timeProvider.GetUtcNow().UtcDateTime);
        return Task.CompletedTask;
    }
}
