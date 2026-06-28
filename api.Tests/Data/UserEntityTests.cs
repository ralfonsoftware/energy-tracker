using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using EnergyTracker.Api.Data.Entities;
using Shouldly;

namespace api.Tests.Data;

public class UserEntityTests
{
    [Fact]
    public void User_HasUserId_Property()
    {
        var user = new User { UserId = "sub|123" };
        user.UserId.ShouldBe("sub|123");
    }

    [Fact]
    public void User_HasLocaleOverride_NullableProperty()
    {
        var user = new User { UserId = "sub|123" };
        user.LocaleOverride.ShouldBeNull();

        user.LocaleOverride = "de-DE";
        user.LocaleOverride.ShouldBe("de-DE");
    }

    [Fact]
    public void User_HasNoDataAnnotationAttributes()
    {
        var type = typeof(User);
        var forbiddenAttributeTypes = new[]
        {
            typeof(KeyAttribute),
            typeof(RequiredAttribute),
            typeof(MaxLengthAttribute),
            typeof(MinLengthAttribute),
            typeof(StringLengthAttribute),
            typeof(ColumnAttribute),
            typeof(TableAttribute),
            typeof(DatabaseGeneratedAttribute),
        };

        foreach (var property in type.GetProperties())
        {
            foreach (var forbidden in forbiddenAttributeTypes)
            {
                property.GetCustomAttribute(forbidden).ShouldBeNull(
                    $"Property '{property.Name}' on User must not have [{forbidden.Name}] — use Fluent API in UserConfiguration instead");
            }
        }

        foreach (var forbidden in forbiddenAttributeTypes)
        {
            type.GetCustomAttribute(forbidden).ShouldBeNull(
                $"User class must not have [{forbidden.Name}] — use Fluent API in UserConfiguration instead");
        }
    }

    [Fact]
    public void User_IsClass_NotRecord()
    {
        typeof(User).IsClass.ShouldBeTrue();
        // Records expose a Clone method; regular classes do not
        typeof(User).GetMethod("<Clone>$").ShouldBeNull("User must be a regular class, not a record");
    }
}
