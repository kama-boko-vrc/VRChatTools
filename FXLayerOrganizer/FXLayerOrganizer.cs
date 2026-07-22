#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// アバターを指定すると、そのアバターが使う全Animator Controller
/// （VRCAvatarDescriptorの各Playable Layer、および配下のAnimatorコンポーネント）を自動収集し、
/// レイヤー・パラメータを一覧表示するエディタ拡張。
/// どのState/Transition/BlendTreeからも参照されていないパラメータはチェックリストから選択の上で
/// 一括削除できる（レイヤー自体は一覧表示のみで削除しない）。
/// VRCSDKへの直接参照は持たせず、SerializedObject経由でVRCAvatarDescriptorのフィールドを読む。
/// </summary>
public class FXLayerOrganizer : EditorWindow
{
    private class ControllerInfo
    {
        public AnimatorController controller;
        public readonly HashSet<string> unusedParameterNames = new HashSet<string>();
        public readonly Dictionary<string, bool> selected = new Dictionary<string, bool>();
    }

    private Transform avatarRoot;
    private readonly List<ControllerInfo> scannedControllers = new List<ControllerInfo>();
    private Vector2 scroll;
    private bool hasScanned;

    [MenuItem("Tools/VRChatTools/FX Layer Organizer")]
    private static void ShowWindow()
    {
        FXLayerOrganizer window = GetWindow<FXLayerOrganizer>("FX Layer Organizer");
        window.minSize = new Vector2(420, 500);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "アバターを指定すると、使用している全Animator Controller\n" +
            "（VRCAvatarDescriptorの各Playable Layer、配下のAnimator）を自動収集し、\n" +
            "レイヤー・パラメータを一覧表示します。どのState/Transition/BlendTreeからも\n" +
            "参照されていないパラメータは削除候補としてチェックリストに表示されます\n" +
            "（レイヤー自体は削除されません）。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("アバタールート");
        avatarRoot = (Transform)EditorGUILayout.ObjectField(avatarRoot, typeof(Transform), true);
        if (EditorGUI.EndChangeCheck())
        {
            ClearResults();
        }

        EditorGUI.BeginDisabledGroup(avatarRoot == null);
        if (GUILayout.Button("スキャン"))
        {
            Scan();
        }
        EditorGUI.EndDisabledGroup();

        if (!hasScanned) return;

        EditorGUILayout.Space();

        if (scannedControllers.Count == 0)
        {
            EditorGUILayout.HelpBox("Animator Controllerが見つかりませんでした。", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        foreach (ControllerInfo info in scannedControllers)
        {
            DrawController(info);
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("選択した未使用パラメータを削除（全Controller対象）"))
        {
            DeleteSelected();
        }
    }

    private void DrawController(ControllerInfo info)
    {
        AnimatorController controller = info.controller;

        EditorGUILayout.LabelField(controller.name, EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"レイヤー（{controller.layers.Length}件）");
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            int stateCount = layer.stateMachine != null ? layer.stateMachine.states.Length : 0;
            EditorGUILayout.LabelField($"　・{layer.name}（State数: {stateCount}, Weight: {layer.defaultWeight}）");
        }

        EditorGUILayout.LabelField(
            $"パラメータ（全{controller.parameters.Length}件 / 未使用{info.unusedParameterNames.Count}件）");

        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (info.unusedParameterNames.Contains(p.name))
            {
                bool current = info.selected.TryGetValue(p.name, out bool v) && v;
                info.selected[p.name] = EditorGUILayout.ToggleLeft($"　{p.name} ({p.type}) - 未使用", current);
            }
            else
            {
                EditorGUILayout.LabelField($"　　{p.name} ({p.type})");
            }
        }

        if (info.unusedParameterNames.Count == 0) return;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全選択", GUILayout.Width(80))) SetAllSelected(info, true);
        if (GUILayout.Button("全解除", GUILayout.Width(80))) SetAllSelected(info, false);
        EditorGUILayout.EndHorizontal();
    }

    private void ClearResults()
    {
        scannedControllers.Clear();
        hasScanned = false;
    }

