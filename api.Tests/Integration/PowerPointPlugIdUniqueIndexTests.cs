using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Integration;

public class PowerPointPlugIdUniqueIndexTests : SqliteIntegrationTestBase
{
    private static Flat MakeFlat(string userId) => new()
    {
        FlatId = Guid.NewGuid(),
        UserId = userId,
        Name = "Test Flat",
        AnnualKwhBaseline = 3500m,
        SpikeThreshold = 2.0m
    };

    private static Room MakeRoom(Guid flatId) => new()
    {
        RoomId = Guid.NewGuid(),
        FlatId = flatId,
        Name = "Room",
        SortOrder = 1
    };

    [Fact]
    public async Task SaveChanges_DuplicatePlugIdWithinSameFlat_ThrowsDbUpdateException()
    {
        using var db = CreateContext();

        var user = new User { UserId = "user-1" };
        var flat = MakeFlat(user.UserId);
        var room = MakeRoom(flat.FlatId);
        db.Users.Add(user);
        db.Flats.Add(flat);
        db.Rooms.Add(room);

        db.PowerPoints.Add(new PowerPoint
        {
            PowerPointId = Guid.NewGuid(),
            RoomId = room.RoomId,
            FlatId = flat.FlatId,
            Name = "Socket 1",
            PlugId = "shared-plug"
        });
        db.PowerPoints.Add(new PowerPoint
        {
            PowerPointId = Guid.NewGuid(),
            RoomId = room.RoomId,
            FlatId = flat.FlatId,
            Name = "Socket 2",
            PlugId = "shared-plug"
        });

        var act = async () => await db.SaveChangesAsync();

        await Should.ThrowAsync<DbUpdateException>(act);
    }

    [Fact]
    public async Task SaveChanges_TwoNullPlugIdsWithinSameFlat_Succeeds()
    {
        using var db = CreateContext();

        var user = new User { UserId = "user-2" };
        var flat = MakeFlat(user.UserId);
        var room = MakeRoom(flat.FlatId);
        db.Users.Add(user);
        db.Flats.Add(flat);
        db.Rooms.Add(room);

        var powerPoint1 = new PowerPoint
        {
            PowerPointId = Guid.NewGuid(),
            RoomId = room.RoomId,
            FlatId = flat.FlatId,
            Name = "Socket 1",
            PlugId = null
        };
        var powerPoint2 = new PowerPoint
        {
            PowerPointId = Guid.NewGuid(),
            RoomId = room.RoomId,
            FlatId = flat.FlatId,
            Name = "Socket 2",
            PlugId = null
        };
        db.PowerPoints.Add(powerPoint1);
        db.PowerPoints.Add(powerPoint2);

        var act = async () => await db.SaveChangesAsync();

        await Should.NotThrowAsync(act);

        using var verifyDb = CreateContext();
        (await verifyDb.PowerPoints.CountAsync(p =>
            p.PowerPointId == powerPoint1.PowerPointId || p.PowerPointId == powerPoint2.PowerPointId))
            .ShouldBe(2);
    }

    [Fact]
    public async Task SaveChanges_SamePlugIdAcrossDifferentFlats_Succeeds()
    {
        using var db = CreateContext();

        var userA = new User { UserId = "user-3a" };
        var flatA = MakeFlat(userA.UserId);
        var roomA = MakeRoom(flatA.FlatId);
        var userB = new User { UserId = "user-3b" };
        var flatB = MakeFlat(userB.UserId);
        var roomB = MakeRoom(flatB.FlatId);

        db.Users.AddRange(userA, userB);
        db.Flats.AddRange(flatA, flatB);
        db.Rooms.AddRange(roomA, roomB);

        var powerPointA = new PowerPoint
        {
            PowerPointId = Guid.NewGuid(),
            RoomId = roomA.RoomId,
            FlatId = flatA.FlatId,
            Name = "Socket A",
            PlugId = "cross-flat-plug"
        };
        var powerPointB = new PowerPoint
        {
            PowerPointId = Guid.NewGuid(),
            RoomId = roomB.RoomId,
            FlatId = flatB.FlatId,
            Name = "Socket B",
            PlugId = "cross-flat-plug"
        };
        db.PowerPoints.Add(powerPointA);
        db.PowerPoints.Add(powerPointB);

        var act = async () => await db.SaveChangesAsync();

        await Should.NotThrowAsync(act);

        using var verifyDb = CreateContext();
        (await verifyDb.PowerPoints.CountAsync(p =>
            p.PowerPointId == powerPointA.PowerPointId || p.PowerPointId == powerPointB.PowerPointId))
            .ShouldBe(2);
    }
}
