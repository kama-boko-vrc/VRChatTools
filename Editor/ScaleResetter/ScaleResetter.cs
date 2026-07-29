#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 指定したTransform配下を再帰的に全てlocalScale(1,1,1)にリセットするエディタ拡張。
/// </summary>
public class ScaleResetter : EditorWindow
{
    private Transform root;
    private int resetCount;

    internal static void ShowWindow()
    {
        GetWindow<ScaleResetter>("Scale Resetter");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "指定したTransformとその配下すべてのlocalScaleを(1, 1, 1)にリセットします。",
            MessageType.Info);

        EditorGUILayout.LabelField("対象");
        root = (Transform)EditorGUILayout.ObjectField(root, typeof(Transform), true);

        EditorGUI.BeginDisabledGroup(root == null);
        if (GUILayout.Button("スケールを1.0にリセット"))
        {
            Execute();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void Execute()
    {
        resetCount = 0;

        Undo.SetCurrentGroupName("Reset Scale To One");
        int undoGroup = Undo.GetCurrentGroup();

        ResetRecursive(root);

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[ScaleResetter] 完了: {resetCount}件のTransformをスケール1.0にリセットしました");
    }

    private void ResetRecursive(Transform t)
    {
        Undo.RecordObject(t, "Reset Scale");
        t.localScale = Vector3.one;
        resetCount++;

        foreach (Transform child in t)
        {
            ResetRecursive(child);
        }
    }
}
#endif