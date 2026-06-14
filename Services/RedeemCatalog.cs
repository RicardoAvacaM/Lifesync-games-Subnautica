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

        /// <summary>Resumen legible de los costos, p. ej. «30 pts (dim 2) + 20 pts (dim 4)».</summary>
        public string DescribeCosts()
        {
            if (Costs == null || Costs.Count == 0)
            {
                return "sin costo";
            }

            var sb = new StringBuilder();
            for (var i = 0; i < Costs.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" + ");
                }

                sb.Append($"{Costs[i].Amount} pts (dim {Costs[i].PointDimensionId})");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Catálogo estático que mapea cada mecánica conocida a su receta de canje y a la acción local
    /// que se ejecuta tras un canje exitoso (p. ej. sumar al daño del cuchillo o curar la vida).
    /// Añade aquí más entradas a medida que se incorporen mecánicas al backend.
    /// </summary>
    internal static class RedeemCatalog
    {
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

            return d;
        }

        internal static bool TryGet(int mechanicVideogameId, out RedeemRecipe recipe)
        {
            return _byMechanicVideogameId.TryGetValue(mechanicVideogameId, out recipe);
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
