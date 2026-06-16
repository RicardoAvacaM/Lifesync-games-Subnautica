using System.Linq;
using TMPro;
using UnityEngine;

namespace MyFirstSubnauticaMod.UI
{
    /// <summary>
    /// Paleta, sprites procedurales y fuente para imitar el estilo de la PDA de Subnautica.
    /// Todo se genera por código (sin archivos de arte): texturas redondeadas 9-slice y colores cian/naranja.
    /// </summary>
    internal static class PdaTheme
    {
        // Paleta tipo PDA (cian translúcido sobre fondo oscuro, acento naranja).
        internal static readonly Color Background = new Color(0.027f, 0.078f, 0.106f, 0.96f);   // azul muy oscuro
        internal static readonly Color Panel = new Color(0.043f, 0.125f, 0.161f, 0.92f);        // panel translúcido
        internal static readonly Color PanelRaised = new Color(0.063f, 0.176f, 0.216f, 0.95f);
        internal static readonly Color Accent = new Color(0.30f, 0.85f, 0.93f, 1f);             // cian PDA
        internal static readonly Color AccentDim = new Color(0.30f, 0.85f, 0.93f, 0.35f);
        internal static readonly Color AccentOrange = new Color(1f, 0.55f, 0.16f, 1f);          // naranja acento
        internal static readonly Color ButtonNormal = new Color(0.07f, 0.20f, 0.25f, 0.95f);
        internal static readonly Color ButtonHover = new Color(0.12f, 0.34f, 0.40f, 1f);
        internal static readonly Color ButtonActive = new Color(0.20f, 0.52f, 0.58f, 1f);
        internal static readonly Color ButtonDisabled = new Color(0.10f, 0.16f, 0.18f, 0.6f);
        internal static readonly Color TextPrimary = new Color(0.85f, 0.97f, 1f, 1f);
        internal static readonly Color TextMuted = new Color(0.55f, 0.78f, 0.84f, 1f);
        internal static readonly Color BarTrack = new Color(0.10f, 0.22f, 0.26f, 1f);

        private static Sprite _panelSprite;
        private static Sprite _flatSprite;
        private static Sprite _softSprite;
        private static TMP_FontAsset _font;
        private static bool _fontResolved;

        /// <summary>Sprite redondeado con borde (9-slice) para paneles.</summary>
        internal static Sprite PanelSprite =>
            _panelSprite ?? (_panelSprite = MakeRoundedSprite(40, 12, 2, Color.white, new Color(1f, 1f, 1f, 0.9f)));

        /// <summary>Sprite redondeado suave (sin borde) para botones / barras.</summary>
        internal static Sprite SoftSprite =>
            _softSprite ?? (_softSprite = MakeRoundedSprite(32, 8, 0, Color.white, default));

        /// <summary>Sprite plano (1x1) para rellenos lisos.</summary>
        internal static Sprite FlatSprite
        {
            get
            {
                if (_flatSprite == null)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    var px = Enumerable.Repeat(Color.white, 4).ToArray();
                    tex.SetPixels(px);
                    tex.Apply();
                    tex.wrapMode = TextureWrapMode.Clamp;
                    _flatSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                }

                return _flatSprite;
            }
        }

        /// <summary>
        /// Devuelve un <see cref="TMP_FontAsset"/> para los textos. Prefiere la tipografía de Subnautica
        /// (Agency / Oxanium) si está cargada; si no, usa la default de TMP o la primera disponible.
        /// </summary>
        internal static TMP_FontAsset Font
        {
            get
            {
                if (_fontResolved)
                {
                    return _font;
                }

                _fontResolved = true;
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

                // Preferencias por nombre (estilo PDA).
                string[] preferred = { "Agency", "Oxanium", "aller", "ApexNew", "expressway" };
                foreach (var pref in preferred)
                {
                    var hit = all.FirstOrDefault(f => f != null && f.name.IndexOf(pref, System.StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hit != null)
                    {
                        _font = hit;
                        break;
                    }
                }

                if (_font == null && TMP_Settings.instance != null)
                {
                    _font = TMP_Settings.defaultFontAsset;
                }

                if (_font == null)
                {
                    _font = all.FirstOrDefault(f => f != null);
                }

                MyFirstSubnauticaModPlugin.Log.LogInfo(
                    $"[LifeSync][UI] Fuente TMP elegida: {(_font != null ? _font.name : "NINGUNA")} (de {all.Length} cargadas).");
                return _font;
            }
        }

        /// <summary>Vuelca al log los nombres de todas las fuentes TMP cargadas, para afinar la tipografía.</summary>
        internal static void DumpFontsToLog()
        {
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            MyFirstSubnauticaModPlugin.Log.LogInfo($"[LifeSync][UI] TMP_FontAsset cargadas ({all.Length}):");
            foreach (var f in all.Where(f => f != null).OrderBy(f => f.name))
            {
                MyFirstSubnauticaModPlugin.Log.LogInfo($"[LifeSync][UI]   • {f.name}");
            }
        }

        /// <summary>
        /// Genera un sprite cuadrado con esquinas redondeadas y borde opcional, marcado como 9-slice
        /// (border = radio) para que escale sin deformar las esquinas.
        /// </summary>
        private static Sprite MakeRoundedSprite(int size, int radius, int borderWidth, Color fill, Color borderColor)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var a = RoundedAlpha(x, y, size, radius);
                    var c = fill;
                    c.a *= a;

                    if (borderWidth > 0 && a > 0f)
                    {
                        var edge = DistanceToEdge(x, y, size, radius);
                        if (edge < borderWidth)
                        {
                            c = Color.Lerp(borderColor, c, edge / borderWidth);
                            c.a = Mathf.Max(c.a, borderColor.a * a);
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            var rect = new Rect(0, 0, size, size);
            var b = Mathf.Max(radius, borderWidth + 1);
            return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(b, b, b, b));
        }

        // Antialiasing simple de la esquina redondeada: alpha 0..1 según distancia al centro del cuarto de círculo.
        private static float RoundedAlpha(int x, int y, int size, int radius)
        {
            float cx, cy;
            var inCorner = true;

            if (x < radius && y < radius) { cx = radius; cy = radius; }
            else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; }
            else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; }
            else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; }
            else { inCorner = false; cx = cy = 0; }

            if (!inCorner)
            {
                return 1f;
            }

            var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(radius - d + 0.5f);
        }

        private static float DistanceToEdge(int x, int y, int size, int radius)
        {
            // Distancia al borde recto más cercano (aprox suficiente para el contorno).
            var dl = x;
            var dr = size - 1 - x;
            var db = y;
            var dt = size - 1 - y;
            return Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
        }
    }
}
