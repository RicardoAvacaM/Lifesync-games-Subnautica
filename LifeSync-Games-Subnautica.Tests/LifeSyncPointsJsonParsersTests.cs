using LifeSyncGamesSubnautica.Services.Models;
using NUnit.Framework;

namespace LifeSyncGamesSubnautica.Tests
{
    [TestFixture]
    public class LifeSyncPointsJsonParsersTests
    {
        [Test]
        public void TryParseWhoamiPlayerId_ValidJson_ReturnsId()
        {
            var ok = LifeSyncPointsJsonParsers.TryParseWhoamiPlayerId(
                "{\"id_players\":42,\"email\":\"a@b.c\"}",
                out var id);
            Assert.That(ok, Is.True);
            Assert.That(id, Is.EqualTo(42));
        }

        [Test]
        public void TryParseWhoamiPlayerId_MissingOrEmpty_ReturnsFalse()
        {
            Assert.That(LifeSyncPointsJsonParsers.TryParseWhoamiPlayerId("", out _), Is.False);
            Assert.That(LifeSyncPointsJsonParsers.TryParseWhoamiPlayerId("{}", out _), Is.False);
            Assert.That(LifeSyncPointsJsonParsers.TryParseWhoamiPlayerId(null, out _), Is.False);
        }

        [Test]
        public void TryParseMechanicsArray_ValidArray_ParsesRows()
        {
            var json =
                "[{\"id_modifiable_mechanic_videogame\":35,\"id_videogame\":19,\"videogame_name\":\"Subnautica\"," +
                "\"options\":{},\"id_modifiable_mechanic\":1,\"modifiable_mechanic_name\":\"KnifeDamageS\"," +
                "\"modifiable_mechanic_description\":\"+1 knife\",\"modifiable_mechanic_type\":\"permanent\"}]";

            Assert.That(LifeSyncPointsJsonParsers.TryParseMechanicsArray(json, out var rows), Is.True);
            Assert.That(rows.Length, Is.EqualTo(1));
            Assert.That(rows[0].id_modifiable_mechanic_videogame, Is.EqualTo(35));
            Assert.That(rows[0].modifiable_mechanic_name, Is.EqualTo("KnifeDamageS"));
        }

        [Test]
        public void TryParseMechanicsArray_EmptyArray_ReturnsEmpty()
        {
            Assert.That(LifeSyncPointsJsonParsers.TryParseMechanicsArray("[]", out var rows), Is.True);
            Assert.That(rows.Length, Is.EqualTo(0));
        }

        [Test]
        public void TryParseMechanicsArray_InvalidJson_ReturnsFalse()
        {
            Assert.That(LifeSyncPointsJsonParsers.TryParseMechanicsArray("not-json", out _), Is.False);
            Assert.That(LifeSyncPointsJsonParsers.TryParseMechanicsArray("", out _), Is.False);
        }

        [Test]
        public void TryParsePlayerPointsBalanceArray_ValidArray_ParsesBalance()
        {
            var json =
                "[{\"id_players\":42,\"id_point_dimension\":2,\"id_attributes\":1,\"balance\":100," +
                "\"dimension_code\":\"PHY\",\"dimension_name\":\"Fisico\",\"attribute_name\":\"A\"," +
                "\"subattribute_name\":\"B\"}]";

            Assert.That(LifeSyncPointsJsonParsers.TryParsePlayerPointsBalanceArray(json, out var rows), Is.True);
            Assert.That(rows.Length, Is.EqualTo(1));
            Assert.That(rows[0].balance, Is.EqualTo(100));
            Assert.That(rows[0].id_point_dimension, Is.EqualTo(2));
        }

        [Test]
        public void TryParsePlayerPointsBalanceArray_Invalid_ReturnsFalse()
        {
            Assert.That(LifeSyncPointsJsonParsers.TryParsePlayerPointsBalanceArray("", out _), Is.False);
            Assert.That(LifeSyncPointsJsonParsers.TryParsePlayerPointsBalanceArray("{}", out _), Is.False);
        }
    }
}
