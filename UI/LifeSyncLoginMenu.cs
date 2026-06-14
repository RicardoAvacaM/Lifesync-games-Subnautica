using System.Collections;
using MyFirstSubnauticaMod.Input;
using MyFirstSubnauticaMod.Services;
using MyFirstSubnauticaMod.Services.Models;
using UnityEngine;

namespace MyFirstSubnauticaMod.UI
{
    /// <summary>
    /// Menú IMGUI: login solo sin token guardado; con token, la tecla abre solo el panel de sesión (duración / renovar).
    /// </summary>
    internal sealed class LifeSyncLoginMenu : MonoBehaviour
    {
        private const int WindowId = 7701;

        internal static LifeSyncLoginMenu Instance { get; private set; }

        private enum MenuPanel
        {
            Login,
            Session,
        }

        private enum SessionTab
        {
            Token,
            Points,
            Mechanics,
        }

        private bool _show;
        private MenuPanel _panel = MenuPanel.Login;
        private SessionTab _sessionTab = SessionTab.Token;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _status = string.Empty;
        private string _sessionStatus = string.Empty;
        private bool _submitting;
        private enum SessionRequestKind
        {
            None,
            Remaining,
            Refresh,
        }

        private SessionRequestKind _sessionRequest;
        private DimensionPointEntry[] _dimensionEntries;
        private string _pointsStatus = string.Empty;
        private bool _pointsBusy;
        private Vector2 _pointsScroll;
        private ModifiableMechanicRow[] _mechanicRows;
        private string _mechanicsStatus = string.Empty;
        private bool _mechanicsBusy;
        private Vector2 _mechanicsScroll;
        private int _redeemingMechanicVideogameId;
        private Rect _windowRect = new Rect(20, 20, 340, 260);
        private bool _positioned;
        private bool _pausedByMenu;
        private float _timeScaleBeforeMenu = 1f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            ReleaseGameplayPause();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (Player.main == null)
            {
                return;
            }

            var log = MyFirstSubnauticaModPlugin.Log;
            var key = MyFirstSubnauticaModPlugin.LifeSyncLoginMenuKey.Value;
            LifeSyncInputRegistration.EnsureRegistered(log, key);

            var gameInputDown = GameInput.IsInitialized &&
                                LifeSyncInputRegistration.IsLoginMenuRegistered &&
                                GameInput.GetButtonDown(LifeSyncInputRegistration.LoginMenuButton);
            var legacyDown = UnityEngine.Input.GetKeyDown(key);

            if (gameInputDown || legacyDown)
            {
                ToggleWindow();
            }

            if (_show)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void ToggleWindow()
        {
            _show = !_show;
            if (_show)
            {
                _status = string.Empty;
                _sessionStatus = string.Empty;
                _pointsStatus = string.Empty;
                _sessionTab = SessionTab.Token;
                _dimensionEntries = null;
                // Con Bearer en cfg: siempre panel de sesión al pulsar la tecla. Sin token: formulario de login (primera vez o tras cerrar sesión).
                var bearer = MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value;
                _panel = HasStoredBearer(bearer) ? MenuPanel.Session : MenuPanel.Login;
                AdjustWindowSizeForPanel();
                ApplyGameplayPause(true);
                ApplyCursorForMenu(true);
            }
            else
            {
                _password = string.Empty;
                ReleaseGameplayPause();
                ApplyCursorForMenu(false);
            }
        }

        /// <summary>Pausa el mundo con <see cref="Time.timeScale"/> mientras el menú LifeSync está abierto.</summary>
        private void ApplyGameplayPause(bool pause)
        {
            if (pause)
            {
                if (_pausedByMenu)
                {
                    return;
                }

                _timeScaleBeforeMenu = Time.timeScale;
                Time.timeScale = 0f;
                _pausedByMenu = true;
                return;
            }

            ReleaseGameplayPause();
        }

        private void ReleaseGameplayPause()
        {
            if (!_pausedByMenu)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforeMenu > 0f ? _timeScaleBeforeMenu : 1f;
            _pausedByMenu = false;
        }

