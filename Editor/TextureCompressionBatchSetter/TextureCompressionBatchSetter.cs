#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// アバターのルートを指定するだけで、配下のRenderer/SkinnedMeshRendererが使う
/// 全マテリアルのテクスチャプロパティを集め、圧縮品質を一括で高品質（CompressedHQ）に
/// 変更するエディタ拡張。解像度（Max Size）など他のインポート設定には触れない。
/// </summary>
public class TextureCompressionBatchSetter : EditorWindow
{
    private Transform avatarRoot;
    private readonly List<Texture2D> textures = new List<Texture2D>();

    internal static void ShowWindow()
    {
        TextureCompressionBatchSetter window = GetWindow<TextureCompressionBatchSetter>("Texture Compression Batch Setter");
        window.minSize = new Vector2(420, 400);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "アバターが使用する全テクスチャの圧縮品質を、一括で高品質（High Quality）に変更します。\n" +
            "解像度（Max Size）など他のインポート設定は変更しません。\n" +
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
        if (GUILayout.Button("圧縮品質を高品質に変更"))
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

        return $"{tex.name}　現在の圧縮品質: {importer.textureCompression}";
    }

    private static TextureImporter GetImporter(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(path)) return null;

        return AssetImporter.GetAtPath(path) as TextureImporter;
    }

    private void Execute()
    {
        int changedCount = 0;

        foreach (Texture2D tex in textures)
        {
            TextureImporter importer = GetImporter(tex);
            if (importer == null) continue;

            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            importer.SaveAndReimport();
            changedCount++;
        }

        Debug.Log($"[TextureCompressionBatchSetter] 完了: {changedCount}件のテクスチャの圧縮品質を高品質に変更しました");
    }
}
#endif
