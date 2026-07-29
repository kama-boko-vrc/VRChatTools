#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// VRChatToolsの全ツールを一覧表示し、選択すると簡単な説明が表示されるハブウィンドウ。
/// メニューの `Tools/VRChatTools` をクリックするとこのハブウィンドウが開く。
/// 「ツールを開く」を押すと、対応するツールのShowWindow()を直接呼び出してそのツール本体の
/// ウィンドウを開く（各ツールは個別のMenuItemを持たない）。
/// </summary>
public class VRChatToolsHub : EditorWindow
{
    private class ToolInfo
    {
        public readonly string name;
        public readonly Action open;
        public readonly string description;

        public ToolInfo(string name, Action open, string description)
        {
            this.name = name;
            this.open = open;
            this.description = description;
        }
    }

    private static readonly ToolInfo[] Tools =
    {
        new ToolInfo(
            "Armature Scale Copier",
            ArmatureScaleCopier.ShowWindow,
            "同一構造のArmature間で、各ボーンのlocalScaleとMA Scale Adjusterをコピーします。\n" +
            "A: コピー元（アバター本体）、B: コピー先（衣装・髪など）。"),
        new ToolInfo(
            "Scale Resetter",
            ScaleResetter.ShowWindow,
            "指定したTransformとその配下すべてのlocalScaleを(1, 1, 1)にリセットします。"),
        new ToolInfo(
            "Armature Component Cleaner",
            ArmatureComponentCleaner.ShowWindow,
            "指定したTransformとその配下すべてから、Transform以外の全コンポーネントを再帰的に削除します。"),
        new ToolInfo(
            "LilToon Property Copier",
            LilToonPropertyCopier.ShowWindow,
            "1つのマテリアルから、選択したシェーダープロパティのみを複数のマテリアルへ一括コピーします。\n" +
            "lilToonに限らず、同じシェーダーであれば利用できます。"),
        new ToolInfo(
            "Write Defaults Batch Setter",
            WriteDefaultsBatchSetter.ShowWindow,
            "アバターが使う全Animator ControllerのWrite Defaultsを集計し、一括でON/OFFに変更します。"),
        new ToolInfo(
            "Avatar Menu Organizer",
            AvatarMenuOrganizer.ShowWindow,
            "Expressions Menuと全Animator Controllerのレイヤー・パラメータをまとめて一覧表示し、\n" +
            "選択したものを一括削除できます。"),
        new ToolInfo(
            "Quick Prefab Placer",
            QuickPrefabPlacer.ShowWindow,
            "登録した複数のプレハブを、ヒエラルキーの右クリックメニューから選んでワンクリック配置します。"),
    };

    private int selectedIndex;
    private Vector2 listScroll;

    [MenuItem("Tools/VRChatTools")]
    private static void ShowWindow()
    {
        VRChatToolsHub window = GetWindow<VRChatToolsHub>("VRChatTools Hub");
        window.minSize = new Vector2(560, 340);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawToolList();
        GUILayout.Space(8);
        DrawDescription();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(220));
        EditorGUILayout.LabelField("ツール一覧", EditorStyles.boldLabel);

        listScroll = EditorGUILayout.BeginScrollView(listScroll);

        for (int i = 0; i < Tools.Length; i++)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = i == selectedIndex ? Color.cyan : previousColor;

            if (GUILayout.Button(Tools[i].name, GUILayout.Height(24)))
            {
                selectedIndex = i;
            }

            GUI.backgroundColor = previousColor;

            GUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawDescription()
    {
        EditorGUILayout.BeginVertical();

        ToolInfo tool = Tools[selectedIndex];
        EditorGUILayout.LabelField(tool.name, EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(tool.description, EditorStyles.wordWrappedLabel);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("ツールを開く"))
        {
            tool.open();
        }

        EditorGUILayout.EndVertical();
    }
}
#endif