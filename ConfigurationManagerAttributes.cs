/// <summary>
/// Atributos que Configuration Manager detecta por reflexión para ocultar o marcar opciones avanzadas.
/// </summary>
public sealed class ConfigurationManagerAttributes
{
    /// <summary>Si es false, la opción no aparece en la ventana de Configuration Manager.</summary>
    public bool? Browsable;

    /// <summary>Si es true, solo se muestra al activar «Advanced» en Configuration Manager.</summary>
    public bool? IsAdvanced;
}
