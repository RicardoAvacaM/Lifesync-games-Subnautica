using LifeSyncGamesSubnautica.Services;
using NUnit.Framework;

namespace LifeSyncGamesSubnautica.Tests
{
    [TestFixture]
    public class GameSessionLogPayloadBuilderTests
    {
        [Test]
        public void BuildMinuteSampleJson_ContainsCoreFieldsAndStats()
        {
            var stats = new PlayerStatsSnapshot
            {
                Health = 80f,
                HealthMax = 100f,
                Oxygen = 40f,
                OxygenMax = 45f,
                Depth = 12.5f,
                PosX = 1f,
                PosY = -10f,
                PosZ = 3f,
                Food = 50f,
                Water = 50f,
            };

            var progress = new SessionProgressSnapshot
            {
                RedemptionsUpgradesTotal = 2,
                RedemptionsMaxHealth = 1,
                RedemptionsKnifeDamage = 1,
            };

            var json = GameSessionLogPayloadBuilder.BuildMinuteSampleJson(
                playerId: 42,
                videogameId: 19,
                sessionStart: "2026-08-02T12:00:00",
                sessionEnd: "2026-08-02T12:01:00",
                modVersion: "1.0.49",
                sessionPointsSpent: 30,
                sessionRedemptionsCount: 2,
                stats,
                progress);

            Assert.That(json, Does.Contain("\"player_id\":42"));
            Assert.That(json, Does.Contain("\"videogame_id\":19"));
            Assert.That(json, Does.Contain("\"mod_version\":\"1.0.49\""));
            Assert.That(json, Does.Contain("\"raw_log\""));
            Assert.That(json, Does.Contain("\"stats_sample\""));
            Assert.That(json, Does.Contain("\"health\":80"));
            Assert.That(json, Does.Contain("\"depth\":12.5"));
            Assert.That(json, Does.Contain("\"total_points_spent\":30"));
            Assert.That(json, Does.Contain("\"redemptions_count\":2"));
        }
    }
}
