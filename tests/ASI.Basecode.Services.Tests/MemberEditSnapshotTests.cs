using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class MemberEditSnapshotTests
{
    [Fact]
    public void SnapshotUsesSameCanonicalValuesForFormAndStoredEntities()
    {
        var form = new MemberViewModel
        {
            MemberId = 12, UserId = 34, FullName = " Ada Lovelace ",
            StudentNumber = " 12345678 ", Course = "BS Information Technology",
            YearLevel = 2, Section = " a ", MobileNumber = " 09171234567 ",
            MembershipType = " Regular "
        };
        var member = new Member
        {
            MemberId = 12, UserId = 34, StudentNumber = "12345678",
            Course = "BSIT", YearLevel = 2, Section = "A",
            MobileNumber = "09171234567", MembershipType = "Regular"
        };

        Assert.Equal(MemberEditSnapshot.FromEntities(member, "Ada Lovelace"), MemberEditSnapshot.FromModel(form));
    }

    [Fact]
    public void SnapshotDetectsRecordIdentityAndEditableChanges()
    {
        var model = new MemberViewModel
        {
            MemberId = 1, UserId = 2, FullName = "Member Name", StudentNumber = "12345678",
            Course = "BSIT", YearLevel = 1, Section = "A"
        };
        var original = MemberEditSnapshot.FromModel(model);

        Assert.NotEqual(original, original with { MemberId = 3 });
        Assert.NotEqual(original, original with { UserId = 4 });
        Assert.NotEqual(original, original with { FullName = "Other Name" });
        Assert.NotEqual(original, original with { StudentNumber = "87654321" });
        Assert.NotEqual(original, original with { Course = "BSCS" });
        Assert.NotEqual(original, original with { YearLevel = 2 });
        Assert.NotEqual(original, original with { Section = "B" });
        Assert.NotEqual(original, original with { MobileNumber = "09171234567" });
        Assert.NotEqual(original, original with { MembershipType = "Regular" });
    }
}
