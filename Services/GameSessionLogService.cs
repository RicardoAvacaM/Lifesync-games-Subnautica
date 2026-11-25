using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using BepInEx;
using UnityEngine;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// En partida, cada 60 s captura stats (columnas del CSV local + extras) y hace POST /game-logs/sessions.
    /// Al logout o cierre del juego solo deja de enviar (no acumula ni sube un paquete final).
    /// Estado estático para sobrevivir recreación del host DDOL.
    /// </summary>
    internal sealed class GameSessionLogService : MonoBehaviour
    {
        private const float SampleIntervalSeconds = 60f;

        internal static GameSessionLogService Instance { get; private set; }

        private static bool _sessionActive;
        private static int _totalPointsSpent;
        private static int _redemptionsCount;
        private static int _postsOk;
        private static int _postsFailed;

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

            MigratePlaintextCsvIfNeeded();

            if (_sessionActive)
            {
                RestartSampleRoutine();
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    "[LifeSync][Logger] Host recreado; se reanudó el envío cada 60 s.");
                return;
            }

            if (HasBearerToken())
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
            return !string.IsNullOrWhiteSpace(LifeSyncGamesSubnauticaPlugin.GetBearerToken());
        }

        private static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var client = LifeSyncGamesSubnauticaPlugin.ResolveApiClient();
            if (client != null)
            {
                EnsureOnHost(client.gameObject);
            }
        }

        internal static void StartSession()
        {
            EnsureInstance();
            Instance?.DoStartSession();
        }

        internal static void RecordRedemption(string mechanicName, int totalCost, int costSteps)
        {
            if (!_sessionActive)
            {
                return;
            }

            _redemptionsCount++;
            _totalPointsSpent += totalCost;
            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                $"[LifeSync][Logger] Canje registrado para próximo sample: {mechanicName} cost={totalCost}.");
        }

        /// <summary>Logout: deja de enviar muestras (no hace POST final).</summary>
        internal static IEnumerator EndSessionUploadRoutine()
        {
            StopSession();
            yield break;
        }

        private void DoStartSession()
        {
            if (_sessionActive)
            {
                RestartSampleRoutine();
                return;
            }

            _sessionActive = true;
            _totalPointsSpent = 0;
            _redemptionsCount = 0;
            _postsOk = 0;
            _postsFailed = 0;

            RestartSampleRoutine();
            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                "[LifeSync][Logger] Sesión activa: POST cada 60 s solo en partida.");
        }

        private static void StopSession()
        {
            if (!_sessionActive)
            {
                return;
            }

            _sessionActive = false;
            Instance?.StopSampleRoutine();
            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                $"[LifeSync][Logger] Sesión detenida (posts_ok={_postsOk}, posts_fail={_postsFailed}).");
        }

        private void RestartSampleRoutine()
        {
            StopSampleRoutine();
            _sampleRoutine = StartCoroutine(SampleRoutine());
        }

        private void StopSampleRoutine()
        {
            if (_sampleRoutine == null)
            {
                return;
            }

            StopCoroutine(_sampleRoutine);
            _sampleRoutine = null;
        }

        private IEnumerator SampleRoutine()
        {
            var wait = new WaitForSecondsRealtime(SampleIntervalSeconds);
            while (_sessionActive)
            {
                yield return wait;
                TryCaptureAndPost();
            }
        }

        /// <summary>
        /// Solo en partida (<see cref="Player.main"/>): captura stats del CSV + progreso y POST inmediato.
        /// </summary>
        private static void TryCaptureAndPost()
        {
            if (!_sessionActive || !HasBearerToken())
            {
                return;
            }

            PlayerStatsSnapshot stats;
            SessionProgressSnapshot progress;
            try
            {
                var player = Player.main;
                if (player == null)
                {
                    return;
                }

                if (!PlayerStatsSnapshot.TryCapture(player, out stats))
                {
                    LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                        "[LifeSync][Logger] TryCapture devolvió false (sin muestra).");
                    return;
                }

                progress = SessionProgressSnapshot.Build(_redemptionsCount, _totalPointsSpent);
            }
            catch (Exception ex)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] Falló captura: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            var sampleEnd = FormatUtcNow();
            var sampleStart = DateTime.UtcNow.AddSeconds(-SampleIntervalSeconds)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            string payload;
            try
            {
                payload = GameSessionLogPayloadBuilder.BuildMinuteSampleJson(
                    LifeSyncGamesSubnauticaPlugin.LifeSyncCachedPlayerId.Value,
                    LifeSyncGamesSubnauticaPlugin.LifeSyncApiTestVideogameId.Value,
                    sampleStart,
                    sampleEnd,
                    LifeSyncGamesSubnauticaPlugin.ModVersion,
                    _totalPointsSpent,
                    _redemptionsCount,
                    stats,
                    progress);
            }
            catch (Exception ex)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] Falló build payload: {ex.Message}");
                return;
            }

            var playerId = LifeSyncGamesSubnauticaPlugin.LifeSyncCachedPlayerId.Value;
            if (playerId <= 0)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    "[LifeSync][Logger] Sin player_id; no se envía sample.");
                WriteLocalCsvFallback(stats);
                return;
            }

            LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                $"[LifeSync][Logger] Enviando sample " +
                $"(health={stats.Health:0.##}/{stats.HealthMax:0.##}, " +
                $"oxygen={stats.Oxygen:0.##}/{stats.OxygenMax:0.##}, depth={stats.Depth:0.##})…");

            // POST en hilo de fondo para no congelar el juego ni depender del host DDOL.
            var payloadCopy = payload;
            var statsCopy = stats;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var ok = TryBlockingUpload(payloadCopy);
                if (ok)
                {
                    Interlocked.Increment(ref _postsOk);
                    LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                        $"[LifeSync][Logger] Sample enviado OK (posts_ok={_postsOk}).");
                }
                else
                {
                    Interlocked.Increment(ref _postsFailed);
                    LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                        $"[LifeSync][Logger] Sample POST falló (posts_fail={_postsFailed}); CSV local.");
                    WriteLocalCsvFallback(statsCopy);
                }
            });
        }

        private void OnApplicationQuitting()
        {
            StopSession();
        }

        private static bool TryBlockingUpload(string payloadJson)
        {
            try
            {
                var baseUrl = LifeSyncGamesSubnauticaPlugin.LifeSyncApiBaseUrl.Value?.Trim() ?? string.Empty;
                if (!baseUrl.EndsWith("/"))
                {
                    baseUrl += "/";
                }

                var url = baseUrl + "game-logs/sessions";
                var token = LifeSyncGamesSubnauticaPlugin.GetBearerToken().Trim();
                var body = Encoding.UTF8.GetBytes(payloadJson ?? "{}");

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = Mathf.Clamp(LifeSyncGamesSubnauticaPlugin.LifeSyncApiTimeoutSeconds.Value, 5, 120) * 1000;
                request.Headers["Authorization"] = "Bearer " + token;
                request.ContentLength = body.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var code = (int)response.StatusCode;
                    var ok = code >= 200 && code < 300;
                    if (!ok)
                    {
                        LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                            $"[LifeSync][Logger] POST sample HTTP {code}.");
                    }

                    return ok;
                }
            }
            catch (WebException ex)
            {
                var code = ex.Response is HttpWebResponse r ? (int)r.StatusCode : 0;
                var body = string.Empty;
                try
                {
                    if (ex.Response != null)
                    {
                        using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                        {
                            body = reader.ReadToEnd();
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] POST sample falló HTTP {code}: {ex.Message} body={body}");
                return false;
            }
            catch (Exception ex)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] POST sample falló: {ex.Message}");
                return false;
            }
        }

        internal const string LocalCsvPlainFileName = "stats_live.csv";
        internal const string LocalCsvDpapiFileName = "stats_live.dpapi";
        internal const string LocalCsvDpapiHeader = "LIFESYNC_DPAPI_CSV_V1";

        private static string GetLoggerDirectory()
        {
            var loggerDir = Path.Combine(Paths.PluginPath, "LifeSync-Games-Subnautica", "logger");
            Directory.CreateDirectory(loggerDir);
            return loggerDir;
        }

        /// <summary>
        /// Pasa un <c>stats_live.csv</c> legado (texto plano) a <c>stats_live.dpapi</c> y borra el CSV en claro.
        /// </summary>
        internal static void MigratePlaintextCsvIfNeeded()
        {
            try
            {
                var loggerDir = GetLoggerDirectory();
                var csvPath = Path.Combine(loggerDir, LocalCsvPlainFileName);
                if (!File.Exists(csvPath))
                {
                    return;
                }

                var lines = File.ReadAllLines(csvPath);
                var blobs = new List<string>();
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i] != null ? lines[i].Trim() : string.Empty;
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (i == 0 && line.StartsWith("utc_time", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (DpapiProtect.IsProtected(line))
                    {
                        blobs.Add(line);
                        continue;
                    }

                    var blob = DpapiProtect.ProtectToStored(line);
                    if (string.IsNullOrEmpty(blob) || !DpapiProtect.IsProtected(blob))
                    {
                        LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                            "[LifeSync][Logger] No se pudo cifrar una fila del CSV legado; se conserva el archivo en claro.");
                        return;
                    }

                    blobs.Add(blob);
                }

                var dpapiPath = Path.Combine(loggerDir, LocalCsvDpapiFileName);
                var dpapiExists = File.Exists(dpapiPath) && new FileInfo(dpapiPath).Length > 0;
                using (var writer = new StreamWriter(dpapiPath, append: true, Encoding.UTF8))
                {
                    if (!dpapiExists)
                    {
                        writer.WriteLine(LocalCsvDpapiHeader);
                    }

                    for (var i = 0; i < blobs.Count; i++)
                    {
                        writer.WriteLine(blobs[i]);
                    }
                }

                File.Delete(csvPath);
                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    $"[LifeSync][Logger] CSV legado cifrado ({blobs.Count} filas) y eliminado: {csvPath}");
            }
            catch (Exception ex)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] No se migró el CSV legado a DPAPI: {ex.Message}");
            }
        }

        private static void WriteLocalCsvFallback(PlayerStatsSnapshot stats)
        {
            try
            {
                var loggerDir = GetLoggerDirectory();

                var path = Path.Combine(loggerDir, LocalCsvDpapiFileName);
                var row = stats.ToCsvRow();
                var blob = DpapiProtect.ProtectToStored(row);
                if (string.IsNullOrEmpty(blob) || !DpapiProtect.IsProtected(blob))
                {
                    LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                        "[LifeSync][Logger] DPAPI no cifró la muestra; no se escribe CSV en claro.");
                    return;
                }

                var writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                using (var writer = new StreamWriter(path, append: true, Encoding.UTF8))
                {
                    if (writeHeader)
                    {
                        writer.WriteLine(LocalCsvDpapiHeader);
                    }

                    writer.WriteLine(blob);
                }

                LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                    $"[LifeSync][Logger] Sample cifrado (DPAPI) guardado en: {path}");
            }
            catch (Exception ex)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning(
                    $"[LifeSync][Logger] No se pudo escribir CSV local cifrado: {ex.Message}");
            }
        }

        private static string FormatUtcNow()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
