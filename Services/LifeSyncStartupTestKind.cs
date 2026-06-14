namespace MyFirstSubnauticaMod.Services
{
    /// <summary>
    /// Tipo de prueba al arrancar (ver CSV prod LSG-CORE-API).
    /// </summary>
    public enum LifeSyncStartupTestKind
    {
        /// <summary>GET /health — no requiere Bearer en los ejemplos del CSV.</summary>
        Health = 0,

        /// <summary>POST /players/{{id}}/points/adjust — requiere Bearer.</summary>
        CorePointsAdjust = 1,

        /// <summary>PUT legacy post-routes (Valheim).</summary>
        LegacyPostRoutesPut = 2
    }
}