        private static bool HasStoredBearer(string bearer)
        {
            return !string.IsNullOrWhiteSpace(bearer);
        }

        private void AdjustWindowSizeForPanel()
        {
            _windowRect.width = 360f;
            if (_panel == MenuPanel.Session)
            {
                switch (_sessionTab)
                {
                    case SessionTab.Points:
                        _windowRect.height = 460f;
                        break;
                    case SessionTab.Mechanics:
                        _windowRect.height = 500f;
                        break;
                    default:
                        _windowRect.height = 320f;
                        break;
                }
            }
            else
            {
                _windowRect.height = 260f;
            }
        }

        /// <summary>Borra token en cfg y cliente; muestra de nuevo el login dentro del menú abierto.</summary>
        private void LogOutAndShowLogin()
        {
            MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value = string.Empty;
            MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value = 0;
            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            client?.SetBearerToken(string.Empty);
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            _password = string.Empty;
            _sessionStatus = string.Empty;
            _pointsStatus = string.Empty;
            _dimensionEntries = null;
            _mechanicRows = null;
            _mechanicsStatus = string.Empty;
            _sessionTab = SessionTab.Token;
            _status = "Sesión cerrada. Introduce usuario y contraseña para volver a entrar.";
            _panel = MenuPanel.Login;
            AdjustWindowSizeForPanel();
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Auth] Sesión cerrada (token e id jugador borrados del .cfg).");
        }

        private static void ApplyCursorForMenu(bool open)
        {
            if (Player.main == null)
            {
                return;
            }

            if (open)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnGUI()
        {
            if (!Player.main)
            {
                return;
            }

            if (_show)
            {
                GUI.depth = -2000;
            }

            if (!_positioned)
            {
                AdjustWindowSizeForPanel();
                _windowRect.x = (Screen.width - _windowRect.width) * 0.5f;
                _windowRect.y = (Screen.height - _windowRect.height) * 0.5f;
                _positioned = true;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (_show)
                {
                    _show = false;
                    _password = string.Empty;
                    ReleaseGameplayPause();
                    ApplyCursorForMenu(false);
                    Event.current.Use();
                }

                return;
            }

            if (!_show)
            {
                return;
            }

            var title = _panel == MenuPanel.Login
                ? "LifeSync — Iniciar sesión"
                : "LifeSync — Sesión";

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, title);
        }

        private void DrawWindow(int id)
        {
            if (_panel == MenuPanel.Login)
            {
                DrawLoginPanel();
            }
            else
            {
                DrawSessionPanel();
            }

            GUI.DragWindow();
        }

