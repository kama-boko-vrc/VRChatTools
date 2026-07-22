#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// アバターを指定すると、そのアバターが使う全Animator Controller
/// （VRCAvatarDescriptorの各Playable Layer、および配下のAnimatorコンポーネント）を自動収集し、
/// レイヤー・パラメータを一覧表示するエディタ拡張。
/// 全レイヤー・全パラメータにチェックボックスがあり、選択したものを一括削除できる。
/// どのState/Transition/BlendTree/Expressions Menu（ラジアルメニュー、サブメニュー含む）からも
/// 参照されていないパラメータ、Stateを持たないレイヤーは「未使用」として表示されるが、
/// あくまで目安でありチェックの有効/無効には影響しない。
/// VRCSDKへの直接参照は持たせず、SerializedObject経由でVRCAvatarDescriptorのフィールドを読む。
/// </summary>
public class FXLayerOrganizer : EditorWindow
{
    private class ControllerInfo
    {
        public AnimatorController controller;
        public readonly HashSet<string> unusedParameterNames = new HashSet<string>();
        public readonly Dictionary<string, bool> parameterSelected = new Dictionary<string, bool>();
        public readonly HashSet<string> unusedLayerNames = new HashSet<string>();
        public readonly Dictionary<string, bool> layerSelected = new Dictionary<string, bool>();
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
            "レイヤー・パラメータを一覧表示します。全レイヤー・全パラメータにチェックボックスがあり、\n" +
            "「未使用」の表示はあくまで目安です（チェックすればレイヤーも削除されるため注意してください）。",
            MessageType.Warning);

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

