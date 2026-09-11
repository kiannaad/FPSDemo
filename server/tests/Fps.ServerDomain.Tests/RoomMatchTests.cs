using Fps.ServerDomain.Matches;
using Fps.ServerDomain.Players;
using Fps.ServerDomain.Rooms;
using NUnit.Framework;

namespace Fps.ServerDomain.Tests;

public sealed class RoomMatchTests
{
    [Test]
    public void SetReady_IsIdempotent_AndStartsExactlyOnceForTwoMembers()
    {
        var rooms = new RoomManager();
        ServerRoom room = rooms.CreateRoom("connection-a");
        rooms.JoinRoom(room.RoomId, "connection-b");

        Assert.That(rooms.SetReady(room.RoomId, "connection-a", true).State, Is.EqualTo(ServerRoomState.Waiting));
        Assert.That(rooms.SetReady(room.RoomId, "connection-a", true).State, Is.EqualTo(ServerRoomState.Waiting));
        Assert.That(rooms.SetReady(room.RoomId, "connection-b", true).State, Is.EqualTo(ServerRoomState.Starting));
        Assert.That(rooms.SetReady(room.RoomId, "connection-b", true).State, Is.EqualTo(ServerRoomState.Starting));
    }

    [Test]
    public void SetReady_SingleMember_StartsTheRoom()
    {
        var rooms = new RoomManager();
        ServerRoom room = rooms.CreateRoom("connection-a");

        Assert.That(rooms.SetReady(room.RoomId, "connection-a", true).State, Is.EqualTo(ServerRoomState.Starting));
    }

    [Test]
    public void JoinRoom_RejectsThirdConnection()
    {
        var rooms = new RoomManager();
        ServerRoom room = rooms.CreateRoom("connection-a");
        rooms.JoinRoom(room.RoomId, "connection-b");

        Assert.Throws<InvalidOperationException>(() => rooms.JoinRoom(room.RoomId, "connection-c"));
    }

    [Test]
    public void Start_ReservesDistinctSpawnPointsAndCreatesOwnedPawns()
    {
        ServerMatch match = CreateMatch();

        match.Start();

        Assert.That(match.Pawns, Has.Count.EqualTo(2));
        Assert.That(match.Pawns.Select(pawn => pawn.SpawnPointId).Distinct().Count(), Is.EqualTo(2));
        Assert.That(match.Players.All(player => player.ControlledPawnId != 0), Is.True);
        Assert.That(match.Players.All(player => player.PossessionRevision == 1), Is.True);
    }

    [Test]
    public void Start_SinglePlayer_CreatesOneOwnedPawn()
    {
        var player = new ServerPlayer(1, "connection-a");
        var match = new ServerMatch(new[] { player }, new[] { "spawn-a" });

        match.Start();

        Assert.That(match.Pawns, Has.Count.EqualTo(1));
        Assert.That(match.Pawns.Single().OwnerPlayerId, Is.EqualTo(player.PlayerId));
        Assert.That(player.ControlledPawnId, Is.Not.Zero);
    }

    [Test]
    public void Start_WhenSecondPawnCreationFails_ReleasesAllReservations()
    {
        ServerMatch match = CreateMatch(failCreationAt: 2);

        Assert.Throws<InvalidOperationException>(() => match.Start());

        Assert.That(match.Pawns, Is.Empty);
        Assert.That(match.AvailableSpawnPointCount, Is.EqualTo(2));
        Assert.That(match.Players.All(player => player.ControlledPawnId == 0), Is.True);
    }

    [Test]
    public void Stop_AfterStart_IsIdempotentAndReleasesPawnsAndSpawnPoints()
    {
        ServerMatch match = CreateMatch();
        match.Start();

        match.Stop();
        match.Stop();

        Assert.That(match.Pawns, Is.Empty);
        Assert.That(match.AvailableSpawnPointCount, Is.EqualTo(2));
        Assert.That(match.Players.All(player => player.ControlledPawnId == 0), Is.True);
    }

    [Test]
    public void PossessionRevision_RejectsOlderControlPackets()
    {
        var player = new ServerPlayer(1, "connection-a");
        player.Possess(101);
        player.Possess(102);

        Assert.That(player.AcceptsPossessionRevision(1), Is.False);
        Assert.That(player.AcceptsPossessionRevision(2), Is.True);
    }

    private static ServerMatch CreateMatch(int failCreationAt = 0)
    {
        var players = new[]
        {
            new ServerPlayer(1, "connection-a"),
            new ServerPlayer(2, "connection-b")
        };
        return new ServerMatch(players, new[] { "spawn-a", "spawn-b" }, failCreationAt);
    }
}