    private void Scan()
    {
        scannedControllers.Clear();

        HashSet<AnimatorController> controllers = new HashSet<AnimatorController>();
        CollectControllersFromAvatarDescriptor(avatarRoot.gameObject, controllers);
        CollectControllersFromAnimators(avatarRoot.gameObject, controllers);

        foreach (AnimatorController controller in controllers)
        {
            ControllerInfo info = new ControllerInfo { controller = controller };

            HashSet<string> used = new HashSet<string>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine != null) CollectUsedParameters(layer.stateMachine, used);
            }

            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (!used.Contains(p.name))
                {
                    info.unusedParameterNames.Add(p.name);
                    info.selected[p.name] = false;
                }
            }

            scannedControllers.Add(info);
        }

        hasScanned = true;
        Debug.Log($"[FXLayerOrganizer] スキャン完了: Animator Controller {scannedControllers.Count}件");
    }

    private static void CollectControllersFromAvatarDescriptor(GameObject avatar, HashSet<AnimatorController> controllers)
    {
        foreach (Component comp in avatar.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "VRCAvatarDescriptor") continue;

            SerializedObject so = new SerializedObject(comp);
            CollectFromLayerArray(so.FindProperty("baseAnimationLayers"), controllers);
            CollectFromLayerArray(so.FindProperty("specialAnimationLayers"), controllers);
        }
    }

    private static void CollectFromLayerArray(SerializedProperty arrayProp, HashSet<AnimatorController> controllers)
    {
        if (arrayProp == null || !arrayProp.isArray) return;

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
            SerializedProperty controllerProp = element.FindPropertyRelative("animatorController");
            if (controllerProp == null) continue;

            if (controllerProp.objectReferenceValue is AnimatorController ac) controllers.Add(ac);
        }
    }

    private static void CollectControllersFromAnimators(GameObject avatar, HashSet<AnimatorController> controllers)
    {
        foreach (Animator animator in avatar.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController is AnimatorController ac) controllers.Add(ac);
        }
    }

    private static void CollectUsedParameters(AnimatorStateMachine sm, HashSet<string> used)
    {
        foreach (AnimatorStateTransition t in sm.anyStateTransitions) CollectFromTransition(t, used);
        foreach (AnimatorTransition t in sm.entryTransitions) CollectFromTransition(t, used);

        foreach (ChildAnimatorState childState in sm.states)
        {
            AnimatorState state = childState.state;
            CollectFromState(state, used);
            foreach (AnimatorStateTransition t in state.transitions) CollectFromTransition(t, used);
        }

        foreach (ChildAnimatorStateMachine childSM in sm.stateMachines)
        {
            foreach (AnimatorTransition t in sm.GetStateMachineTransitions(childSM.stateMachine))
            {
                CollectFromTransition(t, used);
            }
            CollectUsedParameters(childSM.stateMachine, used);
        }
    }

    private static void CollectFromTransition(AnimatorTransitionBase transition, HashSet<string> used)
    {
        foreach (AnimatorCondition cond in transition.conditions)
        {
            used.Add(cond.parameter);
        }
    }

    private static void CollectFromState(AnimatorState state, HashSet<string> used)
    {
        if (state.timeParameterActive) used.Add(state.timeParameter);
        if (state.speedParameterActive) used.Add(state.speedParameter);
        if (state.cycleOffsetParameterActive) used.Add(state.cycleOffsetParameter);
        if (state.mirrorParameterActive) used.Add(state.mirrorParameter);

        if (state.motion is BlendTree tree) CollectFromBlendTree(tree, used);
    }

    private static void CollectFromBlendTree(BlendTree tree, HashSet<string> used)
    {
        if (!string.IsNullOrEmpty(tree.blendParameter)) used.Add(tree.blendParameter);
        if (!string.IsNullOrEmpty(tree.blendParameterY)) used.Add(tree.blendParameterY);

        foreach (ChildMotion child in tree.children)
        {
            if (!string.IsNullOrEmpty(child.directBlendParameter)) used.Add(child.directBlendParameter);
            if (child.motion is BlendTree childTree) CollectFromBlendTree(childTree, used);
        }
    }

    private static void SetAllSelected(ControllerInfo info, bool value)
    {
        foreach (string name in info.unusedParameterNames)
        {
            info.selected[name] = value;
        }
    }

    private void DeleteSelected()
    {
        int deletedCount = 0;

        foreach (ControllerInfo info in scannedControllers)
        {
            AnimatorController controller = info.controller;

            List<AnimatorControllerParameter> toDelete = new List<AnimatorControllerParameter>();
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (info.unusedParameterNames.Contains(p.name) &&
                    info.selected.TryGetValue(p.name, out bool v) && v)
                {
                    toDelete.Add(p);
                }
            }

            if (toDelete.Count == 0) continue;

            Undo.RecordObject(controller, "Delete Unused Parameters");

            foreach (AnimatorControllerParameter p in toDelete)
            {
                controller.RemoveParameter(p);
                info.unusedParameterNames.Remove(p.name);
            }

            EditorUtility.SetDirty(controller);
            deletedCount += toDelete.Count;
        }

        Debug.Log($"[FXLayerOrganizer] 完了: {deletedCount}件のパラメータを削除しました");
    }
}
#endif