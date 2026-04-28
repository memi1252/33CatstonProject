using UnityEngine;
using UnityEditor;

namespace FolderAssetTool
{
    public class AssetToolsWindow : EditorWindow
{
    private string targetPath;
    private FolderColorSettings settings;
    private int selectedTab = 0;
    private string[] tabNames = { "🎨 Colors", "🖼️ Icons", "⭐ Favorites", "⚙️ Options" };
    private Vector2 scrollPosition;

    private Color[] colorPalette = new Color[]
    {
        new Color(0.95f, 0.26f, 0.21f, 0.6f),
        new Color(0.91f, 0.12f, 0.39f, 0.6f),
        new Color(0.61f, 0.15f, 0.69f, 0.6f),
        new Color(0.40f, 0.23f, 0.72f, 0.6f),
        new Color(0.25f, 0.32f, 0.71f, 0.6f),
        new Color(0.13f, 0.59f, 0.95f, 0.6f),
        new Color(0.00f, 0.74f, 0.83f, 0.6f),
        new Color(0.00f, 0.59f, 0.53f, 0.6f),
        new Color(0.30f, 0.69f, 0.31f, 0.6f),
        new Color(0.55f, 0.76f, 0.29f, 0.6f),
        new Color(1.00f, 0.92f, 0.23f, 0.6f),
        new Color(1.00f, 0.76f, 0.03f, 0.6f),
        new Color(1.00f, 0.60f, 0.00f, 0.6f),
        new Color(1.00f, 0.34f, 0.13f, 0.6f),
        new Color(0.47f, 0.33f, 0.28f, 0.6f),
        new Color(0.38f, 0.38f, 0.38f, 0.6f),
        new Color(0.62f, 0.62f, 0.62f, 0.6f),
        new Color(1.00f, 1.00f, 1.00f, 0.4f),
    };

    private string[] colorNames = new string[]
    {
        "Red", "Pink", "Purple", "Deep Purple", "Indigo", "Blue",
        "Cyan", "Teal", "Green", "Light Green", "Yellow", "Amber",
        "Orange", "Deep Orange", "Brown", "Dark Gray", "Gray", "White"
    };

    private string[] textEmojis = new string[]
    {
        "⭐", "🔥", "✅", "❌", "⚠️", "🎯",
        "🚀", "💡", "🔧", "📝", "🎨", "🎮",
        "💎", "🎵", "📁", "🗂️", "📦", "🎁",
        "🔒", "🔓", "🔑", "⚡", "💥", "✨",
        "🌟", "💫", "🎪", "🎭", "🎬", "📱",
        "💻", "⌨️", "🖱️", "🎤", "🎧", "📷",
        "🏆", "🥇", "🥈", "🥉", "🎖️", "👑",
        "💀", "👻", "🤖", "👾", "🎃", "🦄"
    };

    private string[] iconNames = new string[]
    {
        "cs Script Icon", "d_ScriptableObject Icon", "d_Prefab Icon", "d_PrefabVariant Icon",
        "Animation Icon", "d_AnimatorController Icon", "d_AnimationClip Icon",
        "Material Icon", "Shader Icon", "Texture Icon",
        "AudioClip Icon", "VideoClip Icon", "Font Icon",
        "d_SceneAsset Icon", "d_Camera Icon", "d_Light Icon", "d_Terrain Icon",
        "d_ParticleSystem Icon", "d_Canvas Icon",
        "d_Settings Icon", "d_console.infoicon", "d_console.warnicon",
        "d_console.erroricon", "d_Valid", "d_Invalid",
        "d_Animation.Record", "d_UnityEditor.AnimationWindow",
        "d_UnityEditor.SceneView", "d_UnityEditor.GameView", "d_UnityEditor.InspectorWindow", "d_UnityEditor.HierarchyWindow"
    };

    public static void ShowWindow(string assetPath)
    {
        AssetToolsWindow window = GetWindow<AssetToolsWindow>("Folder Asset Tool");
        window.targetPath = assetPath;
        window.minSize = new Vector2(350, 400);
        window.LoadSettings();
        window.Show();
    }

