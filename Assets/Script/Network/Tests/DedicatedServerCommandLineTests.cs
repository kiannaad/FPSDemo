using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network038")]
    public sealed class DedicatedServerCommandLineTests
    {
        [Test]
        public void Parse_WithCompleteArguments_ReturnsTwoAuthorityPawns()
        {
            DedicatedServerLaunchConfiguration configuration = DedicatedServerCommandLine.Parse(new[]
            {
                "--match-id", "17",
                "--data-port", "31000",
                "--health-port", "31001",
                "--credential", "secret",
                "--level-id", "SampleScene",
                "--content-version", "v1",
                "--authority-pawn", "100|1|1|UGxheWVyUG9pbnQgMQ==|Y29ubmVjdGlvbi1h",
                "--authority-pawn", "200|2|1|UGxheWVyUG9pbnQgMg==|Y29ubmVjdGlvbi1i"
            });

            Assert.That(configuration.MatchId, Is.EqualTo(17));
            Assert.That(configuration.AuthorityPawns, Has.Count.EqualTo(2));
            Assert.That(configuration.AuthorityPawns[1].SpawnPointId, Is.EqualTo("PlayerPoint 2"));
            Assert.That(configuration.AuthorityPawns[1].ConnectionId, Is.EqualTo("connection-b"));
        }

        [Test]
        public void Parse_WithoutHealthPort_RejectsArguments()
        {
            Assert.Throws<System.ArgumentException>(() => DedicatedServerCommandLine.Parse(new[]
            {
                "--match-id", "17",
                "--data-port", "31000"
            }));
        }
    }
}
