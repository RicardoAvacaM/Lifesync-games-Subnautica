using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>Un costo del canje: cantidad a descontar de una dimensión de puntos concreta.</summary>
    internal class RedeemCost
    {
        public int PointDimensionId;
        public int Amount;

        public RedeemCost(int pointDimensionId, int amount)
        {
            PointDimensionId = pointDimensionId;
            Amount = amount;
        }
    }

    /// <summary>
    /// Receta hardcodeada para canjear una mecánica: lista de costos + efecto local.
    /// La clave del diccionario en <see cref="RedeemCatalog"/> es <c>id_modifiable_mechanic_videogame</c>
    /// (el campo que va en el body de <c>POST /videogames/{game}/players/{id}/redeem</c>).
    /// Como el endpoint cobra una sola dimensión por llamada, las mecánicas con varios costos
    /// hacen un POST por cada entrada de <see cref="Costs"/>.
    /// </summary>
    internal class RedeemRecipe
    {
        public List<RedeemCost> Costs;
        public string EffectSummary;
        public Action ApplyLocalEffect;

        /// <summary>Resumen legible de los costos, p. ej. «30 pts dimensión: Fisico + 20 pts dimensión: Mental».</summary>
        public string DescribeCosts(IReadOnlyDictionary<int, string> dimensionNames = null)
        {
            return DescribeCostList(Costs, dimensionNames);
        }

        internal static string DescribeCostList(
            IList<RedeemCost> costs,
            IReadOnlyDictionary<int, string> dimensionNames = null)
        {
            if (costs == null || costs.Count == 0)
            {
                return "sin costo";
            }

            var sb = new StringBuilder();
            for (var i = 0; i < costs.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" + ");
                }

                sb.Append($"{costs[i].Amount} pts dimensión: {FormatDimensionLabel(costs[i].PointDimensionId, dimensionNames)}");
            }

            return sb.ToString();
        }

        internal static string FormatDimensionLabel(int pointDimensionId, IReadOnlyDictionary<int, string> dimensionNames)
        {
            if (dimensionNames != null &&
                dimensionNames.TryGetValue(pointDimensionId, out var name) &&
                !string.IsNullOrEmpty(name))
            {
                return name;
            }

            return $"dim {pointDimensionId}";
        }
    }

    /// <summary>
    /// Catálogo estático que mapea cada mecánica conocida a su receta de canje y a la acción local
    /// que se ejecuta tras un canje exitoso (p. ej. sumar al daño del cuchillo o curar la vida).
    /// Añade aquí más entradas a medida que se incorporen mecánicas al backend.
    /// Tras cada canje exitoso, cada monto de la receta aumenta en <see cref="CostIncreasePerRedeem"/> puntos
    /// (persistido en cfg como contador de canjes por mecánica).
    /// </summary>
    internal static class RedeemCatalog
    {
        internal const int CostIncreasePerRedeem = 5;

        private static readonly Dictionary<int, RedeemRecipe> _byMechanicVideogameId = BuildCatalog();

        private static Dictionary<int, RedeemRecipe> BuildCatalog()
        {
            var d = new Dictionary<int, RedeemRecipe>();

            // KnifeDamageS — id 35. Costo: 10 pts (dim 2). Efecto: +1 daño base del cuchillo.
            d[35] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(2, 10) },
                EffectSummary = "+1 daño base del cuchillo (se aplica al próximo Awake del Knife).",
                ApplyLocalEffect = () =>
                {
                    MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value += 1;
                    MyFirstSubnauticaModPlugin.Instance?.Config.Save();
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] KnifeBonusDamage ahora = {MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value}");
                },
            };

            // PlayerMaxHealth — id 36. Costo: 20 pts (dim 2). Efecto: +5 a la vida máxima (en caliente).
            d[36] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(2, 20) },
                EffectSummary = "+5 a la vida máxima del jugador (se aplica al instante).",
                ApplyLocalEffect = () =>
                {
                    PlayerStatsApplier.IncrementMaxHealthAndApply(5);
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] PlayerMaxHealthBonus ahora = {MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus.Value}");
                },
            };

            // PlayerMaxOxygen — id 37. Costo: 20 pts (dim 2). Efecto: +5 al oxígeno máximo (en caliente).
            d[37] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(2, 20) },
                EffectSummary = "+5 al oxígeno máximo del jugador (se aplica al instante).",
                ApplyLocalEffect = () =>
                {
                    PlayerStatsApplier.IncrementMaxOxygenAndApply(5);
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] PlayerMaxOxygenBonus ahora = {MyFirstSubnauticaModPlugin.PlayerMaxOxygenBonus.Value}");
                },
            };

            // HealFull — id 38. Costo: 40 pts (dim 2). Efecto: cura la barra de vida por completo.
            d[38] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(2, 40) },
                EffectSummary = "Cura por completo la barra de vida.",
                ApplyLocalEffect = () =>
                {
                    if (!PlayerStatsApplier.HealToFull())
                    {
                        MyFirstSubnauticaModPlugin.Log.LogWarning("[LifeSync][Redeem] HealToFull: jugador/LiveMixin no listo.");
                    }
                },
            };

            // OxygenRefill — id 39. Costo: 30 pts (dim 2) + 20 pts (dim 4). Efecto: rellena el oxígeno por completo.
            d[39] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(2, 30), new RedeemCost(4, 20) },
                EffectSummary = "Rellena por completo la barra de oxígeno.",
                ApplyLocalEffect = () =>
                {
                    if (!PlayerStatsApplier.RestoreOxygenToFull())
                    {
                        MyFirstSubnauticaModPlugin.Log.LogWarning("[LifeSync][Redeem] RestoreOxygenToFull: jugador/oxygenMgr no listo.");
                    }
                },
            };

            // FlashlightCapacity — id 40. Costo: 20 pts (dim 4). Efecto: +5% capacidad de batería de la linterna.
            d[40] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(4, 20) },
                EffectSummary = "+5% a la capacidad de batería de la linterna (100→105→110…).",
                ApplyLocalEffect = () =>
                {
                    FlashlightModifiers.IncrementCapacityAndApply(5);
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] FlashlightCapacityBonusPercent ahora = {MyFirstSubnauticaModPlugin.FlashlightCapacityBonusPercent.Value}%");
                },
            };

            // FlashlightDrain — id 41. Costo: 40 pts (dim 4). Efecto: -0.05/s consumo (tope mínimo 0.2/s).
            d[41] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(4, 40) },
                EffectSummary = "-0.05/s al consumo de la linterna (no baja de 0.2/s).",
                ApplyLocalEffect = () =>
                {
                    FlashlightModifiers.IncrementDrainReductionAndApply(0.05f);
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] FlashlightDrainReduction ahora = {MyFirstSubnauticaModPlugin.FlashlightDrainReduction.Value:0.###}");
                },
            };

            // SeaglideCapacity — id 42. Costo: 20 pts (dim 4). Efecto: +5% capacidad de batería del deslizador.
            d[42] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(4, 20) },
                EffectSummary = "+5% a la capacidad de batería del deslizador (100→105→110…).",
                ApplyLocalEffect = () =>
                {
                    SeaglideModifiers.IncrementCapacityAndApply(5);
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] SeaglideCapacityBonusPercent ahora = {MyFirstSubnauticaModPlugin.SeaglideCapacityBonusPercent.Value}%");
                },
            };

            // SeaglideSpeed — id 43. Costo: 50 pts (dim 4). Efecto: +4 velocidad del deslizador (tope 48).
            d[43] = new RedeemRecipe
            {
                Costs = new List<RedeemCost> { new RedeemCost(4, 50) },
                EffectSummary = "+4 a la velocidad del deslizador (tope total 48).",
                ApplyLocalEffect = () =>
                {
                    SeaglideModifiers.IncrementSpeedAndApply(4f);
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] SeaglideSpeedBonus ahora = {MyFirstSubnauticaModPlugin.SeaglideSpeedBonus.Value:0.##}");
                },
            };

            return d;
        }

        internal static bool TryGet(int mechanicVideogameId, out RedeemRecipe recipe)
        {
            return _byMechanicVideogameId.TryGetValue(mechanicVideogameId, out recipe);
        }

        /// <summary>Canjes exitosos previos de esta mecánica (desde cfg).</summary>
        internal static int GetTimesRedeemed(int mechanicVideogameId)
        {
            if (mechanicVideogameId <= 0)
            {
                return 0;
            }

            var map = ParseCounts(MyFirstSubnauticaModPlugin.RedeemCostEscalationCounts?.Value);
            return map.TryGetValue(mechanicVideogameId, out var n) ? Math.Max(0, n) : 0;
        }

        /// <summary>
        /// Costes a cobrar ahora: base + (canjes previos × <see cref="CostIncreasePerRedeem"/>) en cada monto.
        /// </summary>
        internal static List<RedeemCost> GetEffectiveCosts(int mechanicVideogameId, RedeemRecipe recipe)
        {
            var result = new List<RedeemCost>();
            if (recipe?.Costs == null)
            {
                return result;
            }

            var bonus = GetTimesRedeemed(mechanicVideogameId) * CostIncreasePerRedeem;
            for (var i = 0; i < recipe.Costs.Count; i++)
            {
                var c = recipe.Costs[i];
                result.Add(new RedeemCost(c.PointDimensionId, c.Amount + bonus));
            }

            return result;
        }

        internal static string DescribeEffectiveCosts(
            int mechanicVideogameId,
            RedeemRecipe recipe,
            IReadOnlyDictionary<int, string> dimensionNames = null)
        {
            return RedeemRecipe.DescribeCostList(GetEffectiveCosts(mechanicVideogameId, recipe), dimensionNames);
        }

        /// <summary>Tras un canje OK: incrementa el contador y guarda cfg (el próximo canje costará +5 por monto).</summary>
        internal static void RegisterSuccessfulRedeem(int mechanicVideogameId)
        {
            if (mechanicVideogameId <= 0)
            {
                return;
            }

            var map = ParseCounts(MyFirstSubnauticaModPlugin.RedeemCostEscalationCounts?.Value);
            map.TryGetValue(mechanicVideogameId, out var n);
            map[mechanicVideogameId] = n + 1;

            if (MyFirstSubnauticaModPlugin.RedeemCostEscalationCounts != null)
            {
                MyFirstSubnauticaModPlugin.RedeemCostEscalationCounts.Value = SerializeCounts(map);
                MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            }

            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Redeem] Escalado coste id={mechanicVideogameId}: canjes={map[mechanicVideogameId]} " +
                $"(próximo +{map[mechanicVideogameId] * CostIncreasePerRedeem} pts por monto).");
        }

        private static Dictionary<int, int> ParseCounts(string raw)
        {
            var map = new Dictionary<int, int>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return map;
            }

            var parts = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                var eq = p.IndexOf('=');
                if (eq <= 0 || eq >= p.Length - 1)
                {
                    continue;
                }

                if (!int.TryParse(p.Substring(0, eq).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                    id <= 0)
                {
                    continue;
                }

                if (!int.TryParse(p.Substring(eq + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                {
                    continue;
                }

                map[id] = Math.Max(0, count);
            }

            return map;
        }

        private static string SerializeCounts(Dictionary<int, int> map)
        {
            if (map == null || map.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var first = true;
            foreach (var kv in map)
            {
                if (kv.Value <= 0)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(';');
                }

                first = false;
                sb.Append(kv.Key.ToString(CultureInfo.InvariantCulture));
                sb.Append('=');
                sb.Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Construye el JSON del body para un costo concreto de <c>POST .../redeem</c>:
        /// incluye un <c>metadata.additionalProp1</c> vacío como en el ejemplo de la API.
        /// </summary>
        internal static string BuildRedeemBodyJson(int mechanicVideogameId, RedeemCost cost)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"modifiable_mechanic_videogame_id\":{0}," +
                "\"point_dimension_id\":{1}," +
                "\"amount\":{2}," +
                "\"metadata\":{{\"additionalProp1\":{{}}}}}}",
                mechanicVideogameId,
                cost.PointDimensionId,
                cost.Amount);
        }
    }
}
