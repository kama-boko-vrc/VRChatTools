#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Animator Controller（FX Layerなど）のレイヤー・パラメータを一覧表示し、
/// どのState/Transition/BlendTreeからも参照されていないパラメータを検出して
/// チェックリストから選択の上で一括削除するエディタ拡張。
/// レイヤー自体は一覧表示のみで削除は行わない。
/// </summary>
public class FXLayerOrganizer : EditorWindow
{
    private AnimatorController controller;

    private readonly HashSet<string> unusedParameterNames = new HashSet<string>();
    private readonly Dictionary<string, bool> selected = new Dictionary<string, bool>();
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
            "Animator Controller（FX Layerなど）のレイヤー・パラメータを一覧表示します。\n" +
            "どのState/Transition/BlendTreeからも参照されていないパラメータは削除候補として\n" +
            "チェックリストに表示されます（レイヤー自体は削除されません）。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Animator Controller");
        controller = (AnimatorController)EditorGUILayout.ObjectField(controller, typeof(AnimatorController), false);
        if (EditorGUI.EndChangeCheck())
        {
            ClearResults();
        }

        EditorGUI.BeginDisabledGroup(controller == null);
        if (GUILayout.Button("スキャン"))
        {
            Scan();
        }
        EditorGUI.EndDisabledGroup();

        if (!hasScanned || controller == null) return;

        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        EditorGUILayout.LabelField($"レイヤー（{controller.layers.Length}件）", EditorStyles.boldLabel);
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            int stateCount = layer.stateMachine != null ? layer.stateMachine.states.Length : 0;
            EditorGUILayout.LabelField($"・{layer.name}（State数: {stateCount}, Weight: {layer.defaultWeight}）");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"パラメータ（全{controller.parameters.Length}件 / 未使用{unusedParameterNames.Count}件）",
            EditorStyles.boldLabel);

        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (unusedParameterNames.Contains(p.name))
            {
                bool current = selected.TryGetValue(p.name, out bool v) && v;
                selected[p.name] = EditorGUILayout.ToggleLeft($"{p.name} ({p.type}) - 未使用", current);
            }
            else
            {
                EditorGUILayout.LabelField($"　{p.name} ({p.type})");
            }
        }

        EditorGUILayout.EndScrollView();

        if (unusedParameterNames.Count == 0) return;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全選択")) SetAllSelected(true);
        if (GUILayout.Button("全解除")) SetAllSelected(false);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("選択した未使用パラメータを削除"))
        {
            DeleteSelected();
        }
    }

    private void ClearResults()
    {
        unusedParameterNames.Clear();
        selected.Clear();
        hasScanned = false;
    }

    private void Scan()
    {
        unusedParameterNames.Clear();
        selected.Clear();

        HashSet<string> used = new HashSet<string>();
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.stateMachine != null) CollectUsedParameters(layer.stateMachine, used);
        }

        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (!used.Contains(p.name))
            {
                unusedParameterNames.Add(p.name);
                selected[p.name] = false;
            }
        }

        hasScanned = true;
        Debug.Log($"[FXLayerOrganizer] スキャン完了: パラメータ{controller.parameters.Length}件中、未使用{unusedParameterNames.Count}件");
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

    private void SetAllSelected(bool value)
    {
        foreach (string name in unusedParameterNames)
        {
            selected[name] = value;
        }
    }

    private void DeleteSelected()
    {
        List<AnimatorControllerParameter> toDelete = new List<AnimatorControllerParameter>();
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (unusedParameterNames.Contains(p.name) && selected.TryGetValue(p.name, out bool v) && v)
            {
                toDelete.Add(p);
            }
        }

        if (toDelete.Count == 0) return;

        Undo.RecordObject(controller, "Delete Unused Parameters");

        foreach (AnimatorControllerParameter p in toDelete)
        {
            controller.RemoveParameter(p);
            unusedParameterNames.Remove(p.name);
        }

        EditorUtility.SetDirty(controller);

        Debug.Log($"[FXLayerOrganizer] 完了: {toDelete.Count}件のパラメータを削除しました");
    }
}
#endif