#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 1つのマテリアル(コピー元)から、選択したシェーダープロパティのみを
/// 複数のマテリアル(コピー先)へ一括コピーするエディタ拡張。
/// lilToonに限らず、コピー元・コピー先が同じシェーダーであれば利用可能。
/// </summary>
public class LilToonPropertyCopier : EditorWindow
{
    private Material sourceMaterial;
    private readonly List<Material> targetMaterials = new List<Material>();
    private readonly Dictionary<string, bool> selectedProperties = new Dictionary<string, bool>();
    private Vector2 scroll;

    [MenuItem("Tools/VRChatTools/LilToon Property Copier")]
    private static void ShowWindow()
    {
        LilToonPropertyCopier window = GetWindow<LilToonPropertyCopier>("LilToon Property Copier");
        window.minSize = new Vector2(400, 500);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "コピー元マテリアルの選択したプロパティのみを、コピー先の複数マテリアルへ一括コピーします。\n" +
            "コピー元・コピー先は同じシェーダーである必要があります。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        sourceMaterial = (Material)EditorGUILayout.ObjectField("コピー元", sourceMaterial, typeof(Material), false);
        if (EditorGUI.EndChangeCheck())
        {
            RefreshProperties();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("コピー先", EditorStyles.boldLabel);
        for (int i = 0; i < targetMaterials.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            targetMaterials[i] = (Material)EditorGUILayout.ObjectField(targetMaterials[i], typeof(Material), false);
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                targetMaterials.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ コピー先を追加"))
        {
            targetMaterials.Add(null);
        }

        if (sourceMaterial == null) return;

        Shader shader = sourceMaterial.shader;
        int propertyCount = shader.GetPropertyCount();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"コピーするプロパティ（全{propertyCount}件）", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全選択")) SetAllSelected(true);
        if (GUILayout.Button("全解除")) SetAllSelected(false);
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        for (int i = 0; i < propertyCount; i++)
        {
            string propName = shader.GetPropertyName(i);
            string description;
            try
            {
                description = shader.GetPropertyDescription(i);
            }
            catch
            {
                description = propName;
            }

            bool current = selectedProperties.TryGetValue(propName, out bool v) && v;
            selectedProperties[propName] = EditorGUILayout.ToggleLeft($"{description} ({propName})", current);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("コピー実行"))
        {
            Execute();
        }
    }

    private void RefreshProperties()
    {
        selectedProperties.Clear();
    }

    private void SetAllSelected(bool value)
    {
        Shader shader = sourceMaterial.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            selectedProperties[shader.GetPropertyName(i)] = value;
        }
    }

    private void Execute()
    {
        Shader shader = sourceMaterial.shader;
        int copiedProperties = 0;

        foreach (Material target in targetMaterials)
        {
            if (target == null) continue;

            Undo.RecordObject(target, "Copy LilToon Properties");

            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string propName = shader.GetPropertyName(i);
                if (!selectedProperties.TryGetValue(propName, out bool selected) || !selected) continue;
                if (!target.HasProperty(propName)) continue;

                CopyProperty(shader.GetPropertyType(i), propName, target);
                copiedProperties++;
            }

            EditorUtility.SetDirty(target);
        }

        Debug.Log($"[LilToonPropertyCopier] 完了: {targetMaterials.Count}件のマテリアルへ、計{copiedProperties}件のプロパティをコピーしました");
    }

    private void CopyProperty(ShaderPropertyType type, string propName, Material target)
    {
        switch (type)
        {
            case ShaderPropertyType.Color:
                target.SetColor(propName, sourceMaterial.GetColor(propName));
                break;
            case ShaderPropertyType.Vector:
                target.SetVector(propName, sourceMaterial.GetVector(propName));
                break;
            case ShaderPropertyType.Float:
            case ShaderPropertyType.Range:
                target.SetFloat(propName, sourceMaterial.GetFloat(propName));
                break;
            case ShaderPropertyType.Texture:
                target.SetTexture(propName, sourceMaterial.GetTexture(propName));
                target.SetTextureOffset(propName, sourceMaterial.GetTextureOffset(propName));
                target.SetTextureScale(propName, sourceMaterial.GetTextureScale(propName));
                break;
            case ShaderPropertyType.Int:
                target.SetInt(propName, sourceMaterial.GetInt(propName));
                break;
        }
    }
}
#endif