        private void DrawLoginPanel()
        {
            GUILayout.Label("Usuario (email en lsg-auth):");
            GUI.enabled = !_submitting;
            _username = GUILayout.TextField(_username, 128);

            GUILayout.Label("Contraseña:");
            _password = GUILayout.PasswordField(_password, '*', 64);

            if (GUILayout.Button(_submitting ? "Conectando…" : "Iniciar sesión en LifeSync") && !_submitting)
            {
                StartCoroutine(LoginRoutine());
            }

            if (GUILayout.Button("Cancelar"))
            {
                _show = false;
                _password = string.Empty;
                ReleaseGameplayPause();
                ApplyCursorForMenu(false);
            }

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(6);
                GUILayout.Label(_status);
            }
        }

        private void DrawSessionPanel()
        {
            GUILayout.Label("Cuenta vinculada — elige sección:");
            var newTab = (SessionTab)GUILayout.Toolbar((int)_sessionTab, new[] { "Token", "Puntos", "Mecánicas" });
            if (newTab != _sessionTab)
            {
                _sessionTab = newTab;
                AdjustWindowSizeForPanel();
            }

            GUILayout.Space(8);

            switch (_sessionTab)
            {
                case SessionTab.Token:
                    DrawSessionTokenTab();
                    break;
                case SessionTab.Points:
                    DrawSessionPointsTab();
                    break;
                case SessionTab.Mechanics:
                    DrawSessionMechanicsTab();
                    break;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Cerrar sesión"))
            {
                LogOutAndShowLogin();
            }

            if (GUILayout.Button("Salir del menú"))
            {
                _show = false;
                _password = string.Empty;
                ReleaseGameplayPause();
                ApplyCursorForMenu(false);
            }
        }

        private void DrawSessionTokenTab()
        {
            GUILayout.Label(
                "Consulta cuánto falta para que venza el token o renueva sin volver a escribir la contraseña.");

            GUILayout.Space(6);
            GUI.enabled = !_submitting && _sessionRequest == SessionRequestKind.None;

            if (GUILayout.Button(_sessionRequest == SessionRequestKind.Remaining ? "Consultando…" : "Tiempo restante del token"))
            {
                StartCoroutine(FetchTokenRemainingRoutine());
            }

            if (GUILayout.Button(_sessionRequest == SessionRequestKind.Refresh ? "Renovando…" : "Renovar token"))
            {
                StartCoroutine(RefreshTokenRoutine());
            }

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_sessionStatus))
            {
                GUILayout.Space(10);
                GUILayout.Label(_sessionStatus);
            }
        }

        private void DrawSessionPointsTab()
        {
            var pid = MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value;
            GUILayout.Label(
                pid > 0
                    ? $"Jugador en caché: id {pid} (se guardó al iniciar sesión; whoami solo si hace falta)."
                    : "Aún no hay id de jugador en caché: al actualizar puntos se llamará a whoami una vez.");

            GUILayout.Space(6);
            GUI.enabled = !_pointsBusy && !_submitting && _sessionRequest == SessionRequestKind.None;

            if (GUILayout.Button(_pointsBusy ? "Actualizando…" : "Cargar puntos por dimensión"))
            {
                StartCoroutine(FetchDimensionsAndBalanceRoutine());
            }

            if (GUILayout.Button("Refrescar id jugador (whoami)"))
            {
                StartCoroutine(CachePlayerIdAfterAuthRoutine(forceRefresh: true));
            }

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_pointsStatus))
            {
                GUILayout.Space(8);
                GUILayout.Label(_pointsStatus);
            }

            if (_dimensionEntries != null && _dimensionEntries.Length > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("Puntos por dimensión (0 si la dimensión no aparece en /points/balance):", GUI.skin.box);

                var maxVal = 0;
                foreach (var e in _dimensionEntries)
                {
                    maxVal = Mathf.Max(maxVal, e.Balance);
                }

                // Escala visual: si todo es 0, usa 10 para que las barras no queden ambiguas.
                var scale = Mathf.Max(maxVal, 10);
                if (maxVal == 0)
                {
                    GUILayout.Label("(Todos los valores en 0; las barras usan escala de referencia 10.)");
                }

                var totalRowsHeight = Mathf.Min(280f, 56f * _dimensionEntries.Length + 8f);
                _pointsScroll = GUILayout.BeginScrollView(_pointsScroll, GUILayout.Height(totalRowsHeight));
                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                var numStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                foreach (var entry in _dimensionEntries)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(entry.Name ?? $"#{entry.IdDimension}", nameStyle, GUILayout.Width(110));
                    GUILayout.Label(BuildBar01((float)entry.Balance / scale, 26), GUILayout.Width(200));
                    GUILayout.Label(entry.Balance.ToString(), numStyle, GUILayout.Width(56));
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }

                GUILayout.EndScrollView();
            }
        }

        private void DrawSessionMechanicsTab()
        {
            var gameId = MyFirstSubnauticaModPlugin.LifeSyncApiTestVideogameId.Value;
            GUILayout.Label($"Catálogo de mecánicas del juego id={gameId} (LSG-CORE-API).");
            GUILayout.Label("Configurable en el .cfg de BepInEx — sección «LifeSync API», clave «Test Videogame Id».");

            GUILayout.Space(6);
            GUI.enabled = !_mechanicsBusy && !_submitting && _sessionRequest == SessionRequestKind.None;
            if (GUILayout.Button(_mechanicsBusy ? "Cargando…" : "Cargar mecánicas"))
            {
                StartCoroutine(FetchMechanicsRoutine());
            }

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_mechanicsStatus))
            {
                GUILayout.Space(6);
                GUILayout.Label(_mechanicsStatus);
            }

            if (_mechanicRows != null && _mechanicRows.Length > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label($"Mecánicas disponibles ({_mechanicRows.Length}):", GUI.skin.box);

                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                var descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
                var hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };

                _mechanicsScroll = GUILayout.BeginScrollView(_mechanicsScroll, GUILayout.Height(300f));
                foreach (var row in _mechanicRows)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();

                    GUILayout.BeginVertical();
                    GUILayout.Label(row.modifiable_mechanic_name ?? "—", nameStyle);
                    GUILayout.Label(row.modifiable_mechanic_description ?? string.Empty, descStyle);

                    var hasRecipe = RedeemCatalog.TryGet(row.id_modifiable_mechanic_videogame, out var recipe);
                    if (hasRecipe)
                    {
                        GUILayout.Label(
                            $"Costo: {recipe.Amount} pts (dim {recipe.PointDimensionId}). Efecto: {recipe.EffectSummary}",
                            hintStyle);
                    }
                    else if (row.id_modifiable_mechanic_videogame > 0)
                    {
                        GUILayout.Label(
                            $"(Sin receta local para mecánica id={row.id_modifiable_mechanic_videogame}; canje no disponible.)",
                            hintStyle);
                    }

                    GUILayout.EndVertical();

                    var thisIsRedeeming = _redeemingMechanicVideogameId == row.id_modifiable_mechanic_videogame
                                          && _redeemingMechanicVideogameId != 0;
                    GUI.enabled = !_mechanicsBusy
                                  && !_submitting
                                  && _sessionRequest == SessionRequestKind.None
                                  && _redeemingMechanicVideogameId == 0
                                  && hasRecipe;
                    if (GUILayout.Button(
                            thisIsRedeeming ? "Canjeando…" : "Canjear",
                            GUILayout.Width(90), GUILayout.Height(48)))
                    {
                        StartCoroutine(RedeemMechanicRoutine(row));
                    }

                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }

                GUILayout.EndScrollView();
            }
        }

        private IEnumerator FetchMechanicsRoutine()
        {
            _mechanicsBusy = true;
            _mechanicsStatus = "Consultando catálogo de mecánicas…";

            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                _mechanicsStatus = "No se pudo crear el cliente API.";
                _mechanicsBusy = false;
                yield break;
            }

            SyncBearerOnClient(client);
            if (string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                _mechanicsStatus = "Sin token. Inicia sesión primero.";
                _mechanicsBusy = false;
                yield break;
            }

            var gameId = MyFirstSubnauticaModPlugin.LifeSyncApiTestVideogameId.Value;
            var task = client.GetVideogameMechanicsAsync(gameId);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            _mechanicsBusy = false;

            if (!task.Result.Success)
            {
                _mechanicsStatus = $"Error al cargar mecánicas (HTTP {(int)task.Result.StatusCode}).";
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    $"[LifeSync][API] videogames/{gameId}/mechanics falló: {task.Result.ErrorMessage}");
                yield break;
            }

            if (!LifeSyncPointsJsonParsers.TryParseMechanicsArray(task.Result.ResponseBody, out var rows))
            {
                _mechanicRows = null;
                _mechanicsStatus = "Respuesta no reconocida (se esperaba un array JSON con mecánicas).";
                yield break;
            }

            _mechanicRows = rows;
            _mechanicsStatus = rows.Length == 0
                ? "El catálogo no tiene mecánicas para este videojuego."
                : $"Actualizado: {rows.Length} mecánica(s).";
            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][API] videogames/{gameId}/mechanics OK ({rows.Length} filas).");
        }

        /// <summary>
        /// POST /videogames/{game}/players/{player}/redeem con la receta hardcodeada.
        /// Si la respuesta es 2xx, aplica el efecto local (p. ej. +1 daño cuchillo).
        /// </summary>
        private IEnumerator RedeemMechanicRoutine(ModifiableMechanicRow row)
        {
            if (row == null || row.id_modifiable_mechanic_videogame <= 0)
            {
                _mechanicsStatus = "Mecánica inválida (sin id_modifiable_mechanic_videogame).";
                yield break;
            }

            if (!RedeemCatalog.TryGet(row.id_modifiable_mechanic_videogame, out var recipe))
            {
                _mechanicsStatus = $"No hay receta local para mecánica id={row.id_modifiable_mechanic_videogame}.";
                yield break;
            }

            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                _mechanicsStatus = "No se pudo crear el cliente API.";
                yield break;
            }

            SyncBearerOnClient(client);
            if (string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                _mechanicsStatus = "Sin token. Inicia sesión primero.";
                yield break;
            }

            yield return StartCoroutine(CachePlayerIdAfterAuthRoutine(forceRefresh: false));
            var playerId = MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value;
            if (playerId <= 0)
            {
                _mechanicsStatus = "No se pudo obtener el id del jugador (whoami).";
                yield break;
            }

            var gameId = MyFirstSubnauticaModPlugin.LifeSyncApiTestVideogameId.Value;
            var body = RedeemCatalog.BuildPreviewBodyJson(row.id_modifiable_mechanic_videogame, recipe);

            _redeemingMechanicVideogameId = row.id_modifiable_mechanic_videogame;
            _mechanicsStatus = $"Canjeando «{row.modifiable_mechanic_name}» — {recipe.Amount} pts (dim {recipe.PointDimensionId})…";
            MyFirstSubnauticaModPlugin.Log.LogInfo($"[LifeSync][Redeem] POST redeem body={body}");

            var task = client.PostRedeemAsync(gameId, playerId, body);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            _redeemingMechanicVideogameId = 0;

            if (!task.Result.Success)
            {
                _mechanicsStatus =
                    $"Canje rechazado (HTTP {(int)task.Result.StatusCode}). Revisa saldo y vuelve a intentar.";
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    $"[LifeSync][Redeem] FAIL HTTP {(int)task.Result.StatusCode}: {task.Result.ErrorMessage} | body={task.Result.ResponseBody}");
                yield break;
            }

            try
            {
                recipe.ApplyLocalEffect?.Invoke();
            }
            catch (System.Exception ex)
            {
                MyFirstSubnauticaModPlugin.Log.LogError($"[LifeSync][Redeem] Error aplicando efecto local: {ex}");
            }

            _mechanicsStatus =
                $"Canje OK: «{row.modifiable_mechanic_name}». Daño bonus cuchillo = {MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value}. " +
                "El cambio se aplicará la próxima vez que el cuchillo se inicialice.";
            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Redeem] OK ({row.modifiable_mechanic_name}). Bonus cuchillo = {MyFirstSubnauticaModPlugin.KnifeBonusDamage.Value}.");

            // Refresca silenciosamente las dimensiones para que el usuario vea el saldo descontado al cambiar de pestaña.
            yield return StartCoroutine(FetchDimensionsAndBalanceRoutine());
        }

        private static string BuildBar01(float t01, int widthChars = 18)
        {
            t01 = Mathf.Clamp01(t01);
            var w = Mathf.Clamp(widthChars, 8, 40);
            var filled = Mathf.RoundToInt(w * t01);
            return new string('█', filled) + new string('·', w - filled);
        }

        /// <summary>
        /// GET lsg-auth/whoami y guarda <see cref="MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId"/>.
        /// Si <paramref name="forceRefresh"/> es false y ya hay id &gt; 0, no hace red.
        /// </summary>
        private IEnumerator CachePlayerIdAfterAuthRoutine(bool forceRefresh = false)
        {
            if (!forceRefresh && MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value > 0)
            {
                yield break;
            }

            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                MyFirstSubnauticaModPlugin.Log.LogWarning("[LifeSync][whoami] Sin cliente API.");
                if (forceRefresh)
                {
                    _pointsStatus = "No hay cliente API para whoami.";
                }

                yield break;
            }

            SyncBearerOnClient(client);
            if (string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                if (forceRefresh)
                {
                    _pointsStatus = "Sin token; inicia sesión primero.";
                }

                yield break;
            }

            var task = client.GetLsgAuthWhoamiAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (!task.Result.Success)
            {
                MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][whoami] HTTP {(int)task.Result.StatusCode}");
                if (forceRefresh)
                {
                    _pointsStatus = $"whoami falló (HTTP {(int)task.Result.StatusCode}).";
                }

                yield break;
            }

            if (!LifeSyncPointsJsonParsers.TryParseWhoamiPlayerId(task.Result.ResponseBody, out var pid))
            {
                MyFirstSubnauticaModPlugin.Log.LogWarning("[LifeSync][whoami] No se pudo leer id_players del JSON.");
                if (forceRefresh)
                {
                    _pointsStatus = "whoami OK pero no se reconoció id_players.";
                }

                yield break;
            }

            MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value = pid;
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            MyFirstSubnauticaModPlugin.Log.LogInfo($"[LifeSync] id_players cacheado: {pid}");
            if (forceRefresh)
            {
                _pointsStatus = $"Id de jugador guardado: {pid}.";
            }
        }

        /// <summary>
        /// Carga el catálogo /attributes y el saldo /players/{id}/points/balance, los junta por
        /// <c>id_attributes == id_point_dimension</c> y rellena las dimensiones sin saldo con <c>0</c>.
        /// </summary>
        private IEnumerator FetchDimensionsAndBalanceRoutine()
        {
            _pointsBusy = true;
            _pointsStatus = "Preparando consulta…";

            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                _pointsStatus = "No se pudo crear el cliente API.";
                _pointsBusy = false;
                yield break;
            }

            SyncBearerOnClient(client);
            if (string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                _pointsStatus = "Sin token. Inicia sesión primero.";
                _pointsBusy = false;
                yield break;
            }

            yield return StartCoroutine(CachePlayerIdAfterAuthRoutine(forceRefresh: false));

            var playerId = MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value;
            if (playerId <= 0)
            {
                _pointsStatus = "No se pudo obtener el id del jugador (whoami).";
                _pointsBusy = false;
                yield break;
            }

            _pointsStatus = "Cargando catálogo /attributes…";
            var attrTask = client.GetAttributesAsync();
            while (!attrTask.IsCompleted)
            {
                yield return null;
            }

            if (!attrTask.Result.Success)
            {
                _pointsStatus = $"Error al cargar /attributes (HTTP {(int)attrTask.Result.StatusCode}).";
                MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][API] /attributes falló: {attrTask.Result.ErrorMessage}");
                _pointsBusy = false;
                yield break;
            }

            if (!LifeSyncPointsJsonParsers.TryParseAttributesArray(attrTask.Result.ResponseBody, out var attrs))
            {
                _pointsStatus = "No se pudo parsear /attributes (formato inesperado).";
                _dimensionEntries = null;
                _pointsBusy = false;
                yield break;
            }

            _pointsStatus = $"Cargando saldos del jugador {playerId}…";
            var balTask = client.GetPlayerPointsBalanceAsync(playerId);
            while (!balTask.IsCompleted)
            {
                yield return null;
            }

            _pointsBusy = false;

            if (!balTask.Result.Success)
            {
                _pointsStatus = $"Error al cargar /points/balance (HTTP {(int)balTask.Result.StatusCode}).";
                MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][API] points/balance falló: {balTask.Result.ErrorMessage}");
                yield break;
            }

            if (!LifeSyncPointsJsonParsers.TryParsePlayerPointsBalanceArray(balTask.Result.ResponseBody, out var balances))
            {
                _pointsStatus = "No se pudo parsear /points/balance (formato inesperado).";
                _dimensionEntries = null;
                yield break;
            }

            // Diccionario rápido: id_point_dimension → balance.
            var byDimension = new System.Collections.Generic.Dictionary<int, int>(balances.Length);
            foreach (var b in balances)
            {
                byDimension[b.id_point_dimension] = b.balance;
            }

            var merged = new DimensionPointEntry[attrs.Length];
            for (var i = 0; i < attrs.Length; i++)
            {
                var a = attrs[i];
                merged[i] = new DimensionPointEntry
                {
                    IdDimension = a.id_attributes,
                    Name = a.name,
                    Balance = byDimension.TryGetValue(a.id_attributes, out var bal) ? bal : 0,
                };
            }

            _dimensionEntries = merged;
            _pointsStatus = $"Actualizado: {merged.Length} dimensión(es); {balances.Length} con saldo en API.";
            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][API] Dimensiones merged OK (attrs={attrs.Length}, balances={balances.Length}).");
        }

        private static void SyncBearerOnClient(LifeSyncApiClient client)
        {
            if (client != null && !string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                client.SetBearerToken(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value.Trim());
            }
        }

        private static string SanitizeTokenRemainingJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return "{}";
            }

            return json.Replace("\"issued_at\":null", "\"issued_at\":\"\"");
        }

        private static string FormatTokenRemaining(LsgAuthTokenRemainingResponse r)
        {
            if (r == null)
            {
                return "Respuesta sin datos reconocibles.";
            }

            var msgPart = string.IsNullOrEmpty(r.message) ? string.Empty : $" [{r.message}]";
            if (r.expires_in_seconds < 0)
            {
                var expiresAtNeg = string.IsNullOrEmpty(r.expires_at) ? "—" : r.expires_at;
                return
                    $"El servidor devolvió expires_in_seconds={r.expires_in_seconds}{msgPart}. " +
                    $"expires_at: {expiresAtNeg}. (Si el backend aún no calcula el tiempo restante, usa «Renovar token».)";
            }

            var minutes = r.expires_in_seconds / 60;
            var secs = r.expires_in_seconds % 60;
            var expiresAt = string.IsNullOrEmpty(r.expires_at) ? "—" : r.expires_at;
            var issuedPart = string.IsNullOrEmpty(r.issued_at) ? string.Empty : $" Emitido: {r.issued_at}.";
            return
                $"Quedan {r.expires_in_seconds} segundos (~{minutes} min {secs} s). " +
                $"Expira (servidor): {expiresAt}.{issuedPart}{msgPart}";
        }

        private IEnumerator FetchTokenRemainingRoutine()
        {
            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                _sessionStatus = "No se pudo crear el cliente API (revisa el log de BepInEx).";
                yield break;
            }

            SyncBearerOnClient(client);

            if (string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                _sessionStatus = "No hay token guardado. Cierra el menú y ábrelo de nuevo para ver el inicio de sesión.";
                yield break;
            }

            _sessionRequest = SessionRequestKind.Remaining;
            _sessionStatus = "Consultando tiempo restante…";

            var task = client.GetLsgAuthTokenRemainingAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            _sessionRequest = SessionRequestKind.None;

            if (task.IsFaulted)
            {
                _sessionStatus = "Error: " + task.Exception?.GetBaseException().Message;
                yield break;
            }

            var result = task.Result;
            if (!result.Success)
            {
                _sessionStatus =
                    $"No se pudo obtener el tiempo restante (HTTP {(int)result.StatusCode}). " +
                    "¿Token caducado? Prueba «Renovar token» o vuelve a iniciar sesión.";
                MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][Auth] token/remaining falló: {result.ErrorMessage}");
                yield break;
            }

            LsgAuthTokenRemainingResponse remaining;
            try
            {
                var json = SanitizeTokenRemainingJson(result.ResponseBody);
                remaining = JsonUtility.FromJson<LsgAuthTokenRemainingResponse>(json);
            }
            catch
            {
                remaining = null;
            }

            if (remaining == null)
            {
                _sessionStatus = "Respuesta inesperada: " + (result.ResponseBody ?? string.Empty);
                yield break;
            }

            _sessionStatus = FormatTokenRemaining(remaining);
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Auth] token/remaining OK.");
        }

        private IEnumerator RefreshTokenRoutine()
        {
            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                _sessionStatus = "No se pudo crear el cliente API (revisa el log de BepInEx).";
                yield break;
            }

            SyncBearerOnClient(client);

            if (string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                _sessionStatus = "No hay token para renovar. Inicia sesión primero.";
                yield break;
            }

            _sessionRequest = SessionRequestKind.Refresh;
            _sessionStatus = "Renovando token…";

            var task = client.PostLsgAuthTokenRefreshAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            _sessionRequest = SessionRequestKind.None;

            if (task.IsFaulted)
            {
                _sessionStatus = "Error: " + task.Exception?.GetBaseException().Message;
                yield break;
            }

            var result = task.Result;
            if (!result.Success)
            {
                _sessionStatus =
                    $"No se pudo renovar (HTTP {(int)result.StatusCode}). " +
                    "Puede que el token haya expirado; inicia sesión de nuevo con usuario y contraseña.";
                MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][Auth] token/refresh falló: {result.ErrorMessage}");
                yield break;
            }

            LsgAuthTokenResponse token;
            try
            {
                token = JsonUtility.FromJson<LsgAuthTokenResponse>(result.ResponseBody ?? string.Empty);
            }
            catch
            {
                token = null;
            }

            if (token == null || string.IsNullOrEmpty(token.access_token))
            {
                _sessionStatus = "El servidor respondió pero no trajo access_token.";
                MyFirstSubnauticaModPlugin.Log.LogWarning("[LifeSync][Auth] refresh sin access_token.");
                yield break;
            }

            MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value = token.access_token;
            client.SetBearerToken(token.access_token);
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            _sessionStatus = "Token renovado correctamente. Ya está guardado en la configuración.";
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Auth] Token renovado y guardado.");
            if (MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value <= 0)
            {
                StartCoroutine(CachePlayerIdAfterAuthRoutine(forceRefresh: true));
            }
        }

        private IEnumerator LoginRoutine()
        {
            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrEmpty(_password))
            {
                _status = "Introduce usuario y contraseña.";
                yield break;
            }

            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                _status = "No se pudo crear el cliente API (revisa el log de BepInEx).";
                yield break;
            }

            _submitting = true;
            _status = "Contactando lsg-auth…";

            var task = client.PostLsgAuthLoginAsync(_username.Trim(), _password);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            _submitting = false;

            if (task.IsFaulted)
            {
                _status = "Error inesperado: " + task.Exception?.GetBaseException().Message;
                yield break;
            }

            var result = task.Result;
            if (!result.Success)
            {
                _status = $"Fallo HTTP {(int)result.StatusCode}. Revisa credenciales o la URL de Auth.";
                MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][Auth] Login fallido: {result.ErrorMessage}");
                yield break;
            }

            LsgAuthTokenResponse token;
            try
            {
                token = JsonUtility.FromJson<LsgAuthTokenResponse>(result.ResponseBody ?? string.Empty);
            }
            catch
            {
                token = null;
            }

            if (token == null || string.IsNullOrEmpty(token.access_token))
            {
                _status = "Respuesta sin access_token.";
                MyFirstSubnauticaModPlugin.Log.LogWarning("[LifeSync][Auth] JSON sin access_token.");
                yield break;
            }

            MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value = token.access_token;
            client.SetBearerToken(token.access_token);
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            _password = string.Empty;
            _status = string.Empty;
            _sessionStatus = "Sesión iniciada. Pestañas Token / Puntos.";
            _panel = MenuPanel.Session;
            AdjustWindowSizeForPanel();
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Auth] Login correcto; token guardado.");
            StartCoroutine(CachePlayerIdAfterAuthRoutine(forceRefresh: true));
        }
    }
}
