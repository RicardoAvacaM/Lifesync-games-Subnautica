using System.Collections;
using System.Collections.Generic;
using MyFirstSubnauticaMod.Input;
using MyFirstSubnauticaMod.Services;
using MyFirstSubnauticaMod.Services.Models;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyFirstSubnauticaMod.UI
{
    /// <summary>
    /// Menú LifeSync en uGUI (Canvas + TextMeshPro) con estética PDA. Sin token muestra el login;
    /// con token, abre el panel de sesión (Token / Puntos / Mecánicas). La lógica de red es la misma de siempre.
    /// </summary>
    internal sealed class LifeSyncLoginMenu : MonoBehaviour
    {
        internal static LifeSyncLoginMenu Instance { get; private set; }

        private enum MenuPanel { Login, Session }

        private enum SessionTab { Token, Points, Mechanics }

        private enum SessionRequestKind { None, Remaining, Refresh }

        // ----- Estado (igual que la versión IMGUI) -----
        private bool _show;
        private MenuPanel _panel = MenuPanel.Login;
        private SessionTab _sessionTab = SessionTab.Token;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _status = string.Empty;
        private string _sessionStatus = string.Empty;
        private bool _submitting;
        private SessionRequestKind _sessionRequest;
        private DimensionPointEntry[] _dimensionEntries;
        private Dictionary<int, string> _dimensionNameById;
        private string _pointsStatus = string.Empty;
        private bool _pointsBusy;
        private ModifiableMechanicRow[] _mechanicRows;
        private string _mechanicsStatus = string.Empty;
        private bool _mechanicsBusy;
        private int _redeemingMechanicVideogameId;
        private bool _pausedByMenu;
        private float _timeScaleBeforeMenu = 1f;

        // EventSystem propio para que nuestros botones reciban clics (el FPSInputModule del juego filtra canvases).
        private EventSystem _ownEventSystem;
        private readonly List<EventSystem> _suspendedEventSystems = new List<EventSystem>();

        // ----- uGUI -----
        private bool _uiBuilt;
        private GameObject _canvasGo;
        private GameObject _loginPanel;
        private GameObject _sessionPanel;
        private TextMeshProUGUI _titleLabel;

        private TMP_InputField _usernameInput;
        private TMP_InputField _passwordInput;
        private Button _loginButton;
        private Button _loginCancelButton;
        private TextMeshProUGUI _loginStatusLabel;

        private Button _tabTokenButton;
        private Button _tabPointsButton;
        private Button _tabMechanicsButton;
        private GameObject _tokenContent;
        private GameObject _pointsContent;
        private GameObject _mechanicsContent;
        private Button _logoutButton;
        private Button _exitButton;

        private Button _btnRemaining;
        private Button _btnRefresh;
        private TextMeshProUGUI _tokenStatusLabel;
        private Toggle _fatigueToggle;
        private TextMeshProUGUI _fatigueHintLabel;
        private string _lastFatigueHint;

        private RectTransform _pointsListContent;
        private TextMeshProUGUI _pointsStatusLabel;

        private RectTransform _mechListContent;

        // Para rebuild de listas dinámicas (solo cuando cambian los datos).
        private DimensionPointEntry[] _lastPointsRef;
        private string _lastPointsStatus = string.Empty;
        private ModifiableMechanicRow[] _lastMechRef;
        private int _lastRedeemingId = -1;
        private bool _lastMechBusy;
        private readonly List<MechRowRefs> _mechRowRefs = new List<MechRowRefs>();

        private struct MechRowRefs
        {
            public int MechanicVideogameId;
            public bool HasRecipe;
            public Button RedeemButton;
            public TMP_Text RedeemLabel;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            SetInputActive(false);
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

            if (_show && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu();
                return;
            }

            if (_show)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                SyncUi();
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
                _mechanicsStatus = string.Empty;
                _sessionTab = SessionTab.Token;
                _dimensionEntries = null;
                _dimensionNameById = null;
                _mechanicRows = null;

                var bearer = MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value;
                _panel = HasStoredBearer(bearer) ? MenuPanel.Session : MenuPanel.Login;

                EnsureUi();
                _canvasGo.SetActive(true);
                ApplyPanelVisibility();
                ApplyGameplayPause(true);
                ApplyCursorForMenu(true);
                SetInputActive(true);
            }
            else
            {
                CloseMenu();
            }
        }

        private void CloseMenu()
        {
            _show = false;
            _password = string.Empty;
            if (_passwordInput != null)
            {
                _passwordInput.text = string.Empty;
            }

            if (_canvasGo != null)
            {
                _canvasGo.SetActive(false);
            }

            SetInputActive(false);
            ReleaseGameplayPause();
            ApplyCursorForMenu(false);
        }

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

        /// <summary>Borra token en cfg y cliente; muestra de nuevo el login dentro del menú abierto.</summary>
        private void LogOutAndShowLogin()
        {
            StartCoroutine(LogOutAndShowLoginRoutine());
        }

        private IEnumerator LogOutAndShowLoginRoutine()
        {
            yield return GameSessionLogService.EndSessionUploadRoutine();

            MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value = string.Empty;
            MyFirstSubnauticaModPlugin.LifeSyncCachedPlayerId.Value = 0;
            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            client?.SetBearerToken(string.Empty);
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            _password = string.Empty;
            if (_passwordInput != null)
            {
                _passwordInput.text = string.Empty;
            }

            _sessionStatus = string.Empty;
            _pointsStatus = string.Empty;
            _mechanicRows = null;
            _mechanicsStatus = string.Empty;
            _dimensionEntries = null;
            _dimensionNameById = null;
            _sessionTab = SessionTab.Token;
            _status = "Sesión cerrada. Introduce usuario y contraseña para volver a entrar.";
            _panel = MenuPanel.Login;
            ApplyPanelVisibility();
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Auth] Sesión cerrada (token e id jugador borrados del .cfg).");
        }

        // ===================== Construcción de UI =====================

        private void EnsureUi()
        {
            if (_uiBuilt && _canvasGo != null)
            {
                return;
            }

            BuildUi();
            _uiBuilt = true;
        }

        /// <summary>
        /// Activa/desactiva un EventSystem propio con <see cref="StandaloneInputModule"/> para que nuestros
        /// botones reciban clics. Mientras está activo, suspende los demás EventSystems (el del juego usa un
        /// módulo que filtra canvases ajenos); se restauran al cerrar. El juego está en pausa, así que es seguro.
        /// </summary>
        private void SetInputActive(bool active)
        {
            if (active)
            {
                _suspendedEventSystems.Clear();
                foreach (var es in Object.FindObjectsOfType<EventSystem>())
                {
                    if (es != null && es != _ownEventSystem && es.enabled)
                    {
                        es.enabled = false;
                        _suspendedEventSystems.Add(es);
                    }
                }

                if (_ownEventSystem == null)
                {
                    try
                    {
                        var go = new GameObject("LifeSyncEventSystem");
                        DontDestroyOnLoad(go);
                        _ownEventSystem = go.AddComponent<EventSystem>();
                        var module = go.AddComponent<StandaloneInputModule>();
                        module.forceModuleActive = true;
                    }
                    catch (System.Exception ex)
                    {
                        MyFirstSubnauticaModPlugin.Log.LogWarning($"[LifeSync][UI] No se pudo crear EventSystem propio: {ex.Message}");
                        return;
                    }
                }

                _ownEventSystem.gameObject.SetActive(true);
                _ownEventSystem.enabled = true;
                EventSystem.current = _ownEventSystem;
            }
            else
            {
                if (_ownEventSystem != null)
                {
                    _ownEventSystem.enabled = false;
                    _ownEventSystem.gameObject.SetActive(false);
                }

                foreach (var es in _suspendedEventSystems)
                {
                    if (es != null)
                    {
                        es.enabled = true;
                        EventSystem.current = es;
                    }
                }

                _suspendedEventSystems.Clear();
            }
        }

        private void BuildUi()
        {
            _canvasGo = new GameObject("LifeSyncCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Tinte oscuro de fondo para enfocar la atención.
            var dim = PdaUi.CreatePanel("Dim", _canvasGo.transform, new Color(0f, 0f, 0f, 0.55f), false);
            PdaUi.Stretch(dim.rectTransform, 0, 0, 0, 0);

            // Ventana central.
            var window = PdaUi.CreatePanel("Window", _canvasGo.transform, PdaTheme.Background);
            var wrt = window.rectTransform;
            wrt.anchorMin = new Vector2(0.5f, 0.5f);
            wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = new Vector2(600f, 680f);

            // Barra de título.
            var header = PdaUi.CreatePanel("Header", wrt, PdaTheme.PanelRaised);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.sizeDelta = new Vector2(-20f, 54f);
            header.rectTransform.anchoredPosition = new Vector2(0f, -10f);

            _titleLabel = PdaUi.CreateLabel("Title", header.rectTransform, "LIFESYNC", 24, PdaTheme.Accent, TextAlignmentOptions.Left, false);
            PdaUi.Stretch(_titleLabel.rectTransform, 18, 4, 18, 4);

            BuildLoginPanel(wrt);
            BuildSessionPanel(wrt);

            _canvasGo.SetActive(false);
        }

        private void BuildLoginPanel(RectTransform window)
        {
            _loginPanel = PdaUi.CreateRect("LoginPanel", window).gameObject;
            var rt = (RectTransform)_loginPanel.transform;
            PdaUi.Stretch(rt, 16, 74, 16, 16);

            var y = 0f;
            var lblUser = PdaUi.CreateLabel("UserLbl", rt, "Usuario (email en lsg-auth)", 15, PdaTheme.TextMuted, TextAlignmentOptions.Left, false);
            PlaceTop(lblUser.rectTransform, ref y, 24f, 4f);

            _usernameInput = PdaUi.CreateInput("UserInput", rt, "tu-correo@usach.cl", password: false);
            PlaceTop(_usernameInput.GetComponent<RectTransform>(), ref y, 46f, 14f);

            var lblPass = PdaUi.CreateLabel("PassLbl", rt, "Contraseña", 15, PdaTheme.TextMuted, TextAlignmentOptions.Left, false);
            PlaceTop(lblPass.rectTransform, ref y, 24f, 4f);

            _passwordInput = PdaUi.CreateInput("PassInput", rt, "••••••••", password: true);
            PlaceTop(_passwordInput.GetComponent<RectTransform>(), ref y, 46f, 20f);

            _loginButton = PdaUi.CreateButton("LoginBtn", rt, "INICIAR SESIÓN EN LIFESYNC", () =>
            {
                if (!_submitting)
                {
                    StartCoroutine(LoginRoutine());
                }
            });
            PlaceTop(_loginButton.GetComponent<RectTransform>(), ref y, 52f, 10f);

            _loginCancelButton = PdaUi.CreateButton("CancelBtn", rt, "Cancelar", CloseMenu, 15);
            PlaceTop(_loginCancelButton.GetComponent<RectTransform>(), ref y, 40f, 16f);

            _loginStatusLabel = PdaUi.CreateLabel("LoginStatus", rt, string.Empty, 14, PdaTheme.AccentOrange, TextAlignmentOptions.TopLeft);
            _loginStatusLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _loginStatusLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _loginStatusLabel.rectTransform.offsetMin = new Vector2(2f, 0f);
            _loginStatusLabel.rectTransform.offsetMax = new Vector2(-2f, -(y + 4f));
        }

        private void BuildSessionPanel(RectTransform window)
        {
            _sessionPanel = PdaUi.CreateRect("SessionPanel", window).gameObject;
            var rt = (RectTransform)_sessionPanel.transform;
            PdaUi.Stretch(rt, 16, 74, 16, 16);

            // Fila de pestañas.
            var tabs = PdaUi.CreateRect("Tabs", rt);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.sizeDelta = new Vector2(0f, 44f);
            tabs.anchoredPosition = Vector2.zero;
            var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 8f;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            _tabTokenButton = PdaUi.CreateButton("TabToken", tabs, "TOKEN", () => SetTab(SessionTab.Token), 15);
            _tabPointsButton = PdaUi.CreateButton("TabPoints", tabs, "PUNTOS", () => SetTab(SessionTab.Points), 15);
            _tabMechanicsButton = PdaUi.CreateButton("TabMech", tabs, "MECÁNICAS", () => SetTab(SessionTab.Mechanics), 15);

            // Footer (cerrar sesión / salir).
            var footer = PdaUi.CreateRect("Footer", rt);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(0f, 46f);
            footer.anchoredPosition = Vector2.zero;
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 8f;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = true;
            _logoutButton = PdaUi.CreateButton("Logout", footer, "Cerrar sesión", LogOutAndShowLogin, 15);
            _exitButton = PdaUi.CreateButton("Exit", footer, "Salir del menú", CloseMenu, 15);

            // Área de contenido entre pestañas y footer.
            var content = PdaUi.CreateRect("Content", rt);
            PdaUi.Stretch(content, 0, 52, 0, 54);

            BuildTokenTab(content);
            BuildPointsTab(content);
            BuildMechanicsTab(content);
        }

        private void BuildTokenTab(RectTransform content)
        {
            _tokenContent = PdaUi.CreateRect("TokenTab", content).gameObject;
            var rt = (RectTransform)_tokenContent.transform;
            PdaUi.Stretch(rt, 0, 0, 0, 0);

            var y = 0f;
            var info = PdaUi.CreateLabel("Info", rt,
                "Consulta cuánto falta para que venza el token o renuévalo sin escribir la contraseña.",
                14, PdaTheme.TextMuted, TextAlignmentOptions.TopLeft);
            PlaceTop(info.rectTransform, ref y, 44f, 10f);

            _btnRemaining = PdaUi.CreateButton("BtnRemaining", rt, "Tiempo restante del token",
                () => StartCoroutine(FetchTokenRemainingRoutine()));
            PlaceTop(_btnRemaining.GetComponent<RectTransform>(), ref y, 46f, 8f);

            _btnRefresh = PdaUi.CreateButton("BtnRefresh", rt, "Renovar token",
                () => StartCoroutine(RefreshTokenRoutine()));
            PlaceTop(_btnRefresh.GetComponent<RectTransform>(), ref y, 46f, 14f);

            var fatigueEnabled = MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled != null &&
                                 MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled.Value;
            _fatigueToggle = PdaUi.CreateToggle(
                "FatigueToggle",
                rt,
                "Penalizar juego prolongado (−5 vida/oxígeno máx.)",
                fatigueEnabled,
                OnFatigueToggleChanged,
                out var fatigueRow);
            PlaceTop(fatigueRow, ref y, 40f, 6f);

            _fatigueHintLabel = PdaUi.CreateLabel(
                "FatigueHint",
                rt,
                BuildFatigueHintText(),
                12,
                PdaTheme.TextMuted,
                TextAlignmentOptions.TopLeft,
                true);
            PlaceTop(_fatigueHintLabel.rectTransform, ref y, 52f, 8f);

            var statusBox = PdaUi.CreatePanel("StatusBox", rt, PdaTheme.Panel);
            statusBox.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusBox.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusBox.rectTransform.offsetMin = new Vector2(0f, 0f);
            statusBox.rectTransform.offsetMax = new Vector2(0f, -(y + 4f));
            _tokenStatusLabel = PdaUi.CreateLabel("TokenStatus", statusBox.rectTransform, string.Empty, 14, PdaTheme.TextPrimary);
            PdaUi.Stretch(_tokenStatusLabel.rectTransform, 12, 10, 12, 10);
        }

        private void OnFatigueToggleChanged(bool enabled)
        {
            if (MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled == null)
            {
                return;
            }

            MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled.Value = enabled;
            MyFirstSubnauticaModPlugin.Instance?.Config.Save();
            ContinuousPlayPenaltyService.OnSettingChanged();
            RefreshFatigueHint(force: true);
        }

        private static string BuildFatigueHintText()
        {
            var enabled = MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled != null &&
                          MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled.Value;
            var healthPenalty = MyFirstSubnauticaModPlugin.PlayerMaxHealthPenalty?.Value ?? 0;
            var oxygenPenalty = MyFirstSubnauticaModPlugin.PlayerMaxOxygenPenalty?.Value ?? 0;

            if (!enabled)
            {
                return "Desactivado. Si lo activas: tras <b>1 h</b> seguida en partida pierdes <b>5</b> de vida y oxígeno máx. " +
                       "permanentes; luego cada <b>30 min</b> (mínimo 30 vida / 20 oxígeno). " +
                       "El menú pausado no cuenta.";
            }

            var played = ContinuousPlayPenaltyService.FormatMinutes(ContinuousPlayPenaltyService.ContinuousPlaySeconds);
            var until = ContinuousPlayPenaltyService.FormatMinutes(ContinuousPlayPenaltyService.SecondsUntilNextPenalty);
            var streak = ContinuousPlayPenaltyService.PenaltiesAppliedThisStreak;

            var sb = new System.Text.StringBuilder(256);
            sb.Append("Activo · Juego seguido: <b>").Append(played).Append("</b>");
            sb.Append(" · Próxima penalización en <b>").Append(until).Append("</b>");
            if (streak > 0)
            {
                sb.Append(" (").Append(streak).Append(" ya aplicadas en esta racha)");
            }

            sb.AppendLine();
            sb.Append("Acumulado permanente: −").Append(healthPenalty).Append(" vida máx., −")
                .Append(oxygenPenalty).Append(" oxígeno máx.");
            return sb.ToString();
        }

        private void RefreshFatigueHint(bool force = false)
        {
            if (_fatigueHintLabel == null)
            {
                return;
            }

            var text = BuildFatigueHintText();
            if (!force && text == _lastFatigueHint)
            {
                return;
            }

            _lastFatigueHint = text;
            _fatigueHintLabel.text = text;
        }

        private void BuildPointsTab(RectTransform content)
        {
            _pointsContent = PdaUi.CreateRect("PointsTab", content).gameObject;
            var rt = (RectTransform)_pointsContent.transform;
            PdaUi.Stretch(rt, 0, 0, 0, 0);

            var statusRect = PdaUi.CreateRect("PointsStatus", rt);
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(0f, 28f);
            statusRect.offsetMin = new Vector2(12f, 8f);
            statusRect.offsetMax = new Vector2(-12f, 8f);
            _pointsStatusLabel = PdaUi.CreateLabel(
                "PointsStatusLabel",
                statusRect,
                string.Empty,
                13,
                PdaTheme.TextMuted,
                TextAlignmentOptions.Center,
                true);
            PdaUi.Stretch(_pointsStatusLabel.rectTransform, 0, 0, 0, 0);

            ScrollRect sr;
            var scrollRect = PdaUi.CreateRect("PointsScrollHolder", rt);
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(0f, 40f);
            scrollRect.offsetMax = new Vector2(0f, 0f);
            _pointsListContent = PdaUi.CreateScroll("PointsScroll", scrollRect, out sr);
            PdaUi.Stretch((RectTransform)sr.transform, 0, 0, 0, 0);
        }

        private void BuildMechanicsTab(RectTransform content)
        {
            _mechanicsContent = PdaUi.CreateRect("MechTab", content).gameObject;
            var rt = (RectTransform)_mechanicsContent.transform;
            PdaUi.Stretch(rt, 0, 0, 0, 0);

            ScrollRect sr;
            _mechListContent = PdaUi.CreateScroll("MechScroll", rt, out sr);
            PdaUi.Stretch((RectTransform)sr.transform, 0, 0, 0, 0);
        }

        /// <summary>Coloca un rect a lo ancho, anclado arriba, a la altura indicada e incrementa el cursor y.</summary>
        private static void PlaceTop(RectTransform rt, ref float y, float height, float gap)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -y);
            y += height + gap;
        }

        // ===================== Sincronización de UI =====================

        private void SetTab(SessionTab tab)
        {
            _sessionTab = tab;
            ApplyPanelVisibility();
            if (tab == SessionTab.Points)
            {
                TryAutoLoadPoints();
            }
            else if (tab == SessionTab.Mechanics)
            {
                TryAutoLoadMechanics();
            }
        }

        /// <summary>Al abrir la pestaña Puntos, carga dimensiones/saldos sin botón manual (whoami solo si falta id).</summary>
        private void TryAutoLoadPoints()
        {
            if (!_show || _panel != MenuPanel.Session || _pointsBusy || _submitting ||
                _sessionRequest != SessionRequestKind.None)
            {
                return;
            }

            StartCoroutine(FetchDimensionsAndBalanceRoutine());
        }

        /// <summary>Al abrir la pestaña Mecánicas, carga el catálogo automáticamente.</summary>
        private void TryAutoLoadMechanics()
        {
            if (!_show || _panel != MenuPanel.Session || _mechanicsBusy || _submitting ||
                _sessionRequest != SessionRequestKind.None || _redeemingMechanicVideogameId != 0)
            {
                return;
            }

            StartCoroutine(LoadMechanicsTabRoutine());
        }

        /// <summary>Asegura nombres de dimensión (/attributes) y luego carga mecánicas.</summary>
        private IEnumerator LoadMechanicsTabRoutine()
        {
            if (_dimensionNameById == null || _dimensionNameById.Count == 0)
            {
                yield return StartCoroutine(FetchAttributeNamesRoutine());
            }

            yield return StartCoroutine(FetchMechanicsRoutine());
        }

        private void ApplyPanelVisibility()
        {
            if (!_uiBuilt)
            {
                return;
            }

            var isLogin = _panel == MenuPanel.Login;
            _loginPanel.SetActive(isLogin);
            _sessionPanel.SetActive(!isLogin);
            _titleLabel.text = isLogin ? "LIFESYNC — INICIAR SESIÓN" : "LIFESYNC — SESIÓN";

            if (!isLogin)
            {
                _tokenContent.SetActive(_sessionTab == SessionTab.Token);
                _pointsContent.SetActive(_sessionTab == SessionTab.Points);
                _mechanicsContent.SetActive(_sessionTab == SessionTab.Mechanics);
                HighlightTab(_tabTokenButton, _sessionTab == SessionTab.Token);
                HighlightTab(_tabPointsButton, _sessionTab == SessionTab.Points);
                HighlightTab(_tabMechanicsButton, _sessionTab == SessionTab.Mechanics);
            }
        }

        private static void HighlightTab(Button btn, bool active)
        {
            PdaUi.SetButtonColor(btn, active ? PdaTheme.ButtonActive : PdaTheme.ButtonNormal);
            var label = PdaUi.ButtonLabel(btn);
            if (label != null)
            {
                label.color = active ? PdaTheme.TextPrimary : PdaTheme.TextMuted;
            }
        }

        private void SyncUi()
        {
            if (!_uiBuilt)
            {
                return;
            }

            if (_panel == MenuPanel.Login)
            {
                SetEnabled(_loginButton, !_submitting);
                SetEnabled(_usernameInput, !_submitting);
                SetEnabled(_passwordInput, !_submitting);
                SetButtonText(_loginButton, _submitting ? "CONECTANDO…" : "INICIAR SESIÓN EN LIFESYNC");
                _loginStatusLabel.text = _status ?? string.Empty;
                return;
            }

            // Token tab.
            var busyAny = _submitting || _sessionRequest != SessionRequestKind.None;
            SetEnabled(_btnRemaining, !busyAny);
            SetEnabled(_btnRefresh, !busyAny);
            SetButtonText(_btnRemaining, _sessionRequest == SessionRequestKind.Remaining ? "Consultando…" : "Tiempo restante del token");
            SetButtonText(_btnRefresh, _sessionRequest == SessionRequestKind.Refresh ? "Renovando…" : "Renovar token");
            _tokenStatusLabel.text = _sessionStatus ?? string.Empty;

            if (_sessionTab == SessionTab.Token)
            {
                RefreshFatigueHint();
                if (_fatigueToggle != null &&
                    MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled != null &&
                    _fatigueToggle.isOn != MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled.Value)
                {
                    _fatigueToggle.SetIsOnWithoutNotify(MyFirstSubnauticaModPlugin.ContinuousPlayPenaltyEnabled.Value);
                }
            }

            // Points tab (carga automática; mensaje de estado si falla o está cargando).
            if (_pointsStatusLabel != null && _pointsStatusLabel.text != (_pointsStatus ?? string.Empty))
            {
                _pointsStatusLabel.text = _pointsStatus ?? string.Empty;
            }

            if (_lastPointsStatus != (_pointsStatus ?? string.Empty))
            {
                _lastPointsStatus = _pointsStatus ?? string.Empty;
                RebuildPointsList();
            }
            else if (!ReferenceEquals(_lastPointsRef, _dimensionEntries))
            {
                _lastPointsRef = _dimensionEntries;
                RebuildPointsList();
            }

            // Mechanics tab (carga automática; solo lista de tarjetas).
            var mechBusy = _mechanicsBusy || busyAny;
            if (!ReferenceEquals(_lastMechRef, _mechanicRows))
            {
                _lastMechRef = _mechanicRows;
                RebuildMechanicsList();
            }

            if (_lastRedeemingId != _redeemingMechanicVideogameId || _lastMechBusy != mechBusy)
            {
                _lastRedeemingId = _redeemingMechanicVideogameId;
                _lastMechBusy = mechBusy;
                UpdateRedeemButtons(mechBusy);
            }
        }

        private static void SetEnabled(Selectable s, bool enabled)
        {
            if (s != null && s.interactable != enabled)
            {
                s.interactable = enabled;
            }
        }

        private static void SetButtonText(Button btn, string text)
        {
            var label = PdaUi.ButtonLabel(btn);
            if (label != null && label.text != text)
            {
                label.text = text;
            }
        }

        private void RebuildPointsList()
        {
            ClearChildren(_pointsListContent);
            if (_dimensionEntries == null || _dimensionEntries.Length == 0)
            {
                if (!string.IsNullOrEmpty(_pointsStatus))
                {
                    var msg = PdaUi.CreateLabel(
                        "PointsMessage",
                        _pointsListContent,
                        _pointsStatus,
                        14,
                        PdaTheme.TextMuted,
                        TextAlignmentOptions.Center,
                        true);
                    PdaUi.SetPreferredHeight(msg.gameObject, 48f);
                }

                return;
            }

            _lastPointsRef = _dimensionEntries;

            var maxVal = 0;
            foreach (var e in _dimensionEntries)
            {
                maxVal = Mathf.Max(maxVal, e.Balance);
            }

            var scale = Mathf.Max(maxVal, 10);

            foreach (var entry in _dimensionEntries)
            {
                var row = PdaUi.CreatePanel("Row", _pointsListContent, PdaTheme.PanelRaised);
                PdaUi.SetPreferredHeight(row.gameObject, 46f);

                var name = PdaUi.CreateLabel("Name", row.rectTransform, entry.Name ?? $"#{entry.IdDimension}", 15, PdaTheme.TextPrimary, TextAlignmentOptions.Left, false);
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(0.34f, 1f);
                name.rectTransform.offsetMin = new Vector2(12f, 0f);
                name.rectTransform.offsetMax = new Vector2(0f, 0f);

                var bar = PdaUi.CreateRect("BarHolder", row.rectTransform);
                bar.anchorMin = new Vector2(0.34f, 0.28f);
                bar.anchorMax = new Vector2(0.84f, 0.72f);
                bar.offsetMin = Vector2.zero;
                bar.offsetMax = Vector2.zero;
                PdaUi.CreateBar("Bar", bar, (float)entry.Balance / scale, PdaTheme.Accent);

                var val = PdaUi.CreateLabel("Val", row.rectTransform, entry.Balance.ToString(), 15, PdaTheme.AccentOrange, TextAlignmentOptions.Right, false);
                val.rectTransform.anchorMin = new Vector2(0.84f, 0f);
                val.rectTransform.anchorMax = new Vector2(1f, 1f);
                val.rectTransform.offsetMin = new Vector2(0f, 0f);
                val.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }
        }

        private void RebuildMechanicsList()
        {
            ClearChildren(_mechListContent);
            _mechRowRefs.Clear();
            _lastRedeemingId = -1;
            if (_mechanicRows == null || _mechanicRows.Length == 0)
            {
                return;
            }

            foreach (var row in _mechanicRows)
            {
                var card = PdaUi.CreatePanel("MechCard", _mechListContent, PdaTheme.PanelRaised);
                PdaUi.SetPreferredHeight(card.gameObject, 104f);

                var name = PdaUi.CreateLabel("Name", card.rectTransform, row.modifiable_mechanic_name ?? "—", 16, PdaTheme.Accent, TextAlignmentOptions.TopLeft, true);
                name.rectTransform.anchorMin = new Vector2(0f, 1f);
                name.rectTransform.anchorMax = new Vector2(0.72f, 1f);
                name.rectTransform.pivot = new Vector2(0.5f, 1f);
                name.rectTransform.sizeDelta = new Vector2(0f, 24f);
                name.rectTransform.offsetMin = new Vector2(12f, name.rectTransform.offsetMin.y);
                name.rectTransform.anchoredPosition = new Vector2(name.rectTransform.anchoredPosition.x, -8f);

                var desc = PdaUi.CreateLabel("Desc", card.rectTransform, row.modifiable_mechanic_description ?? string.Empty, 13, PdaTheme.TextMuted, TextAlignmentOptions.TopLeft, true);
                desc.rectTransform.anchorMin = new Vector2(0f, 0f);
                desc.rectTransform.anchorMax = new Vector2(0.72f, 1f);
                desc.rectTransform.offsetMin = new Vector2(12f, 8f);
                desc.rectTransform.offsetMax = new Vector2(0f, -34f);

                var hasRecipe = RedeemCatalog.TryGet(row.id_modifiable_mechanic_videogame, out var recipe);
                var costText = hasRecipe
                    ? $"Costo: {recipe.DescribeCosts(_dimensionNameById)}"
                    : (row.id_modifiable_mechanic_videogame > 0 ? "Sin receta local (canje no disponible)" : string.Empty);
                var cost = PdaUi.CreateLabel("Cost", card.rectTransform, costText, 12, PdaTheme.AccentOrange, TextAlignmentOptions.BottomLeft, true);
                cost.rectTransform.anchorMin = new Vector2(0f, 0f);
                cost.rectTransform.anchorMax = new Vector2(0.72f, 0f);
                cost.rectTransform.pivot = new Vector2(0.5f, 0f);
                cost.rectTransform.sizeDelta = new Vector2(0f, 20f);
                cost.rectTransform.offsetMin = new Vector2(12f, 6f);

                var capturedRow = row;
                var btn = PdaUi.CreateButton("Redeem", card.rectTransform, "CANJEAR", () => StartCoroutine(RedeemMechanicRoutine(capturedRow)), 15);
                var brt = btn.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.74f, 0.18f);
                brt.anchorMax = new Vector2(1f, 0.82f);
                brt.offsetMin = new Vector2(0f, 0f);
                brt.offsetMax = new Vector2(-12f, 0f);

                _mechRowRefs.Add(new MechRowRefs
                {
                    MechanicVideogameId = row.id_modifiable_mechanic_videogame,
                    HasRecipe = hasRecipe,
                    RedeemButton = btn,
                    RedeemLabel = PdaUi.ButtonLabel(btn),
                });
            }

            UpdateRedeemButtons(_mechanicsBusy || _submitting || _sessionRequest != SessionRequestKind.None);
        }

        private void UpdateRedeemButtons(bool globallyBusy)
        {
            foreach (var r in _mechRowRefs)
            {
                var redeemingThis = _redeemingMechanicVideogameId == r.MechanicVideogameId && _redeemingMechanicVideogameId != 0;
                var canRedeem = r.HasRecipe && !globallyBusy && _redeemingMechanicVideogameId == 0;
                SetEnabled(r.RedeemButton, canRedeem);
                if (r.RedeemLabel != null)
                {
                    r.RedeemLabel.text = redeemingThis ? "Canjeando…" : (r.HasRecipe ? "CANJEAR" : "—");
                }
            }
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        // ===================== Lógica de red (igual que antes) =====================

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
            _mechanicsStatus = string.Empty;
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

            if (recipe.Costs == null || recipe.Costs.Count == 0)
            {
                _mechanicsStatus = "La receta no tiene costos definidos.";
                yield break;
            }

            var gameId = MyFirstSubnauticaModPlugin.LifeSyncApiTestVideogameId.Value;
            _redeemingMechanicVideogameId = row.id_modifiable_mechanic_videogame;

            // El endpoint cobra una dimensión por POST: si la mecánica tiene varios costos, se hace uno por uno.
            var costsCharged = 0;
            for (var i = 0; i < recipe.Costs.Count; i++)
            {
                var cost = recipe.Costs[i];
                var body = RedeemCatalog.BuildRedeemBodyJson(row.id_modifiable_mechanic_videogame, cost);
                _mechanicsStatus =
                    $"Canjeando «{row.modifiable_mechanic_name}» — paso {i + 1}/{recipe.Costs.Count}: {cost.Amount} pts dimensión: {RedeemRecipe.FormatDimensionLabel(cost.PointDimensionId, _dimensionNameById)}…";
                MyFirstSubnauticaModPlugin.Log.LogInfo($"[LifeSync][Redeem] POST redeem body={body}");

                var task = client.PostRedeemAsync(gameId, playerId, body);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (!task.Result.Success)
                {
                    _redeemingMechanicVideogameId = 0;
                    var partial = costsCharged > 0
                        ? $" ATENCIÓN: ya se descontaron {costsCharged} costo(s) previos."
                        : string.Empty;
                    _mechanicsStatus =
                        $"Canje rechazado en el paso {i + 1} (HTTP {(int)task.Result.StatusCode}). Revisa saldo.{partial}";
                    MyFirstSubnauticaModPlugin.Log.LogWarning(
                        $"[LifeSync][Redeem] FAIL paso {i + 1} HTTP {(int)task.Result.StatusCode}: {task.Result.ErrorMessage} | body={task.Result.ResponseBody}");
                    yield return StartCoroutine(FetchDimensionsAndBalanceRoutine());
                    yield break;
                }

                costsCharged++;
            }

            _redeemingMechanicVideogameId = 0;

            try
            {
                recipe.ApplyLocalEffect?.Invoke();
            }
            catch (System.Exception ex)
            {
                MyFirstSubnauticaModPlugin.Log.LogError($"[LifeSync][Redeem] Error aplicando efecto local: {ex}");
            }

            _mechanicsStatus = $"Canje OK: «{row.modifiable_mechanic_name}». {recipe.EffectSummary}";
            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][Redeem] OK ({row.modifiable_mechanic_name}); {recipe.Costs.Count} costo(s) descontado(s).");

            var totalCost = 0;
            foreach (var cost in recipe.Costs)
            {
                totalCost += cost.Amount;
            }

            GameSessionLogService.RecordRedemption(row.modifiable_mechanic_name, totalCost, recipe.Costs.Count);

            yield return StartCoroutine(FetchDimensionsAndBalanceRoutine());
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
                GameSessionLogService.StartSession();
            }
        }

        /// <summary>
        /// Carga el catálogo /attributes y el saldo /players/{id}/points/balance, los junta por
        /// <c>id_attributes == id_point_dimension</c> y rellena las dimensiones sin saldo con <c>0</c>.
        /// </summary>
        private IEnumerator FetchAttributeNamesRoutine()
        {
            if (_dimensionNameById != null && _dimensionNameById.Count > 0)
            {
                yield break;
            }

            var client = MyFirstSubnauticaModPlugin.ResolveApiClient();
            if (client == null)
            {
                yield break;
            }

            SyncBearerOnClient(client);
            if (string.IsNullOrWhiteSpace(MyFirstSubnauticaModPlugin.LifeSyncApiBearerToken.Value))
            {
                yield break;
            }

            var task = client.GetAttributesAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (!task.Result.Success ||
                !LifeSyncPointsJsonParsers.TryParseAttributesArray(task.Result.ResponseBody, out var attrs))
            {
                yield break;
            }

            UpdateDimensionNameCache(attrs);
        }

        private void UpdateDimensionNameCache(AttributeRow[] attrs)
        {
            if (attrs == null || attrs.Length == 0)
            {
                return;
            }

            if (_dimensionNameById == null)
            {
                _dimensionNameById = new Dictionary<int, string>(attrs.Length);
            }
            else
            {
                _dimensionNameById.Clear();
            }

            foreach (var a in attrs)
            {
                _dimensionNameById[a.id_attributes] = a.name;
            }

            // Si las mecánicas ya están en pantalla, refrescar etiquetas de costo con nombres.
            if (_mechanicRows != null && _mechanicRows.Length > 0)
            {
                _lastMechRef = null;
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
                _dimensionNameById = null;
                _pointsBusy = false;
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    "[LifeSync][API] No se pudo parsear /attributes. Respuesta: " +
                    TruncateForLog(attrTask.Result.ResponseBody));
                yield break;
            }

            UpdateDimensionNameCache(attrs);

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
                MyFirstSubnauticaModPlugin.Log.LogWarning(
                    "[LifeSync][API] No se pudo parsear /points/balance. Respuesta: " +
                    TruncateForLog(balTask.Result.ResponseBody));
                yield break;
            }

            _dimensionEntries = LifeSyncPointsJsonParsers.MergeAttributesWithBalances(attrs, balances);
            _pointsStatus = string.Empty;
            MyFirstSubnauticaModPlugin.Log.LogInfo(
                $"[LifeSync][API] Dimensiones merged OK (attrs={attrs.Length}, balances={balances.Length}, filas={_dimensionEntries.Length}).");
        }

        private static string TruncateForLog(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "(vacío)";
            }

            const int max = 400;
            return text.Length <= max ? text : text.Substring(0, max) + "…";
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
            _username = _usernameInput != null ? _usernameInput.text : _username;
            _password = _passwordInput != null ? _passwordInput.text : _password;

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
            if (_passwordInput != null)
            {
                _passwordInput.text = string.Empty;
            }

            _status = string.Empty;
            _sessionStatus = "Sesión iniciada. Pestañas Token / Puntos / Mecánicas.";
            _panel = MenuPanel.Session;
            ApplyPanelVisibility();
            MyFirstSubnauticaModPlugin.Log.LogInfo("[LifeSync][Auth] Login correcto; token guardado.");
            StartCoroutine(CachePlayerIdAfterAuthRoutine(forceRefresh: true));
        }
    }
}
