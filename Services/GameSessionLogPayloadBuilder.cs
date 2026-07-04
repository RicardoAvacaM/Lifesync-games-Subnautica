using System.Globalization;
using System.Text;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>Construye el JSON de POST /game-logs/sessions (sin JsonUtility por raw_log anidado).</summary>
    internal static class GameSessionLogPayloadBuilder
    {
        /// <summary>
        /// Payload de un muestreo de 1 minuto en partida: un evento <c>stats_sample</c>
        /// con todas las columnas del CSV local + resumen de máximos y canjes de mejoras.
        /// </summary>
        internal static string BuildMinuteSampleJson(
            int playerId,
            int videogameId,
            string sessionStart,
            string sessionEnd,
            string modVersion,
            int sessionPointsSpent,
            int sessionRedemptionsCount,
            PlayerStatsSnapshot stats,
            SessionProgressSnapshot progress)
        {
            var statsJson = stats.ToJsonDataObject();
            var sb = new StringBuilder(statsJson.Length + 512);
            sb.Append('{');
            AppendInt(sb, "player_id", playerId, first: true);
            AppendInt(sb, "videogame_id", videogameId);
            AppendString(sb, "session_start", sessionStart);
            AppendString(sb, "session_end", sessionEnd);
            AppendString(sb, "mod_version", modVersion);
            AppendInt(sb, "total_points_spent", sessionPointsSpent);
            AppendInt(sb, "redemptions_count", sessionRedemptionsCount);
            sb.Append(",\"raw_log\":{");
            sb.Append("\"events\":[");
            sb.Append(BuildEventJson("stats_sample", sessionEnd, statsJson));
            sb.Append("],\"summary\":{");
            sb.Append("\"samples_count\":1");
            sb.Append(",\"redemptions_count\":").Append(sessionRedemptionsCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"total_points_spent\":").Append(sessionPointsSpent.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"health\":").Append(FormatFloat(stats.Health));
            sb.Append(",\"health_max\":").Append(FormatFloat(stats.HealthMax));
            sb.Append(",\"oxygen\":").Append(FormatFloat(stats.Oxygen));
            sb.Append(",\"oxygen_max\":").Append(FormatFloat(stats.OxygenMax));
            sb.Append(",\"health_bonus_cfg\":").Append(stats.HealthBonusCfg.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"oxygen_bonus_cfg\":").Append(stats.OxygenBonusCfg.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"food\":").Append(FormatFloat(stats.Food));
            sb.Append(",\"water\":").Append(FormatFloat(stats.Water));
            sb.Append(",\"pos_x\":").Append(FormatFloat(stats.PosX));
            sb.Append(",\"pos_y\":").Append(FormatFloat(stats.PosY));
            sb.Append(",\"pos_z\":").Append(FormatFloat(stats.PosZ));
            sb.Append(",\"depth\":").Append(FormatFloat(stats.Depth));
            sb.Append(",\"redemptions_upgrades_total\":").Append(progress.RedemptionsUpgradesTotal.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"upgrade_redemptions\":{");
            sb.Append("\"max_health\":").Append(progress.RedemptionsMaxHealth.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"max_oxygen\":").Append(progress.RedemptionsMaxOxygen.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"knife_damage\":").Append(progress.RedemptionsKnifeDamage.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"flashlight_capacity\":").Append(progress.RedemptionsFlashlightCapacity.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"flashlight_drain\":").Append(progress.RedemptionsFlashlightDrain.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"seaglide_capacity\":").Append(progress.RedemptionsSeaglideCapacity.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"seaglide_speed\":").Append(progress.RedemptionsSeaglideSpeed.ToString(CultureInfo.InvariantCulture));
            sb.Append("}}}");
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildEventJson(string type, string timestampUtc, string dataJson)
        {
            var data = string.IsNullOrEmpty(dataJson) ? "{}" : dataJson;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"type\":\"{0}\",\"timestamp\":\"{1}\",\"data\":{2}}}",
                EscapeJson(type),
                EscapeJson(timestampUtc),
                data);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AppendInt(StringBuilder sb, string key, int value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append(",\"").Append(key).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append('"');
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
