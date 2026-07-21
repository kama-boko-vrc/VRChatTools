#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 指定したArmature配下のボーンのうち、SkinnedMeshRendererから参照されていないものを検出し、
/// チェックリスト形式で選択した上で一括削除するエディタ拡張。
/// PhysBoneなど他のコンポーネントからの参照は判定に含めない（SkinnedMeshRendererの
/// rootBone/bonesにあるかどうかのみを基準にする）。
/// </summary>
public class UnusedArmatureBoneCleaner : EditorWindow
{
    private Transform avatarRoot;
    private Transform armatureRoot;

    private readonly List<Transform> unusedRoots = new List<Transform>();
    private readonly Dictionary<Transform, bool> selected = new Dictionary<Transform, bool>();
    private Vector2 scroll;
    private bool hasScanned;

    [MenuItem("Tools/VRChatTools/Unused Armature Bone Cleaner")]
    private static void ShowWindow()
    {
        UnusedArmatureBoneCleaner window = GetWindow<UnusedArmatureBoneCleaner>("Unused Armature Bone Cleaner");
        window.minSize = new Vector2(400, 500);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "指定したArmature配下で、SkinnedMeshRendererから参照されていないボーンを検出します。\n" +
            "PhysBoneなど他のコンポーネントからの参照は判定に含みません。そのボーンが\n" +
            "PhysBone等で使われている場合は、削除前に該当コンポーネントも確認してください。\n" +
            "削除はUndo対応（Ctrl+Zで復元可）ですが、実行前に一覧を確認してください。",
            MessageType.Warning);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("アバタールート（参照スキャン範囲）");
        avatarRoot = (Transform)EditorGUILayout.ObjectField(avatarRoot, typeof(Transform), true);
        EditorGUILayout.LabelField("Armatureルート（削除候補の範囲）");
        armatureRoot = (Transform)EditorGUILayout.ObjectField(armatureRoot, typeof(Transform), true);
        if (EditorGUI.EndChangeCheck())
        {
            ClearResults();
        }

        EditorGUI.BeginDisabledGroup(avatarRoot == null || armatureRoot == null);
        if (GUILayout.Button("スキャン"))
        {
            Scan();
        }
        EditorGUI.EndDisabledGroup();

        if (!hasScanned) return;

        EditorGUILayout.Space();

        if (unusedRoots.Count == 0)
        {
            EditorGUILayout.HelpBox("未参照のボーンは見つかりませんでした。", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"未参照のボーン（{unusedRoots.Count}件）", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全選択")) SetAllSelected(true);
        if (GUILayout.Button("全解除")) SetAllSelected(false);
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        foreach (Transform t in unusedRoots)
        {
            if (t == null) continue;
            bool current = selected.TryGetValue(t, out bool v) && v;
            selected[t] = EditorGUILayout.ToggleLeft(GetRelativePath(armatureRoot, t), current);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("選択したボーンを削除"))
        {
            DeleteSelected();
        }
    }

    private void ClearResults()
    {
        unusedRoots.Clear();
        selected.Clear();
        hasScanned = false;
    }

    private void Scan()
    {
        unusedRoots.Clear();
        selected.Clear();

        HashSet<Transform> referenced = new HashSet<Transform>();
        CollectReferencedTransforms(avatarRoot, referenced);

        Dictionary<Transform, bool> unusedCache = new Dictionary<Transform, bool>();
        foreach (Transform child in armatureRoot)
        {
            CollectTopLevelUnusedRoots(child, referenced, unusedCache, unusedRoots);
        }

        foreach (Transform t in unusedRoots)
        {
            selected[t] = true;
        }

        hasScanned = true;
        Debug.Log($"[UnusedArmatureBoneCleaner] スキャン完了: 未参照のボーン {unusedRoots.Count}件");
    }

    private static void CollectReferencedTransforms(Transform avatar, HashSet<Transform> referenced)
    {
        foreach (SkinnedMeshRenderer smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.rootBone != null) referenced.Add(smr.rootBone);
            if (smr.bones == null) continue;

            foreach (Transform b in smr.bones)
            {
                if (b != null) referenced.Add(b);
            }
        }
    }

    private static bool IsEndBoneName(string name)
    {
        return name.TrimEnd().EndsWith("end", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFullyUnused(Transform t, HashSet<Transform> referenced, Dictionary<Transform, bool> cache)
    {
        if (cache.TryGetValue(t, out bool cached)) return cached;

        bool result = !referenced.Contains(t);
        if (result)
        {
            foreach (Transform child in t)
            {
                if (!IsFullyUnused(child, referenced, cache))
                {
                    result = false;
                    break;
                }
            }
        }

        cache[t] = result;
        return result;
    }

    private static void CollectTopLevelUnusedRoots(Transform t, HashSet<Transform> referenced,
        Dictionary<Transform, bool> cache, List<Transform> result)
    {
        // 単体で見つかったendボーンは削除候補にしない（例: 現役の指チェーン末端のendボーンを誤検出しないため）。
        // ただし、その祖先チェーンごと未参照であれば、祖先側がここより先に候補として追加されるため
        // endボーンごと削除対象に含まれる。
        if (IsFullyUnused(t, referenced, cache) && !IsEndBoneName(t.name))
        {
            result.Add(t);
            return;
        }

        foreach (Transform child in t)
        {
            CollectTopLevelUnusedRoots(child, referenced, cache, result);
        }
    }

    private static string GetRelativePath(Transform root, Transform t)
    {
        string path = t.name;
        Transform current = t.parent;
        while (current != null && current != root)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }
        return path;
    }

    private void SetAllSelected(bool value)
    {
        foreach (Transform t in unusedRoots)
        {
            selected[t] = value;
        }
    }

    private void DeleteSelected()
    {
        int deletedCount = 0;
        List<Transform> remaining = new List<Transform>();

        foreach (Transform t in unusedRoots)
        {
            if (t == null) continue;

            if (selected.TryGetValue(t, out bool isSelected) && isSelected)
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                deletedCount++;
            }
            else
            {
                remaining.Add(t);
            }
        }

        unusedRoots.Clear();
        unusedRoots.AddRange(remaining);

        Debug.Log($"[UnusedArmatureBoneCleaner] 完了: {deletedCount}件のボーンを削除しました");
    }
}
#endif
