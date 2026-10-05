using System.Collections.Generic;
using System.IO;
using Imitacoes.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Imitacoes.EditorTools
{
    /// <summary>
    /// Gera (uma vez) os assets visuais do jogo:
    /// - Sprites procedurais em PNG: círculo liso (fatiado = cápsulas e retângulos arredondados),
    ///   anel (molduras e cursor) e círculo suave (sombras e brilhos).
    /// - Fonte TextMeshPro a partir da Lilita One (Fonts/LilitaOne-Regular.ttf, licença OFL).
    /// Tudo fica em Assets/_Imitacoes/Art/Generated e é referenciado pela cena.
    /// </summary>
    internal static class SkinAssets
    {
        private const string Folder = "Assets/_Imitacoes/Art/Generated";
        private const string FontFile = "Assets/_Imitacoes/Fonts/LilitaOne-Regular.ttf";
        private const string FontAssetPath = Folder + "/LilitaOne SDF.asset";
        private const int Size = 256;

        public static UISkin Create()
        {
            Directory.CreateDirectory(Folder);

            var skin = new UISkin
            {
                circle = EnsureSprite("Circulo.png", CirclePixels(), Size / 2),
                ring = EnsureSprite("Anel.png", RingPixels(26f), Size / 2),
                soft = EnsureSprite("CirculoSuave.png", SoftPixels(), 0),
                font = EnsureFont()
            };
            return skin;
        }

        // ------------------------------------------------------------------ Sprites

        private static Sprite EnsureSprite(string file, Color32[] pixels, int border)
        {
            string path = $"{Folder}/{file}";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = importer.textureType != TextureImporterType.Sprite
                         || importer.spriteBorder != new Vector4(border, border, border, border);
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Círculo branco com borda anti-serrilhada.</summary>
        private static Color32[] CirclePixels() => Radial((d, r) => Mathf.Clamp01(r - d + 0.5f));

        /// <summary>Anel branco de espessura fixa (px), anti-serrilhado dos dois lados.</summary>
        private static Color32[] RingPixels(float thickness)
            => Radial((d, r) => Mathf.Clamp01(r - d + 0.5f) * Mathf.Clamp01(d - (r - thickness) + 0.5f));

        /// <summary>Círculo com borda bem suave (gaussiana): sombras, brilhos, fundo.</summary>
        private static Color32[] SoftPixels() => Radial((d, r) =>
        {
            float t = Mathf.Clamp01(d / r);
            return Mathf.Exp(-t * t * 4f) * (1f - t);
        });

        private static Color32[] Radial(System.Func<float, float, float> alpha)
        {
            var pixels = new Color32[Size * Size];
            float c = (Size - 1) * 0.5f;
            float r = Size * 0.5f - 1f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(d, r)) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }
            return pixels;
        }

        // ------------------------------------------------------------------ Fonte

        private static TMP_FontAsset EnsureFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
            if (font == null)
            {
                Debug.LogWarning($"[Imitações] Fonte não encontrada em {FontFile}; usando a fonte padrão do TMP.");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
            asset.name = "LilitaOne SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            // Atlas e material precisam ser sub-assets para serem salvos junto.
            asset.atlasTextures[0].name = "LilitaOne Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = "LilitaOne Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            // Pré-carrega os caracteres usados no jogo (o resto entra dinamicamente).
            asset.TryAddCharacters(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 " +
                "ÁÀÂÃÉÊÍÓÔÕÚÇáàâãéêíóôõúç.,:;!?%()-+º•'\"/");

            TMP_FontAsset fallback = UIFactory.DefaultFont();
            if (fallback != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
