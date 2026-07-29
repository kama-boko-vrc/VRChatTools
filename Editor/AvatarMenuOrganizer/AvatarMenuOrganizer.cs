#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// アバターを指定すると、Expressions Menu（ラジアルメニュー、サブメニュー含む）と、
/// 使用している全Animator Controller（VRCAvatarDescriptorの各Playable Layer、配下のAnimator）の
/// レイヤー・パラメータをまとめて一覧表示するエディタ拡張。
/// 全メニューコントロール・全レイヤー・全パラメータにチェックボックスがあり、選択したものを一括削除できる。
/// メニューコントロールのチェックをONにすると、そのコントロールが参照するパラメータのチェックも
/// 自動でONになるため、メニューとパラメータを合わせて削除しやすい（パラメータ側は手動で解除可能）。
/// 「未使用」の表示はあくまで目安で、チェックの有効/無効には影響しない。
/// VRCSDKへの直接参照は持たせず、SerializedObject経由でVRCAvatarDescriptor等のフィールドを読む。
/// </summary>
public class AvatarMenuOrganizer : EditorWindow
{
    private class ControlEntry
    {
        public int index;
        public string label;
        public string parameterName;
        public readonly List<string> subParameterNames = new List<string>();
        public bool isUnused;
        public string key;
    }

    private class MenuNode
    {
        public Object menuAsset;
        public string displayPath;
        public readonly List<ControlEntry> controls = new List<ControlEntry>();
    }

    private class ControllerInfo
    {
        public AnimatorController controller;
        public readonly HashSet<string> unusedParameterNames = new HashSet<string>();
        public readonly Dictionary<string, bool> parameterSelected = new Dictionary<string, bool>();
        public readonly HashSet<string> unusedLayerNames = new HashSet<string>();
        public readonly Dictionary<string, bool> layerSelected = new Dictionary<string, bool>();
    }

    private Transform avatarRoot;

    private readonly List<MenuNode> menuNodes = new List<MenuNode>();
    private readonly Dictionary<string, bool> controlSelected = new Dictionary<string, bool>();
    private readonly List<ControllerInfo> scannedControllers = new List<ControllerInfo>();

    private Vector2 scroll;
    private bool hasScanned;

