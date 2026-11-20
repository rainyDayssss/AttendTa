using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Queries;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class StartCutoffConsistencyTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void InMemoryCutoffMatchesQueryAtBoundary(long ticks)
    {
        var start = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
        var now = start.AddTicks(ticks);
        var queryResult = ParticipationFacts.StartCutoffPassed(now).Compile()(new Event { StartsAt = start });
        Assert.Equal(queryResult, ParticipationFacts.HasStartCutoffPassed(start, now));
    }
}
