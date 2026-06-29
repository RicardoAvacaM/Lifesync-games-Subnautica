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
            int statsSampleCount)
        {
            var rawLog = BuildRawLogJson(events, statsSampleCount, totalPointsSpent, redemptionsCount);
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
            int statsSampleCount,
            int totalPointsSpent,
            int redemptionsCount)
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
            sb.Append("\"samples_count\":").Append(statsSampleCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"redemptions_count\":").Append(redemptionsCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"total_points_spent\":").Append(totalPointsSpent.ToString(CultureInfo.InvariantCulture));
            sb.Append("}}");
            return sb.ToString();
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
