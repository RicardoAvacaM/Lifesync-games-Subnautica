using System.Globalization;
using System.Text;
using LifeSyncGamesSubnautica;
using UnityEngine;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// Foto de progreso al cerrar sesión LifeSync: máximos de vida/oxígeno y canjes acumulados de mejoras permanentes.
    /// Usa cfg del mod (siempre disponible) y valores in-game si <see cref="Player.main"/> existe.
    /// </summary>
    internal struct SessionProgressSnapshot
    {
        private const float DefaultBaseHealth = 100f;
        private const float DefaultBaseOxygen = 45f;

        internal static readonly string[] CsvHeader =
        {
            "utc_time",
            "player_id",
            "health_max",
            "oxygen_max",
            "health_bonus_cfg",
            "oxygen_bonus_cfg",
            "health_max_from_player",
            "oxygen_max_from_player",
            "knife_bonus_damage_cfg",
            "flashlight_capacity_bonus_pct_cfg",
            "flashlight_drain_reduction_cfg",
            "seaglide_capacity_bonus_pct_cfg",
            "seaglide_speed_bonus_cfg",
            "redemptions_max_health",
            "redemptions_max_oxygen",
            "redemptions_knife_damage",
            "redemptions_flashlight_capacity",
            "redemptions_flashlight_drain",
            "redemptions_seaglide_capacity",
            "redemptions_seaglide_speed",
            "redemptions_upgrades_total",
            "session_redemptions_count",
            "session_points_spent"
        };

        public string UtcTime;
        public int PlayerId;
        public float HealthMax;
        public float OxygenMax;
        public int HealthBonusCfg;
        public int OxygenBonusCfg;
        public bool HealthMaxFromPlayer;
        public bool OxygenMaxFromPlayer;
        public int KnifeBonusDamageCfg;
        public int FlashlightCapacityBonusPctCfg;
        public float FlashlightDrainReductionCfg;
        public int SeaglideCapacityBonusPctCfg;
        public float SeaglideSpeedBonusCfg;
        public int RedemptionsMaxHealth;
        public int RedemptionsMaxOxygen;
        public int RedemptionsKnifeDamage;
        public int RedemptionsFlashlightCapacity;
        public int RedemptionsFlashlightDrain;
        public int RedemptionsSeaglideCapacity;
        public int RedemptionsSeaglideSpeed;
        public int RedemptionsUpgradesTotal;
        public int SessionRedemptionsCount;
        public int SessionPointsSpent;

        internal static SessionProgressSnapshot Build(int sessionRedemptionsCount, int sessionPointsSpent)
        {
            var healthBonus = LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthBonus.Value;
            var oxygenBonus = LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenBonus.Value;

            var healthMax = DefaultBaseHealth + healthBonus;
            var oxygenMax = DefaultBaseOxygen + oxygenBonus;
            var healthFromPlayer = false;
            var oxygenFromPlayer = false;

            var player = Player.main;
            if (player?.liveMixin != null)
            {
                healthMax = player.liveMixin.maxHealth;
                healthFromPlayer = true;
            }

            if (player != null)
            {
                var oxygen = player.GetComponent<Oxygen>() ?? player.GetComponentInChildren<Oxygen>();
                if (oxygen != null)
                {
                    oxygenMax = oxygen.oxygenCapacity;
                    oxygenFromPlayer = true;
                }
            }

            GetUpgradeRedemptionCounts(
                out var redemptionsMaxHealth,
                out var redemptionsMaxOxygen,
                out var redemptionsKnife,
                out var redemptionsFlashlightCapacity,
                out var redemptionsFlashlightDrain,
                out var redemptionsSeaglideCapacity,
                out var redemptionsSeaglideSpeed,
                out var redemptionsTotal);

            return new SessionProgressSnapshot
            {
                UtcTime = FormatUtcNow(),
                PlayerId = LifeSyncGamesSubnauticaPlugin.LifeSyncCachedPlayerId.Value,
                HealthMax = healthMax,
                OxygenMax = oxygenMax,
                HealthBonusCfg = healthBonus,
                OxygenBonusCfg = oxygenBonus,
                HealthMaxFromPlayer = healthFromPlayer,
                OxygenMaxFromPlayer = oxygenFromPlayer,
                KnifeBonusDamageCfg = LifeSyncGamesSubnauticaPlugin.KnifeBonusDamage.Value,
                FlashlightCapacityBonusPctCfg = LifeSyncGamesSubnauticaPlugin.FlashlightCapacityBonusPercent.Value,
                FlashlightDrainReductionCfg = LifeSyncGamesSubnauticaPlugin.FlashlightDrainReduction.Value,
                SeaglideCapacityBonusPctCfg = LifeSyncGamesSubnauticaPlugin.SeaglideCapacityBonusPercent.Value,
                SeaglideSpeedBonusCfg = LifeSyncGamesSubnauticaPlugin.SeaglideSpeedBonus.Value,
                RedemptionsMaxHealth = redemptionsMaxHealth,
                RedemptionsMaxOxygen = redemptionsMaxOxygen,
                RedemptionsKnifeDamage = redemptionsKnife,
                RedemptionsFlashlightCapacity = redemptionsFlashlightCapacity,
                RedemptionsFlashlightDrain = redemptionsFlashlightDrain,
                RedemptionsSeaglideCapacity = redemptionsSeaglideCapacity,
                RedemptionsSeaglideSpeed = redemptionsSeaglideSpeed,
                RedemptionsUpgradesTotal = redemptionsTotal,
                SessionRedemptionsCount = sessionRedemptionsCount,
                SessionPointsSpent = sessionPointsSpent
            };
        }

        /// <summary>
        /// Canjes totales de mejoras permanentes inferidos desde la cfg (cada canje suma un delta fijo).
        /// </summary>
        internal static void GetUpgradeRedemptionCounts(
            out int maxHealth,
            out int maxOxygen,
            out int knifeDamage,
            out int flashlightCapacity,
            out int flashlightDrain,
            out int seaglideCapacity,
            out int seaglideSpeed,
            out int total)
        {
            maxHealth = SafeDivide(LifeSyncGamesSubnauticaPlugin.PlayerMaxHealthBonus.Value, 5);
            maxOxygen = SafeDivide(LifeSyncGamesSubnauticaPlugin.PlayerMaxOxygenBonus.Value, 5);
            knifeDamage = Mathf.Max(0, LifeSyncGamesSubnauticaPlugin.KnifeBonusDamage.Value);
            flashlightCapacity = SafeDivide(LifeSyncGamesSubnauticaPlugin.FlashlightCapacityBonusPercent.Value, 5);
            flashlightDrain = SafeDivideFloat(LifeSyncGamesSubnauticaPlugin.FlashlightDrainReduction.Value, 0.05f);
            seaglideCapacity = SafeDivide(LifeSyncGamesSubnauticaPlugin.SeaglideCapacityBonusPercent.Value, 5);
            seaglideSpeed = SafeDivideFloat(LifeSyncGamesSubnauticaPlugin.SeaglideSpeedBonus.Value, 4f);

            total = maxHealth + maxOxygen + knifeDamage + flashlightCapacity + flashlightDrain +
                    seaglideCapacity + seaglideSpeed;
        }

        internal string ToJsonDataObject()
        {
            var sb = new StringBuilder(768);
            sb.Append('{');
            AppendJsonPair(sb, "utc_time", UtcTime, first: true);
            AppendJsonPair(sb, "player_id", PlayerId.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "health_max", FormatFloat(HealthMax));
            AppendJsonPair(sb, "oxygen_max", FormatFloat(OxygenMax));
            AppendJsonPair(sb, "health_bonus_cfg", HealthBonusCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "oxygen_bonus_cfg", OxygenBonusCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "health_max_from_player", FormatBool(HealthMaxFromPlayer));
            AppendJsonPair(sb, "oxygen_max_from_player", FormatBool(OxygenMaxFromPlayer));
            AppendJsonPair(sb, "knife_bonus_damage_cfg", KnifeBonusDamageCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "flashlight_capacity_bonus_pct_cfg", FlashlightCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "flashlight_drain_reduction_cfg", FormatFloat(FlashlightDrainReductionCfg));
            AppendJsonPair(sb, "seaglide_capacity_bonus_pct_cfg", SeaglideCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "seaglide_speed_bonus_cfg", FormatFloat(SeaglideSpeedBonusCfg));
            AppendJsonPair(sb, "redemptions_max_health", RedemptionsMaxHealth.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "redemptions_max_oxygen", RedemptionsMaxOxygen.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "redemptions_knife_damage", RedemptionsKnifeDamage.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "redemptions_flashlight_capacity", RedemptionsFlashlightCapacity.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "redemptions_flashlight_drain", RedemptionsFlashlightDrain.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "redemptions_seaglide_capacity", RedemptionsSeaglideCapacity.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "redemptions_seaglide_speed", RedemptionsSeaglideSpeed.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "redemptions_upgrades_total", RedemptionsUpgradesTotal.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "session_redemptions_count", SessionRedemptionsCount.ToString(CultureInfo.InvariantCulture));
            AppendJsonPair(sb, "session_points_spent", SessionPointsSpent.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        internal string ToCsvRow()
        {
            var values = new[]
            {
                UtcTime,
                PlayerId.ToString(CultureInfo.InvariantCulture),
                FormatFloat(HealthMax),
                FormatFloat(OxygenMax),
                HealthBonusCfg.ToString(CultureInfo.InvariantCulture),
                OxygenBonusCfg.ToString(CultureInfo.InvariantCulture),
                FormatBool(HealthMaxFromPlayer),
                FormatBool(OxygenMaxFromPlayer),
                KnifeBonusDamageCfg.ToString(CultureInfo.InvariantCulture),
                FlashlightCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture),
                FormatFloat(FlashlightDrainReductionCfg),
                SeaglideCapacityBonusPctCfg.ToString(CultureInfo.InvariantCulture),
                FormatFloat(SeaglideSpeedBonusCfg),
                RedemptionsMaxHealth.ToString(CultureInfo.InvariantCulture),
                RedemptionsMaxOxygen.ToString(CultureInfo.InvariantCulture),
                RedemptionsKnifeDamage.ToString(CultureInfo.InvariantCulture),
                RedemptionsFlashlightCapacity.ToString(CultureInfo.InvariantCulture),
                RedemptionsFlashlightDrain.ToString(CultureInfo.InvariantCulture),
                RedemptionsSeaglideCapacity.ToString(CultureInfo.InvariantCulture),
                RedemptionsSeaglideSpeed.ToString(CultureInfo.InvariantCulture),
                RedemptionsUpgradesTotal.ToString(CultureInfo.InvariantCulture),
                SessionRedemptionsCount.ToString(CultureInfo.InvariantCulture),
                SessionPointsSpent.ToString(CultureInfo.InvariantCulture)
            };

            return string.Join(",", values);
        }

        private static int SafeDivide(int value, int step)
        {
            if (step <= 0 || value <= 0)
            {
                return 0;
            }

            return value / step;
        }

        private static int SafeDivideFloat(float value, float step)
        {
            if (step <= 0f || value <= 0f)
            {
                return 0;
            }

            return Mathf.RoundToInt(value / step);
        }

        private static string FormatUtcNow()
        {
            return System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatBool(bool value)
        {
            return value ? "1" : "0";
        }

        private static void AppendJsonPair(StringBuilder sb, string key, string value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(EscapeJson(key)).Append("\":");
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('"').Append(EscapeJson(value)).Append('"');
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
