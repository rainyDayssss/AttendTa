using System.Security.Cryptography;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.DemoData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoCheckInWindowFlagTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task ScannerAndManualLookupReadTheRealCheckInFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var published = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(6), 20, null, "Check-in flag event", ct);

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var unitOfWork = new UnitOfWork(context);
        var events = new EventRepository(unitOfWork);
        var registrations = new RegistrationRepository(unitOfWork);
        var attendance = new AttendanceRepository(unitOfWork);
        var actor = new Actor(officerId);
        var clock = TimeProvider.System;
        var engine = new AttendanceRecordingService(
            registrations,
            new UserRepository(unitOfWork),
            new MemberRepository(unitOfWork),
            events,
            attendance,
            unitOfWork,
            actor,
            new ActivityLogWriter(context, actor, clock),
            clock);
        var scanner = new CheckInService(events, registrations, attendance, engine);
        var lookup = new CheckInLookupService(events, registrations, engine);

        Assert.False((await scanner.GetScreenAsync(published.EventId, ct)).IsCheckInOpen);
        Assert.Equal(CheckInOutcomeViewModel.Closed, (await scanner.ScanAsync(published.EventId, "unknown", ct)).Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Closed, (await lookup.CheckInAsync(published.EventId, 999, "unknown", ct)).Outcome);

        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Events.SingleAsync(item => item.EventId == published.EventId, ct);
            row.IsCheckInOpen = true;
            await writer.SaveChangesAsync(ct);
        }

        Assert.True((await scanner.GetScreenAsync(published.EventId, ct)).IsCheckInOpen);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, (await scanner.ScanAsync(published.EventId, "unknown", ct)).Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, (await lookup.CheckInAsync(published.EventId, 999, "unknown", ct)).Outcome);
    }

    [Fact]
    public void DemoSwitchStillResolvesTheRealCheckInService()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var demo = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DemoData:Enabled", "true");
            builder.UseSetting("DemoData:AllowDatabase", fixture.DatabaseName);
            builder.UseSetting("DemoData:TokenKey", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        });

        using var client = demo.CreateClient();
        using var scope = demo.Services.CreateScope();

        Assert.IsType<CheckInService>(scope.ServiceProvider.GetRequiredService<ICheckInService>());
        Assert.IsType<CheckInLookupService>(scope.ServiceProvider.GetRequiredService<ICheckInLookupService>());
    }

    [Fact]
    public void SwitchOffResolvesTheRealCheckInLookupService()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        Assert.IsType<CheckInLookupService>(scope.ServiceProvider.GetRequiredService<ICheckInLookupService>());
    }

    private sealed class Actor(int id) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => RoleCodes.Officer;
    }
}
