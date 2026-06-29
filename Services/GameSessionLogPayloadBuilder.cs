using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MyFirstSubnauticaMod.Services
{
    internal sealed class SessionLogEvent
    {
        public string Type;
        public string TimestampUtc;
        public string DataJson;
    }

    /// <summary>Construye el JSON de POST /game-logs/sessions (sin JsonUtility por raw_log anidado).</summary>
    internal static class GameSessionLogPayloadBuilder
    {
        internal static string BuildRequestJson(
            int playerId,
            int videogameId,
            string sessionStart,
            string sessionEnd,
            string modVersion,
            int totalPointsSpent,
            int redemptionsCount,
            IReadOnlyList<SessionLogEvent> events,
            SessionProgressSnapshot progress)
        {
            var rawLog = BuildRawLogJson(events, totalPointsSpent, redemptionsCount, progress);
            var sb = new StringBuilder(rawLog.Length + 256);
            sb.Append('{');
            AppendInt(sb, "player_id", playerId, first: true);
            AppendInt(sb, "videogame_id", videogameId);
            AppendString(sb, "session_start", sessionStart);
            AppendString(sb, "session_end", sessionEnd);
            AppendString(sb, "mod_version", modVersion);
            AppendInt(sb, "total_points_spent", totalPointsSpent);
            AppendInt(sb, "redemptions_count", redemptionsCount);
            sb.Append(",\"raw_log\":");
            sb.Append(rawLog);
            sb.Append('}');
            return sb.ToString();
        }

        internal static string BuildEventJson(string type, string timestampUtc, string dataJson)
        {
            var data = string.IsNullOrEmpty(dataJson) ? "{}" : dataJson;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"type\":\"{0}\",\"timestamp\":\"{1}\",\"data\":{2}}}",
                EscapeJson(type),
                EscapeJson(timestampUtc),
                data);
        }

        private static string BuildRawLogJson(
            IReadOnlyList<SessionLogEvent> events,
            int totalPointsSpent,
            int redemptionsCount,
            SessionProgressSnapshot progress)
        {
            var sb = new StringBuilder(4096);
            sb.Append("{\"events\":[");
            for (var i = 0; i < events.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(BuildEventJson(events[i].Type, events[i].TimestampUtc, events[i].DataJson));
            }

            sb.Append("],\"summary\":{");
            sb.Append("\"redemptions_count\":").Append(redemptionsCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"total_points_spent\":").Append(totalPointsSpent.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"health_max\":").Append(FormatFloat(progress.HealthMax));
            sb.Append(",\"oxygen_max\":").Append(FormatFloat(progress.OxygenMax));
            sb.Append(",\"health_bonus_cfg\":").Append(progress.HealthBonusCfg.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"oxygen_bonus_cfg\":").Append(progress.OxygenBonusCfg.ToString(CultureInfo.InvariantCulture));
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
            return sb.ToString();
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
