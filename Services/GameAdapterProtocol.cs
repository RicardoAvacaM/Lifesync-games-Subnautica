using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// Contrato HTTP del game adapter que el cliente consulta en localhost:7878.
    /// El cuerpo de /health y /reward no admite propiedades extra.
    /// </summary>
    internal static class GameAdapterProtocol
    {
        internal const int Port = 7878;

        internal static string HealthJson(bool gameReady)
        {
            return gameReady
                ? "{\"status\":\"ok\",\"game_ready\":true}"
                : "{\"status\":\"ok\",\"game_ready\":false}";
        }

        internal static string RewardOkJson()
        {
            return "{\"ok\":true}";
        }

        internal static string ErrorJson(string error)
        {
            return "{\"ok\":false,\"error\":" + Quote(error ?? string.Empty) + "}";
        }

        internal static bool TryParseRequest(string headerText, out string method, out string path, out int contentLength)
        {
            method = null;
            path = null;
            contentLength = 0;
            if (string.IsNullOrEmpty(headerText))
            {
                return false;
            }

            var lineEnd = headerText.IndexOf('\n');
            var requestLine = (lineEnd >= 0 ? headerText.Substring(0, lineEnd) : headerText).Trim();
            var parts = requestLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            method = parts[0].ToUpperInvariant();
            var rawPath = parts[1];
            var query = rawPath.IndexOf('?');
            path = (query >= 0 ? rawPath.Substring(0, query) : rawPath).TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }

            var match = Regex.Match(
                headerText,
                @"(?im)^Content-Length\s*:\s*(\d+)\s*$");
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) &&
                length >= 0)
            {
                contentLength = length;
            }

            return true;
        }

        /// <summary>Lee <c>reward_id</c> como entero positivo. Un string o un cero no valen.</summary>
        internal static bool TryReadRewardId(string body, out int rewardId)
        {
            rewardId = 0;
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            var match = Regex.Match(
                body,
                "\"reward_id\"\\s*:\\s*(\\d+)",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rewardId))
            {
                return false;
            }

            return rewardId > 0;
        }

        internal static string FormatResponse(int statusCode, string reason, string json)
        {
            var payload = Encoding.UTF8.GetBytes(json ?? string.Empty);
            var head = new StringBuilder();
            head.Append("HTTP/1.1 ");
            head.Append(statusCode.ToString(CultureInfo.InvariantCulture));
            head.Append(' ');
            head.Append(reason);
            head.Append("\r\nContent-Type: application/json\r\nContent-Length: ");
            head.Append(payload.Length.ToString(CultureInfo.InvariantCulture));
            head.Append("\r\nConnection: close\r\n\r\n");
            return head.ToString();
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
