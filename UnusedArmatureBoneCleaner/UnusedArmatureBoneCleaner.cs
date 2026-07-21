#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 指定したArmature配下のボーンのうち、アバター内のどこからも参照されていないものを検出し、
/// チェックリスト形式で選択した上で一括削除するエディタ拡張。
/// 参照の判定対象: SkinnedMeshRenderer(rootBone/bones)、HumanoidのAnimator(GetBoneTransform)、
/// アバター配下の全コンポーネントのシリアライズ済みTransform/GameObject参照。
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
            "指定したArmature配下で、アバターのどこからも参照されていないボーンを検出します。\n" +
            "検出対象: SkinnedMeshRendererのボーン、HumanoidのAnimatorが参照するボーン、\n" +
            "全コンポーネントのTransform/GameObject参照。\n" +
            "削除はUndo対応（Ctrl+Zで復元可）ですが、実行前に一覧を確認してください。",
            MessageType.Warning);

        EditorGUI.BeginChangeCheck();
        avatarRoot = (Transform)EditorGUILayout.ObjectField("アバタールート（参照スキャン範囲）", avatarRoot, typeof(Transform), true);
        armatureRoot = (Transform)EditorGUILayout.ObjectField("Armatureルート（削除候補の範囲）", armatureRoot, typeof(Transform), true);
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
        foreach (Transform t in avatar.GetComponentsInChildren<Transform>(true))
        {
            foreach (Component comp in t.GetComponents<Component>())
            {
                if (comp == null || comp is Transform) continue;

                if (comp is SkinnedMeshRenderer smr)
                {
                    if (smr.rootBone != null) referenced.Add(smr.rootBone);
                    if (smr.bones != null)
                    {
                        foreach (Transform b in smr.bones)
                        {
                            if (b != null) referenced.Add(b);
                        }
                    }
                }

                if (comp is Animator animator && animator.isHuman)
                {
                    foreach (HumanBodyBones hb in Enum.GetValues(typeof(HumanBodyBones)))
                    {
                        if (hb == HumanBodyBones.LastBone) continue;
                        Transform bone = animator.GetBoneTransform(hb);
                        if (bone != null) referenced.Add(bone);
                    }
                }

                SerializedObject so = new SerializedObject(comp);
                SerializedProperty prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                    UnityEngine.Object obj = prop.objectReferenceValue;
                    if (obj == null) continue;

                    Transform refTransform = obj as Transform;
                    if (refTransform == null && obj is GameObject go) refTransform = go.transform;
                    if (refTransform == null && obj is Component c) refTransform = c.transform;

                    if (refTransform != null) referenced.Add(refTransform);
                }
            }
        }
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
        if (IsFullyUnused(t, referenced, cache))
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
