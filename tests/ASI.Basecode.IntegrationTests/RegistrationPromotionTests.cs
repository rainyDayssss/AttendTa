using System.Net;
using System.Data.Common;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Explicit waitlist promotion: the earliest eligible waitlisted registration for an event is
/// confirmed under the user-then-event lock order, with one audit row in the same transaction.
/// </summary>
public sealed class RegistrationPromotionTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Start = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PromotionConfirmsEarliestEligibleWaitlistedAndKeepsReviewMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var ineligible = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-3), ct, MembershipStatusCode.Inactive);
        // Insert the later row first so registration_id order disagrees with reviewed_at order.
        var later = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-1), ct);
        var earliest = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct, reviewNotes: "queued first");
        var now = Start.AddHours(-1);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        await Service(context, setup.Officer.Id, now).PromoteNextAsync(setup.Event.EventId, earliest.Registration.RegistrationId, ct);
        context.ChangeTracker.Clear();

        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == earliest.Registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, actual.RegistrationStatusId);
        Assert.Matches("^[0-9a-f]{64}$", actual.QrToken!);
        Assert.Equal(setup.Officer.Id, actual.ReviewedBy);
        Assert.Equal(Start.AddHours(-2), actual.ReviewedAt);
        Assert.Equal("queued first", actual.ReviewNotes);
        Assert.Equal(setup.Officer.Id, actual.ConfirmedBy);
        Assert.Equal(now, actual.ConfirmedAt);
        Assert.Equal(setup.Officer.Id, actual.UpdatedBy);
        Assert.Equal(now, actual.UpdatedAt);

        foreach (var untouched in new[] { later, ineligible })
        {
            var row = await context.Registrations.SingleAsync(r => r.RegistrationId == untouched.Registration.RegistrationId, ct);
            Assert.Equal(RegistrationStatusCode.Waitlisted, row.RegistrationStatusId);
            Assert.Null(row.QrToken);
            Assert.Null(row.ConfirmedAt);
        }

        var logs = await context.ActivityLogs.Where(l => l.EntityType == "registrations"
            && (l.EntityId == ineligible.Registration.RegistrationId || l.EntityId == earliest.Registration.RegistrationId || l.EntityId == later.Registration.RegistrationId)).ToListAsync(ct);
        var promotion = Assert.Single(logs);
        Assert.Equal("registration.promoted", promotion.Action);
        Assert.Equal(earliest.Registration.RegistrationId, promotion.EntityId);
        Assert.Equal("Registration promoted from the waitlist.", promotion.Description);
        Assert.Equal(setup.Officer.Id, promotion.ActorUserId);
        Assert.DoesNotContain(actual.QrToken!, promotion.NewValues!, StringComparison.Ordinal);
        Assert.DoesNotContain("token", promotion.NewValues!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PromotionWithoutEligibleCandidateMakesNoChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var pending = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, null, ct);
        var ineligible = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct, MembershipStatusCode.Inactive);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var error = await Assert.ThrowsAsync<DomainException>(() => Service(context, setup.Officer.Id, Start.AddHours(-1)).PromoteNextAsync(setup.Event.EventId, ineligible.Registration.RegistrationId, ct));
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        context.ChangeTracker.Clear();
        Assert.Equal(RegistrationStatusCode.Pending, (await context.Registrations.SingleAsync(r => r.RegistrationId == pending.Registration.RegistrationId, ct)).RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Waitlisted, (await context.Registrations.SingleAsync(r => r.RegistrationId == ineligible.Registration.RegistrationId, ct)).RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations"
            && (l.EntityId == pending.Registration.RegistrationId || l.EntityId == ineligible.Registration.RegistrationId)).ToListAsync(ct));
    }

    [Fact]
    public async Task PromotionWithoutFreeSeatIsCapacityUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, limit: 1);
        var confirmed = await AddRegistrationAsync(setup, RegistrationStatusCode.Confirmed, Start.AddHours(-3), ct);
        var waitlisted = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var error = await Assert.ThrowsAsync<DomainException>(() => Service(context, setup.Officer.Id, Start.AddHours(-1)).PromoteNextAsync(setup.Event.EventId, waitlisted.Registration.RegistrationId, ct));
        Assert.Equal(OutcomeCode.CapacityUnavailable, error.Outcome);
        context.ChangeTracker.Clear();
        Assert.Equal(RegistrationStatusCode.Confirmed, (await context.Registrations.SingleAsync(r => r.RegistrationId == confirmed.Registration.RegistrationId, ct)).RegistrationStatusId);
        var untouched = await context.Registrations.SingleAsync(r => r.RegistrationId == waitlisted.Registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Waitlisted, untouched.RegistrationStatusId);
        Assert.Null(untouched.QrToken);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == waitlisted.Registration.RegistrationId).ToListAsync(ct));
    }

    [Fact]
    public async Task WaitlistQueueUsesPromotionOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var later = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-1), ct);
        var earlier = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var page = await Service(context, setup.Officer.Id, Start.AddHours(-1)).GetPageAsync("waitlisted", setup.Event.EventId, 1, ct);
        Assert.Equal(new[] { earlier.Registration.RegistrationId, later.Registration.RegistrationId }, page.Items.Select(row => row.RegistrationId));
    }

    [Theory]
    [InlineData(0, EventStatusCode.Published)]
    [InlineData(1, EventStatusCode.Published)]
    [InlineData(-1, EventStatusCode.Draft)]
    [InlineData(-1, EventStatusCode.Cancelled)]
    [InlineData(-1, EventStatusCode.Completed)]
    public async Task PromotionRejectsCutoffAndNonPublishedEvents(int tickOffset, EventStatusCode eventStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var waitlisted = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        if (eventStatus != EventStatusCode.Published)
        {
            (await context.Events.SingleAsync(e => e.EventId == setup.Event.EventId, ct)).EventStatusId = eventStatus;
            await context.SaveChangesAsync(ct);
            context.ChangeTracker.Clear();
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => Service(context, setup.Officer.Id, Start.AddTicks(tickOffset)).PromoteNextAsync(setup.Event.EventId, waitlisted.Registration.RegistrationId, ct));
        Assert.Equal(tickOffset >= 0 ? OutcomeCode.CutoffPassed : OutcomeCode.Stale, error.Outcome);
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == waitlisted.Registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Waitlisted, actual.RegistrationStatusId);
        Assert.Null(actual.QrToken);
        Assert.Null(actual.ConfirmedAt);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == waitlisted.Registration.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData("priority")]
    [InlineData("status")]
    [InlineData("owner")]
    [InlineData("event")]
    public async Task WaitingForEventLockRechecksCompetingChanges(string change)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var first = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-1), ct);
        Member replacement = null!;
        if (change == "owner")
        {
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"promotion-replacement-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
            replacement = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        }

        await using var blocker = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var transaction = await blocker.Database.BeginTransactionAsync(ct);
        await new EventRepository(new UnitOfWork(blocker)).LockEventAsync(setup.Event.EventId, ct);
        var gate = new EventLockAttempts(1);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var command = Service(context, setup.Officer.Id, Start.AddTicks(-1)).PromoteNextAsync(setup.Event.EventId, first.Registration.RegistrationId, ct);
        var committed = false;
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            Assert.False(command.IsCompleted);
            if (change == "priority") (await blocker.Registrations.SingleAsync(r => r.RegistrationId == first.Registration.RegistrationId, ct)).ReviewedAt = Start.AddHours(-0.5);
            if (change == "status") (await blocker.Registrations.SingleAsync(r => r.RegistrationId == first.Registration.RegistrationId, ct)).RegistrationStatusId = RegistrationStatusCode.Cancelled;
            if (change == "owner") (await blocker.Registrations.SingleAsync(r => r.RegistrationId == first.Registration.RegistrationId, ct)).MemberId = replacement.MemberId;
            if (change == "event") (await blocker.Events.SingleAsync(e => e.EventId == setup.Event.EventId, ct)).EventStatusId = EventStatusCode.Cancelled;
            await blocker.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            var error = await Assert.ThrowsAsync<DomainException>(() => command);
            Assert.Equal(OutcomeCode.Stale, error.Outcome);
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync(CancellationToken.None);
        }

        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == first.Registration.RegistrationId, ct);
        Assert.Equal(change == "status" ? RegistrationStatusCode.Cancelled : RegistrationStatusCode.Waitlisted, actual.RegistrationStatusId);
        Assert.Null(actual.QrToken);
        Assert.Null(actual.ConfirmedAt);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations"
            && (l.EntityId == first.Registration.RegistrationId || l.EntityId == second.Registration.RegistrationId)).ToListAsync(ct));
        Assert.Equal(RegistrationStatusCode.Waitlisted, (await context.Registrations.SingleAsync(r => r.RegistrationId == second.Registration.RegistrationId, ct)).RegistrationStatusId);
    }

    [Fact]
    public async Task WaitingForUserLockRechecksEligibility()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var waitlisted = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        var gate = new MemberEventTestSupport.LockGateInterceptor("dbo.users");
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var command = Service(context, setup.Officer.Id, Start.AddTicks(-1)).PromoteNextAsync(setup.Event.EventId, waitlisted.Registration.RegistrationId, ct);
        await gate.Attempted.WaitAsync(TimeSpan.FromSeconds(15), ct);
        Assert.False(command.IsCompleted);
        await MemberEventTestSupport.ExecuteSqlAsync(fixture.ConnectionString, $"UPDATE dbo.users SET is_active = 0 WHERE user_id = {waitlisted.User.Id}", ct);
        gate.Release.TrySetResult();
        var error = await Assert.ThrowsAsync<DomainException>(() => command);
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == waitlisted.Registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Waitlisted, actual.RegistrationStatusId);
        Assert.Null(actual.QrToken);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == waitlisted.Registration.RegistrationId).ToListAsync(ct));
    }

    [Fact]
    public async Task AuditFailureRollsBackPromotion()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var waitlisted = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.Officer.Id, Start.AddHours(-1), failAuditCall: 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PromoteNextAsync(setup.Event.EventId, waitlisted.Registration.RegistrationId, ct));
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == waitlisted.Registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Waitlisted, actual.RegistrationStatusId);
        Assert.Null(actual.QrToken);
        Assert.Null(actual.ConfirmedAt);
        Assert.Null(actual.UpdatedAt);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == waitlisted.Registration.RegistrationId).ToListAsync(ct));
    }

    [Fact]
    public async Task ConcurrentPromotionAndApprovalKeepExactlyOneConfirmedSeat()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, limit: 1);
        var pending = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, null, ct);
        var waitlisted = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);

        // Hold the pending owner's users row so the approval cannot reach the event lock until the
        // promotion has already taken the last seat.
        await using var blocker = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var blockerTransaction = await blocker.Database.BeginTransactionAsync(ct);
        await new UserRepository(new UnitOfWork(blocker)).LockUserAsync(pending.User.Id, ct);
        await using var promotionContext = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var approvalContext = MemberEventTestSupport.Context(fixture.ConnectionString);
        var promotion = Service(promotionContext, setup.Officer.Id, Start.AddHours(-1)).PromoteNextAsync(setup.Event.EventId, waitlisted.Registration.RegistrationId, ct);
        var approval = Service(approvalContext, setup.Officer.Id, Start.AddHours(-1)).ApproveAsync(pending.Registration.RegistrationId, ct);
        var committed = false;
        try
        {
            await promotion.WaitAsync(TimeSpan.FromSeconds(15), ct);
            await blockerTransaction.CommitAsync(ct);
            committed = true;
            await approval.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
        finally
        {
            if (!committed) await blockerTransaction.RollbackAsync(CancellationToken.None);
        }

        await using var verify = MemberEventTestSupport.Context(fixture.ConnectionString);
        var rows = await verify.Registrations.Where(r => r.EventId == setup.Event.EventId).ToListAsync(ct);
        var confirmed = Assert.Single(rows, r => r.RegistrationStatusId == RegistrationStatusCode.Confirmed);
        Assert.Equal(waitlisted.Registration.RegistrationId, confirmed.RegistrationId);
        Assert.Matches("^[0-9a-f]{64}$", confirmed.QrToken!);
        Assert.True(rows.Count(r => r.RegistrationStatusId == RegistrationStatusCode.Confirmed) <= setup.Event.ParticipantLimit);
        var loser = rows.Single(r => r.RegistrationId == pending.Registration.RegistrationId);
        Assert.Equal(RegistrationStatusCode.Waitlisted, loser.RegistrationStatusId);
        Assert.Null(loser.QrToken);
        Assert.Single(await verify.ActivityLogs.Where(l => l.Action == "registration.promoted" && (l.EntityId == pending.Registration.RegistrationId || l.EntityId == waitlisted.Registration.RegistrationId)).ToListAsync(ct));
        Assert.Single(await verify.ActivityLogs.Where(l => l.Action == "registration.approved" && (l.EntityId == pending.Registration.RegistrationId || l.EntityId == waitlisted.Registration.RegistrationId)).ToListAsync(ct));
    }

    [Fact]
    public async Task ConcurrentPromotionsConfirmOnlyOneCandidate()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, limit: 2);
        var first = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-1), ct);

        // Both commands discover the same earliest candidate, then pause before the user lock.
        var gate1 = new MemberEventTestSupport.LockGateInterceptor("dbo.users");
        var gate2 = new MemberEventTestSupport.LockGateInterceptor("dbo.users");
        await using var context1 = MemberEventTestSupport.Context(fixture.ConnectionString, gate1);
        await using var context2 = MemberEventTestSupport.Context(fixture.ConnectionString, gate2);
        var promotion1 = Service(context1, setup.Officer.Id, Start.AddHours(-1)).PromoteNextAsync(setup.Event.EventId, first.Registration.RegistrationId, ct);
        var promotion2 = Service(context2, setup.Officer.Id, Start.AddHours(-1)).PromoteNextAsync(setup.Event.EventId, first.Registration.RegistrationId, ct);
        await Task.WhenAll(gate1.Attempted, gate2.Attempted).WaitAsync(TimeSpan.FromSeconds(15), ct);
        gate1.Release.TrySetResult();
        gate2.Release.TrySetResult();

        var results = await Task.WhenAll(Capture(promotion1), Capture(promotion2));
        Assert.Single(results, outcome => outcome == null);
        Assert.Single(results, outcome => outcome == OutcomeCode.Stale);

        await using var verify = MemberEventTestSupport.Context(fixture.ConnectionString);
        var rows = await verify.Registrations.Where(r => r.EventId == setup.Event.EventId).ToListAsync(ct);
        var confirmed = Assert.Single(rows, r => r.RegistrationStatusId == RegistrationStatusCode.Confirmed);
        Assert.Equal(first.Registration.RegistrationId, confirmed.RegistrationId);
        Assert.Matches("^[0-9a-f]{64}$", confirmed.QrToken!);
        Assert.Equal(RegistrationStatusCode.Waitlisted, rows.Single(r => r.RegistrationId == second.Registration.RegistrationId).RegistrationStatusId);
        Assert.Single(await verify.ActivityLogs.Where(l => l.Action == "registration.promoted" && (l.EntityId == first.Registration.RegistrationId || l.EntityId == second.Registration.RegistrationId)).ToListAsync(ct));
    }

    private static async Task<OutcomeCode?> Capture(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (DomainException exception)
        {
            return exception.Outcome;
        }
    }

    [Fact]
    public async Task BrowserPromotionRequiresManagementAndAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var waitlisted = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"promotion-browser-officer-{Guid.NewGuid():N}@test.invalid";
        var memberEmail = $"promotion-browser-member-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        var queueUrl = $"/RegistrationReview/Index?status=waitlisted&eventId={setup.Event.EventId}";
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(queueUrl, ct)).StatusCode);

        using var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(member, memberEmail, password, ct);
        var denied = await member.GetAsync(queueUrl, ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", MemberEventTestSupport.LocationPath(denied), StringComparison.Ordinal);
        var forbidden = await member.PostAsync($"/RegistrationReview/Promote?eventId={setup.Event.EventId}", MemberEventTestSupport.Form(new()), ct);
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", MemberEventTestSupport.LocationPath(forbidden), StringComparison.Ordinal);

        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(officer, officerEmail, password, ct);
        var queue = await officer.GetStringAsync(queueUrl, ct);
        Assert.Contains("Promote next eligible participant", queue, StringComparison.Ordinal);
        Assert.DoesNotContain("Promote next eligible participant", await officer.GetStringAsync($"/RegistrationReview/Index?status=pending&eventId={setup.Event.EventId}", ct), StringComparison.Ordinal);
        Assert.DoesNotContain("Promote next eligible participant", await officer.GetStringAsync("/RegistrationReview/Index?status=waitlisted", ct), StringComparison.Ordinal);
        var events = await officer.GetStringAsync("/Event?pageSize=25", ct);
        Assert.Contains($"/Event/Details?id={setup.Event.EventId}", events, StringComparison.Ordinal);

        var missingToken = await officer.PostAsync($"/RegistrationReview/Promote?eventId={setup.Event.EventId}", MemberEventTestSupport.Form(new()), ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        await using (var rejected = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(RegistrationStatusCode.Waitlisted, (await rejected.Registrations.SingleAsync(r => r.RegistrationId == waitlisted.Registration.RegistrationId, ct)).RegistrationStatusId);
            Assert.Empty(await rejected.ActivityLogs.Where(l => l.EntityId == waitlisted.Registration.RegistrationId).ToListAsync(ct));
        }

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(officer, queueUrl, ct);
        var response = await officer.PostAsync("/RegistrationReview/Promote", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = setup.Event.EventId.ToString(), ["expectedRegistrationId"] = waitlisted.Registration.RegistrationId.ToString(), ["status"] = "waitlisted", ["page"] = "1", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"eventId={setup.Event.EventId}", MemberEventTestSupport.LocationPath(response), StringComparison.Ordinal);

        await using var verify = MemberEventTestSupport.Context(fixture.ConnectionString);
        var actual = await verify.Registrations.SingleAsync(r => r.RegistrationId == waitlisted.Registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, actual.RegistrationStatusId);
        Assert.Matches("^[0-9a-f]{64}$", actual.QrToken!);
        Assert.Single(await verify.ActivityLogs.Where(l => l.Action == "registration.promoted" && l.EntityId == waitlisted.Registration.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredRosterNamesAndPromotesTheEarliestEligibleCandidateFromTheFullQueue(bool dossier)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var inactive = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-3), ct, MembershipStatusCode.Inactive);
        var earliest = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        var later = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-1), ct);
        await SetNamesAsync(ct, (inactive.User.Id, "Inactive Person"), (earliest.User.Id, "First Eligible"), (later.User.Id, "Later Eligible"));
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            (await context.Registrations.SingleAsync(r => r.RegistrationId == later.Registration.RegistrationId, ct)).SubmittedAt = new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc);
            await context.SaveChangesAsync(ct);
        }

        var password = MemberEventTestSupport.NewPassword();
        var email = $"promotion-filtered-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Officer, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(officer, email, password, ct);
        var url = PromotionPage(setup.Event.EventId, dossier) + "&search=" + Uri.EscapeDataString(later.Member.StudentNumber);
        var html = await officer.GetStringAsync(url, ct);
        Assert.Contains($"data-registration-id=\"{later.Registration.RegistrationId}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-registration-id=\"{earliest.Registration.RegistrationId}\"", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Submission date</th><th scope=\"col\">Submission time</th>", html, StringComparison.Ordinal);
        const string dateClass = "numeric cell-date";
        Assert.Contains($"data-label=\"Submission date\" class=\"{dateClass}\">2026-01-02</td>", html, StringComparison.Ordinal);
        Assert.Contains($"data-label=\"Submission time\" class=\"{dateClass}\">12:00 AM</td>", html, StringComparison.Ordinal);
        var dialog = PromotionDialog(html);
        Assert.Contains($"Promote First Eligible ({earliest.Member.StudentNumber})?", dialog, StringComparison.Ordinal);
        Assert.Contains($"name=\"expectedRegistrationId\" value=\"{earliest.Registration.RegistrationId}\"", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Promote Later Eligible", dialog, StringComparison.Ordinal);

        var emptyHtml = await officer.GetStringAsync(PromotionPage(setup.Event.EventId, dossier) + "&search=NoSuchParticipantXYZ", ct);
        Assert.Contains($"colspan=\"{(dossier ? 5 : 6)}\" class=\"empty-state\"", emptyHtml, StringComparison.Ordinal);
        Assert.Contains("Promote First Eligible", PromotionDialog(emptyHtml), StringComparison.Ordinal);

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(officer, url, ct);
        var response = await PostPromotionAsync(officer, setup.Event.EventId, earliest.Registration.RegistrationId, dossier, token, ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = MemberEventTestSupport.Context(fixture.ConnectionString);
        var rows = await verify.Registrations.Where(r => r.EventId == setup.Event.EventId).ToListAsync(ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, rows.Single(r => r.RegistrationId == earliest.Registration.RegistrationId).RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Waitlisted, rows.Single(r => r.RegistrationId == later.Registration.RegistrationId).RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Waitlisted, rows.Single(r => r.RegistrationId == inactive.Registration.RegistrationId).RegistrationStatusId);
        var log = Assert.Single(await verify.ActivityLogs.Where(l => l.Action == "registration.promoted" && l.EntityId == earliest.Registration.RegistrationId).ToListAsync(ct));
        Assert.NotNull(log.ActorUserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleConfirmationDoesNotPromoteTheReplacementAndShowsRetry(bool dossier)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var first = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-1), ct);
        await SetNamesAsync(ct, (first.User.Id, "Original Candidate"), (second.User.Id, "Replacement Candidate"));
        var password = MemberEventTestSupport.NewPassword();
        var email = $"promotion-stale-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Officer, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(officer, email, password, ct);
        var url = PromotionPage(setup.Event.EventId, dossier);
        var dialog = PromotionDialog(await officer.GetStringAsync(url, ct));
        Assert.Contains("Promote Original Candidate", dialog, StringComparison.Ordinal);
        Assert.Contains($"name=\"expectedRegistrationId\" value=\"{first.Registration.RegistrationId}\"", dialog, StringComparison.Ordinal);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(officer, url, ct);

        // Invalidate the candidate after the confirmation was rendered, leaving a replacement.
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            (await context.Members.SingleAsync(m => m.MemberId == first.Member.MemberId, ct)).MembershipStatusId = MembershipStatusCode.Inactive;
            await context.SaveChangesAsync(ct);
        }
        var response = await PostPromotionAsync(officer, setup.Event.EventId, first.Registration.RegistrationId, dossier, token, ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var result = await officer.GetStringAsync(response.Headers.Location!, ct);
        Assert.Contains("The waitlist changed. Refresh and try again.", result, StringComparison.Ordinal);
        Assert.DoesNotContain("data-reopen=\"true\"", PromotionDialog(result), StringComparison.Ordinal);
        await using var verify = MemberEventTestSupport.Context(fixture.ConnectionString);
        var rows = await verify.Registrations.Where(r => r.EventId == setup.Event.EventId).ToListAsync(ct);
        Assert.All(rows, row => { Assert.Equal(RegistrationStatusCode.Waitlisted, row.RegistrationStatusId); Assert.Null(row.QrToken); });
        Assert.Empty(await verify.ActivityLogs.Where(l => l.Action == "registration.promoted" && (l.EntityId == first.Registration.RegistrationId || l.EntityId == second.Registration.RegistrationId)).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoEligibleWaitlistedCandidateShowsUnavailabilityWithoutAnAction(bool dossier)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var inactive = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, Start.AddHours(-2), ct, MembershipStatusCode.Inactive);
        await SetNamesAsync(ct, (inactive.User.Id, "Ineligible Candidate"));
        var password = MemberEventTestSupport.NewPassword();
        var email = $"promotion-empty-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Officer, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(officer, email, password, ct);
        var html = await officer.GetStringAsync(PromotionPage(setup.Event.EventId, dossier), ct);
        Assert.Contains("No eligible waitlisted participant is available.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open=\"promote\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Promote Ineligible Candidate", html, StringComparison.Ordinal);
    }

    private static string PromotionPage(int eventId, bool dossier) => dossier
        ? $"/Event/Details?id={eventId}&status=waitlisted"
        : $"/RegistrationReview/Index?status=waitlisted&eventId={eventId}";

    private static string PromotionDialog(string html)
    {
        var start = html.IndexOf("<dialog id=\"promote\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "Promotion dialog was not rendered.");
        var end = html.IndexOf("</dialog>", start, StringComparison.Ordinal);
        Assert.True(end > start, "Promotion dialog was not closed.");
        return html[start..(end + "</dialog>".Length)];
    }

    private static Task<HttpResponseMessage> PostPromotionAsync(HttpClient client, int eventId, int registrationId, bool dossier, string token, CancellationToken ct) =>
        client.PostAsync("/RegistrationReview/Promote", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = eventId.ToString(), ["expectedRegistrationId"] = registrationId.ToString(),
            ["dossier"] = dossier ? "true" : "false", ["page"] = "1", ["__RequestVerificationToken"] = token
        }), ct);

    private async Task SetNamesAsync(CancellationToken ct, params (int Id, string Name)[] names)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        foreach (var (id, name) in names) (await context.Users.SingleAsync(user => user.Id == id, ct)).Name = name;
        await context.SaveChangesAsync(ct);
    }

    private async Task<(User Officer, Event Event)> SetupAsync(CancellationToken ct, int limit = 20)
    {
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"promotion-officer-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Officer, ct);
        var (type, venue) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, type, venue, EventStatusCode.Published, Start, Start.AddDays(-1), limit, null, "Promotion event", ct);
        return (officer, eventItem);
    }

    private async Task<(Registration Registration, User User, Member Member)> AddRegistrationAsync(
        (User Officer, Event Event) setup,
        RegistrationStatusCode status,
        DateTime? reviewedAt,
        CancellationToken ct,
        MembershipStatusCode membershipStatus = MembershipStatusCode.Active,
        string? reviewNotes = null)
    {
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"promotion-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, membershipStatus, ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, setup.Event.EventId, member.MemberId, status, setup.Officer.Id, ct);
        if (reviewedAt != null || reviewNotes != null)
        {
            await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
            var row = await context.Registrations.SingleAsync(r => r.RegistrationId == registration.RegistrationId, ct);
            row.ReviewedBy = setup.Officer.Id;
            row.ReviewedAt = reviewedAt;
            row.ReviewNotes = reviewNotes;
            await context.SaveChangesAsync(ct);
            registration.ReviewedBy = setup.Officer.Id;
            registration.ReviewedAt = reviewedAt;
            registration.ReviewNotes = reviewNotes;
        }

        return (registration, user, member);
    }

    private static RegistrationReviewService Service(AsiBasecodeDBContext context, int actorId, DateTime now, int failAuditCall = 0)
        => Service(context, actorId, new Clock(now), failAuditCall);

    private static RegistrationReviewService Service(AsiBasecodeDBContext context, int actorId, TimeProvider clock, int failAuditCall = 0)
    {
        var uow = new UnitOfWork(context);
        var actor = new Actor(actorId);
        var registrations = new RegistrationRepository(uow);
        var events = new EventRepository(uow);
        IActivityLogWriter writer = new ActivityLogWriter(context, actor, clock);
        if (failAuditCall != 0) writer = new FailingWriter(writer, failAuditCall);
        return new RegistrationReviewService(registrations, new UserRepository(uow), new MemberRepository(uow), events, uow, actor,
            new RegistrationCancellationService(registrations, events, uow, actor, writer, clock), writer, clock);
    }

    private sealed class Actor(int id) : ICurrentActor { public bool IsAuthenticated => true; public int UserId => id; public string Role => "officer"; }
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc)); }

    private sealed class EventLockAttempts(int required) : DbCommandInterceptor
    {
        private int count;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("dbo.events", StringComparison.OrdinalIgnoreCase) && Interlocked.Increment(ref count) == required) Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailingWriter(IActivityLogWriter inner, int failAt) : IActivityLogWriter
    {
        private int calls;
        public void Append(AuditEntry entry) { if (++calls == failAt) throw new InvalidOperationException("audit failure"); inner.Append(entry); }
    }
}
