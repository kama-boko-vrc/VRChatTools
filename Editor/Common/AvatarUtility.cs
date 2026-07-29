#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// VRCAvatarDescriptorが持つ情報（Animator Controller、Expressions Menu、
/// Expression Parameters）を、VRCSDKへの直接参照なしに読み取るための共通ヘルパー。
/// SerializedObject経由でフィールド名を直接参照するため、VRCSDK未導入でもコンパイル可能。
/// 複数のツールから再利用される。
/// </summary>
internal static class AvatarUtility
{
    /// <summary>アバター配下からVRCAvatarDescriptorと思われるコンポーネントをすべて集める。</summary>
    public static void FindVRCAvatarDescriptors(GameObject avatar, List<Component> results)
    {
        foreach (Component comp in avatar.GetComponentsInChildren<Component>(true))
        {
            if (comp != null && comp.GetType().Name == "VRCAvatarDescriptor") results.Add(comp);
        }
    }

    /// <summary>
    /// VRCAvatarDescriptorの各Playable Layer、および配下のAnimatorコンポーネントが参照する
    /// 全Animator Controllerを集める。
    /// </summary>
    public static HashSet<AnimatorController> CollectAnimatorControllers(GameObject avatar)
    {
        HashSet<AnimatorController> controllers = new HashSet<AnimatorController>();

        List<Component> descriptors = new List<Component>();
        FindVRCAvatarDescriptors(avatar, descriptors);
        foreach (Component comp in descriptors)
        {
            SerializedObject so = new SerializedObject(comp);
            CollectControllersFromLayerArray(so.FindProperty("baseAnimationLayers"), controllers);
            CollectControllersFromLayerArray(so.FindProperty("specialAnimationLayers"), controllers);
        }

        foreach (Animator animator in avatar.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController is AnimatorController ac) controllers.Add(ac);
        }

        return controllers;
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

    /// <summary>VRCAvatarDescriptorのexpressionsMenuを返す（最初に見つかったもの）。</summary>
    public static Object FindExpressionsMenu(GameObject avatar)
    {
        List<Component> descriptors = new List<Component>();
        FindVRCAvatarDescriptors(avatar, descriptors);

        foreach (Component comp in descriptors)
        {
            SerializedObject so = new SerializedObject(comp);
            SerializedProperty menuProp = so.FindProperty("expressionsMenu");
            if (menuProp != null && menuProp.objectReferenceValue != null) return menuProp.objectReferenceValue;
        }

        return null;
    }

    /// <summary>VRCAvatarDescriptorのexpressionParametersに定義された全パラメータ名を集める。</summary>
    public static void CollectExpressionParameterNames(GameObject avatar, HashSet<string> names)
    {
        List<Component> descriptors = new List<Component>();
        FindVRCAvatarDescriptors(avatar, descriptors);

        foreach (Component comp in descriptors)
        {
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
}
#endif