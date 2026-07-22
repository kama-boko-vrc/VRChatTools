#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// アバターを指定すると、Expressions Menu（ラジアルメニュー）をサブメニューまで含めてツリー表示するエディタ拡張。
/// 全コントロールにチェックボックスがあり、選択したものを一括削除できる。
/// 参照先パラメータが見つからないコントロールや、空/欠落したサブメニューへの参照は「未使用」として
/// 表示されるが、あくまで目安でありチェックの有効/無効には影響しない。
/// VRCSDKへの直接参照は持たせず、SerializedObject経由でVRCAvatarDescriptor/Expressions Menuのフィールドを読む。
/// </summary>
public class RadialMenuOrganizer : EditorWindow
{
    private class ControlEntry
    {
        public int index;
        public string label;
        public bool isUnused;
        public string key;
    }

    private class MenuNode
    {
        public Object menuAsset;
        public string displayPath;
        public readonly List<ControlEntry> controls = new List<ControlEntry>();
    }

    private Transform avatarRoot;
    private readonly List<MenuNode> menuNodes = new List<MenuNode>();
    private readonly Dictionary<string, bool> selected = new Dictionary<string, bool>();
    private Vector2 scroll;
    private bool hasScanned;

    [MenuItem("Tools/VRChatTools/Radial Menu Organizer")]
    private static void ShowWindow()
    {
        RadialMenuOrganizer window = GetWindow<RadialMenuOrganizer>("Radial Menu Organizer");
        window.minSize = new Vector2(420, 500);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "アバターを指定すると、Expressions Menu（ラジアルメニュー）をサブメニューまで含めて\n" +
            "ツリー表示します。全コントロールにチェックボックスがあり、選択したものを一括削除できます。\n" +
            "参照先パラメータが見つからない、または空/欠落したサブメニューへの参照は「未使用」として\n" +
            "表示されますが、あくまで目安でありチェックの有効/無効には影響しません。",
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

        if (menuNodes.Count == 0)
        {
            EditorGUILayout.HelpBox("Expressions Menuが見つかりませんでした。", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        foreach (MenuNode node in menuNodes)
        {
            EditorGUILayout.LabelField($"{node.displayPath}（{node.controls.Count}件）", EditorStyles.boldLabel);

            foreach (ControlEntry entry in node.controls)
            {
                string suffix = entry.isUnused ? " - 未使用" : "";
                bool current = selected.TryGetValue(entry.key, out bool v) && v;
                selected[entry.key] = EditorGUILayout.ToggleLeft($"　{entry.label}{suffix}", current);
            }

            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全選択")) SetAllSelected(true);
        if (GUILayout.Button("全解除")) SetAllSelected(false);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("選択したコントロールを削除"))
        {
            DeleteSelected();
        }
    }

    private void ClearResults()
    {
        menuNodes.Clear();
        selected.Clear();
        hasScanned = false;
    }

    private void Scan()
    {
        menuNodes.Clear();
        selected.Clear();

        Object rootMenu = FindExpressionsMenu(avatarRoot.gameObject);
        if (rootMenu != null)
        {
            HashSet<string> validParameterNames = new HashSet<string>();
            CollectExpressionParameterNames(avatarRoot.gameObject, validParameterNames);
            CollectAnimatorParameterNames(avatarRoot.gameObject, validParameterNames);

            CollectMenuTree(rootMenu, "Root", validParameterNames, new HashSet<Object>());
        }

        hasScanned = true;
        Debug.Log($"[RadialMenuOrganizer] スキャン完了: メニュー{menuNodes.Count}件");
    }

    private void CollectMenuTree(Object menu, string path, HashSet<string> validParams, HashSet<Object> visited)
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
                    CollectMenuTree(subMenu, $"{path}/{name}", validParams, visited);
                    MenuNode childNode = menuNodes.Find(n => n.menuAsset == subMenu);
                    subMenuIsEmpty = childNode != null && childNode.controls.Count == 0;
                }
            }

            bool isUnusedParam = !string.IsNullOrEmpty(paramName) && !validParams.Contains(paramName);
            bool isUnused = isUnusedParam || subMenuIsEmpty;

            string label = string.IsNullOrEmpty(paramName)
                ? $"{name} [{typeName}]"
                : $"{name} [{typeName}] (param: {paramName})";

            node.controls.Add(new ControlEntry
            {
                index = i,
                label = label,
                isUnused = isUnused,
                key = $"{menu.GetInstanceID()}_{i}"
            });
            selected[$"{menu.GetInstanceID()}_{i}"] = false;
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

    private static void CollectAnimatorParameterNames(GameObject avatar, HashSet<string> names)
    {
        HashSet<AnimatorController> controllers = new HashSet<AnimatorController>();

        foreach (Component comp in avatar.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "VRCAvatarDescriptor") continue;

            SerializedObject so = new SerializedObject(comp);
            CollectControllersFromLayerArray(so.FindProperty("baseAnimationLayers"), controllers);
            CollectControllersFromLayerArray(so.FindProperty("specialAnimationLayers"), controllers);
        }

        foreach (Animator animator in avatar.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController is AnimatorController ac) controllers.Add(ac);
        }

        foreach (AnimatorController controller in controllers)
        {
            foreach (AnimatorControllerParameter p in controller.parameters) names.Add(p.name);
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

    private void SetAllSelected(bool value)
    {
        foreach (string key in new List<string>(selected.Keys))
        {
            selected[key] = value;
        }
    }

    private void DeleteSelected()
    {
        int deletedCount = 0;

        foreach (MenuNode node in menuNodes)
        {
            List<int> indicesToDelete = new List<int>();
            foreach (ControlEntry entry in node.controls)
            {
                if (selected.TryGetValue(entry.key, out bool v) && v) indicesToDelete.Add(entry.index);
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
                deletedCount++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(node.menuAsset);
        }

        Debug.Log($"[RadialMenuOrganizer] 完了: {deletedCount}件のコントロールを削除しました");

        Scan();
    }
}
#endif