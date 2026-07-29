#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Transform階層に関する共通ヘルパー。複数のツールから再利用される。
/// </summary>
internal static class TransformUtility
{
    /// <summary>
    /// 指定した親（nullならシーン直下）の兄弟に同名オブジェクトがある場合、Unityの重複名規則に
    /// 倣い "(1)", "(2)"... を付けて重複しない名前を返す。同名がなければbaseNameをそのまま返す。
    /// </summary>
    public static string GetUniqueSiblingName(Transform parent, string baseName)
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
#endif