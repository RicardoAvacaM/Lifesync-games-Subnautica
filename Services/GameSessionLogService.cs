using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using BepInEx;
using UnityEngine;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Acumula eventos de sesión LifeSync en memoria, muestrea stats cada 30 s en partida,
    /// y sube POST /game-logs/sessions al cerrar sesión (logout) o al cerrar el juego.
    /// Si falla el POST, escribe CSV local (mismo formato legacy).
    /// </summary>
    internal sealed class GameSessionLogService : MonoBehaviour
    {
        private const float SampleIntervalSeconds = 30f;

        internal static GameSessionLogService Instance { get; private set; }

        private bool _sessionActive;
        private bool _uploadCompleted;
        private string _sessionStartUtc;
        private readonly List<SessionLogEvent> _events = new List<SessionLogEvent>();
        private readonly List<PlayerStatsSnapshot> _statsSamples = new List<PlayerStatsSnapshot>();
        private int _totalPointsSpent;
        private int _redemptionsCount;
        private Coroutine _sampleRoutine;

        internal static void EnsureOnHost(GameObject host)
        {
            if (host == null)
            {
                return;
            }

            if (host.GetComponent<GameSessionLogService>() != null)
            {
                return;
            }

            host.AddComponent<GameSessionLogService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            Application.quitting += OnApplicationQuitting;
            if (HasBearerToken() && !_sessionActive)
            {
                StartSession();
            }
        }

        private void OnDestroy()
        {
            Application.quitting -= OnApplicationQuitting;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static bool HasBearerToken()
        {
            return !string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value);
        }

        internal static void StartSession()
        {
            if (Instance == null)
            {
                var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
                if (client != null)
                {
                    EnsureOnHost(client.gameObject);
                }
            }

            Instance?.DoStartSession();
        }

        internal static void RecordRedemption(string mechanicName, int totalCost, int costSteps)
        {
            Instance?.DoRecordRedemption(mechanicName, totalCost, costSteps);
        }

        /// <summary>Corrutina para logout: sube la sesión antes de borrar el token.</summary>
        internal static IEnumerator EndSessionUploadRoutine()
        {
            if (Instance == null || !Instance._sessionActive || Instance._uploadCompleted)
            {
                yield break;
            }

            yield return Instance.DoEndSessionRoutine(useBlockingUploadOnFailure: false);
        }

        private void DoStartSession()
        {
            if (_sessionActive)
            {
                return;
            }

            _sessionActive = true;
            _uploadCompleted = false;
            _sessionStartUtc = FormatUtcNow();
            _events.Clear();
            _statsSamples.Clear();
            _totalPointsSpent = 0;
            _redemptionsCount = 0;

            AddEvent("session_start", "{}");

            if (_sampleRoutine != null)
            {
                StopCoroutine(_sampleRoutine);
            }

            _sampleRoutine = StartCoroutine(SampleRoutine());
            MyFirstSubnauticaModPlugin.Log.LogInfo($"[LifeSync][Logger] Sesión iniciada ({_sessionStartUtc}).");
        }

        private void DoRecordRedemption(string mechanicName, int totalCost, int costSteps)
        {
            if (!_sessionActive)
            {
                return;
            }

            _redemptionsCount++;
            _totalPointsSpent += totalCost;

            var data = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"mechanic\":\"{0}\",\"cost\":{1},\"cost_steps\":{2}}}",
                EscapeJson(mechanicName ?? string.Empty),
                totalCost,
                costSteps);

            AddEvent("mechanic_redeemed", data);
        }

        private IEnumerator SampleRoutine()
        {
            yield return null;
            TryCaptureStatsSample();

            var wait = new WaitForSecondsRealtime(SampleIntervalSeconds);
            while (_sessionActive)
            {
                yield return wait;
                TryCaptureStatsSample();
            }
        }

        private void TryCaptureStatsSample()
        {
            if (!_sessionActive || !HasBearerToken())
            {
                return;
            }

            var player = Player.main;
            if (player == null || !PlayerStatsSnapshot.TryCapture(player, out var snapshot))
            {
                return;
            }

            _statsSamples.Add(snapshot);
            AddEvent("stats_sample", snapshot.ToJsonDataObject());
        }

        private void AddEvent(string type, string dataJson)
        {
            _events.Add(new SessionLogEvent
            {
                Type = type,
                TimestampUtc = FormatUtcNow(),
                DataJson = string.IsNullOrEmpty(dataJson) ? "{}" : dataJson
            });
        }

        private IEnumerator DoEndSessionRoutine(bool useBlockingUploadOnFailure)
        {
            if (!_sessionActive || _uploadCompleted)
            {
                yield break;
            }

            _sessionActive = false;
            if (_sampleRoutine != null)
            {
                StopCoroutine(_sampleRoutine);
                _sampleRoutine = null;
            }

            AddEvent("session_end", "{}");

            var sessionEndUtc = FormatUtcNow();
            var playerId = MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value;
            var videogameId = MyFirstSubnauticaModPlugin.LifeSyncApiTestVideogameId.Value;
            var modVersion = MyFirstSubnauticaModPlugin.ModVersion;

            var payload = GameSessionLogPayloadBuilder.BuildRequestJson(
                playerId,
                videogameId,
                _sessionStartUtc,
                sessionEndUtc,
                modVersion,
                _totalPointsSpent,
                _redemptionsCount,
                _events,
                _statsSamples.Count);

            var uploaded = false;
            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client != null && HasBearerToken() && playerId > 0)
            {
                var task = client.PostGameLogSessionAsync(payload);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (!task.IsFaulted && task.Result.Success)
                {
                    uploaded = true;
                    MyFirstSubnauticaModPlugin.Log.LogInfo(
                        $"[LifeSync][Logger] Sesión subida OK (HTTP {task.Result.StatusCode}, muestras={_statsSamples.Count}).");
                }
                else
                {
                    var err = task.IsFaulted
                        ? task.Exception?.GetBaseException().Message
                        : task.Result.ErrorMessage;
                    MyFirstSubnauticaModPlugin.Log.LogWarning(
                        $"[LifeSync][Logger] POST game-logs/sessions falló: {err}");
                }
            }
            else
            {
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    "[LifeSync][Logger] No se pudo subir sesión (sin cliente, token o player_id).");
            }

            if (!uploaded)
            {
                if (useBlockingUploadOnFailure)
                {
                    uploaded = TryBlockingUpload(payload);
                }

                if (!uploaded)
                {
                    WriteLocalCsvFallback(sessionEndUtc);
                }
            }

            _uploadCompleted = true;
            _events.Clear();
            _statsSamples.Clear();
        }

        private void OnApplicationQuitting()
        {
            if (!_sessionActive || _uploadCompleted)
            {
                return;
            }

            _sessionActive = false;
            if (_sampleRoutine != null)
            {
                StopCoroutine(_sampleRoutine);
                _sampleRoutine = null;
            }

            AddEvent("session_end", "{}");

            var sessionEndUtc = FormatUtcNow();
            var playerId = MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value;
            var videogameId = MyFirstSubnauticaModPlugin.LifeSyncApiTestVideogameId.Value;
            var modVersion = MyFirstSubnauticaModPlugin.ModVersion;

            var payload = GameSessionLogPayloadBuilder.BuildRequestJson(
                playerId,
                videogameId,
                _sessionStartUtc,
                sessionEndUtc,
                modVersion,
                _totalPointsSpent,
                _redemptionsCount,
                _events,
                _statsSamples.Count);

            var uploaded = false;
            if (HasBearerToken() && playerId > 0)
            {
                uploaded = TryBlockingUpload(payload);
            }

            if (!uploaded)
            {
                WriteLocalCsvFallback(sessionEndUtc);
            }
            else
            {
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][Logger] Sesión subida al cerrar juego (muestras={_statsSamples.Count}).");
            }

            _uploadCompleted = true;
        }

        private static bool TryBlockingUpload(string payloadJson)
        {
            try
            {
                var baseUrl = MyFirstSubnauticaModPlugin.LifeSyncApiBaseUrl.Value?.Trim() ?? string.Empty;
                if (!baseUrl.EndsWith("/"))
                {
                    baseUrl += "/";
                }

                var url = baseUrl + "game-logs/sessions";
                var token = MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value.Trim();
                var body = Encoding.UTF8.GetBytes(payloadJson ?? "{}");

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = Mathf.Clamp(MyFirstSubnauticaModPlugin.LifeSyncApiTimeoutSeconds.Value, 5, 120) * 1000;
                request.Headers["Authorization"] = "Bearer " + token;
                request.ContentLength = body.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var ok = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300;
                    if (ok)
                    {
                        MyFirstSubnauticaModPlugin.Log.LogInfo(
                            $"[LifeSync][Logger] POST game-logs/sessions OK ({(int)response.StatusCode}).");
                    }

                    return ok;
                }
            }
            catch (WebException ex)
            {
                var code = ex.Response is HttpWebResponse r ? (int)r.StatusCode : 0;
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] POST bloqueante falló HTTP {code}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] POST bloqueante falló: {ex.Message}");
                return false;
            }
        }

        private void WriteLocalCsvFallback(string sessionEndUtc)
        {
            try
            {
                var loggerDir = Path.Combine(Paths.PluginPath, "MyFirstSubnauticaMod", "logger");
                Directory.CreateDirectory(loggerDir);

                var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
                var path = Path.Combine(loggerDir, $"stats_{stamp}.csv");

                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", PlayerStatsSnapshot.CsvHeader));
                foreach (var sample in _statsSamples)
                {
                    sb.AppendLine(sample.ToCsvRow());
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][Logger] Respaldo CSV local: {path} (muestras={_statsSamples.Count}, fin={sessionEndUtc}).");
            }
            catch (Exception ex)
            {
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] No se pudo escribir CSV de respaldo: {ex.Message}");
            }
        }

        /// <summary>ISO-8601 sin sufijo Z (MySQL DATETIME no acepta la Z en session_start/session_end).</summary>
        private static string FormatUtcNow()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
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
