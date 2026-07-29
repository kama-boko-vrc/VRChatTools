#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 同一構造のArmature間で、各ボーンのlocalScaleとMA Scale Adjusterコンポーネントをコピーするエディタ拡張。
/// A: コピー元（アバター本体のArmature）
/// B: コピー先（衣装・髪など、一部ボーンが欠損している可能性があるArmature）
/// </summary>
public class ArmatureScaleCopier : EditorWindow
{
    private Transform sourceRoot;
    private Transform targetRoot;
    private bool copyScaleAdjuster = true;

    private int copiedScaleCount;
    private int copiedComponentCount;
    private int missingBoneCount;

    [MenuItem("Tools/VRChatTools/Armature Scale Copier")]
    private static void ShowWindow()
    {
        GetWindow<ArmatureScaleCopier>("Armature Scale Copier");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "同一構造のArmature間で localScale と MA Scale Adjuster をコピーします。\n" +
            "A: コピー元（アバター本体のArmature）\n" +
            "B: コピー先（衣装・髪などのArmature）\n" +
            "Bに存在しないボーンはスキップされます。",
            MessageType.Info);

        EditorGUILayout.LabelField("A (コピー元)");
        sourceRoot = (Transform)EditorGUILayout.ObjectField(sourceRoot, typeof(Transform), true);
        EditorGUILayout.LabelField("B (コピー先)");
        targetRoot = (Transform)EditorGUILayout.ObjectField(targetRoot, typeof(Transform), true);
        copyScaleAdjuster = EditorGUILayout.Toggle("MA Scale Adjuster もコピー", copyScaleAdjuster);

        EditorGUI.BeginDisabledGroup(sourceRoot == null || targetRoot == null);
        if (GUILayout.Button("コピー実行"))
        {
            Execute();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void Execute()
    {
        copiedScaleCount = 0;
        copiedComponentCount = 0;
        missingBoneCount = 0;

        Undo.SetCurrentGroupName("Copy Armature Scale");
        int undoGroup = Undo.GetCurrentGroup();

        CopyRecursive(sourceRoot, targetRoot);

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[ArmatureScaleCopier] 完了: Scaleコピー {copiedScaleCount}件 / " +
                  $"ScaleAdjusterコピー {copiedComponentCount}件 / 欠損ボーン {missingBoneCount}件");
    }

    private void CopyRecursive(Transform src, Transform dst)
    {
        Undo.RecordObject(dst, "Copy Scale");
        dst.localScale = src.localScale;
        copiedScaleCount++;

        if (copyScaleAdjuster)
        {
            CopyScaleAdjuster(src, dst);
        }

        foreach (Transform srcChild in src)
        {
            Transform dstChild = dst.Find(srcChild.name);
            if (dstChild == null)
            {
                missingBoneCount++;
                continue;
            }
            CopyRecursive(srcChild, dstChild);
        }
    }

    private void CopyScaleAdjuster(Transform src, Transform dst)
    {
        // 型名に "ScaleAdjuster" を含むコンポーネントを対象にする（MA本体への直接参照を避け、依存を持たせない）
        Component srcComponent = src.GetComponents<Component>()
            .FirstOrDefault(c => c != null &&
                c.GetType().Name.IndexOf("ScaleAdjuster", StringComparison.OrdinalIgnoreCase) >= 0);

        if (srcComponent == null) return;

        if (!ComponentUtility.CopyComponent(srcComponent)) return;

        Component dstComponent = dst.GetComponent(srcComponent.GetType());
        if (dstComponent != null)
        {
            Undo.RecordObject(dstComponent, "Copy Scale Adjuster Values");
            ComponentUtility.PasteComponentValues(dstComponent);
        }
        else
        {
            ComponentUtility.PasteComponentAsNew(dst.gameObject);
            Component added = dst.GetComponent(srcComponent.GetType());
            if (added != null)
            {
                Undo.RegisterCreatedObjectUndo(added, "Add Scale Adjuster");
            }
        }

        copiedComponentCount++;
    }
}
#endif