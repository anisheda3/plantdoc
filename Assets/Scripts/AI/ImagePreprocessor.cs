using UnityEngine;

namespace PlantDoctor.AI
{
    /// <summary>Crops to the region of interest and resizes to the model input size.</summary>
    public static class ImagePreprocessor
    {
        public const int DefaultModelInputSize = 224;

        /// <param name="normalizedCrop">ROI in 0..1 texture space; full frame if default.</param>
        public static Texture2D Prepare(Texture2D source, Rect normalizedCrop = default, int targetSize = DefaultModelInputSize)
        {
            if (source == null) return null;
            if (normalizedCrop.width <= 0f || normalizedCrop.height <= 0f)
                normalizedCrop = new Rect(0f, 0f, 1f, 1f);

            var output = new Texture2D(targetSize, targetSize, TextureFormat.RGB24, false);
            var pixels = new Color[targetSize * targetSize];

            for (int y = 0; y < targetSize; y++)
            {
                float v = normalizedCrop.y + (y + 0.5f) / targetSize * normalizedCrop.height;
                for (int x = 0; x < targetSize; x++)
                {
                    float u = normalizedCrop.x + (x + 0.5f) / targetSize * normalizedCrop.width;
                    pixels[y * targetSize + x] = source.GetPixelBilinear(Mathf.Clamp01(u), Mathf.Clamp01(v));
                }
            }

            output.SetPixels(pixels);
            output.Apply(false);
            return output;
        }
    }
}
