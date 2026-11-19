using TMPro;
using UnityEngine;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// Central visual style. Every UI script reads colours, fonts, sizes and shapes from
    /// here, so the whole app can be restyled from one asset without touching code.
    ///
    /// Design language: dark botanical glass over the live camera. Surfaces are
    /// translucent so the plant (real or twin) stays visible behind the chrome; a single
    /// mint accent marks health and primary actions; amber and coral are reserved for
    /// warnings and disease so colour always carries meaning.
    /// Sizes are in canvas units for a 1080 x 1920 portrait reference resolution.
    /// </summary>
    [CreateAssetMenu(menuName = "PlantDoctor/UI Theme", fileName = "ClassicTheme")]
    public class ClassicTheme : ScriptableObject
    {
        [Header("Surfaces")]
        public Color background = new Color32(0x07, 0x11, 0x0C, 0xF0);
        public Color scrim = new Color32(0x04, 0x0B, 0x08, 0x8C);
        public Color panel = new Color32(0x0F, 0x1C, 0x16, 0xF2);   // linear blending makes dark glass read thinner than its alpha
        public Color panelRaised = new Color32(0x1B, 0x2C, 0x24, 0xFF);
        public Color panelSunken = new Color32(0x08, 0x12, 0x0E, 0xB8);
        public Color divider = new Color(1f, 1f, 1f, 0.09f);

        [Header("Text")]
        public Color textPrimary = new Color32(0xF2, 0xF5, 0xEE, 0xFF);
        public Color textSecondary = new Color32(0xA9, 0xBD, 0xB1, 0xFF);
        public Color textTertiary = new Color32(0x6F, 0x85, 0x7A, 0xFF);
        public Color textOnAccent = new Color32(0x06, 0x20, 0x12, 0xFF);

        [Header("Brand / status")]
        public Color accent = new Color32(0x6B, 0xE3, 0x9A, 0xFF);      // healthy mint
        public Color accentDim = new Color32(0x2E, 0x6B, 0x4A, 0xFF);
        public Color accentSoft = new Color32(0x6B, 0xE3, 0x9A, 0x29);
        public Color warning = new Color32(0xF7, 0xC5, 0x48, 0xFF);
        public Color danger = new Color32(0xFF, 0x6B, 0x5E, 0xFF);
        public Color info = new Color32(0x7C, 0xC4, 0xFF, 0xFF);
        public Color demoBanner = new Color32(0xF7, 0xC5, 0x48, 0xFF);

        [Header("Fonts (Poppins, OFL)")]
        public TMP_FontAsset fontRegular;
        public TMP_FontAsset fontMedium;
        public TMP_FontAsset fontSemiBold;
        public TMP_FontAsset fontBold;

        [Header("Type scale")]
        public float fontDisplay = 72f;
        public float fontTitle = 50f;
        public float fontHeading = 38f;
        public float fontBody = 31f;
        public float fontCaption = 25f;
        public float fontOverline = 22f;
        public float fontButton = 32f;

        [Header("Shapes (generated sprites, see UIShapeLibrary)")]
        public Sprite roundedSprite;
        public Sprite outlineSprite;
        public Sprite circleSprite;
        public Sprite ringSprite;
        public Sprite softShadowSprite;
        public Sprite leafSprite;
        public Sprite checkSprite;
        public Sprite gearSprite;
        public Sprite cornerSprite;
        public Sprite gradientSprite;

        [Header("Metrics")]
        public float cornerPadding = 44f;
        public float sheetRadius = 56f;
        public float cardRadius = 36f;
        public float rowHeight = 132f;
        public float buttonHeight = 128f;
        public float chipHeight = 64f;

        /// <summary>Colour for a health value where lower is better (severity, lesions).</summary>
        public Color StatusColorForRisk(float value01)
        {
            if (value01 >= 0.55f) return danger;
            if (value01 >= 0.25f) return warning;
            return accent;
        }

        /// <summary>Colour for a confidence band.</summary>
        public Color ConfidenceColor(AI.ConfidenceBand band) => band switch
        {
            AI.ConfidenceBand.High => accent,
            AI.ConfidenceBand.Moderate => warning,
            _ => danger
        };

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
