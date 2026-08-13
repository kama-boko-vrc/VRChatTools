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

    private Transform avatarRoot;
    private readonly List<Material> avatarMaterials = new List<Material>();

    internal static void ShowWindow()
    {
        LilToonPropertyCopier window = GetWindow<LilToonPropertyCopier>("LilToon Property Copier");
        window.minSize = new Vector2(400, 500);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "コピー元マテリアルの選択したプロパティのみを、コピー先の複数マテリアルへ一括コピーします。\n" +
            "コピー元・コピー先は同じシェーダーである必要があります。\n" +
            "アバターを指定すると、使用中のマテリアル一覧からチェックボックスで選択できます。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("コピー元");
        sourceMaterial = (Material)EditorGUILayout.ObjectField(sourceMaterial, typeof(Material), false);
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("アバターから選択（任意）", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("アバター");
        avatarRoot = (Transform)EditorGUILayout.ObjectField(avatarRoot, typeof(Transform), true);
        if (EditorGUI.EndChangeCheck())
        {
            ScanAvatarMaterials();
        }

        if (avatarRoot != null)
        {
            if (avatarMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("使用しているマテリアルが見つかりませんでした。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("「元」「先」にチェックすると、上のコピー元・コピー先に反映されます", EditorStyles.miniLabel);

                foreach (Material mat in avatarMaterials)
                {
                    EditorGUILayout.BeginHorizontal();

                    bool isSource = mat == sourceMaterial;
                    bool newIsSource = EditorGUILayout.ToggleLeft("元", isSource, GUILayout.Width(35));
                    if (newIsSource != isSource)
                    {
                        sourceMaterial = newIsSource ? mat : null;
                        RefreshProperties();
                    }

                    bool isTarget = targetMaterials.Contains(mat);
                    bool newIsTarget = EditorGUILayout.ToggleLeft("先", isTarget, GUILayout.Width(35));
                    if (newIsTarget && !isTarget) targetMaterials.Add(mat);
                    else if (!newIsTarget && isTarget) targetMaterials.Remove(mat);

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.EndHorizontal();
                }
            }
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

    private void ScanAvatarMaterials()
    {
        avatarMaterials.Clear();
        if (avatarRoot == null) return;

        HashSet<Material> seen = new HashSet<Material>();
        foreach (Renderer renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && seen.Add(mat)) avatarMaterials.Add(mat);
            }
        }
    }

    private void SetAllSelected(bool value)
    {
        Shader shader = sourceMaterial.shader;
        int propertyCount = shader.GetPropertyCount();
        for (int i = 0; i < propertyCount; i++)
        {
            selectedProperties[shader.GetPropertyName(i)] = value;
        }
    }

    private void Execute()
    {
        Shader shader = sourceMaterial.shader;
        int propertyCount = shader.GetPropertyCount();
        int copiedProperties = 0;

        Undo.SetCurrentGroupName("Copy LilToon Properties");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Material target in targetMaterials)
        {
            if (target == null) continue;

            Undo.RecordObject(target, "Copy LilToon Properties");

            for (int i = 0; i < propertyCount; i++)
            {
                string propName = shader.GetPropertyName(i);
                if (!selectedProperties.TryGetValue(propName, out bool selected) || !selected) continue;
                if (!target.HasProperty(propName)) continue;

                CopyProperty(shader.GetPropertyType(i), propName, target);
                copiedProperties++;
            }

            EditorUtility.SetDirty(target);
        }

        Undo.CollapseUndoOperations(undoGroup);

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