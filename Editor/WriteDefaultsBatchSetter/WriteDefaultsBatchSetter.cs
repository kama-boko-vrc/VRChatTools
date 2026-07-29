#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// アバターを指定すると、使用している全Animator Controller
/// （VRCAvatarDescriptorの各Playable Layer、および配下のAnimatorコンポーネント）の
/// 全State（サブステートマシン含む）についてWrite Defaultsの現在値を集計し、
/// 一括でON/OFFに変更するエディタ拡張。
/// VRCSDKへの直接参照は持たせず、SerializedObject経由でVRCAvatarDescriptorのフィールドを読む。
/// </summary>
public class WriteDefaultsBatchSetter : EditorWindow
{
    private class ControllerSummary
    {
        public AnimatorController controller;
        public int totalStates;
        public int onCount;
        public int offCount;
    }

    private Transform avatarRoot;
    private readonly List<ControllerSummary> summaries = new List<ControllerSummary>();
    private bool hasScanned;

    [MenuItem("Tools/VRChatTools/Write Defaults Batch Setter")]
    private static void ShowWindow()
    {
        GetWindow<WriteDefaultsBatchSetter>("Write Defaults Batch Setter");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "アバターを指定すると、使用している全Animator Controllerの全State（サブステート\n" +
            "マシン含む）についてWrite Defaultsの現在値を集計し、一括でON/OFFに変更できます。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("アバタールート");
        avatarRoot = (Transform)EditorGUILayout.ObjectField(avatarRoot, typeof(Transform), true);
        if (EditorGUI.EndChangeCheck())
        {
            summaries.Clear();
            hasScanned = false;
        }

        EditorGUI.BeginDisabledGroup(avatarRoot == null);
        if (GUILayout.Button("スキャン"))
        {
            Scan();
        }
        EditorGUI.EndDisabledGroup();

        if (!hasScanned) return;

        EditorGUILayout.Space();

        if (summaries.Count == 0)
        {
            EditorGUILayout.HelpBox("Animator Controllerが見つかりませんでした。", MessageType.Warning);
            return;
        }

        int totalOn = 0, totalOff = 0, totalStates = 0;
        foreach (ControllerSummary s in summaries)
        {
            EditorGUILayout.LabelField($"{s.controller.name}: State数 {s.totalStates} / ON {s.onCount} / OFF {s.offCount}");
            totalOn += s.onCount;
            totalOff += s.offCount;
            totalStates += s.totalStates;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"合計: State数 {totalStates} / ON {totalOn} / OFF {totalOff}", EditorStyles.boldLabel);

        if (totalOn > 0 && totalOff > 0)
        {
            EditorGUILayout.HelpBox(
                "ON/OFFが混在しています。VRChatでは意図しない挙動の原因になるため統一を推奨します。",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全StateをONにする")) SetAll(true);
        if (GUILayout.Button("全StateをOFFにする")) SetAll(false);
        EditorGUILayout.EndHorizontal();
    }

    private void Scan()
    {
        summaries.Clear();

        HashSet<AnimatorController> controllers = new HashSet<AnimatorController>();
        CollectControllersFromAvatarDescriptor(avatarRoot.gameObject, controllers);
        CollectControllersFromAnimators(avatarRoot.gameObject, controllers);

        foreach (AnimatorController controller in controllers)
        {
            ControllerSummary summary = new ControllerSummary { controller = controller };

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine != null) CountStates(layer.stateMachine, summary);
            }

            summaries.Add(summary);
        }

        hasScanned = true;
        Debug.Log($"[WriteDefaultsBatchSetter] スキャン完了: Animator Controller {summaries.Count}件");
    }

    private static void CountStates(AnimatorStateMachine sm, ControllerSummary summary)
    {
        foreach (ChildAnimatorState childState in sm.states)
        {
            summary.totalStates++;
            if (childState.state.writeDefaultValues) summary.onCount++;
            else summary.offCount++;
        }

        foreach (ChildAnimatorStateMachine childSM in sm.stateMachines)
        {
            CountStates(childSM.stateMachine, summary);
        }
    }

    private void SetAll(bool value)
    {
        int changedCount = 0;

        Undo.SetCurrentGroupName("Set Write Defaults");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (ControllerSummary summary in summaries)
        {
            foreach (AnimatorControllerLayer layer in summary.controller.layers)
            {
                if (layer.stateMachine != null) changedCount += ApplyToStateMachine(layer.stateMachine, value);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[WriteDefaultsBatchSetter] 完了: {changedCount}件のStateのWrite Defaultsを{(value ? "ON" : "OFF")}に変更しました");

        Scan();
    }

    private static int ApplyToStateMachine(AnimatorStateMachine sm, bool value)
    {
        int count = 0;

        foreach (ChildAnimatorState childState in sm.states)
        {
            AnimatorState state = childState.state;
            if (state.writeDefaultValues != value)
            {
                Undo.RecordObject(state, "Set Write Defaults");
                state.writeDefaultValues = value;
                EditorUtility.SetDirty(state);
                count++;
            }
        }

        foreach (ChildAnimatorStateMachine childSM in sm.stateMachines)
        {
            count += ApplyToStateMachine(childSM.stateMachine, value);
        }

        return count;
    }

    private static void CollectControllersFromAvatarDescriptor(GameObject avatar, HashSet<AnimatorController> controllers)
    {
        foreach (Component comp in avatar.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "VRCAvatarDescriptor") continue;

            SerializedObject so = new SerializedObject(comp);
            CollectControllersFromLayerArray(so.FindProperty("baseAnimationLayers"), controllers);
            CollectControllersFromLayerArray(so.FindProperty("specialAnimationLayers"), controllers);
        }
    }

    private static void CollectControllersFromLayerArray(SerializedProperty arrayProp, HashSet<AnimatorController> controllers)
    {
        if (arrayProp == null || !arrayProp.isArray) return;

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty controllerProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("animatorController");
            if (controllerProp != null && controllerProp.objectReferenceValue is AnimatorController ac) controllers.Add(ac);
        }
    }

    private static void CollectControllersFromAnimators(GameObject avatar, HashSet<AnimatorController> controllers)
    {
        foreach (Animator animator in avatar.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController is AnimatorController ac) controllers.Add(ac);
        }
    }
}
#endif