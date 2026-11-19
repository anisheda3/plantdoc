namespace PlantDoctor.AI
{
    /// <summary>
    /// The numbers LeafFeatureTracer uses. They are public statics so the evaluation (Assets/Editor/TracerEval.cs) can sweep them on a
    /// tuning set of real photos; the defaults here are what the app runs with, and are the values that won on that tuning set.
    /// </summary>
    public static class TracerSettings
    {
        /// <summary>true: colour distance from the leaf's own healthy green in CIELAB. false: the first fixed hue/brightness rules (kept to reproduce the baseline).</summary>
        public static bool UseLab = true;
        /// <summary>With UseLab: false = colour distance from the healthy green (all three CIELAB axes); true = Otsu split of the a* axis (how red rather than green a pixel is) inside the leaf.</summary>
        public static bool RednessOnly = true;
        /// <summary>Added to the Otsu threshold of a* (RednessOnly): negative marks more as affected.</summary>
        public static float RednessOffset = 0f;
        /// <summary>The split only counts as damage if the redder group's mean a* is at least this far above the greener group's; otherwise the leaf is healthy (an even leaf has no real split).</summary>
        public static float MinRednessGap = 12f;
        /// <summary>0 = Otsu split of a* with a minimum gap; 1 = deviation from the leaf's median a*: affected when a* is above the median by at least max(<see cref="MinDeviation"/>, <see cref="MadK"/> robust standard deviations).</summary>
        public static int ThresholdMode = 2;
        public static float MinDeviation = 11f;
        public static float MadK = 2f;

        /// <summary>Smallest colour distance (CIELAB, lightness weighted by <see cref="LightnessWeight"/>) from the healthy green that counts as affected.</summary>
        public static float MinDelta = 20f;
        /// <summary>Lightness differences matter less than colour (veins, shading and glare change lightness on a healthy leaf).</summary>
        public static float LightnessWeight = 0.5f;
        /// <summary>The threshold is the larger of <see cref="MinDelta"/> and this multiple of the Otsu split of the leaf's own distances (0 = fixed threshold).</summary>
        public static float OtsuBlend = 1f;
        /// <summary>Affected pixels whose b* (blue to yellow) is this much above the healthy green and that are not darker count as yellowing, the rest as lesions.</summary>
        public static float YellowDeltaB = 10f;
        public static float YellowMinLightnessDrop = -8f;

        public static int OpenRadius = 0;
        /// <summary>Radius of the closing that joins green pixels into one leaf body.</summary>
        public static int LeafCloseRadius = 6;
        /// <summary>Also count yellow pixels when looking for the leaf (a yellowed leaf), not only green ones.</summary>
        public static bool LeafSeedYellow = true;
        /// <summary>Smallest saturation of a pixel that can belong to the leaf.</summary>
        public static float LeafMinSaturation = 0.22f;
        /// <summary>Smallest spot, as a share of the leaf's area.</summary>
        public static float MinLesionFraction = 0.0012f;
        public static float MinYellowFraction = 0.012f;
    }
}
