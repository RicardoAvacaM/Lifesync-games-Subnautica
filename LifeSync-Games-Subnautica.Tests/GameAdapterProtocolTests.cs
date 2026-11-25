using LifeSyncGamesSubnautica.Services;
using NUnit.Framework;

namespace LifeSyncGamesSubnautica.Tests
{
    [TestFixture]
    public class GameAdapterProtocolTests
    {
        [Test]
        public void HealthJson_UsesExactContractFields()
        {
            Assert.That(GameAdapterProtocol.HealthJson(true), Is.EqualTo("{\"status\":\"ok\",\"game_ready\":true}"));
            Assert.That(GameAdapterProtocol.HealthJson(false), Is.EqualTo("{\"status\":\"ok\",\"game_ready\":false}"));
        }

        [Test]
        public void TryParseRequest_ReadsMethodPathAndContentLength()
        {
            const string header = "POST /reward?x=1 HTTP/1.1\r\nHost: localhost:7878\r\nContent-Length: 18\r\n\r\n";
            Assert.That(GameAdapterProtocol.TryParseRequest(header, out var method, out var path, out var length), Is.True);
            Assert.That(method, Is.EqualTo("POST"));
            Assert.That(path, Is.EqualTo("/reward"));
            Assert.That(length, Is.EqualTo(18));
        }

        [Test]
        public void TryReadRewardId_RequiresPositiveInteger()
        {
            Assert.That(GameAdapterProtocol.TryReadRewardId("{\"reward_id\":35}", out var id), Is.True);
            Assert.That(id, Is.EqualTo(35));
            Assert.That(GameAdapterProtocol.TryReadRewardId("{\"reward_id\":0}", out _), Is.False);
            Assert.That(GameAdapterProtocol.TryReadRewardId("{\"reward_id\":\"35\"}", out _), Is.False);
            Assert.That(GameAdapterProtocol.TryReadRewardId("", out _), Is.False);
        }
    }
}
