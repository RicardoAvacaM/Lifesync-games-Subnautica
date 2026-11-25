using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LifeSyncGamesSubnautica.Services
{
    /// <summary>
    /// Servidor local del game adapter (<c>http://localhost:7878</c>).
    /// El overlay lo usa para pasar de "Server Offline" a "Game Ready" y para aplicar el efecto del canje.
    /// </summary>
    internal sealed class GameAdapterServer : MonoBehaviour
    {
        private const int MaxHeaderBytes = 16384;
        private const int MaxBodyBytes = 8192;
        private const int ApplyTimeoutMs = 5000;

        private static readonly ConcurrentQueue<RewardJob> Pending = new ConcurrentQueue<RewardJob>();

        private readonly object _startLock = new object();
        private Socket _ipv4;
        private Socket _ipv6;
        private volatile bool _running;
        private volatile bool _gameReady;

        internal static GameAdapterServer Instance { get; private set; }

        internal static void EnsureOnHost(GameObject host)
        {
            if (host == null || Instance != null)
            {
                return;
            }

            if (host.GetComponent<GameAdapterServer>() == null)
            {
                host.AddComponent<GameAdapterServer>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            StartListeners();
        }

        private void Update()
        {
            _gameReady = Player.main != null;
            DrainRewards();
        }

        private void OnDestroy()
        {
            // El puerto tiene que seguir abierto aunque Unity destruya el GameObject al cambiar de escena.
            if (Instance == this)
            {
                Instance = null;
            }

            LogLine("info", "El componente se destruyó. El puerto " + GameAdapterProtocol.Port + " sigue abierto.");
        }

        private void StartListeners()
        {
            lock (_startLock)
            {
                if (_running)
                {
                    return;
                }

                _running = true;
                _ipv4 = StartOne(IPAddress.Loopback, "127.0.0.1");
                _ipv6 = StartOne(IPAddress.IPv6Loopback, "::1");
                if (_ipv4 == null && _ipv6 == null)
                {
                    _running = false;
                    LogLine("error", "No se pudo abrir el puerto " + GameAdapterProtocol.Port + ". El overlay seguirá en Server Offline.");
                }
            }
        }

        private Socket StartOne(IPAddress address, string label)
        {
            Socket socket = null;
            try
            {
                // TcpListener.Start() llama a ExclusiveAddressUse, que Mono de Unity no implementa.
                socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Bind(new IPEndPoint(address, GameAdapterProtocol.Port));
                socket.Listen(16);
            }
            catch (Exception ex)
            {
                LogLine("warn", "No se escuchó en " + label + ":" + GameAdapterProtocol.Port + ": " + ex);
                if (socket != null)
                {
                    try
                    {
                        socket.Close();
                    }
                    catch (Exception closeEx)
                    {
                        LogLine("warn", "Al cerrar el socket fallido: " + closeEx.Message);
                    }
                }

                return null;
            }

            var captured = socket;
            var thread = new Thread(() => AcceptLoop(captured))
            {
                IsBackground = true,
                Name = "LifeSync.GameAdapter." + label
            };
            thread.Start();
            LogLine("info", "Escuchando http://" + label + ":" + GameAdapterProtocol.Port + "/");
            return socket;
        }

        private void StopListeners()
        {
            _running = false;
            StopOne(_ipv4);
            StopOne(_ipv6);
            _ipv4 = null;
            _ipv6 = null;
        }

        private static void StopOne(Socket listener)
        {
            if (listener == null)
            {
                return;
            }

            try
            {
                listener.Close();
            }
            catch (Exception ex)
            {
                LogLine("warn", "Al cerrar el puerto: " + ex.Message);
            }
        }

        private void AcceptLoop(Socket listener)
        {
            while (_running)
            {
                Socket client = null;
                try
                {
                    client = listener.Accept();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (Exception)
                {
                    if (client != null)
                    {
                        try
                        {
                            client.Close();
                        }
                        catch (Exception)
                        {
                            // el accept ya falló
                        }
                    }

                    if (!_running)
                    {
                        break;
                    }
                }
            }
        }

        private void HandleClient(Socket client)
        {
            try
            {
                client.ReceiveTimeout = ApplyTimeoutMs;
                client.SendTimeout = ApplyTimeoutMs;
                using (var stream = new NetworkStream(client, true))
                {
                    if (!TryReadHttp(stream, out var method, out var path, out var body))
                    {
                        Write(stream, 400, "Bad Request", GameAdapterProtocol.ErrorJson("Invalid request"));
                        return;
                    }

                    Route(method, path, body, out var status, out var reason, out var json);
                    Write(stream, status, reason, json);
                }
            }
            catch (Exception ex)
            {
                LifeSyncGamesSubnauticaPlugin.Log.LogWarning("[LifeSync][Adapter] Petición descartada: " + ex.Message);
            }
        }

        private void Route(string method, string path, string body, out int status, out string reason, out string json)
        {
            if (path == "/health" && method == "GET")
            {
                status = 200;
                reason = "OK";
                json = GameAdapterProtocol.HealthJson(_gameReady);
                return;
            }

            if (path == "/reward" && method == "POST")
            {
                if (!GameAdapterProtocol.TryReadRewardId(body, out var rewardId))
                {
                    status = 400;
                    reason = "Bad Request";
                    json = GameAdapterProtocol.ErrorJson("'reward_id' must be a positive integer");
                    return;
                }

                var job = new RewardJob(rewardId);
                Pending.Enqueue(job);
                if (!job.Done.Wait(ApplyTimeoutMs))
                {
                    status = 503;
                    reason = "Service Unavailable";
                    json = GameAdapterProtocol.ErrorJson("Game is not ready.");
                    return;
                }

                status = job.StatusCode;
                reason = job.Reason;
                json = job.Body;
                return;
            }

            status = 404;
            reason = "Not Found";
            json = GameAdapterProtocol.ErrorJson("Not found");
        }

        private void DrainRewards()
        {
            while (Pending.TryDequeue(out var job))
            {
                try
                {
                    if (!_gameReady)
                    {
                        job.Complete(503, "Service Unavailable", GameAdapterProtocol.ErrorJson("Game is not ready."));
                        continue;
                    }

                    if (!RedeemCatalog.TryGet(job.RewardId, out var recipe))
                    {
                        job.Complete(422, "Unprocessable Entity", GameAdapterProtocol.ErrorJson("Reward is not supported."));
                        continue;
                    }

                    recipe.ApplyLocalEffect?.Invoke();
                    RedeemCatalog.RegisterSuccessfulRedeem(job.RewardId);
                    LifeSyncGamesSubnauticaPlugin.Log.LogInfo(
                        "[LifeSync][Adapter] Reward " + job.RewardId + " aplicado. " + recipe.EffectSummary);
                    job.Complete(200, "OK", GameAdapterProtocol.RewardOkJson());
                }
                catch (Exception ex)
                {
                    LifeSyncGamesSubnauticaPlugin.Log.LogError("[LifeSync][Adapter] Error aplicando reward " + job.RewardId + ": " + ex);
                    job.Complete(500, "Internal Server Error", GameAdapterProtocol.ErrorJson("Internal Server Error"));
                }
            }
        }

        private static bool TryReadHttp(NetworkStream stream, out string method, out string path, out string body)
        {
            method = null;
            path = null;
            body = string.Empty;
            var buffer = new MemoryStream();
            var tmp = new byte[1024];
            var headerEnd = -1;
            while (headerEnd < 0 && buffer.Length < MaxHeaderBytes)
            {
                var read = stream.Read(tmp, 0, tmp.Length);
                if (read <= 0)
                {
                    break;
                }

                buffer.Write(tmp, 0, read);
                headerEnd = IndexOf(buffer.GetBuffer(), (int)buffer.Length, new byte[] { 13, 10, 13, 10 });
            }

            if (headerEnd < 0)
            {
                return false;
            }

            var headerText = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, headerEnd);
            if (!GameAdapterProtocol.TryParseRequest(headerText, out method, out path, out var contentLength))
            {
                return false;
            }

            if (contentLength > MaxBodyBytes)
            {
                return false;
            }

            var bodyStart = headerEnd + 4;
            var already = (int)buffer.Length - bodyStart;
            if (already < 0)
            {
                already = 0;
            }

            var bodyBytes = new byte[contentLength];
            var copied = 0;
            if (already > 0 && contentLength > 0)
            {
                copied = Math.Min(already, contentLength);
                Buffer.BlockCopy(buffer.GetBuffer(), bodyStart, bodyBytes, 0, copied);
            }

            while (copied < contentLength)
            {
                var read = stream.Read(bodyBytes, copied, contentLength - copied);
                if (read <= 0)
                {
                    break;
                }

                copied += read;
            }

            body = Encoding.UTF8.GetString(bodyBytes, 0, copied);
            return true;
        }

        private static int IndexOf(byte[] data, int length, byte[] pattern)
        {
            if (data == null || pattern == null || pattern.Length == 0 || length < pattern.Length)
            {
                return -1;
            }

            var last = length - pattern.Length;
            for (var i = 0; i <= last; i++)
            {
                var found = true;
                for (var j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void LogLine(string level, string message)
        {
            var line = "[LifeSync][Adapter] " + message;
            try
            {
                var logger = LifeSyncGamesSubnauticaPlugin.Log;
                if (level == "error")
                {
                    logger.LogError(line);
                }
                else if (level == "warn")
                {
                    logger.LogWarning(line);
                }
                else
                {
                    logger.LogInfo(line);
                }
            }
            catch (Exception)
            {
                // el logger de BepInEx puede no estar conectado todavía
            }

            try
            {
                var dir = Path.GetDirectoryName(typeof(GameAdapterServer).Assembly.Location);
                if (!string.IsNullOrEmpty(dir))
                {
                    File.AppendAllText(
                        Path.Combine(dir, "adapter-boot.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + line + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // no impedir el arranque si el log de archivo falla
            }
        }

        private static void Write(NetworkStream stream, int status, string reason, string json)
        {
            var head = Encoding.UTF8.GetBytes(GameAdapterProtocol.FormatResponse(status, reason, json));
            var payload = Encoding.UTF8.GetBytes(json ?? string.Empty);
            stream.Write(head, 0, head.Length);
            stream.Write(payload, 0, payload.Length);
        }

        private sealed class RewardJob
        {
            internal readonly int RewardId;
            internal readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
            internal int StatusCode;
            internal string Reason;
            internal string Body;

            internal RewardJob(int rewardId)
            {
                RewardId = rewardId;
            }

            internal void Complete(int statusCode, string reason, string body)
            {
                StatusCode = statusCode;
                Reason = reason;
                Body = body;
                Done.Set();
            }
        }
    }
}