    [MenuItem("Tools/VRChatTools/Avatar Menu Organizer")]
    private static void ShowWindow()
    {
        AvatarMenuOrganizer window = GetWindow<AvatarMenuOrganizer>("Avatar Menu Organizer");
        window.minSize = new Vector2(440, 560);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "アバターを指定すると、Expressions Menu（ラジアルメニュー）と全Animator Controllerの\n" +
            "レイヤー・パラメータをまとめて表示します。全項目にチェックボックスがあり、\n" +
            "メニューコントロールをチェックすると対応するパラメータも自動でチェックされます\n" +
            "（パラメータ側は手動で解除できます）。「未使用」はあくまで目安表示です。",
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

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        DrawMenus();
        EditorGUILayout.Space();
        DrawControllers();

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("選択したメニュー・レイヤー・パラメータを削除"))
        {
            DeleteSelected();
        }
    }

    private void DrawMenus()
    {
        EditorGUILayout.LabelField("Expressions Menu", EditorStyles.boldLabel);

        if (menuNodes.Count == 0)
        {
            EditorGUILayout.HelpBox("Expressions Menuが見つかりませんでした。", MessageType.Info);
            return;
        }

        foreach (MenuNode node in menuNodes)
        {
            EditorGUILayout.LabelField($"{node.displayPath}（{node.controls.Count}件）");

            foreach (ControlEntry entry in node.controls)
            {
                string suffix = entry.isUnused ? " - 未使用" : "";
                bool current = controlSelected.TryGetValue(entry.key, out bool v) && v;

                EditorGUI.BeginChangeCheck();
                bool newValue = EditorGUILayout.ToggleLeft($"　{entry.label}{suffix}", current);
                if (EditorGUI.EndChangeCheck())
                {
                    controlSelected[entry.key] = newValue;
                    if (newValue)
                    {
                        SetParameterSelectedAcrossControllers(entry.parameterName, true);
                        foreach (string sub in entry.subParameterNames)
                        {
                            SetParameterSelectedAcrossControllers(sub, true);
                        }
                    }
                }
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("メニュー全選択", GUILayout.Width(100))) SetAllSelected(controlSelected, true);
        if (GUILayout.Button("メニュー全解除", GUILayout.Width(100))) SetAllSelected(controlSelected, false);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawControllers()
    {
        EditorGUILayout.LabelField("Animator Controller", EditorStyles.boldLabel);

        if (scannedControllers.Count == 0)
        {
            EditorGUILayout.HelpBox("Animator Controllerが見つかりませんでした。", MessageType.Info);
            return;
        }

        foreach (ControllerInfo info in scannedControllers)
        {
            DrawController(info);
            EditorGUILayout.Space();
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
        menuNodes.Clear();
        controlSelected.Clear();
        scannedControllers.Clear();
        hasScanned = false;
    }

    private void Scan()
    {
        menuNodes.Clear();
        controlSelected.Clear();
        scannedControllers.Clear();

        GameObject avatar = avatarRoot.gameObject;

        HashSet<AnimatorController> controllers = new HashSet<AnimatorController>();
        CollectControllersFromAvatarDescriptor(avatar, controllers);
        CollectControllersFromAnimators(avatar, controllers);

        HashSet<string> declaredParameterNames = new HashSet<string>();
        CollectExpressionParameterNames(avatar, declaredParameterNames);
        foreach (AnimatorController controller in controllers)
        {
            foreach (AnimatorControllerParameter p in controller.parameters) declaredParameterNames.Add(p.name);
        }

        HashSet<string> usedByExpressionsMenu = new HashSet<string>();

        Object rootMenu = FindExpressionsMenu(avatar);
        if (rootMenu != null)
        {
            CollectMenuTree(rootMenu, "Root", declaredParameterNames, usedByExpressionsMenu, new HashSet<Object>());
        }

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
        Debug.Log($"[AvatarMenuOrganizer] スキャン完了: メニュー{menuNodes.Count}件 / Animator Controller{scannedControllers.Count}件");
    }

    private void CollectMenuTree(Object menu, string path, HashSet<string> declaredParams,
        HashSet<string> usedByExpressionsMenu, HashSet<Object> visited)
    {
        if (menu == null || !visited.Add(menu)) return; // サブメニューの循環参照対策

        MenuNode node = new MenuNode { menuAsset = menu, displayPath = path };
        menuNodes.Add(node);

        SerializedObject so = new SerializedObject(menu);
        SerializedProperty controlsProp = so.FindProperty("controls");
        if (controlsProp == null || !controlsProp.isArray) return;

        for (int i = 0; i < controlsProp.arraySize; i++)
        {
            SerializedProperty control = controlsProp.GetArrayElementAtIndex(i);

            string name = control.FindPropertyRelative("name")?.stringValue ?? "";
            SerializedProperty typeProp = control.FindPropertyRelative("type");
            string typeName = "?";
            if (typeProp != null && typeProp.enumValueIndex >= 0 && typeProp.enumValueIndex < typeProp.enumDisplayNames.Length)
            {
                typeName = typeProp.enumDisplayNames[typeProp.enumValueIndex];
            }

            SerializedProperty paramProp = control.FindPropertyRelative("parameter");
            string paramName = paramProp?.FindPropertyRelative("name")?.stringValue ?? "";
            if (!string.IsNullOrEmpty(paramName)) usedByExpressionsMenu.Add(paramName);

            List<string> subParameterNames = new List<string>();
            SerializedProperty subParamsProp = control.FindPropertyRelative("subParameters");
            if (subParamsProp != null && subParamsProp.isArray)
            {
                for (int j = 0; j < subParamsProp.arraySize; j++)
                {
                    SerializedProperty subNameProp = subParamsProp.GetArrayElementAtIndex(j).FindPropertyRelative("name");
                    string subName = subNameProp?.stringValue;
                    if (!string.IsNullOrEmpty(subName))
                    {
                        subParameterNames.Add(subName);
                        usedByExpressionsMenu.Add(subName);
                    }
                }
            }

            SerializedProperty subMenuProp = control.FindPropertyRelative("subMenu");
            Object subMenu = subMenuProp?.objectReferenceValue;

            bool subMenuIsEmpty = false;
            if (typeName == "SubMenu")
            {
                if (subMenu == null)
                {
                    subMenuIsEmpty = true;
                }
                else
                {
                    CollectMenuTree(subMenu, $"{path}/{name}", declaredParams, usedByExpressionsMenu, visited);
                    MenuNode childNode = menuNodes.Find(n => n.menuAsset == subMenu);
                    subMenuIsEmpty = childNode != null && childNode.controls.Count == 0;
                }
            }

            bool isUnusedParam = !string.IsNullOrEmpty(paramName) && !declaredParams.Contains(paramName);
            bool isUnused = isUnusedParam || subMenuIsEmpty;

            string label = string.IsNullOrEmpty(paramName)
                ? $"{name} [{typeName}]"
                : $"{name} [{typeName}] (param: {paramName})";

            string key = $"{menu.GetInstanceID()}_{i}";
            ControlEntry entry = new ControlEntry
            {
                index = i,
                label = label,
                parameterName = paramName,
                isUnused = isUnused,
                key = key
            };
            entry.subParameterNames.AddRange(subParameterNames);
            node.controls.Add(entry);
            controlSelected[key] = false;
        }
    }

    private static Object FindExpressionsMenu(GameObject avatar)
    {
        foreach (Component comp in avatar.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "VRCAvatarDescriptor") continue;

            SerializedObject so = new SerializedObject(comp);
            SerializedProperty menuProp = so.FindProperty("expressionsMenu");
            if (menuProp != null && menuProp.objectReferenceValue != null) return menuProp.objectReferenceValue;
        }
        return null;
    }

    private static void CollectExpressionParameterNames(GameObject avatar, HashSet<string> names)
    {
        foreach (Component comp in avatar.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "VRCAvatarDescriptor") continue;

            SerializedObject so = new SerializedObject(comp);
            Object paramsAsset = so.FindProperty("expressionParameters")?.objectReferenceValue;
            if (paramsAsset == null) continue;

            SerializedObject paramsSo = new SerializedObject(paramsAsset);
            SerializedProperty parametersProp = paramsSo.FindProperty("parameters");
            if (parametersProp == null || !parametersProp.isArray) continue;

            for (int i = 0; i < parametersProp.arraySize; i++)
            {
                SerializedProperty nameProp = parametersProp.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)) names.Add(nameProp.stringValue);
            }
        }
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

    private void SetParameterSelectedAcrossControllers(string paramName, bool value)
    {
        if (string.IsNullOrEmpty(paramName)) return;

        foreach (ControllerInfo info in scannedControllers)
        {
            if (info.parameterSelected.ContainsKey(paramName)) info.parameterSelected[paramName] = value;
        }
    }

    private static void SetAllSelected(Dictionary<string, bool> selection, bool value)
    {
        foreach (string key in new List<string>(selection.Keys))
        {
            selection[key] = value;
        }
    }

    private void DeleteSelected()
    {
        int deletedControlCount = 0;
        int deletedLayerCount = 0;
        int deletedParameterCount = 0;

        Undo.SetCurrentGroupName("Delete Selected Menu/Layers/Parameters");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (MenuNode node in menuNodes)
        {
            List<int> indicesToDelete = new List<int>();
            foreach (ControlEntry entry in node.controls)
            {
                if (controlSelected.TryGetValue(entry.key, out bool v) && v) indicesToDelete.Add(entry.index);
            }

            if (indicesToDelete.Count == 0) continue;

            indicesToDelete.Sort();
            indicesToDelete.Reverse(); // 降順に削除してインデックスのズレを防ぐ

            Undo.RecordObject(node.menuAsset, "Delete Menu Controls");

            SerializedObject so = new SerializedObject(node.menuAsset);
            SerializedProperty controlsProp = so.FindProperty("controls");

            foreach (int index in indicesToDelete)
            {
                controlsProp.DeleteArrayElementAtIndex(index);
                deletedControlCount++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(node.menuAsset);
        }

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
                deletedLayerCount++;
            }

            foreach (AnimatorControllerParameter p in parametersToDelete)
            {
                controller.RemoveParameter(p);
                deletedParameterCount++;
            }

            EditorUtility.SetDirty(controller);
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[AvatarMenuOrganizer] 完了: メニュー{deletedControlCount}件 / " +
                  $"レイヤー{deletedLayerCount}件 / パラメータ{deletedParameterCount}件を削除しました");

        Scan();
    }
}
#endif