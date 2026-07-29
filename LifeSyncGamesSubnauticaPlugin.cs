using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LifeSyncGamesSubnautica.Input;
using LifeSyncGamesSubnautica.Services;
using UnityEngine;

namespace LifeSyncGamesSubnautica
{
    // TODO Review this file and update to your own requirements.

    [BepInPlugin(MyGUID, PluginName, VersionString)]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.HardDependency)]
    public class LifeSyncGamesSubnauticaPlugin : BaseUnityPlugin
    {
        // Mod specific details. MyGUID should be unique, and follow the reverse domain pattern
        // e.g.
        // com.mynameororg.pluginname
        // Version should be a valid version string.
        // e.g.
        // 1.0.0
        private const string MyGUID = "com.lifesyncgames.subnautica";
        private const string PluginName = "LifeSync-Games-Subnautica";
        private const string VersionString = "1.0.49";

        internal static string ModVersion => VersionString;

        /// <summary>Sube este número cuando quieras forzar una sola vez los defaults de LifeSync en cfg antiguos.</summary>
        private const int LifeSyncSettingsBundleRevision = 5;

        // Config entry key strings
        // These will appear in the config file created by BepInEx and can also be used
        // by the OnSettingsChange event to determine which setting has changed.
        public static string KnifeDamageMultiplierKey = "Knife Damage Modifier Key";

        // Configuration entries. Static, so can be accessed directly elsewhere in code via
        // e.g.
        // float myFloat = LifeSyncGamesSubnauticaPlugin.FloatExample.Value;
        // TODO Change this code or remove the code if not required.
        public static ConfigEntry<float> KnifeDamageMultiplier;

        /// <summary>Bonus aditivo al daño del cuchillo (se suma tras el multiplicador). Crece con cada canje exitoso.</summary>
        public static ConfigEntry<int> KnifeBonusDamage;

        /// <summary>Bonus aditivo a la vida máxima del jugador (se aplica sobre <c>liveMixin.data.maxHealth</c>).</summary>
        public static ConfigEntry<int> PlayerMaxHealthBonus;

        /// <summary>Bonus aditivo al oxígeno máximo del jugador (se aplica sobre <c>Oxygen.oxygenCapacity</c>).</summary>
        public static ConfigEntry<int> PlayerMaxOxygenBonus;

        /// <summary>Porcentaje extra de capacidad de batería de la linterna (5 = +5% sobre la capacidad base).</summary>
        public static ConfigEntry<int> FlashlightCapacityBonusPercent;

        /// <summary>Reducción acumulada del consumo de la linterna (energía/seg restada al consumo base de 1.0).</summary>
        public static ConfigEntry<float> FlashlightDrainReduction;

        /// <summary>Porcentaje extra de capacidad de batería del deslizador (5 = +5% sobre la capacidad base).</summary>
        public static ConfigEntry<int> SeaglideCapacityBonusPercent;

        /// <summary>Velocidad extra acumulada del deslizador (sumada a la velocidad base de 25, tope 48).</summary>
        public static ConfigEntry<float> SeaglideSpeedBonus;

        /// <summary>Activa penalización por juego prolongado (−5 vida/oxígeno máx. tras 1 h, luego cada 30 min).</summary>
        public static ConfigEntry<bool> ContinuousPlayPenaltyEnabled;

        /// <summary>Penalización permanente acumulada a la vida máxima (restada junto al bonus de canjes).</summary>
        public static ConfigEntry<int> PlayerMaxHealthPenalty;

        /// <summary>Penalización permanente acumulada al oxígeno máximo.</summary>
        public static ConfigEntry<int> PlayerMaxOxygenPenalty;

        /// <summary>
        /// Contadores de canjes por mecánica (id=count;…) para escalar el coste +5 por canje previo.
        /// </summary>
        public static ConfigEntry<string> RedeemCostEscalationCounts;

        /// <summary>Instancia del plugin (Awake); para guardar cfg tras login.</summary>
        internal static LifeSyncGamesSubnauticaPlugin Instance { get; private set; }

        public static ConfigEntry<string> LifeSyncApiBaseUrl;
        public static ConfigEntry<string> LifeSyncAuthBaseUrl;
        public static ConfigEntry<KeyCode> LifeSyncLoginMenuKey;
        public static ConfigEntry<int> LifeSyncApiTimeoutSeconds;
        /// <summary>videogame_id en Core API (redeem, mechanics, logger).</summary>
        public static ConfigEntry<int> LifeSyncApiTestVideogameId;
        public static ConfigEntry<string> LifeSyncApiBearerToken;
        /// <summary>Cache de <c>id_players</c> desde GET lsg-auth/whoami (tras login); usado para puntos por atributo.</summary>
        public static ConfigEntry<int> LifeSyncCachedPlayerId;
        private static ConfigEntry<int> LifeSyncAppliedSettingsRevision;

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log = new ManualLogSource(PluginName);
        internal static LifeSyncApiClient ApiClient { get; private set; }

        /// <summary>
        /// Obtiene un <see cref="LifeSyncApiClient"/> usable: estático, búsqueda en escena, o recreación si el host DDOL se destruyó.
        /// </summary>
        internal static LifeSyncApiClient ResolveApiClient()
        {
            if (ApiClient != null)
            {
                return ApiClient;
            }

            var found = UnityEngine.Object.FindObjectOfType<LifeSyncApiClient>();
            if (found != null)
            {
                ApiClient = found;
                ConfigureLifeSyncApiClient(found);
                return found;
            }

            return RecreateLifeSyncApiClientHost();
        }

        /// <summary>
        /// Asegura raíz de escena antes de DDOL (evita el warning de Unity y la destrucción del cliente al cargar menú/partida).
        /// </summary>
        private static GameObject CreatePersistentServiceHost()
        {
            var host = new GameObject($"{PluginName}.Services");
            host.transform.SetParent(null);
            UnityEngine.Object.DontDestroyOnLoad(host);
            return host;
        }

        private static void ConfigureLifeSyncApiClient(LifeSyncApiClient client)
        {
            if (client == null)
            {
                return;
            }

            client.Initialize(LifeSyncApiBaseUrl.Value, LifeSyncApiTimeoutSeconds.Value);
            client.SetAuthBaseUrl(LifeSyncAuthBaseUrl.Value);
            client.SetPutSingleAttributePath("player_attributes_single");
            client.SetBearerToken(LifeSyncApiBearerToken.Value);
            var logSource = Instance != null ? Instance.Logger : Log;
            client.SetLogger(logSource);
        }

        /// <summary>
        /// Si el GameObject DDOL no sobrevivió al cambio de escena, recrea cliente y cfg en memoria.
        /// </summary>
        private static LifeSyncApiClient RecreateLifeSyncApiClientHost()
        {
            var host = CreatePersistentServiceHost();
            var client = host.AddComponent<LifeSyncApiClient>();
            ApiClient = client;
            ConfigureLifeSyncApiClient(client);
            GameSessionLogService.EnsureOnHost(host);
            ContinuousPlayPenaltyService.EnsureOnHost(host);

            Log.LogWarning(
                "[LifeSync][API] Se recreó LifeSyncApiClient (el anterior no existía o fue destruido al cambiar de escena). " +
                "Si persiste, elimina la carpeta duplicada BepInEx\\plugins\\LifeSync-Games-Subnautica.");
            return client;
        }

        /// <summary>
        /// Initialise the configuration settings and patch methods
        /// </summary>
        private void Awake()
        {
            Instance = this;

            // Persistencia interna (oculta en Configuration Manager). Visible solo: Login Menu Key.
            KnifeDamageMultiplier = Config.Bind(
                "General",
                KnifeDamageMultiplierKey,
                5.0f,
                Hidden(
                    "Multiplicador de daño del cuchillo.",
                    new AcceptableValueRange<float>(1.0f, 10.0f)));

            KnifeBonusDamage = Config.Bind(
                "General",
                "Knife Bonus Damage",
                0,
                Hidden("Daño aditivo del cuchillo tras canjes LifeSync."));

            PlayerMaxHealthBonus = Config.Bind(
                "General",
                "Player Max Health Bonus",
                0,
                Hidden("Bonus de vida máxima por canjes LifeSync."));

            PlayerMaxOxygenBonus = Config.Bind(
                "General",
                "Player Max Oxygen Bonus",
                0,
                Hidden("Bonus de oxígeno máximo por canjes LifeSync."));

            FlashlightCapacityBonusPercent = Config.Bind(
                "General",
                "Flashlight Capacity Bonus Percent",
                0,
                Hidden("Bonus % capacidad de batería de la linterna."));

            FlashlightDrainReduction = Config.Bind(
                "General",
                "Flashlight Drain Reduction",
                0f,
                Hidden("Reducción de consumo de la linterna."));

            SeaglideCapacityBonusPercent = Config.Bind(
                "General",
                "Seaglide Capacity Bonus Percent",
                0,
                Hidden("Bonus % capacidad de batería del deslizador."));

            SeaglideSpeedBonus = Config.Bind(
                "General",
                "Seaglide Speed Bonus",
                0f,
                Hidden("Bonus de velocidad del deslizador."));

            ContinuousPlayPenaltyEnabled = Config.Bind(
                "LifeSync Fatigue",
                "Continuous Play Penalty Enabled",
                false,
                Hidden("Penalización por juego prolongado (también en menú LifeSync → Token)."));

            PlayerMaxHealthPenalty = Config.Bind(
                "LifeSync Fatigue",
                "Player Max Health Penalty",
                0,
                Hidden("Penalización acumulada a la vida máxima."));

            PlayerMaxOxygenPenalty = Config.Bind(
                "LifeSync Fatigue",
                "Player Max Oxygen Penalty",
                0,
                Hidden("Penalización acumulada al oxígeno máximo."));

            RedeemCostEscalationCounts = Config.Bind(
                "LifeSync Redeem",
                "Cost Escalation Counts",
                string.Empty,
                Hidden("Contadores de canjes para escalado de coste (+5)."));

            LifeSyncApiBaseUrl = Config.Bind(
                "LifeSync API",
                "Base URL",
                "https://lsg.diinf.usach.cl/lsg-core-api/",
                Hidden("Raíz de la Core API."));

            LifeSyncAuthBaseUrl = Config.Bind(
                "LifeSync API",
                "Auth Base URL",
                "https://lsg.diinf.usach.cl/lsg-auth/",
                Hidden("Raíz de lsg-auth."));

            LifeSyncLoginMenuKey = Config.Bind(
                "LifeSync",
                "Login Menu Key",
                KeyCode.F10,
                "Tecla del menú LifeSync en partida. También reasignable en Opciones → Controles → LifeSync Games.");

            LifeSyncApiTimeoutSeconds = Config.Bind(
                "LifeSync API",
                "Timeout Seconds",
                15,
                Hidden(
                    "Tiempo máximo de espera para peticiones HTTPS.",
                    new AcceptableValueRange<int>(5, 120)));

            LifeSyncApiTestVideogameId = Config.Bind(
                "LifeSync API",
                "Test Videogame Id",
                19,
                Hidden("videogame_id en Core API (mecánicas, canjes y logger)."));

            LifeSyncApiBearerToken = Config.Bind(
                "LifeSync API",
                "Bearer Token (optional)",
                string.Empty,
                Hidden("Token de sesión (se rellena al iniciar sesión con F10)."));

            LifeSyncCachedPlayerId = Config.Bind(
                "LifeSync API",
                "Cached Player Id (whoami)",
                0,
                Hidden("id_players cacheado tras login."));

            LifeSyncAppliedSettingsRevision = Config.Bind(
                "LifeSync API",
                "Applied Settings Bundle Revision",
                0,
                Hidden("Revisión interna de migración de defaults."));

            ApplyLifeSyncSettingsMigrationIfNeeded();

            // Apply all of our patches
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loading...");
            Harmony.PatchAll();
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loaded.");

            var serviceHost = CreatePersistentServiceHost();
            ApiClient = serviceHost.AddComponent<LifeSyncApiClient>();
            ConfigureLifeSyncApiClient(ApiClient);
            GameSessionLogService.EnsureOnHost(serviceHost);
            ContinuousPlayPenaltyService.EnsureOnHost(serviceHost);

            LifeSyncInputRegistration.EnsureRegistered(Logger, LifeSyncLoginMenuKey.Value);

            Log = Logger;
        }

        private static ConfigDescription Hidden(string description, AcceptableValueBase acceptableValues = null)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new ConfigurationManagerAttributes { Browsable = false });
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ApplyLifeSyncSettingsMigrationIfNeeded()
        {
            if (LifeSyncAppliedSettingsRevision.Value >= LifeSyncSettingsBundleRevision)
            {
                return;
            }

            LifeSyncApiBaseUrl.Value = "https://lsg.diinf.usach.cl/lsg-core-api/";
            LifeSyncAuthBaseUrl.Value = "https://lsg.diinf.usach.cl/lsg-auth/";
            LifeSyncApiTestVideogameId.Value = 19;
            LifeSyncLoginMenuKey.Value = KeyCode.F10;
            LifeSyncAppliedSettingsRevision.Value = LifeSyncSettingsBundleRevision;
            Config.Save();
            Logger.LogInfo(
                $"[LifeSync][API] Se aplicaron defaults de LifeSync (bundle revision {LifeSyncSettingsBundleRevision}). " +
                "Puedes cambiarlos en el .cfg; para volver a migrar sube LifeSyncSettingsBundleRevision en el código del mod.");
        }
    }
}
