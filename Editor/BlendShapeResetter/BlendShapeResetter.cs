#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 指定したTransform配下の全SkinnedMeshRendererについて、全BlendShapeのWeightを
/// 一括で0にリセットするエディタ拡張。
/// </summary>
public class BlendShapeResetter : EditorWindow
{
    private Transform root;
    private int resetCount;

    internal static void ShowWindow()
    {
        GetWindow<BlendShapeResetter>("Blend Shape Resetter");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "指定したTransformとその配下すべてのSkinnedMeshRendererについて、\n" +
            "全BlendShapeのWeightを0にリセットします。",
            MessageType.Info);

        EditorGUILayout.LabelField("対象");
        root = (Transform)EditorGUILayout.ObjectField(root, typeof(Transform), true);

        EditorGUI.BeginDisabledGroup(root == null);
        if (GUILayout.Button("BlendShapeを0にリセット"))
        {
            Execute();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void Execute()
    {
        resetCount = 0;

        Undo.SetCurrentGroupName("Reset Blend Shapes To Zero");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            ResetRenderer(renderer);
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[BlendShapeResetter] 完了: {resetCount}件のSkinnedMeshRendererのBlendShapeを0にリセットしました");
    }

    private void ResetRenderer(SkinnedMeshRenderer renderer)
    {
        Mesh mesh = renderer.sharedMesh;
        if (mesh == null || mesh.blendShapeCount == 0) return;

        Undo.RecordObject(renderer, "Reset Blend Shapes");

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            renderer.SetBlendShapeWeight(i, 0f);
        }

        resetCount++;
    }
}
#endif
