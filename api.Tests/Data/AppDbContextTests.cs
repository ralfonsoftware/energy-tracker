using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Data;

public class AppDbContextTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void AppDbContext_HasUsers_DbSet()
    {
        using var ctx = CreateInMemoryContext();
        ctx.Users.ShouldNotBeNull();
    }

    [Fact]
    public async Task AppDbContext_CanAddAndRetrieve_UserAsync()
    {
        await using var ctx = CreateInMemoryContext();

        var user = new User { UserId = "sub|abc123", LocaleOverride = "de-DE" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(CancellationToken.None);

        var saved = await ctx.Users.FindAsync(new object[] { "sub|abc123" }, CancellationToken.None);
        saved.ShouldNotBeNull();
        saved.UserId.ShouldBe("sub|abc123");
        saved.LocaleOverride.ShouldBe("de-DE");
    }

    [Fact]
    public async Task AppDbContext_CanAddUser_WithNullLocaleOverrideAsync()
    {
        await using var ctx = CreateInMemoryContext();

        var user = new User { UserId = "sub|nolocale" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(CancellationToken.None);

        var saved = await ctx.Users.FindAsync(new object[] { "sub|nolocale" }, CancellationToken.None);
        saved.ShouldNotBeNull();
        saved.LocaleOverride.ShouldBeNull();
    }

    [Fact]
    public async Task AppDbContext_RejectsDuplicateUserId_OnSaveAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        await using (var ctx1 = new AppDbContext(options))
        {
            ctx1.Users.Add(new User { UserId = "sub|dup" });
            await ctx1.SaveChangesAsync(CancellationToken.None);
        }

        await using var ctx2 = new AppDbContext(options);
        ctx2.Users.Add(new User { UserId = "sub|dup" });
        await Should.ThrowAsync<Exception>(() => ctx2.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public void AppDbContext_UserConfiguration_MapsUserId_AsPrimaryKey()
    {
        using var ctx = CreateInMemoryContext();
        var entityType = ctx.Model.FindEntityType(typeof(User));
        entityType.ShouldNotBeNull();

        var pk = entityType.FindPrimaryKey();
        pk.ShouldNotBeNull();
        pk.Properties.Count.ShouldBe(1);
        pk.Properties[0].Name.ShouldBe("UserId");
    }

    [Fact]
    public void AppDbContext_UserConfiguration_MapsLocaleOverride_AsOptional()
    {
        using var ctx = CreateInMemoryContext();
        var entityType = ctx.Model.FindEntityType(typeof(User));
        entityType.ShouldNotBeNull();

        var prop = entityType.FindProperty("LocaleOverride");
        prop.ShouldNotBeNull();
        prop.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void AppDbContext_UserConfiguration_MapsToUsers_Table()
    {
        using var ctx = CreateInMemoryContext();
        var entityType = ctx.Model.FindEntityType(typeof(User));
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("Users");
    }
}
