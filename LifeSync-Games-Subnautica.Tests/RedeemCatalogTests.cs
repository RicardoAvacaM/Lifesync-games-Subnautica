using System.Linq;
using LifeSyncGamesSubnautica.Services;
using NUnit.Framework;

namespace LifeSyncGamesSubnautica.Tests
{
    [TestFixture]
    public class RedeemCatalogTests
    {
        [TearDown]
        public void TearDown()
        {
            RedeemCatalog.EscalationCountsOverride = null;
        }

        [Test]
        public void TryGet_KnownMechanicIds_ReturnsTrue()
        {
            for (var id = 35; id <= 43; id++)
            {
                Assert.That(RedeemCatalog.TryGet(id, out var recipe), Is.True, "id=" + id);
                Assert.That(recipe, Is.Not.Null);
                Assert.That(recipe.Costs, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            Assert.That(RedeemCatalog.TryGet(99999, out var recipe), Is.False);
            Assert.That(recipe, Is.Null);
        }

        [Test]
        public void GetEffectiveCosts_ZeroRedeems_ReturnsBaseAmounts()
        {
            RedeemCatalog.EscalationCountsOverride = string.Empty;
            Assert.That(RedeemCatalog.TryGet(35, out var recipe), Is.True);

            var costs = RedeemCatalog.GetEffectiveCosts(35, recipe);
            Assert.That(costs.Count, Is.EqualTo(1));
            Assert.That(costs[0].PointDimensionId, Is.EqualTo(2));
            Assert.That(costs[0].Amount, Is.EqualTo(10));
        }

        [Test]
        public void GetEffectiveCosts_OneRedeem_AddsFive()
        {
            RedeemCatalog.EscalationCountsOverride = "35=1";
            Assert.That(RedeemCatalog.TryGet(35, out var recipe), Is.True);

            var costs = RedeemCatalog.GetEffectiveCosts(35, recipe);
            Assert.That(costs[0].Amount, Is.EqualTo(15));
        }

        [Test]
        public void GetEffectiveCosts_ThreeRedeems_AddsFifteen()
        {
            RedeemCatalog.EscalationCountsOverride = "35=3";
            Assert.That(RedeemCatalog.TryGet(35, out var recipe), Is.True);

            var costs = RedeemCatalog.GetEffectiveCosts(35, recipe);
            Assert.That(costs[0].Amount, Is.EqualTo(25));
        }

        [Test]
        public void GetEffectiveCosts_MultiCostRecipe_ScalesEachAmount()
        {
            // OxygenRefill id 39: 30 (dim 2) + 20 (dim 4)
            RedeemCatalog.EscalationCountsOverride = "39=2";
            Assert.That(RedeemCatalog.TryGet(39, out var recipe), Is.True);

            var costs = RedeemCatalog.GetEffectiveCosts(39, recipe);
            Assert.That(costs.Count, Is.EqualTo(2));
            Assert.That(costs[0].Amount, Is.EqualTo(40)); // 30 + 10
            Assert.That(costs[1].Amount, Is.EqualTo(30)); // 20 + 10
        }

        [Test]
        public void BuildRedeemBodyJson_ContainsRequiredFields()
        {
            var json = RedeemCatalog.BuildRedeemBodyJson(35, new RedeemCost(2, 10));
            Assert.That(json, Does.Contain("\"modifiable_mechanic_videogame_id\":35"));
            Assert.That(json, Does.Contain("\"point_dimension_id\":2"));
            Assert.That(json, Does.Contain("\"amount\":10"));
            Assert.That(json, Does.Contain("\"metadata\""));
        }

        [Test]
        public void ParseCounts_ValidString_ReturnsMap()
        {
            var map = RedeemCatalog.ParseCounts("40=4;37=1");
            Assert.That(map[40], Is.EqualTo(4));
            Assert.That(map[37], Is.EqualTo(1));
        }

        [Test]
        public void ParseCounts_EmptyOrGarbage_IgnoresInvalid()
        {
            Assert.That(RedeemCatalog.ParseCounts(null), Is.Empty);
            Assert.That(RedeemCatalog.ParseCounts(""), Is.Empty);
            Assert.That(RedeemCatalog.ParseCounts("foo;bar=;=-1;0=3;41=x;42=2"), Is.EquivalentTo(
                new System.Collections.Generic.Dictionary<int, int> { { 42, 2 } }));
        }

        [Test]
        public void SerializeCounts_RoundTrip()
        {
            var original = "40=4;37=1";
            var map = RedeemCatalog.ParseCounts(original);
            var serialized = RedeemCatalog.SerializeCounts(map);
            var again = RedeemCatalog.ParseCounts(serialized);
            Assert.That(again[40], Is.EqualTo(4));
            Assert.That(again[37], Is.EqualTo(1));
            Assert.That(again.Count, Is.EqualTo(2));
        }

        [Test]
        public void RegisterSuccessfulRedeem_IncrementsOverrideCounter()
        {
            RedeemCatalog.EscalationCountsOverride = string.Empty;
            RedeemCatalog.RegisterSuccessfulRedeem(36);
            Assert.That(RedeemCatalog.GetTimesRedeemed(36), Is.EqualTo(1));
            RedeemCatalog.RegisterSuccessfulRedeem(36);
            Assert.That(RedeemCatalog.GetTimesRedeemed(36), Is.EqualTo(2));
            Assert.That(RedeemCatalog.EscalationCountsOverride, Does.Contain("36=2"));
        }
    }
}
