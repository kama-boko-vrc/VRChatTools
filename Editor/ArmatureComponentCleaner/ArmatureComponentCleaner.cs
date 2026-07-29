#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 指定したTransformとその配下すべてから、Transform以外の全コンポーネントを再帰的に削除するエディタ拡張。
/// </summary>
public class ArmatureComponentCleaner : EditorWindow
{
    private Transform root;
    private int removedCount;

    [MenuItem("Tools/VRChatTools/Armature Component Cleaner")]
    private static void ShowWindow()
    {
        GetWindow<ArmatureComponentCleaner>("Armature Component Cleaner");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "指定したTransformとその配下すべてから、Transform以外の全コンポーネントを再帰的に削除します。\n" +
            "元に戻す場合はCtrl+Zで取り消せます。",
            MessageType.Warning);

        EditorGUILayout.LabelField("対象");
        root = (Transform)EditorGUILayout.ObjectField(root, typeof(Transform), true);

        EditorGUI.BeginDisabledGroup(root == null);
        if (GUILayout.Button("Transform以外を削除"))
        {
            Execute();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void Execute()
    {
        removedCount = 0;

        Undo.SetCurrentGroupName("Remove Non-Transform Components");
        int undoGroup = Undo.GetCurrentGroup();

        RemoveRecursive(root);

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[ArmatureComponentCleaner] 完了: {removedCount}件のコンポーネントを削除しました");
    }

    private void RemoveRecursive(Transform t)
    {
        foreach (Component c in t.GetComponents<Component>())
        {
            if (c is Transform) continue;
            Undo.DestroyObjectImmediate(c);
            removedCount++;
        }

        foreach (Transform child in t)
        {
            RemoveRecursive(child);
        }
    }
}
#endif