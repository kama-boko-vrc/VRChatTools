#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 複数のプレハブを登録しておき、ヒエラルキーの右クリックメニュー（GameObjectメニュー）の
/// 「prefabを配置」サブメニューから選んでワンクリック配置できるエディタ拡張。
/// サブメニュー項目は登録内容に応じてMenu.AddMenuItemで動的に生成される。
/// 登録内容はEditorPrefsに保存され、プロジェクト内で永続化される（マシン/ユーザーごと）。
/// </summary>
public class QuickPrefabPlacer : EditorWindow
{
    private const string PrefsKey = "VRChatTools.QuickPrefabPlacer.PrefabGuids";
    private const string MenuRoot = "GameObject/prefabを配置/";

    [Serializable]
    private class GuidListWrapper
    {
        public List<string> guids = new List<string>();
    }

    private static readonly List<string> registeredMenuPaths = new List<string>();

    private readonly List<GameObject> prefabs = new List<GameObject>();

    [MenuItem("Tools/VRChatTools/Quick Prefab Placer")]
    private static void ShowWindow()
    {
        GetWindow<QuickPrefabPlacer>("Quick Prefab Placer");
    }

    private void OnEnable()
    {
        prefabs.Clear();
        prefabs.AddRange(LoadPrefabs());
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "ここで登録したプレハブは、ヒエラルキーを右クリックした際に表示される\n" +
            "「GameObject > prefabを配置」のサブメニューから選んで配置できます\n" +
            "（選択中のオブジェクトの子として配置。未選択時はシーン直下）。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < prefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                prefabs.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ プレハブを追加"))
        {
            prefabs.Add(null);
        }

        if (EditorGUI.EndChangeCheck())
        {
            SavePrefabs(prefabs);
            RebuildMenu();
        }
    }

    private static List<GameObject> LoadPrefabs()
    {
        List<GameObject> result = new List<GameObject>();

        string json = EditorPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return result;

        GuidListWrapper wrapper = JsonUtility.FromJson<GuidListWrapper>(json);
        if (wrapper?.guids == null) return result;

        foreach (string guid in wrapper.guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            result.Add(prefab);
        }

        return result;
    }

    private static void SavePrefabs(List<GameObject> value)
    {
        GuidListWrapper wrapper = new GuidListWrapper();
        foreach (GameObject prefab in value)
        {
            if (prefab == null) continue;

            string path = AssetDatabase.GetAssetPath(prefab);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid)) wrapper.guids.Add(guid);
        }

        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(wrapper));
    }

    // 登録中のプレハブ一覧に合わせて、「GameObject/prefabを配置/」配下の
    // サブメニュー項目を作り直す（起動時・登録内容の変更時に呼ばれる）。
    // Menu.AddMenuItem/RemoveMenuItemはUnity内部APIでpublicではないため、リフレクション経由で呼び出す。
    internal static void RebuildMenu()
    {
        foreach (string path in registeredMenuPaths)
        {
            InvokeRemoveMenuItem(path);
        }
        registeredMenuPaths.Clear();

        List<GameObject> registered = LoadPrefabs();
        registered.RemoveAll(p => p == null);

        int priority = 0;
        foreach (GameObject prefab in registered)
        {
            string menuPath = MenuRoot + prefab.name;

            InvokeAddMenuItem(menuPath, priority++, () => PlaceFromSelection(prefab), () => prefab != null);
            registeredMenuPaths.Add(menuPath);
        }
    }

    private static readonly MethodInfo AddMenuItemMethod =
        typeof(Menu).GetMethod("AddMenuItem", BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly MethodInfo RemoveMenuItemMethod =
        typeof(Menu).GetMethod("RemoveMenuItem", BindingFlags.Static | BindingFlags.NonPublic);

    private static void InvokeAddMenuItem(string name, int priority, Action execute, Func<bool> validate)
    {
        AddMenuItemMethod?.Invoke(null, new object[] { name, "", false, priority, execute, validate });
    }

    private static void InvokeRemoveMenuItem(string name)
    {
        RemoveMenuItemMethod?.Invoke(null, new object[] { name });
    }

    private static void PlaceFromSelection(GameObject prefabAsset)
    {
        GameObject parent = Selection.activeGameObject;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        instance.name = GetUniqueName(parent != null ? parent.transform : null, prefabAsset.name);

        if (parent != null)
        {
            Undo.SetTransformParent(instance.transform, parent.transform, "Place Quick Prefab");
        }

        Undo.RegisterCreatedObjectUndo(instance, "Place Quick Prefab");
        Selection.activeGameObject = instance;
    }

    // 配置先の兄弟（parentがnullならシーン直下）に同名オブジェクトがある場合、
    // Unityの重複名規則に倣い "(1)", "(2)"... を付けて重複しない名前にする。
    private static string GetUniqueName(Transform parent, string baseName)
    {
        HashSet<string> existingNames = new HashSet<string>();

        if (parent != null)
        {
            foreach (Transform child in parent)
            {
                existingNames.Add(child.name);
            }
        }
        else
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                existingNames.Add(root.name);
            }
        }

        if (!existingNames.Contains(baseName)) return baseName;

        int index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName} ({index})";
            index++;
        } while (existingNames.Contains(candidate));

        return candidate;
    }
}

// [InitializeOnLoad]の静的コンストラクタからEditorPrefs等のUnity APIを呼ぶと、
// QuickPrefabPlacer（ScriptableObjectを継承するEditorWindow）自身の静的コンストラクタ内では
// 「ScriptableObjectのコンストラクタから呼べない」エラーになるため、
// ScriptableObjectを継承しない別クラスに初期化処理を分離する。
[InitializeOnLoad]
internal static class QuickPrefabPlacerMenuInitializer
{
    static QuickPrefabPlacerMenuInitializer()
    {
        QuickPrefabPlacer.RebuildMenu();
    }
}
#endif