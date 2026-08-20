#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// アバターのルートを指定するだけで、配下のRenderer/SkinnedMeshRendererが使う
/// 全マテリアルのテクスチャプロパティを集め、テクスチャ圧縮設定を一括変更するエディタ拡張。
/// 解像度（Max Size）は各テクスチャの元画像の解像度に応じて自動決定される
/// （元画像と同じ解像度、それがUnityの選択肢にない場合は長辺を超える最も近い選択肢）。
/// </summary>
public class TextureCompressionBatchSetter : EditorWindow
{
    private static readonly int[] AllowedMaxSizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

    private Transform avatarRoot;
    private readonly List<Texture2D> textures = new List<Texture2D>();

    private TextureImporterCompression compression = TextureImporterCompression.Compressed;
    private bool useCrunchCompression;
    private int compressorQuality = 50;

    internal static void ShowWindow()
    {
        TextureCompressionBatchSetter window = GetWindow<TextureCompressionBatchSetter>("Texture Compression Batch Setter");
        window.minSize = new Vector2(420, 400);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "アバターが使用する全テクスチャの圧縮設定を一括変更します。\n" +
            "解像度（Max Size）は各テクスチャの元画像の解像度に応じて自動決定されます\n" +
            "（元画像と同じ解像度、選択肢にない場合は長辺を超える最も近い選択肢）。\n" +
            "インポート設定への変更のためCtrl+Zで元に戻せません。事前にバックアップを推奨します。",
            MessageType.Warning);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("アバター");
        avatarRoot = (Transform)EditorGUILayout.ObjectField(avatarRoot, typeof(Transform), true);
        if (EditorGUI.EndChangeCheck())
        {
            ScanTextures();
        }

        if (avatarRoot == null) return;

        EditorGUILayout.Space();

        if (textures.Count == 0)
        {
            EditorGUILayout.HelpBox("テクスチャが見つかりませんでした。", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"対象テクスチャ（全{textures.Count}件）", EditorStyles.boldLabel);

        foreach (Texture2D tex in textures)
        {
            EditorGUILayout.LabelField(DescribeTexture(tex), EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("圧縮設定", EditorStyles.boldLabel);
        compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", compression);
        useCrunchCompression = EditorGUILayout.Toggle("Use Crunch Compression", useCrunchCompression);
        compressorQuality = EditorGUILayout.IntSlider("Compressor Quality", compressorQuality, 0, 100);

        EditorGUILayout.Space();
        if (GUILayout.Button("設定を一括適用"))
        {
            Execute();
        }
    }

    private void ScanTextures()
    {
        textures.Clear();
        if (avatarRoot == null) return;

        HashSet<Texture2D> seen = new HashSet<Texture2D>();
        foreach (Renderer renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                Shader shader = mat.shader;
                int propertyCount = shader.GetPropertyCount();
                for (int i = 0; i < propertyCount; i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;

                    string propName = shader.GetPropertyName(i);
                    if (mat.GetTexture(propName) is Texture2D tex && seen.Add(tex))
                    {
                        textures.Add(tex);
                    }
                }
            }
        }
    }

    private string DescribeTexture(Texture2D tex)
    {
        TextureImporter importer = GetImporter(tex);
        if (importer == null) return $"{tex.name}（インポーター取得不可、スキップされます）";

        importer.GetSourceTextureWidthAndHeight(out int srcWidth, out int srcHeight);
        int targetMaxSize = ResolveMaxTextureSize(Mathf.Max(srcWidth, srcHeight));

        return $"{tex.name}　元画像 {srcWidth}x{srcHeight} → Max Size {targetMaxSize}（現在 {importer.maxTextureSize}）";
    }

    private static TextureImporter GetImporter(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(path)) return null;

        return AssetImporter.GetAtPath(path) as TextureImporter;
    }

    private static int ResolveMaxTextureSize(int longEdge)
    {
        foreach (int size in AllowedMaxSizes)
        {
            if (size >= longEdge) return size;
        }

        return AllowedMaxSizes[AllowedMaxSizes.Length - 1];
    }

    private void Execute()
    {
        int changedCount = 0;

        foreach (Texture2D tex in textures)
        {
            TextureImporter importer = GetImporter(tex);
            if (importer == null) continue;

            importer.GetSourceTextureWidthAndHeight(out int srcWidth, out int srcHeight);

            importer.maxTextureSize = ResolveMaxTextureSize(Mathf.Max(srcWidth, srcHeight));
            importer.textureCompression = compression;
            importer.crunchedCompression = useCrunchCompression;
            importer.compressionQuality = compressorQuality;

            importer.SaveAndReimport();
            changedCount++;
        }

        Debug.Log($"[TextureCompressionBatchSetter] 完了: {changedCount}件のテクスチャの圧縮設定を変更しました");
    }
}
#endif
