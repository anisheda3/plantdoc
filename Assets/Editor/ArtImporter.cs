#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Import settings for everything under Assets/Art: icons are UI sprites, photos are plain
    /// textures shown by RawImages, and the leaf cutout keeps its alpha and stays readable (the
    /// annotation code looks up its outline on the CPU).
    /// </summary>
    public class ArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/Art/")) return;

            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;

            if (path.StartsWith("Assets/Art/Icons/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else if (path.StartsWith("Assets/Art/Leaf/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.isReadable = true;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            else if (path.StartsWith("Assets/Art/Plants/"))
            {
                // Leaf atlases (cut-out photos) and the tileable stem, bark, soil and root textures of the plant twins.
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                bool tiles = path.StartsWith("Assets/Art/Plants/shared/");
                importer.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.alphaIsTransparency = !tiles;
                importer.alphaSource = tiles ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = false;
                importer.isReadable = !tiles;   // the zoomed leaf reads its photo's outline to keep labels on the leaf
            }
            else if (path.StartsWith("Assets/Art/Photos/") || path.StartsWith("Assets/Art/Backdrops/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.compressionQuality = 70;
            }
        }
    }
}
#endif
