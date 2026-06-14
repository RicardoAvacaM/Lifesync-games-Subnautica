using System;
using System.Collections.Generic;
using System.Globalization;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Receta hardcodeada para canjear una mecánica: dimensión + costo + efecto local.
    /// La clave del diccionario en <see cref="RedeemCatalog"/> es <c>id_modifiable_mechanic_videogame</c>
    /// (el campo que va en el body de <c>POST /videogames/{game}/players/{id}/redeem</c>).
    /// </summary>
    internal class RedeemRecipe
    {
        public int PointDimensionId;
        public int Amount;
        public string EffectSummary;
        public Action ApplyLocalEffect;
    }

    /// <summary>
    /// Catálogo estático que mapea cada mecánica conocida a su receta de canje y a la acción local
    /// que se ejecuta tras un canje exitoso (p. ej. sumar al daño del cuchillo).
    /// Añade aquí más entradas a medida que se incorporen mecánicas al backend.
    /// </summary>
    internal static class RedeemCatalog
    {
        private static readonly Dictionary<int, RedeemRecipe> _byMechanicVideogameId = BuildCatalog();

        private static Dictionary<int, RedeemRecipe> BuildCatalog()
        {
            var d = new Dictionary<int, RedeemRecipe>();

            // KnifeDamageS — id_modifiable_mechanic_videogame = 35.
            // Costo: 10 puntos de la dimensión 2. Efecto local: +1 daño base del cuchillo.
            d[35] = new RedeemRecipe
            {
                PointDimensionId = 2,
                Amount = 10,
                EffectSummary = "+1 daño base del cuchillo (se aplica al próximo Awake del Knife).",
                ApplyLocalEffect = () =>
                {
                    MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value += 1;
                    MyFirstSubnauticaModPlugin.Instance?.Config.Save();
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] KnifeBonusDamage ahora = {MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value}");
                },
            };

            // PlayerMaxHealth — id_modifiable_mechanic_videogame = 36.
            // Costo: 20 puntos de la dimensión 2. Efecto local: +5 a la vida máxima (en caliente).
            d[36] = new RedeemRecipe
            {
                PointDimensionId = 2,
                Amount = 20,
                EffectSummary = "+5 a la vida máxima del jugador (se aplica al instante).",
                ApplyLocalEffect = () =>
                {
                    PlayerStatsApplier.IncrementMaxHealthAndApply(5);
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Redeem] PlayerMaxHealthBonus ahora = {MyFirstSubnauticaModPlugin.PlayerMaxHealthBonus.Value}");
                },
            };

            return d;
        }

        internal static bool TryGet(int mechanicVideogameId, out RedeemRecipe recipe)
        {
            return _byMechanicVideogameId.TryGetValue(mechanicVideogameId, out recipe);
        }

        /// <summary>
        /// Construye el JSON del body esperado por <c>POST .../redeem</c>: incluye un <c>metadata.additionalProp1</c>
        /// vacío como en el ejemplo de la API.
        /// </summary>
        internal static string BuildPreviewBodyJson(int mechanicVideogameId, RedeemRecipe recipe)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"modifiable_mechanic_videogame_id\":{0}," +
                "\"point_dimension_id\":{1}," +
                "\"amount\":{2}," +
                "\"metadata\":{{\"additionalProp1\":{{}}}}}}",
                mechanicVideogameId,
                recipe.PointDimensionId,
                recipe.Amount);
        }
    }
}