        if (GUILayout.Button("選択したレイヤー・パラメータを削除（全Controller対象）"))
        {
            DeleteSelected();
        }
    }

    private void DrawController(ControllerInfo info)
    {
        AnimatorController controller = info.controller;

        EditorGUILayout.LabelField(controller.name, EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"レイヤー（{controller.layers.Length}件 / 未使用{info.unusedLayerNames.Count}件）");
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            int stateCount = layer.stateMachine != null ? layer.stateMachine.states.Length : 0;
            string suffix = info.unusedLayerNames.Contains(layer.name) ? " - 未使用" : "";
            string label = $"　{layer.name}（State数: {stateCount}, Weight: {layer.defaultWeight}）{suffix}";

            bool current = info.layerSelected.TryGetValue(layer.name, out bool v) && v;
            info.layerSelected[layer.name] = EditorGUILayout.ToggleLeft(label, current);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("レイヤー全選択", GUILayout.Width(100))) SetAllSelected(info.layerSelected, true);
        if (GUILayout.Button("レイヤー全解除", GUILayout.Width(100))) SetAllSelected(info.layerSelected, false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"パラメータ（全{controller.parameters.Length}件 / 未使用{info.unusedParameterNames.Count}件）");

        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            string suffix = info.unusedParameterNames.Contains(p.name) ? " - 未使用" : "";
            string label = $"　{p.name} ({p.type}){suffix}";

            bool current = info.parameterSelected.TryGetValue(p.name, out bool v) && v;
            info.parameterSelected[p.name] = EditorGUILayout.ToggleLeft(label, current);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("パラメータ全選択", GUILayout.Width(100))) SetAllSelected(info.parameterSelected, true);
        if (GUILayout.Button("パラメータ全解除", GUILayout.Width(100))) SetAllSelected(info.parameterSelected, false);
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

        HashSet<string> usedByExpressionsMenu = new HashSet<string>();
        CollectUsedParametersFromExpressionsMenu(avatarRoot.gameObject, usedByExpressionsMenu);

        foreach (AnimatorController controller in controllers)
        {
            ControllerInfo info = new ControllerInfo { controller = controller };

            HashSet<string> used = new HashSet<string>(usedByExpressionsMenu);
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine != null) CollectUsedParameters(layer.stateMachine, used);
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                bool isEmpty = layer.stateMachine == null || layer.stateMachine.states.Length == 0;
                if (isEmpty) info.unusedLayerNames.Add(layer.name);
                info.layerSelected[layer.name] = false;
            }

            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (!used.Contains(p.name)) info.unusedParameterNames.Add(p.name);
                info.parameterSelected[p.name] = false;
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

    private static void CollectUsedParametersFromExpressionsMenu(GameObject avatar, HashSet<string> used)
    {
        foreach (Component comp in avatar.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "VRCAvatarDescriptor") continue;

            SerializedObject so = new SerializedObject(comp);
            SerializedProperty menuProp = so.FindProperty("expressionsMenu");
            if (menuProp == null) continue;

            UnityEngine.Object menu = menuProp.objectReferenceValue;
            if (menu != null) CollectUsedParametersFromMenu(menu, used, new HashSet<UnityEngine.Object>());
        }
    }

    private static void CollectUsedParametersFromMenu(UnityEngine.Object menu, HashSet<string> used, HashSet<UnityEngine.Object> visited)
    {
        if (menu == null || !visited.Add(menu)) return; // サブメニューの循環参照対策

        SerializedObject so = new SerializedObject(menu);
        SerializedProperty controlsProp = so.FindProperty("controls");
        if (controlsProp == null || !controlsProp.isArray) return;

        for (int i = 0; i < controlsProp.arraySize; i++)
        {
            SerializedProperty control = controlsProp.GetArrayElementAtIndex(i);

            SerializedProperty paramProp = control.FindPropertyRelative("parameter");
            SerializedProperty nameProp = paramProp?.FindPropertyRelative("name");
            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)) used.Add(nameProp.stringValue);

            SerializedProperty subParamsProp = control.FindPropertyRelative("subParameters");
            if (subParamsProp != null && subParamsProp.isArray)
            {
                for (int j = 0; j < subParamsProp.arraySize; j++)
                {
                    SerializedProperty subName = subParamsProp.GetArrayElementAtIndex(j).FindPropertyRelative("name");
                    if (subName != null && !string.IsNullOrEmpty(subName.stringValue)) used.Add(subName.stringValue);
                }
            }

            SerializedProperty subMenuProp = control.FindPropertyRelative("subMenu");
            if (subMenuProp != null && subMenuProp.objectReferenceValue != null)
            {
                CollectUsedParametersFromMenu(subMenuProp.objectReferenceValue, used, visited);
            }
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

    private static void SetAllSelected(Dictionary<string, bool> selection, bool value)
    {
        foreach (string name in new List<string>(selection.Keys))
        {
            selection[name] = value;
        }
    }

    private void DeleteSelected()
    {
        int deletedParameterCount = 0;
        int deletedLayerCount = 0;

        foreach (ControllerInfo info in scannedControllers)
        {
            AnimatorController controller = info.controller;

            List<string> layerNamesToDelete = new List<string>();
            foreach (KeyValuePair<string, bool> kv in info.layerSelected)
            {
                if (kv.Value) layerNamesToDelete.Add(kv.Key);
            }

            List<AnimatorControllerParameter> parametersToDelete = new List<AnimatorControllerParameter>();
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (info.parameterSelected.TryGetValue(p.name, out bool v) && v)
                {
                    parametersToDelete.Add(p);
                }
            }

            if (layerNamesToDelete.Count == 0 && parametersToDelete.Count == 0) continue;

            Undo.RecordObject(controller, "Delete Selected Layers/Parameters");

            foreach (string layerName in layerNamesToDelete)
            {
                int index = System.Array.FindIndex(controller.layers, l => l.name == layerName);
                if (index < 0) continue;

                controller.RemoveLayer(index);
                info.layerSelected.Remove(layerName);
                info.unusedLayerNames.Remove(layerName);
                deletedLayerCount++;
            }

            foreach (AnimatorControllerParameter p in parametersToDelete)
            {
                controller.RemoveParameter(p);
                info.parameterSelected.Remove(p.name);
                info.unusedParameterNames.Remove(p.name);
                deletedParameterCount++;
            }

            EditorUtility.SetDirty(controller);
        }

        Debug.Log($"[FXLayerOrganizer] 完了: レイヤー{deletedLayerCount}件 / パラメータ{deletedParameterCount}件を削除しました");
    }
}
#endif