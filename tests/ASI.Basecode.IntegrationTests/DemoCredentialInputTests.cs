using System.Text.Json;
using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoCredentialInputTests
{
    private static Dictionary<string, string> Valid() => DemoCredentials.CredentialIds
        .Select((id, index) => (id, password: $"ExamplePassword{index}"))
        .ToDictionary(pair => pair.id, pair => pair.password);

    [Fact]
    public void CompleteDistinctInputIsAccepted()
    {
        var input = Valid();
        var result = DemoCredentials.Parse(JsonSerializer.Serialize(input));
        Assert.Equal(13, result.Count);
        Assert.Equal(input["gabutan"], result["gabutan"]);
    }

    [Fact]
    public void MissingAccountIsRejected()
    {
        var input = Valid();
        input.Remove("member10");
        Assert.Throws<InvalidDataException>(() => DemoCredentials.Parse(JsonSerializer.Serialize(input)));
    }

    [Fact]
    public void MissingApplicantCredentialIsRejected()
    {
        var input = Valid();
        input.Remove("applicant");
        Assert.Throws<InvalidDataException>(() => DemoCredentials.Parse(JsonSerializer.Serialize(input)));
    }

    [Fact]
    public void ApplicantCannotReuseMemberPassword()
    {
        var input = Valid();
        input["applicant"] = input["gabutan"];
        Assert.Throws<InvalidDataException>(() => DemoCredentials.Parse(JsonSerializer.Serialize(input)));
    }

    [Fact]
    public void DuplicatePasswordsAreRejected()
    {
        var input = Valid();
        input["member10"] = input["admin"];
        Assert.Throws<InvalidDataException>(() => DemoCredentials.Parse(JsonSerializer.Serialize(input)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"admin\":\"SECRET-DO-NOT-LOG\"")]
    [InlineData("{\"admin\":\"first\", \"admin\":\"second\"}")]
    [InlineData("[]")]
    public void InvalidInputNeverAppearsInError(string input)
    {
        var error = Assert.Throws<InvalidDataException>(() => DemoCredentials.Parse(input));
        Assert.DoesNotContain("SECRET-DO-NOT-LOG", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void ExtraOrWeakAccountIsRejected()
    {
        var input = Valid();
        input["unrecognized"] = "ExtraPassword12";
        Assert.Throws<InvalidDataException>(() => DemoCredentials.Parse(JsonSerializer.Serialize(input)));
        input.Remove("unrecognized");
        input["admin"] = "weak";
        Assert.Throws<InvalidDataException>(() => DemoCredentials.Parse(JsonSerializer.Serialize(input)));
    }
}
