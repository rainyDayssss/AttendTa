using ASI.Basecode.WebApp.Formatting;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class ManilaTimeFormatterTests
{
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void UtcDatabaseTimestampRollsOverToNextManilaDay(DateTimeKind kind)
    {
        var timestamp = new DateTime(2026, 1, 1, 18, 30, 0, kind);
        Assert.Equal("2026-01-02 02:30 Asia/Manila", ManilaTimeFormatter.Format(timestamp));
        Assert.Equal("2026-01-02 02:30 Asia/Manila", ManilaTimeFormatter.Format((DateTime?)timestamp));
    }

    [Fact]
    public void OffsetIsConvertedAsAnInstant()
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 13, 30, 0, TimeSpan.FromHours(-5));
        Assert.Equal("2026-01-02 02:30 Asia/Manila", ManilaTimeFormatter.Format(timestamp));
        Assert.Equal("2026-01-02 02:30 Asia/Manila", ManilaTimeFormatter.Format((DateTimeOffset?)timestamp));
    }

    [Fact]
    public void LocalDateTimeIsRejectedInsteadOfBeingMisinterpretedAsUtc()
    {
        var timestamp = new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => ManilaTimeFormatter.Format(timestamp));
        Assert.Throws<ArgumentException>(() => ManilaTimeFormatter.Format((DateTime?)timestamp));
    }

    [Fact]
    public void NullTimestampsAreEmpty()
    {
        Assert.Equal(string.Empty, ManilaTimeFormatter.Format((DateTime?)null));
        Assert.Equal(string.Empty, ManilaTimeFormatter.Format((DateTimeOffset?)null));
    }

    [Fact]
    public void FormWallClockValuesConvertToUtcAndBack()
    {
        var posted = new DateTime(2026, 12, 10, 9, 0, 0, DateTimeKind.Unspecified);
        var utc = ManilaTimeFormatter.ToUtc(posted);

        Assert.Equal(new DateTime(2026, 12, 10, 1, 0, 0), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);

        var redisplayed = ManilaTimeFormatter.ToLocal(utc);
        Assert.Equal(posted, redisplayed);
        Assert.Equal(DateTimeKind.Unspecified, redisplayed.Kind);
    }

    [Fact]
    public void ToUtcLeavesAnAlreadyUtcValueUnchanged()
    {
        var utc = new DateTime(2026, 12, 10, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(utc, ManilaTimeFormatter.ToUtc(utc));
    }

    [Fact]
    public void ToLocalRejectsLocalTimestamps()
    {
        Assert.Throws<ArgumentException>(() => ManilaTimeFormatter.ToLocal(new DateTime(2026, 12, 10, 1, 0, 0, DateTimeKind.Local)));
    }
}
