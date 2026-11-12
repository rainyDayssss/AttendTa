using System;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.ServiceModels;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class RegistrationStatusDisplayTests
{
    [Theory]
    [InlineData(RegistrationStatusCode.Pending, false, "Pending review", "badge-full")]
    [InlineData(RegistrationStatusCode.Pending, true, "Not confirmed before cutoff", "badge-closed")]
    [InlineData(RegistrationStatusCode.Waitlisted, false, "Waitlisted", "badge-member")]
    [InlineData(RegistrationStatusCode.Waitlisted, true, "Not confirmed before cutoff", "badge-closed")]
    [InlineData(RegistrationStatusCode.Confirmed, false, "Confirmed", "badge-open")]
    [InlineData(RegistrationStatusCode.Confirmed, true, "Confirmed", "badge-open")]
    [InlineData(RegistrationStatusCode.Cancelled, false, "Cancelled", "badge-inactive")]
    [InlineData(RegistrationStatusCode.Cancelled, true, "Cancelled", "badge-inactive")]
    public void BadgeFollowsTheDerivedCutoffRule(
        RegistrationStatusCode status,
        bool startCutoffPassed,
        string expectedLabel,
        string expectedClass)
    {
        Assert.Equal(expectedLabel, RegistrationStatusDisplay.Label(status, startCutoffPassed));
        Assert.Equal(expectedClass, RegistrationStatusDisplay.BadgeClass(status, startCutoffPassed));
        Assert.Equal(
            startCutoffPassed && status is RegistrationStatusCode.Pending or RegistrationStatusCode.Waitlisted,
            RegistrationStatusDisplay.IsNotConfirmedBeforeCutoff(status, startCutoffPassed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoRegistrationHasNoLabel(bool startCutoffPassed)
    {
        Assert.Null(RegistrationStatusDisplay.Label(null, startCutoffPassed));
        Assert.Null(RegistrationStatusDisplay.BadgeClass(null, startCutoffPassed));
        Assert.False(RegistrationStatusDisplay.IsNotConfirmedBeforeCutoff(null, startCutoffPassed));
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Confirmed)]
    [InlineData(RegistrationStatusCode.Cancelled)]
    public void ResolvedRowsKeepTheirOwnLabelAfterTheCutoff(RegistrationStatusCode status)
    {
        var before = RegistrationStatusDisplay.Label(status, false);
        var after = RegistrationStatusDisplay.Label(status, true);

        Assert.Equal(before, after);
        Assert.NotEqual(RegistrationStatusDisplay.NotConfirmedBeforeCutoff, after);
    }

    [Fact]
    public void OnlyPendingAndWaitlistedRowsAreDerivedAtTheCutoff()
    {
        foreach (var status in Enum.GetValues<RegistrationStatusCode>())
        {
            var derived = RegistrationStatusDisplay.IsNotConfirmedBeforeCutoff(status, true);

            Assert.Equal(status is RegistrationStatusCode.Pending or RegistrationStatusCode.Waitlisted, derived);
            Assert.Equal(derived, RegistrationStatusDisplay.Label(status, true) == RegistrationStatusDisplay.NotConfirmedBeforeCutoff);
        }
    }
}
