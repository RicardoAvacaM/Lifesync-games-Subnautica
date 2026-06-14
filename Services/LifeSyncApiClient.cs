using System.Collections;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Logging;
using MyFirstSubnauticaMod.Services.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Cliente HTTP para conectar Subnautica con LifeSync-Games sin bloquear el hilo principal.
    /// Core API + login form OAuth2 en lsg-auth.
    /// </summary>
    internal class LifeSyncApiClient : MonoBehaviour
    {
        private string _baseUrl = "https://lsg.diinf.usach.cl/lsg-core-api/";
        private string _authBaseUrl = "https://lsg.diinf.usach.cl/lsg-auth/";
        private int _timeoutSeconds = 15;
        private string _bearerToken;
        private string _putSingleRelativePath = "player_attributes_single";
        private ManualLogSource _logger;

        public void Initialize(string baseUrl, int timeoutSeconds)
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                _baseUrl = baseUrl.Trim();
            }

            _timeoutSeconds = Mathf.Clamp(timeoutSeconds, 5, 120);
        }

        /// <summary>Raíz del servicio Auth (POST login con form, sin Bearer).</summary>
        public void SetAuthBaseUrl(string authBaseUrl)
        {
            if (!string.IsNullOrWhiteSpace(authBaseUrl))
            {
                _authBaseUrl = authBaseUrl.Trim();
            }
        }

        public void SetLogger(ManualLogSource logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// JWT u otro token de <c>lsg-auth</c>. Si está vacío, no se envía cabecera Authorization.
        /// </summary>
        public void SetBearerToken(string bearerToken)
        {
            _bearerToken = string.IsNullOrWhiteSpace(bearerToken) ? null : bearerToken.Trim();
        }

        /// <summary>
        /// Ruta relativa al Base URL para el PUT legacy (post-routes / Valheim).
        /// </summary>
        public void SetPutSingleAttributePath(string relativePath)
        {
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                _putSingleRelativePath = relativePath.Trim().TrimStart('/');
            }
        }

        /// <summary>
        /// POST /login con <c>application/x-www-form-urlencoded</c> (username = email, grant_type=password).
        /// </summary>
        public Task<ApiCallResult> PostLsgAuthLoginAsync(string username, string password)
        {
            var tcs = new TaskCompletionSource<ApiCallResult>();
            var url = BuildAuthUrl("login");
            var form = new WWWForm();
            form.AddField("username", username ?? string.Empty);
            form.AddField("password", password ?? string.Empty);
            form.AddField("grant_type", "password");
            StartCoroutine(SendFormPostNoAuthCoroutine(url, form, tcs));
            return tcs.Task;
        }

        /// <summary>GET /token/remaining (Auth, Bearer).</summary>
        public Task<ApiCallResult> GetLsgAuthTokenRemainingAsync()
        {
            return SendAuthBearerGetAsync("token/remaining");
        }

        /// <summary>POST /token/refresh — Bearer actual (ruta real en prod; <c>auth/token/refresh</c> devuelve 404).</summary>
        public Task<ApiCallResult> PostLsgAuthTokenRefreshAsync()
        {
            return SendAuthBearerPostEmptyAsync("token/refresh");
        }

        /// <summary>GET /whoami (Auth, Bearer).</summary>
        public Task<ApiCallResult> GetLsgAuthWhoamiAsync()
        {
            return SendAuthBearerGetAsync("whoami");
        }

        /// <summary>GET /players/{id}/attributes/points (Core API, Bearer).</summary>
        public Task<ApiCallResult> GetPlayerAttributePointsAsync(int playerId)
        {
            return SendGetAsync($"players/{playerId}/attributes/points");
        }

        /// <summary>GET /attributes — catálogo de dimensiones (id_attributes, name, ...).</summary>
        public Task<ApiCallResult> GetAttributesAsync()
        {
            return SendGetAsync("attributes");
        }

        /// <summary>GET /players/{id}/points/balance — saldo por dimensión (id_point_dimension, balance).</summary>
        public Task<ApiCallResult> GetPlayerPointsBalanceAsync(int playerId)
        {
            return SendGetAsync($"players/{playerId}/points/balance");
        }

        /// <summary>GET /videogames/{id}/mechanics — catálogo de mecánicas modificables del juego.</summary>
        public Task<ApiCallResult> GetVideogameMechanicsAsync(int videogameId)
        {
            return SendGetAsync($"videogames/{videogameId}/mechanics");
        }

        /// <summary>POST /videogames/{gameId}/players/{playerId}/redeem — canje de puntos (persiste en el servidor).</summary>
        public Task<ApiCallResult> PostRedeemAsync(int videogameId, int playerId, string jsonBody)
        {
            return SendPostRawJsonAsync(
                $"videogames/{videogameId}/players/{playerId}/redeem",
                jsonBody);
        }

        /// <summary>GET /health (Core API)</summary>
        public Task<ApiCallResult> GetHealthAsync()
        {
            return SendGetAsync("health");
        }

        /// <summary>POST /players/{{id}}/attributes/init (cuerpo vacío JSON <c>{}</c>).</summary>
        public Task<ApiCallResult> PostPlayerAttributesInitAsync(int playerId)
        {
            return SendPostRawJsonAsync($"players/{playerId}/attributes/init", "{}");
        }

        /// <summary>POST /players/{{id}}/points/adjust</summary>
        public Task<ApiCallResult> PostPlayerPointsAdjustAsync(int playerId, PlayerPointsAdjustRequest payload)
        {
            var json = JsonUtility.ToJson(payload);
            return SendPostRawJsonAsync($"players/{playerId}/points/adjust", json);
        }

        public async Task<ApiCallResult> UpdatePlayerAttributeAsync(int playerId, int attributeId, int newData)
        {
            var payload = new PlayerAttributeUpdateRequest
            {
                id_player = playerId,
                id_attributes = attributeId,
                new_data = newData
            };

            return await SendPutJsonAsync(_putSingleRelativePath, payload);
        }

        public async Task LogHealthStartupTestAsync()
        {
            var result = await GetHealthAsync();
            LogStartupOutcome(result, "GET /health");
        }

        public async Task LogPointsAdjustStartupTestAsync(
            int playerId,
            int pointDimensionId,
            string direction,
            int amount,
            string reason,
            int videogameId)
        {
            if (string.IsNullOrEmpty(_bearerToken))
            {
                _logger?.LogWarning("[LifeSync][API] CorePointsAdjust requiere Bearer Token en el .cfg.");
            }

            var payload = new PlayerPointsAdjustRequest
            {
                point_dimension_id = pointDimensionId,
                direction = direction ?? "CREDIT",
                amount = amount,
                reason = string.IsNullOrEmpty(reason) ? "Subnautica mod test" : reason,
                videogame_id = videogameId
            };

            var result = await PostPlayerPointsAdjustAsync(playerId, payload);
            LogStartupOutcome(result, $"POST /players/{playerId}/points/adjust");
        }

        public async Task LogStartupConnectivityTestAsync(int testPlayerId, int testAttributeId, int testNewData)
        {
            var result = await UpdatePlayerAttributeAsync(testPlayerId, testAttributeId, testNewData);
            if (result.Success)
            {
                _logger?.LogInfo($"[LifeSync][API] Startup test OK ({result.StatusCode})");
                if (!string.IsNullOrEmpty(result.ResponseBody) &&
                    (result.ResponseBody.Contains("Rows matched: 0") || result.ResponseBody.Contains("\"changedRows\":0")))
                {
                    _logger?.LogWarning(
                        "[LifeSync][API] El servidor respondió 200 pero no actualizó filas (Rows matched: 0). " +
                        "Revisa Test Player Id / Test Attribute Id y que exista fila en BD para ese par.");
                }
            }
            else
            {
                _logger?.LogWarning($"[LifeSync][API] Startup test fallido: {result.ErrorMessage}");
            }
        }

        private void LogStartupOutcome(ApiCallResult result, string label)
        {
            if (result.Success)
            {
                _logger?.LogInfo($"[LifeSync][API] Startup test OK {label} ({result.StatusCode})");
            }
            else
            {
                _logger?.LogWarning($"[LifeSync][API] Startup test fallido {label}: {result.ErrorMessage}");
            }

            if (!string.IsNullOrEmpty(result.ResponseBody))
            {
                _logger?.LogDebug($"[LifeSync][API] Response {label}: {result.ResponseBody}");
            }
        }

        private Task<ApiCallResult> SendGetAsync(string endpoint)
        {
            var tcs = new TaskCompletionSource<ApiCallResult>();
            var url = BuildCoreUrl(endpoint);
            var request = UnityWebRequest.Get(url);
            StartCoroutine(DispatchCoreRequestCoroutine(request, tcs, logPayload: null));
            return tcs.Task;
        }

        private Task<ApiCallResult> SendPostRawJsonAsync(string endpoint, string jsonBody)
        {
            var tcs = new TaskCompletionSource<ApiCallResult>();
            var url = BuildCoreUrl(endpoint);
            var bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            StartCoroutine(DispatchCoreRequestCoroutine(request, tcs, jsonBody));
            return tcs.Task;
        }

        private Task<ApiCallResult> SendPutJsonAsync(string endpoint, object body)
        {
            var tcs = new TaskCompletionSource<ApiCallResult>();
            var url = BuildCoreUrl(endpoint);
            var jsonBody = JsonUtility.ToJson(body);
            var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            StartCoroutine(DispatchCoreRequestCoroutine(request, tcs, jsonBody));
            return tcs.Task;
        }

        private IEnumerator DispatchCoreRequestCoroutine(
            UnityWebRequest request,
            TaskCompletionSource<ApiCallResult> tcs,
            string logPayload)
        {
            using (request)
            {
                request.timeout = _timeoutSeconds;
                if (!string.IsNullOrEmpty(_bearerToken))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + _bearerToken);
                }

                _logger?.LogInfo($"[LifeSync][API] {request.method} {request.url}");
                if (logPayload != null)
                {
                    _logger?.LogDebug($"[LifeSync][API] Payload: {logPayload}");
                }

                yield return request.SendWebRequest();
                var result = BuildResult(request);
                LogResult(result, "[LifeSync][API]");
                tcs.TrySetResult(result);
            }
        }

        private Task<ApiCallResult> SendAuthBearerGetAsync(string endpoint)
        {
            var tcs = new TaskCompletionSource<ApiCallResult>();
            var url = BuildAuthUrl(endpoint);
            var request = UnityWebRequest.Get(url);
            StartCoroutine(DispatchAuthBearerRequestCoroutine(request, tcs));
            return tcs.Task;
        }

        /// <summary>
        /// POST con cuerpo JSON mínimo. Unity 2019 no admite <see cref="UploadHandlerRaw"/> con array vacío
        /// (ArgumentException: Cannot create a data handler without payload data).
        /// </summary>
        private Task<ApiCallResult> SendAuthBearerPostEmptyAsync(string endpoint)
        {
            var tcs = new TaskCompletionSource<ApiCallResult>();
            var url = BuildAuthUrl(endpoint);
            var bodyRaw = Encoding.UTF8.GetBytes("{}");
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
            StartCoroutine(DispatchAuthBearerRequestCoroutine(request, tcs));
            return tcs.Task;
        }

        private IEnumerator DispatchAuthBearerRequestCoroutine(
            UnityWebRequest request,
            TaskCompletionSource<ApiCallResult> tcs)
        {
            using (request)
            {
                request.timeout = _timeoutSeconds;
                if (string.IsNullOrEmpty(_bearerToken))
                {
                    var missing = new ApiCallResult
                    {
                        Success = false,
                        StatusCode = 0,
                        ResponseBody = string.Empty,
                        ErrorMessage = "Sin Bearer Token (inicia sesión primero)."
                    };
                    _logger?.LogWarning("[LifeSync][Auth] " + missing.ErrorMessage);
                    tcs.TrySetResult(missing);
                    yield break;
                }

                request.SetRequestHeader("Authorization", "Bearer " + _bearerToken);

                _logger?.LogInfo($"[LifeSync][Auth] {request.method} {request.url}");
                yield return request.SendWebRequest();
                var result = BuildResult(request);
                LogResult(result, "[LifeSync][Auth]");
                tcs.TrySetResult(result);
            }
        }

        private IEnumerator SendFormPostNoAuthCoroutine(string url, WWWForm form, TaskCompletionSource<ApiCallResult> tcs)
        {
            using (var request = UnityWebRequest.Post(url, form))
            {
                request.timeout = _timeoutSeconds;
                _logger?.LogInfo($"[LifeSync][Auth] POST {url}");
                yield return request.SendWebRequest();
                var result = BuildResult(request);
                LogResult(result, "[LifeSync][Auth]");
                tcs.TrySetResult(result);
            }
        }

        private static ApiCallResult BuildResult(UnityWebRequest request)
        {
            var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            var hasError = request.isNetworkError || request.isHttpError;
            return new ApiCallResult
            {
                Success = !hasError,
                StatusCode = (long)request.responseCode,
                ResponseBody = responseText,
                ErrorMessage = request.error
            };
        }

        private void LogResult(ApiCallResult result, string tag)
        {
            if (!result.Success)
            {
                _logger?.LogWarning($"{tag} Error {result.StatusCode}: {result.ErrorMessage}");
                _logger?.LogDebug($"{tag} Response: {result.ResponseBody}");
            }
            else
            {
                _logger?.LogDebug($"{tag} Response: {result.ResponseBody}");
            }
        }

        private string BuildCoreUrl(string endpoint)
        {
            var normalizedBaseUrl = _baseUrl.EndsWith("/") ? _baseUrl : _baseUrl + "/";
            return normalizedBaseUrl + endpoint.TrimStart('/');
        }

        private string BuildAuthUrl(string endpoint)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_authBaseUrl)
                ? "https://lsg.diinf.usach.cl/lsg-auth/"
                : _authBaseUrl;
            var normalized = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            return normalized + endpoint.TrimStart('/');
        }
    }
}
