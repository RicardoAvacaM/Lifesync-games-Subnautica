using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MyFirstSubnauticaMod.Services.Models
{
    [System.Serializable]
    internal class PlayerAttributeUpdateRequest
    {
        public int id_player;
        public int id_attributes;
        public int new_data;
    }

    /// <summary>POST /players/{{id}}/points/adjust (LSG-CORE-API prod).</summary>
    [System.Serializable]
    internal class PlayerPointsAdjustRequest
    {
        public int point_dimension_id;
        public string direction;
        public int amount;
        public string reason;
        public int videogame_id;
    }

    /// <summary>Respuesta JSON de POST /lsg-auth/login (Swagger Token).</summary>
    [System.Serializable]
#pragma warning disable CS0649
    internal class LsgAuthTokenResponse
    {
        public string access_token;
        public string token_type;
    }

    /// <summary>GET /lsg-auth/token/remaining (Bearer).</summary>
    [System.Serializable]
    internal class LsgAuthTokenRemainingResponse
    {
        public int expires_in_seconds;
        public string expires_at;
        public string issued_at;
        public string message;
    }
#pragma warning restore CS0649

    /// <summary>Fila de GET /players/{id}/attributes/points (CSV prod).</summary>
    [System.Serializable]
#pragma warning disable CS0649
    internal class PlayerAttributePointsRow
    {
        public int id_players;
        public string player_name;
        public string player_email;
        public int id_attributes;
        public string attribute_name;
        public int balance_ledger;
        public int snapshot_points;
        public int diff_ledger_minus_snapshot;
    }

    /// <summary>Envoltorio para deserializar array JSON con <see cref="JsonUtility"/> (campo <c>rows</c>).</summary>
    [System.Serializable]
    internal class PlayerAttributePointsEnvelope
    {
        public PlayerAttributePointsRow[] rows;
    }

    /// <summary>Fila de GET /attributes (catálogo de dimensiones).</summary>
    [System.Serializable]
    internal class AttributeRow
    {
        public int id_attributes;
        public string name;
    }

    /// <summary>Fila de GET /players/{id}/points/balance.</summary>
    [System.Serializable]
    internal class PlayerPointsBalanceRow
    {
        public int id_players;
        public int id_point_dimension;
        public int id_attributes;
        public int balance;
        public string dimension_code;
        public string dimension_name;
        public string attribute_name;
        public string subattribute_name;
    }

    /// <summary>Fila de GET /videogames/{id}/mechanics. <c>id_modifiable_mechanic_videogame</c> es el id que va en el body del canje.</summary>
    [System.Serializable]
    internal class ModifiableMechanicRow
    {
        public int id_modifiable_mechanic_videogame;
        public int id_modifiable_mechanic;
        public string modifiable_mechanic_name;
        public string modifiable_mechanic_description;
        public string modifiable_mechanic_type;
    }

#pragma warning restore CS0649

    /// <summary>Vista combinada para UI: una dimensión con su saldo (0 si no aparece en balance).</summary>
    internal class DimensionPointEntry
    {
        public int IdDimension;
        public string Name;
        public int Balance;
    }

    /// <summary>Parseo tolerante (whoami incluye <c>roles</c> que JsonUtility no modela bien).</summary>
    internal static class LifeSyncPointsJsonParsers
    {
        internal static bool TryParseWhoamiPlayerId(string body, out int id)
        {
            id = 0;
            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            var m = Regex.Match(body, @"""id_players""\s*:\s*(\d+)");
            if (!m.Success)
            {
                return false;
            }

            return int.TryParse(m.Groups[1].Value, out id) && id > 0;
        }

        /// <summary>
        /// Parsea el array de GET attributes/points. <see cref="JsonUtility"/> suele devolver <c>null</c> en arrays de objetos;
        /// el regex replica el contrato CSV (orden de campos típico de FastAPI).
        /// </summary>
        internal static bool TryParseAttributePointsArray(string json, out PlayerAttributePointsRow[] rows)
        {
            rows = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var s = json.Trim().TrimStart('\uFEFF', '\ufeff');
            if (TryParseAttributePointsRegex(s, out rows) && rows != null && rows.Length > 0)
            {
                return true;
            }

            if (!s.StartsWith("["))
            {
                return false;
            }

            if (s == "[]")
            {
                rows = new PlayerAttributePointsRow[0];
                return true;
            }

            try
            {
                var wrapped = "{\"rows\":" + s + "}";
                var env = JsonUtility.FromJson<PlayerAttributePointsEnvelope>(wrapped);
                if (env?.rows != null)
                {
                    rows = env.rows;
                    return true;
                }
            }
            catch
            {
                // ignorar; ya probó regex
            }

            return TryParseAttributePointsRegex(s, out rows) && rows != null;
        }

        private static bool TryParseAttributePointsRegex(string s, out PlayerAttributePointsRow[] rows)
        {
            rows = null;
            // Orden típico en prod (CSV): player_name, player_email, id_attributes, attribute_name, balances…
            var rx = new Regex(
                "\"player_name\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"player_email\"\\s*:\\s*\"[^\"]*\"\\s*,\\s*\"id_attributes\"\\s*:\\s*(\\d+)\\s*,\\s*\"attribute_name\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"balance_ledger\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"snapshot_points\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"diff_ledger_minus_snapshot\"\\s*:\\s*(-?\\d+)",
                RegexOptions.CultureInvariant);
            var matches = rx.Matches(s);
            if (matches.Count == 0)
            {
                // Respaldo: solo attribute_name + balances (por si cambia el orden de campos).
                var rx2 = new Regex(
                    "\"attribute_name\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"balance_ledger\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"snapshot_points\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"diff_ledger_minus_snapshot\"\\s*:\\s*(-?\\d+)",
                    RegexOptions.CultureInvariant);
                matches = rx2.Matches(s);
                if (matches.Count == 0)
                {
                    return false;
                }

                var list2 = new List<PlayerAttributePointsRow>(matches.Count);
                foreach (Match m in matches)
                {
                    var row = new PlayerAttributePointsRow { attribute_name = m.Groups[1].Value };
                    int.TryParse(m.Groups[2].Value, out row.balance_ledger);
                    int.TryParse(m.Groups[3].Value, out row.snapshot_points);
                    int.TryParse(m.Groups[4].Value, out row.diff_ledger_minus_snapshot);
                    list2.Add(row);
                }

                rows = list2.ToArray();
                return true;
            }

            var list = new List<PlayerAttributePointsRow>(matches.Count);
            foreach (Match m in matches)
            {
                var row = new PlayerAttributePointsRow
                {
                    player_name = m.Groups[1].Value,
                    attribute_name = m.Groups[3].Value,
                };
                int.TryParse(m.Groups[2].Value, out row.id_attributes);
                int.TryParse(m.Groups[4].Value, out row.balance_ledger);
                int.TryParse(m.Groups[5].Value, out row.snapshot_points);
                int.TryParse(m.Groups[6].Value, out row.diff_ledger_minus_snapshot);
                list.Add(row);
            }

            rows = list.ToArray();
            return true;
        }

        /// <summary>
        /// Combina catálogo /attributes con saldos /points/balance para la pestaña Puntos.
        /// </summary>
        internal static DimensionPointEntry[] MergeAttributesWithBalances(
            AttributeRow[] attrs,
            PlayerPointsBalanceRow[] balances)
        {
            attrs = attrs ?? new AttributeRow[0];
            balances = balances ?? new PlayerPointsBalanceRow[0];

            var byId = new Dictionary<int, DimensionPointEntry>();
            foreach (var a in attrs)
            {
                if (a == null || a.id_attributes <= 0)
                {
                    continue;
                }

                byId[a.id_attributes] = new DimensionPointEntry
                {
                    IdDimension = a.id_attributes,
                    Name = a.name ?? $"#{a.id_attributes}",
                    Balance = 0
                };
            }

            foreach (var b in balances)
            {
                if (b == null)
                {
                    continue;
                }

                var mergeKey = b.id_attributes > 0 ? b.id_attributes : b.id_point_dimension;
                if (mergeKey <= 0)
                {
                    continue;
                }

                var displayName = FirstNonEmpty(
                    b.attribute_name,
                    b.dimension_name,
                    b.subattribute_name,
                    byId.TryGetValue(mergeKey, out var existing) ? existing.Name : null,
                    $"#{mergeKey}");

                if (byId.TryGetValue(mergeKey, out var entry))
                {
                    entry.Balance = b.balance;
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        entry.Name = displayName;
                    }
                }
                else
                {
                    byId[mergeKey] = new DimensionPointEntry
                    {
                        IdDimension = mergeKey,
                        Name = displayName,
                        Balance = b.balance
                    };
                }
            }

            if (byId.Count == 0)
            {
                return new DimensionPointEntry[0];
            }

            var merged = new DimensionPointEntry[byId.Count];
            var i = 0;
            foreach (var pair in byId)
            {
                merged[i++] = pair.Value;
            }

            return merged;
        }

        /// <summary>
        /// Parsea GET /attributes (array con id_attributes + name). Regex porque <see cref="JsonUtility"/>
        /// no resuelve bien arrays de objetos anidados.
        /// </summary>
        internal static bool TryParseAttributesArray(string json, out AttributeRow[] rows)
        {
            rows = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var s = ExtractJsonArray(json);
            if (s == null)
            {
                return false;
            }

            if (s == "[]")
            {
                rows = new AttributeRow[0];
                return true;
            }

            var list = new List<AttributeRow>();
            foreach (var block in EnumerateJsonObjectBlocks(s))
            {
                if (!TryGetJsonIntField(block, "id_attributes", out var idAttributes) || idAttributes <= 0)
                {
                    continue;
                }

                var name = TryGetJsonStringField(block, "name");
                list.Add(new AttributeRow { id_attributes = idAttributes, name = name ?? string.Empty });
            }

            if (list.Count == 0)
            {
                return false;
            }

            rows = list.ToArray();
            return true;
        }

        /// <summary>
        /// Parsea GET /videogames/{id}/mechanics. Captura <c>id_modifiable_mechanic</c>,
        /// <c>modifiable_mechanic_name</c>, <c>modifiable_mechanic_description</c> y
        /// <c>modifiable_mechanic_type</c> en bloque (el orden es estable en prod).
        /// </summary>
        internal static bool TryParseMechanicsArray(string json, out ModifiableMechanicRow[] rows)
        {
            rows = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var s = json.Trim().TrimStart('\uFEFF', '\ufeff');
            if (s == "[]")
            {
                rows = new ModifiableMechanicRow[0];
                return true;
            }

            if (!s.StartsWith("["))
            {
                return false;
            }

            // Orden estable en prod: id_modifiable_mechanic_videogame, id_videogame, videogame_name, options,
            // id_modifiable_mechanic, modifiable_mechanic_name, modifiable_mechanic_description, modifiable_mechanic_type.
            // [\s\S]*? salta los campos intermedios sin cruzar al siguiente objeto (el siguiente anchor termina antes).
            var rx = new Regex(
                "\"id_modifiable_mechanic_videogame\"\\s*:\\s*(\\d+)[\\s\\S]*?" +
                "\"id_modifiable_mechanic\"\\s*:\\s*(\\d+)\\s*,\\s*" +
                "\"modifiable_mechanic_name\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*" +
                "\"modifiable_mechanic_description\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*" +
                "\"modifiable_mechanic_type\"\\s*:\\s*\"([^\"]*)\"",
                RegexOptions.CultureInvariant);
            var matches = rx.Matches(s);
            if (matches.Count == 0)
            {
                // Respaldo: solo nombre + descripción (sin id_videogame, el canje no será posible).
                var rx2 = new Regex(
                    "\"modifiable_mechanic_name\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"modifiable_mechanic_description\"\\s*:\\s*\"([^\"]*)\"",
                    RegexOptions.CultureInvariant);
                matches = rx2.Matches(s);
                if (matches.Count == 0)
                {
                    return false;
                }

                var list2 = new List<ModifiableMechanicRow>(matches.Count);
                foreach (Match m in matches)
                {
                    list2.Add(new ModifiableMechanicRow
                    {
                        modifiable_mechanic_name = m.Groups[1].Value,
                        modifiable_mechanic_description = m.Groups[2].Value,
                    });
                }

                rows = list2.ToArray();
                return true;
            }

            var list = new List<ModifiableMechanicRow>(matches.Count);
            foreach (Match m in matches)
            {
                var row = new ModifiableMechanicRow
                {
                    modifiable_mechanic_name = m.Groups[3].Value,
                    modifiable_mechanic_description = m.Groups[4].Value,
                    modifiable_mechanic_type = m.Groups[5].Value,
                };
                int.TryParse(m.Groups[1].Value, out row.id_modifiable_mechanic_videogame);
                int.TryParse(m.Groups[2].Value, out row.id_modifiable_mechanic);
                list.Add(row);
            }

            rows = list.ToArray();
            return true;
        }

        /// <summary>Parsea GET /players/{id}/points/balance (campos en cualquier orden).</summary>
        internal static bool TryParsePlayerPointsBalanceArray(string json, out PlayerPointsBalanceRow[] rows)
        {
            rows = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var s = ExtractJsonArray(json);
            if (s == null)
            {
                return false;
            }

            if (s == "[]")
            {
                rows = new PlayerPointsBalanceRow[0];
                return true;
            }

            var list = new List<PlayerPointsBalanceRow>();
            foreach (var block in EnumerateJsonObjectBlocks(s))
            {
                if (!TryGetJsonIntField(block, "id_point_dimension", out var idPointDimension))
                {
                    continue;
                }

                if (!TryGetJsonNumberField(block, "balance", out var balance))
                {
                    continue;
                }

                var row = new PlayerPointsBalanceRow
                {
                    id_point_dimension = idPointDimension,
                    balance = balance,
                    dimension_code = TryGetJsonStringField(block, "dimension_code"),
                    dimension_name = TryGetJsonStringField(block, "dimension_name"),
                    attribute_name = TryGetJsonStringField(block, "attribute_name"),
                    subattribute_name = TryGetJsonStringField(block, "subattribute_name")
                };

                if (TryGetJsonIntField(block, "id_players", out var idPlayers))
                {
                    row.id_players = idPlayers;
                }

                if (TryGetJsonIntField(block, "id_attributes", out var idAttributes))
                {
                    row.id_attributes = idAttributes;
                }

                list.Add(row);
            }

            if (list.Count == 0)
            {
                return false;
            }

            rows = list.ToArray();
            return true;
        }

        private static string ExtractJsonArray(string json)
        {
            var s = json.Trim().TrimStart('\uFEFF', '\ufeff');
            if (s.StartsWith("["))
            {
                return s;
            }

            var start = s.IndexOf('[');
            var end = s.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                return s.Substring(start, end - start + 1);
            }

            return null;
        }

        private static IEnumerable<string> EnumerateJsonObjectBlocks(string arrayJson)
        {
            if (string.IsNullOrEmpty(arrayJson))
            {
                yield break;
            }

            var depth = 0;
            var start = -1;
            for (var i = 0; i < arrayJson.Length; i++)
            {
                var c = arrayJson[i];
                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        yield return arrayJson.Substring(start, i - start + 1);
                        start = -1;
                    }
                }
            }
        }

        private static bool TryGetJsonIntField(string obj, string field, out int value)
        {
            value = 0;
            var pattern = "\"" + Regex.Escape(field) + "\"\\s*:\\s*(-?\\d+)";
            var m = Regex.Match(obj, pattern, RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                return false;
            }

            return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetJsonNumberField(string obj, string field, out int value)
        {
            value = 0;
            var pattern = "\"" + Regex.Escape(field) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)";
            var m = Regex.Match(obj, pattern, RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                return false;
            }

            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                value = (int)Math.Round(number);
                return true;
            }

            return false;
        }

        private static string TryGetJsonStringField(string obj, string field)
        {
            var pattern = "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]*)\"";
            var m = Regex.Match(obj, pattern, RegexOptions.CultureInvariant);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    return v;
                }
            }

            return string.Empty;
        }
    }

    internal class ApiCallResult
    {
        public bool Success;
        public long StatusCode;
        public string ResponseBody;
        public string ErrorMessage;
    }
}
