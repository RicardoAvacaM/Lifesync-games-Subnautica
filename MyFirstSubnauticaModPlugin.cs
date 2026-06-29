using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MyFirstSubnauticaMod.Input;
using MyFirstSubnauticaMod.Services;
using UnityEngine;

namespace MyFirstSubnauticaMod
{
    // TODO Review this file and update to your own requirements.

    [BepInPlugin(MyGUID, PluginName, VersionString)]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.HardDependency)]
    public class MyFirstSubnauticaModPlugin : BaseUnityPlugin
    {
        // Mod specific details. MyGUID should be unique, and follow the reverse domain pattern
        // e.g.
        // com.mynameororg.pluginname
        // Version should be a valid version string.
        // e.g.
        // 1.0.0
        private const string MyGUID = "com.Ricardo.MyFirstSubnauticaMod";
        private const string PluginName = "MyFirstSubnauticaMod";
        private const string VersionString = "1.0.37";

        internal static string ModVersion => VersionString;

        /// <summary>Sube este número cuando quieras forzar una sola vez los defaults de LifeSync en cfg antiguos.</summary>
        private const int LifeSyncSettingsBundleRevision = 3;

        // Config entry key strings
        // These will appear in the config file created by BepInEx and can also be used
        // by the OnSettingsChange event to determine which setting has changed.
        public static string KnifeDamageMultiplierKey = "Knife Damage Modifier Key";

        // Configuration entries. Static, so can be accessed directly elsewhere in code via
        // e.g.
        // float myFloat = MyFirstSubnauticaModPlugin.FloatExample.Value;
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

        /// <summary>Instancia del plugin (Awake); para guardar cfg tras login.</summary>
        internal static MyFirstSubnauticaModPlugin Instance { get; private set; }

        public static ConfigEntry<string> LifeSyncApiBaseUrl;
        public static ConfigEntry<string> LifeSyncAuthBaseUrl;
        public static ConfigEntry<KeyCode> LifeSyncLoginMenuKey;
        public static ConfigEntry<int> LifeSyncApiTimeoutSeconds;
        public static ConfigEntry<bool> LifeSyncApiTestOnStartup;
        /// <summary>Valores: Health, CorePointsAdjust, LegacyPostRoutesPut (ver <see cref="LifeSyncStartupTestKind"/>).</summary>
        public static ConfigEntry<string> LifeSyncApiStartupTestMode;
        public static ConfigEntry<int> LifeSyncApiTestPlayerId;
        public static ConfigEntry<int> LifeSyncApiTestAttributeId;
        public static ConfigEntry<int> LifeSyncApiTestNewData;
        public static ConfigEntry<int> LifeSyncApiTestVideogameId;
        public static ConfigEntry<int> LifeSyncApiTestPointDimensionId;
        public static ConfigEntry<string> LifeSyncApiPointsAdjustDirection;
        public static ConfigEntry<string> LifeSyncApiPointsAdjustReason;
        public static ConfigEntry<string> LifeSyncApiBearerToken;
        /// <summary>Cache de <c>id_players</c> desde GET lsg-auth/whoami (tras login); usado para puntos por atributo.</summary>
        public static ConfigEntry<int> LifeSyncCachedPlayerId;
        public static ConfigEntry<string> LifeSyncApiPutSinglePath;
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
            client.SetPutSingleAttributePath(LifeSyncApiPutSinglePath.Value);
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

            Log.LogWarning(
                "[LifeSync][API] Se recreó LifeSyncApiClient (el anterior no existía o fue destruido al cambiar de escena). " +
                "Si persiste, elimina la carpeta duplicada BepInEx\\pluginsMyFirstSubnauticaMod.");
            return client;
        }

        /// <summary>
        /// Initialise the configuration settings and patch methods
        /// </summary>
        private void Awake()
        {
            Instance = this;

            // Float configuration setting example
            // TODO Change this code or remove the code if not required.
            KnifeDamageMultiplier = Config.Bind("General",    // The section under which the option is shown
                KnifeDamageMultiplierKey,                            // The key of the configuration option
                5.0f,                            // The default value
                new ConfigDescription("Knife Damage Multiplier",         // Description that appears in Configuration Manager
                    new AcceptableValueRange<float>(1.0f, 10.0f)));     // Acceptable range, enabled slider and validation in Configuration Manager

            KnifeBonusDamage = Config.Bind(
                "General",
                "Knife Bonus Damage",
                0,
                "Daño aditivo (entero) que se suma al cuchillo después del multiplicador. " +
                "Aumenta +1 por cada canje exitoso de la mecánica KnifeDamageS desde el menú LifeSync.");

            PlayerMaxHealthBonus = Config.Bind(
                "General",
                "Player Max Health Bonus",
                0,
                "Puntos extra de vida máxima del jugador (sumados al maxHealth original del LiveMixin). " +
                "Aumenta +5 por cada canje exitoso de la mecánica PlayerMaxHealth desde el menú LifeSync.");

            PlayerMaxOxygenBonus = Config.Bind(
                "General",
                "Player Max Oxygen Bonus",
                0,
                "Puntos extra de oxígeno máximo del jugador (sumados a la oxygenCapacity original). " +
                "Aumenta +5 por cada canje exitoso de la mecánica PlayerMaxOxygen desde el menú LifeSync.");

            FlashlightCapacityBonusPercent = Config.Bind(
                "General",
                "Flashlight Capacity Bonus Percent",
                0,
                "Porcentaje extra de capacidad de batería de la linterna sobre la base (100). " +
                "Aumenta +5 por cada canje de la mecánica FlashlightCapacity (100→105→110…).");

            FlashlightDrainReduction = Config.Bind(
                "General",
                "Flashlight Drain Reduction",
                0f,
                "Energía/seg restada al consumo base de la linterna (1.0). " +
                "Aumenta +0.05 por cada canje de FlashlightDrain; el consumo final nunca baja de 0.2.");

            SeaglideCapacityBonusPercent = Config.Bind(
                "General",
                "Seaglide Capacity Bonus Percent",
                0,
                "Porcentaje extra de capacidad de batería del deslizador sobre la base (100). " +
                "Aumenta +5 por cada canje de SeaglideCapacity (100→105→110…).");

            SeaglideSpeedBonus = Config.Bind(
                "General",
                "Seaglide Speed Bonus",
                0f,
                "Velocidad extra sumada a la base del deslizador (25). " +
                "Aumenta +4 por cada canje de SeaglideSpeed; la velocidad final nunca supera 48.");

            LifeSyncApiBaseUrl = Config.Bind(
                "LifeSync API",
                "Base URL",
                "https://lsg.diinf.usach.cl/lsg-core-api/",
                "Raíz de la Core API (CSV prod). Swagger: https://lsg.diinf.usach.cl/lsg-core-api/docs#/");

            LifeSyncAuthBaseUrl = Config.Bind(
                "LifeSync API",
                "Auth Base URL",
                "https://lsg.diinf.usach.cl/lsg-auth/",
                "Raíz de lsg-auth (POST /login form). Swagger: https://lsg.diinf.usach.cl/lsg-auth/docs");

            LifeSyncLoginMenuKey = Config.Bind(
                "LifeSync API",
                "Login Menu Key",
                KeyCode.F8,
                "Tecla del menú LifeSync en partida: con token guardado abre solo sesión (duración / renovar). " +
                "Sin token, el formulario de inicio de sesión (solo primera configuración o tras «Cerrar sesión»). " +
                "Opciones → Controles → LifeSync Games. Reinicia si cambias el KeyCode.");

            LifeSyncApiPutSinglePath = Config.Bind(
                "LifeSync API",
                "Put Single Attribute Path",
                "player_attributes_single",
                "Solo LegacyPostRoutesPut: ruta relativa al Base URL (post-routes). Core API usa otros endpoints.");

            LifeSyncApiTimeoutSeconds = Config.Bind(
                "LifeSync API",
                "Timeout Seconds",
                15,
                new ConfigDescription(
                    "Tiempo máximo de espera para peticiones HTTPS.",
                    new AcceptableValueRange<int>(5, 120)));

            LifeSyncApiStartupTestMode = Config.Bind(
                "LifeSync API",
                "Startup Test Mode",
                nameof(LifeSyncStartupTestKind.Health),
                new ConfigDescription(
                    "Health = GET /health. CorePointsAdjust = POST players/{id}/points/adjust (Bearer). " +
                    "LegacyPostRoutesPut = PUT player_attributes_single (Base URL debe ser post-routes).",
                    new AcceptableValueList<string>(
                        nameof(LifeSyncStartupTestKind.Health),
                        nameof(LifeSyncStartupTestKind.CorePointsAdjust),
                        nameof(LifeSyncStartupTestKind.LegacyPostRoutesPut))));

            LifeSyncApiTestOnStartup = Config.Bind(
                "LifeSync API",
                "Test Connection On Startup",
                false,
                "Si está activo, ejecuta la prueba según Startup Test Mode.");

            LifeSyncApiTestPlayerId = Config.Bind(
                "LifeSync API",
                "Test Player Id",
                54,
                "id_players para pruebas (points/adjust, attributes/init, legacy PUT).");

            LifeSyncApiTestAttributeId = Config.Bind(
                "LifeSync API",
                "Test Attribute Id",
                1,
                "Solo LegacyPostRoutesPut: id_attributes / new_data.");

            LifeSyncApiTestNewData = Config.Bind(
                "LifeSync API",
                "Test New Data",
                0,
                "LegacyPut: valor new_data. CorePointsAdjust: se usa como amount si quieres un número distinto de 0.");

            LifeSyncApiTestVideogameId = Config.Bind(
                "LifeSync API",
                "Test Videogame Id",
                19,
                "CorePointsAdjust: videogame_id (CSV prod: 19 = Subnautica: Below Zero; ajusta al id registrado en Core).");

            LifeSyncApiTestPointDimensionId = Config.Bind(
                "LifeSync API",
                "Test Point Dimension Id",
                1,
                "CorePointsAdjust: point_dimension_id (dimensión de puntos en Core API).");

            LifeSyncApiPointsAdjustDirection = Config.Bind(
                "LifeSync API",
                "Points Adjust Direction",
                "CREDIT",
                "CorePointsAdjust: CREDIT o DEBIT según contrato del CSV/Swagger.");

            LifeSyncApiPointsAdjustReason = Config.Bind(
                "LifeSync API",
                "Points Adjust Reason",
                "Subnautica mod connectivity test",
                "CorePointsAdjust: campo reason del JSON.");

            LifeSyncApiBearerToken = Config.Bind(
                "LifeSync API",
                "Bearer Token (optional)",
                string.Empty,
                "Se rellena solo al usar la ventana de login (F8) o pega un access_token manual. Si está vacío, no se envía Bearer. " +
                "No subas este .cfg a internet; rota el token si se filtró.");

            LifeSyncCachedPlayerId = Config.Bind(
                "LifeSync API",
                "Cached Player Id (whoami)",
                0,
                "Se rellena al iniciar sesión (GET lsg-auth/whoami). Sirve para GET .../players/{id}/attributes/points. " +
                "0 = sin cache (se consultará whoami al cargar puntos). «Cerrar sesión» lo borra.");

            LifeSyncAppliedSettingsRevision = Config.Bind(
                "LifeSync API",
                "Applied Settings Bundle Revision",
                0,
                "Interno del mod: no editar manualmente. Al subir de versión se aplican defaults de prueba una sola vez.");

            ApplyLifeSyncSettingsMigrationIfNeeded();

            // Apply all of our patches
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loading...");
            Harmony.PatchAll();
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loaded.");

            var serviceHost = CreatePersistentServiceHost();
            ApiClient = serviceHost.AddComponent<LifeSyncApiClient>();
            ConfigureLifeSyncApiClient(ApiClient);
            GameSessionLogService.EnsureOnHost(serviceHost);

            LifeSyncInputRegistration.EnsureRegistered(Logger, LifeSyncLoginMenuKey.Value);

            if (LifeSyncApiTestOnStartup.Value)
            {
                Logger.LogInfo(
                    $"[LifeSync][API] Startup test: mode={LifeSyncApiStartupTestMode.Value} BaseUrl={LifeSyncApiBaseUrl.Value} player={LifeSyncApiTestPlayerId.Value}");
                RunStartupConnectivityTest();
            }

            // Sets up our static Log, so it can be used elsewhere in code.
            // .e.g.
            // MyFirstSubnauticaModPlugin.Log.LogDebug("Debug Message to BepInEx log file");
            Log = Logger;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static LifeSyncStartupTestKind ParseStartupTestMode(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (s == nameof(LifeSyncStartupTestKind.CorePointsAdjust))
            {
                return LifeSyncStartupTestKind.CorePointsAdjust;
            }

            if (s == nameof(LifeSyncStartupTestKind.LegacyPostRoutesPut))
            {
                return LifeSyncStartupTestKind.LegacyPostRoutesPut;
            }

            return LifeSyncStartupTestKind.Health;
        }

        private void RunStartupConnectivityTest()
        {
            switch (ParseStartupTestMode(LifeSyncApiStartupTestMode.Value))
            {
                case LifeSyncStartupTestKind.Health:
                    _ = ApiClient.LogHealthStartupTestAsync();
                    break;
                case LifeSyncStartupTestKind.CorePointsAdjust:
                {
                    var amount = LifeSyncApiTestNewData.Value;
                    if (amount == 0)
                    {
                        amount = 1;
                    }

                    _ = ApiClient.LogPointsAdjustStartupTestAsync(
                        LifeSyncApiTestPlayerId.Value,
                        LifeSyncApiTestPointDimensionId.Value,
                        LifeSyncApiPointsAdjustDirection.Value,
                        amount,
                        LifeSyncApiPointsAdjustReason.Value,
                        LifeSyncApiTestVideogameId.Value);
                    break;
                }
                default:
                    Logger.LogInfo(
                        $"[LifeSync][API] Legacy PUT: attribute={LifeSyncApiTestAttributeId.Value} new_data={LifeSyncApiTestNewData.Value}");
                    _ = ApiClient.LogStartupConnectivityTestAsync(
                        LifeSyncApiTestPlayerId.Value,
                        LifeSyncApiTestAttributeId.Value,
                        LifeSyncApiTestNewData.Value);
                    break;
            }
        }

        private void ApplyLifeSyncSettingsMigrationIfNeeded()
        {
            if (LifeSyncAppliedSettingsRevision.Value >= LifeSyncSettingsBundleRevision)
            {
                return;
            }

            LifeSyncApiBaseUrl.Value = "https://lsg.diinf.usach.cl/lsg-core-api/";
            LifeSyncApiPutSinglePath.Value = "player_attributes_single";
            LifeSyncApiStartupTestMode.Value = nameof(LifeSyncStartupTestKind.Health);
            LifeSyncApiTestPlayerId.Value = 54;
            LifeSyncApiTestAttributeId.Value = 1;
            LifeSyncApiTestNewData.Value = 1;
            LifeSyncApiTestVideogameId.Value = 19;
            LifeSyncApiTestPointDimensionId.Value = 1;
            LifeSyncAppliedSettingsRevision.Value = LifeSyncSettingsBundleRevision;
            Config.Save();
            Logger.LogInfo(
                $"[LifeSync][API] Se aplicaron defaults de LifeSync (bundle revision {LifeSyncSettingsBundleRevision}). " +
                "Puedes cambiarlos en el .cfg; para volver a migrar sube LifeSyncSettingsBundleRevision en el código del mod.");
        }
    }
}