    private void OnEnable()
    {
        LoadSettings();
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeObject != null)
        {
            GameObject go = Selection.activeObject as GameObject;
            if (go != null && go.scene.IsValid())
            {
                targetPath = "hierarchy:" + go.name;
                Repaint();
                return;
            }

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(path))
            {
                targetPath = path;
                Repaint();
            }
        }
    }

    private void LoadSettings()
    {
        settings = AssetDatabase.LoadAssetAtPath<FolderColorSettings>("Assets/Editor/FolderColorSettings.asset");
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            LoadSettings();
            if (settings == null)
                return;
        }

        DrawHeader();
        DrawTabs();
        GUILayout.Space(5);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        switch (selectedTab)
        {
            case 0:
                DrawColorsTab();
                break;
            case 1:
                DrawEmojisTab();
                break;
            case 2:
                DrawFavoritesTab();
                break;
            case 3:
                DrawOptionsTab();
                break;
        }

        EditorGUILayout.EndScrollView();

        DrawFooter();
    }

    private void DrawHeader()
    {
        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 16;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUILayout.Label("🎨 Folder Asset Tool", titleStyle);

        GUILayout.Space(5);

        if (!string.IsNullOrEmpty(targetPath))
        {
            string assetName;
            string assetType;

            if (targetPath.StartsWith("hierarchy:"))
            {
                assetName = targetPath.Substring("hierarchy:".Length);
                assetType = "Scene GameObject";
            }
            else
            {
                assetName = System.IO.Path.GetFileName(targetPath);
                if (string.IsNullOrEmpty(assetName))
                    assetName = targetPath;
                assetType = "Project Asset";
            }

            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel);
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label($"Editing: {assetName} ({assetType})", subtitleStyle);
        }
        else
        {
            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel);
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label("Select an asset or GameObject", subtitleStyle);
        }

        EditorGUILayout.EndVertical();

        GUI.backgroundColor = originalBg;
        GUILayout.Space(5);
    }

    private void DrawTabs()
    {
        GUIStyle tabStyle = new GUIStyle(GUI.skin.button);
        tabStyle.fontSize = 12;
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, tabStyle, GUILayout.Height(30));
    }

    private void DrawColorsTab()
    {
        GUILayout.Space(10);

        GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
        sectionStyle.fontSize = 13;
        GUILayout.Label("Choose a Color", sectionStyle);

        GUILayout.Space(5);

        int columns = 6;
        int index = 0;

        foreach (var color in colorPalette)
        {
            if (index % columns == 0)
                EditorGUILayout.BeginHorizontal();

            Color currentColor = settings.GetFolderColor(targetPath);
            bool isSelected = ColorMatch(currentColor, color);

            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = color;

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            if (isSelected)
            {
                buttonStyle.border = new RectOffset(3, 3, 3, 3);
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button("", buttonStyle, GUILayout.Width(60), GUILayout.Height(50)))
                SetColor(color);

            GUI.backgroundColor = originalBg;

            if (index % columns == columns - 1 || index == colorPalette.Length - 1)
                EditorGUILayout.EndHorizontal();

            index++;
        }

        GUILayout.Space(10);

        if (settings.HasColor(targetPath))
        {
            Color guiColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
            if (GUILayout.Button("🗑️ Remove Color", GUILayout.Height(35)))
                RemoveColor();
            GUI.backgroundColor = guiColor;
        }

        GUILayout.Space(10);
    }

    private void DrawEmojisTab()
    {
        GUILayout.Space(10);

        GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
        sectionStyle.fontSize = 13;
        GUILayout.Label("Choose an Icon", sectionStyle);

        GUILayout.Space(5);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Text Emojis", EditorStyles.miniLabel);
        GUILayout.Space(3);

        int columns = 8;
        int index = 0;

        foreach (var emoji in textEmojis)
        {
            if (index % columns == 0)
                EditorGUILayout.BeginHorizontal();

            string currentEmoji = settings.GetEmoji(targetPath);
            bool isSelected = currentEmoji == emoji;

            Color originalBg = GUI.backgroundColor;

            if (isSelected)
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);

            GUIStyle emojiStyle = new GUIStyle(GUI.skin.button);
            emojiStyle.fontSize = 20;

            if (GUILayout.Button(emoji, emojiStyle, GUILayout.Width(45), GUILayout.Height(45)))
                SetEmoji(emoji);

            GUI.backgroundColor = originalBg;

            if (index % columns == columns - 1 || index == textEmojis.Length - 1)
                EditorGUILayout.EndHorizontal();

            index++;
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Unity Icons", EditorStyles.miniLabel);
        GUILayout.Space(3);

        columns = 8;
        index = 0;

        for (int i = 0; i < iconNames.Length; i++)
        {
            if (index % columns == 0)
                EditorGUILayout.BeginHorizontal();

            string currentEmoji = settings.GetEmoji(targetPath);
            bool isSelected = currentEmoji == iconNames[i];

            Color originalBg = GUI.backgroundColor;

            if (isSelected)
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);

            GUIContent iconContent = EditorGUIUtility.IconContent(iconNames[i]);
            GUIStyle iconButtonStyle = new GUIStyle(GUI.skin.button);

            if (iconContent != null && iconContent.image != null)
            {
                iconContent.text = "";
                if (GUILayout.Button(iconContent, iconButtonStyle, GUILayout.Width(45), GUILayout.Height(45)))
                    SetEmoji(iconNames[i]);
            }
            else
            {
                if (GUILayout.Button("?", iconButtonStyle, GUILayout.Width(45), GUILayout.Height(45)))
                    SetEmoji(iconNames[i]);
            }

            GUI.backgroundColor = originalBg;

            if (index % columns == columns - 1 || index == iconNames.Length - 1)
                EditorGUILayout.EndHorizontal();

            index++;
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        if (settings.HasEmoji(targetPath))
        {
            Color guiColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
            if (GUILayout.Button("🗑️ Remove Icon", GUILayout.Height(35)))
                SetEmoji(string.Empty);
            GUI.backgroundColor = guiColor;
        }

        GUILayout.Space(10);
    }

    private void DrawFavoritesTab()
    {
        GUILayout.Space(10);

        GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
        sectionStyle.fontSize = 13;
        GUILayout.Label("Favorites", sectionStyle);

        GUILayout.Space(10);

        bool isFavorite = IsFavorite();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (!isFavorite)
        {
            GUILayout.Space(20);

            GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
            iconStyle.fontSize = 40;
            iconStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("⭐", iconStyle);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.wordWrap = true;
            GUILayout.Label("Add this asset to your favorites for quick access", labelStyle);

            GUILayout.Space(20);

            Color guiColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("⭐ Add to Favorites", GUILayout.Height(45)))
                AddToFavorites();
            GUI.backgroundColor = guiColor;

            GUILayout.Space(10);
        }
        else
        {
            GUILayout.Space(20);

            GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
            iconStyle.fontSize = 40;
            iconStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("✅", iconStyle);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.wordWrap = true;
            labelStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
            GUILayout.Label("This asset is in your favorites!", labelStyle);

            GUILayout.Space(20);

            Color guiColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
            if (GUILayout.Button("Remove from Favorites", GUILayout.Height(45)))
                RemoveFromFavorites();
            GUI.backgroundColor = guiColor;

            GUILayout.Space(10);
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        Color buttonColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);
        if (GUILayout.Button("📂 Open Favorites Window", GUILayout.Height(40)))
            FavoritesWindow.ShowWindow();
        GUI.backgroundColor = buttonColor;

        GUILayout.Space(10);
    }

    private void DrawOptionsTab()
    {
        GUILayout.Space(10);

        GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
        sectionStyle.fontSize = 13;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Global Options", sectionStyle);
        GUILayout.Space(5);

        EditorGUI.BeginChangeCheck();

        bool newGlobalShowColors = EditorGUILayout.ToggleLeft("Show Colors", settings.globalShowColors);
        bool newGlobalShowHighlight = EditorGUILayout.ToggleLeft("Show Highlight", settings.globalShowHighlight);
        bool newGlobalShowEmojis = EditorGUILayout.ToggleLeft("Show Emojis", settings.globalShowEmojis);

        if (EditorGUI.EndChangeCheck())
        {
            settings.globalShowColors = newGlobalShowColors;
            settings.globalShowHighlight = newGlobalShowHighlight;
            settings.globalShowEmojis = newGlobalShowEmojis;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            EditorApplication.RepaintProjectWindow();
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        if (!string.IsNullOrEmpty(targetPath))
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Current Asset Options", sectionStyle);
            GUILayout.Space(5);

            FolderColorData folderData = settings.GetFolderData(targetPath);
            bool showColor = folderData != null ? folderData.showColor : true;
            bool showHighlight = folderData != null ? folderData.showHighlight : true;
            bool showEmoji = folderData != null ? folderData.showEmoji : true;

            EditorGUI.BeginChangeCheck();

            bool newShowColor = EditorGUILayout.ToggleLeft("Show Color for this asset", showColor);
            bool newShowHighlight = EditorGUILayout.ToggleLeft("Show Highlight for this asset", showHighlight);
            bool newShowEmoji = EditorGUILayout.ToggleLeft("Show Emoji for this asset", showEmoji);

            if (EditorGUI.EndChangeCheck())
            {
                settings.SetFolderOption(targetPath, "showColor", newShowColor);
                settings.SetFolderOption(targetPath, "showHighlight", newShowHighlight);
                settings.SetFolderOption(targetPath, "showEmoji", newShowEmoji);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                EditorApplication.RepaintProjectWindow();
            }

            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("Select an asset to see per-asset options", MessageType.Info);
        }

        GUILayout.Space(10);
    }

    private void DrawFooter()
    {
        GUILayout.FlexibleSpace();
    }

    private bool ColorMatch(Color a, Color b)
    {
        if (a == Color.clear || b == Color.clear)
            return false;
        return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    }

    private void SetColor(Color color)
    {
        if (settings == null)
            return;

        settings.SetFolderColor(targetPath, color);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
        Repaint();
    }

    private void RemoveColor()
    {
        if (settings == null)
            return;

        settings.RemoveFolderColor(targetPath);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
        Repaint();
    }

    private void SetEmoji(string emoji)
    {
        if (settings == null)
            return;

        settings.SetEmoji(targetPath, emoji);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
        Repaint();
    }

    private void AddToFavorites()
    {
        if (settings == null)
            return;

        settings.SetFavorite(targetPath, true);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
        Repaint();
    }

    private void RemoveFromFavorites()
    {
        if (settings == null)
            return;

        settings.SetFavorite(targetPath, false);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
        Repaint();
    }

    private bool IsFavorite()
    {
        if (settings == null)
            return false;

        return settings.IsFavorite(targetPath);
    }
    }
}
