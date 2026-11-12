using ASI.Basecode.Services.Services;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class ServicesSmokeTests
{
    [Fact]
    public void MemberServiceTypeIsAvailable()
    {
        Assert.Equal("ASI.Basecode.Services", typeof(MemberService).Assembly.GetName().Name);
    }
}
