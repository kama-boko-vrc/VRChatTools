#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// VRChatToolsの全ツールを一覧表示し、選択すると簡単な説明が表示されるハブウィンドウ。
/// 「ツールを開く」を押すと、対応するツールのMenuItemを実行してそのツール本体のウィンドウを開く。
/// 各ツールのメニューパスをここに文字列で持っているため、ツール側のMenuItemパスを変更した場合は
/// このファイルのToolsも合わせて更新すること。
/// </summary>
public class VRChatToolsHub : EditorWindow
{
    private class ToolInfo
    {
        public readonly string name;
        public readonly string menuPath;
        public readonly string description;

        public ToolInfo(string name, string menuPath, string description)
        {
            this.name = name;
            this.menuPath = menuPath;
            this.description = description;
        }
    }

    private static readonly ToolInfo[] Tools =
    {
        new ToolInfo(
            "Armature Scale Copier",
            "Tools/VRChatTools/Armature Scale Copier",
            "同一構造のArmature間で、各ボーンのlocalScaleとMA Scale Adjusterをコピーします。\n" +
            "A: コピー元（アバター本体）、B: コピー先（衣装・髪など）。"),
        new ToolInfo(
            "Scale Resetter",
            "Tools/VRChatTools/Scale Resetter",
            "指定したTransformとその配下すべてのlocalScaleを(1, 1, 1)にリセットします。"),
        new ToolInfo(
            "Armature Component Cleaner",
            "Tools/VRChatTools/Armature Component Cleaner",
            "指定したTransformとその配下すべてから、Transform以外の全コンポーネントを再帰的に削除します。"),
        new ToolInfo(
            "LilToon Property Copier",
            "Tools/VRChatTools/LilToon Property Copier",
            "1つのマテリアルから、選択したシェーダープロパティのみを複数のマテリアルへ一括コピーします。\n" +
            "lilToonに限らず、同じシェーダーであれば利用できます。"),
        new ToolInfo(
            "Write Defaults Batch Setter",
            "Tools/VRChatTools/Write Defaults Batch Setter",
            "アバターが使う全Animator ControllerのWrite Defaultsを集計し、一括でON/OFFに変更します。"),
        new ToolInfo(
            "Avatar Menu Organizer",
            "Tools/VRChatTools/Avatar Menu Organizer",
            "Expressions Menuと全Animator Controllerのレイヤー・パラメータをまとめて一覧表示し、\n" +
            "選択したものを一括削除できます。"),
        new ToolInfo(
            "Quick Prefab Placer",
            "Tools/VRChatTools/Quick Prefab Placer",
            "登録した複数のプレハブを、ヒエラルキーの右クリックメニューから選んでワンクリック配置します。"),
    };

    private int selectedIndex;

    [MenuItem("Tools/VRChatTools/Tool Hub")]
    private static void ShowWindow()
    {
        VRChatToolsHub window = GetWindow<VRChatToolsHub>("VRChatTools Hub");
        window.minSize = new Vector2(480, 280);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawToolList();
        DrawDescription();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(180));
        EditorGUILayout.LabelField("ツール一覧", EditorStyles.boldLabel);

        for (int i = 0; i < Tools.Length; i++)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = i == selectedIndex ? Color.cyan : previousColor;

            if (GUILayout.Button(Tools[i].name))
            {
                selectedIndex = i;
            }

            GUI.backgroundColor = previousColor;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawDescription()
    {
        EditorGUILayout.BeginVertical();

        ToolInfo tool = Tools[selectedIndex];
        EditorGUILayout.LabelField(tool.name, EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(tool.description, EditorStyles.wordWrappedLabel);

        EditorGUILayout.FlexibleSpace();

        if (GUILayout.Button("ツールを開く"))
        {
            EditorApplication.ExecuteMenuItem(tool.menuPath);
        }

        EditorGUILayout.EndVertical();
    }
}
#endif