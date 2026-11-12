using ASI.Basecode.Services.Security;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class Argon2PasswordHasherTests
{
    private const string Password = "CorrectHorseBatteryStaple!42";

    private readonly Argon2PasswordHasher hasher = new();

    [Fact]
    public void HashProducesPhcEncodedArgon2idString()
    {
        var hash = hasher.Hash(Password);

        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
        Assert.Equal(6, hash.Split('$').Length);
        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void HashUsesAFreshSaltForEveryCall()
    {
        var first = hasher.Hash(Password);
        var second = hasher.Hash(Password);

        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify(Password, first));
        Assert.True(hasher.Verify(Password, second));
    }

    [Fact]
    public void VerifyAcceptsTheMatchingPassword()
    {
        var hash = hasher.Hash(Password);

        Assert.True(hasher.Verify(Password, hash));
    }

    [Fact]
    public void VerifyRejectsAWrongPassword()
    {
        var hash = hasher.Hash(Password);

        Assert.False(hasher.Verify("correcthorsebatterystaple!42", hash));
        Assert.False(hasher.Verify(string.Empty, hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plaintext")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$onlysalt")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2FsdHNhbHRzYWx0c2E$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    [InlineData("$argon2id$v=16$m=19456,t=2,p=1$c2FsdHNhbHRzYWx0c2E$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$not base64!$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    public void VerifyRejectsMalformedHashes(string hash)
    {
        Assert.False(hasher.Verify(Password, hash));
    }

    [Fact]
    public void VerifyRejectsANullHash()
    {
        Assert.False(hasher.Verify(Password, null!));
    }

    [Fact]
    public void VerifyRejectsHashWithUnreasonableCostParameters()
    {
        // A corrupted row must not make verification allocate an unbounded amount of memory.
        var hash = hasher.Hash(Password);
        var parts = hash.Split('$');
        parts[3] = "m=1073741824,t=2,p=1";

        Assert.False(hasher.Verify(Password, string.Join('$', parts)));
    }

    [Fact]
    public void VerifyRejectsMemoryCostBelowFourKibPerLane()
    {
        // Konscious throws when memorySize < 4 * parallelism; a corrupted row must verify false.
        var hash = hasher.Hash(Password);
        var parts = hash.Split('$');
        parts[3] = "m=8,t=2,p=16";

        Assert.False(hasher.Verify(Password, string.Join('$', parts)));
    }

    [Fact]
    public void VerifyUsesTheParametersStoredInTheHash()
    {
        var hash = hasher.Hash(Password);
        var parts = hash.Split('$');

        // Re-encoding with a different but valid cost must not verify against the old digest.
        parts[3] = "m=8192,t=1,p=1";
        Assert.False(hasher.Verify(Password, string.Join('$', parts)));
    }
}
