using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class UserEmailMappingTests
{
    [Fact]
    public void EmailUsesTheExistingUniqueUsersEmailColumn()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer("Server=localhost;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new AsiBasecodeDBContext(options);
        var entity = context.Model.FindEntityType(typeof(User))!;
        var property = entity.FindProperty(nameof(User.Email))!;

        Assert.Equal("users", entity.GetTableName());
        Assert.Equal("email", property.GetColumnName(StoreObjectIdentifier.Table("users", entity.GetSchema())));
        Assert.Equal(255, property.GetMaxLength());
        Assert.False(property.IsNullable);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0] == property);
    }
}
