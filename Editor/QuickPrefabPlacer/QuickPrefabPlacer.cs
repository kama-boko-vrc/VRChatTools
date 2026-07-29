#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 複数のプレハブを登録しておき、ヒエラルキーの右クリックメニュー（GameObjectメニュー）から
/// 「クイックプレハブを配置...」を選ぶと、登録済みプレハブの一覧から選択して配置できるようにする
/// エディタ拡張。登録内容はEditorPrefsに保存され、プロジェクト内で永続化される（マシン/ユーザーごと）。
/// </summary>
public class QuickPrefabPlacer : EditorWindow
{
    private const string PrefsKey = "VRChatTools.QuickPrefabPlacer.PrefabGuids";

    [System.Serializable]
    private class GuidListWrapper
    {
        public List<string> guids = new List<string>();
    }

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
            "「GameObject > クイックプレハブを配置...」から一覧選択で配置できます\n" +
            "（右クリックした対象の子として配置。未選択時はシーン直下）。",
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

    [MenuItem("GameObject/クイックプレハブを配置...", false, 0)]
    private static void ShowQuickPrefabMenu(MenuCommand command)
    {
        List<GameObject> registered = LoadPrefabs();
        registered.RemoveAll(p => p == null);

        if (registered.Count == 0)
        {
            Debug.LogWarning("[QuickPrefabPlacer] 配置できるプレハブが登録されていません。" +
                             "Tools > VRChatTools > Quick Prefab Placer で登録してください。");
            return;
        }

        GameObject parent = command.context as GameObject;

        GenericMenu menu = new GenericMenu();
        foreach (GameObject prefab in registered)
        {
            GameObject capturedPrefab = prefab;
            menu.AddItem(new GUIContent(capturedPrefab.name), false, () => PlacePrefab(capturedPrefab, parent));
        }
        menu.ShowAsContext();
    }

    [MenuItem("GameObject/クイックプレハブを配置...", true)]
    private static bool ValidateShowQuickPrefabMenu()
    {
        List<GameObject> registered = LoadPrefabs();
        registered.RemoveAll(p => p == null);
        return registered.Count > 0;
    }

    private static void PlacePrefab(GameObject prefabAsset, GameObject parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);

        if (parent != null)
        {
            Undo.SetTransformParent(instance.transform, parent.transform, "Place Quick Prefab");
        }

        Undo.RegisterCreatedObjectUndo(instance, "Place Quick Prefab");
        Selection.activeGameObject = instance;
    }
}
#endif