using UnityEngine;
using UnityEditor;
using System.IO;

namespace FolderAssetTool
{
    [InitializeOnLoad]
    public class FolderColorizer
    {
        private static FolderColorSettings settings;
        private static readonly string settingsPath = "Assets/Editor/FolderColorSettings.asset";

        static FolderColorizer()
        {
            LoadSettings();
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
        }

        private static void LoadSettings()
        {
            settings = AssetDatabase.LoadAssetAtPath<FolderColorSettings>(settingsPath);

            if (settings == null)
            {
                if (!Directory.Exists("Assets/Editor"))
                    Directory.CreateDirectory("Assets/Editor");

                settings = ScriptableObject.CreateInstance<FolderColorSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
    {
        if (settings == null)
            return;

        string path = AssetDatabase.GUIDToAssetPath(guid);

        if (string.IsNullOrEmpty(path))
            return;

        bool isSmallIcon = selectionRect.height <= 20;
        bool isListView = selectionRect.width > selectionRect.height;

        Rect iconRect;

        if (isSmallIcon)
        {
            iconRect = new Rect(selectionRect.x, selectionRect.y, 16, 16);
        }
        else if (isListView)
        {
            float iconSize = selectionRect.height;
            iconRect = new Rect(selectionRect.x, selectionRect.y, iconSize, iconSize);
        }
        else
        {
            float iconSize = selectionRect.width;
            float yOffset = 0;
            iconRect = new Rect(selectionRect.x, selectionRect.y + yOffset, iconSize, iconSize);
        }

        FolderColorData folderData = settings.GetFolderData(path);
        bool showColor = folderData != null ? folderData.showColor : true;
        bool showHighlight = folderData != null ? folderData.showHighlight : true;
        bool showEmoji = folderData != null ? folderData.showEmoji : true;

        Color assetColor = settings.GetFolderColor(path);

        if (assetColor != Color.clear && (isListView || isSmallIcon) && settings.globalShowHighlight && showHighlight)
        {
            Rect textRect = new Rect(selectionRect.x + iconRect.width + 2, selectionRect.y, selectionRect.width - iconRect.width - 2, selectionRect.height);
            Color highlightColor = new Color(assetColor.r, assetColor.g, assetColor.b, 0.3f);
            EditorGUI.DrawRect(textRect, highlightColor);
        }

        if (assetColor != Color.clear && settings.globalShowColors && showColor)
        {
            Texture2D icon = AssetDatabase.GetCachedIcon(path) as Texture2D;

            if (icon != null)
            {
                Color originalColor = GUI.color;
                GUI.color = assetColor;
                GUI.DrawTexture(iconRect, icon);
                GUI.color = originalColor;
            }
        }

        if (settings.IsFavorite(path))
        {
            Color goldenColor = new Color(1f, 0.84f, 0f, 1f);
            Texture2D icon = AssetDatabase.GetCachedIcon(path) as Texture2D;

            if (icon != null)
            {
                int outlineSize = isSmallIcon ? 1 : 2;

                for (int x = -outlineSize; x <= outlineSize; x++)
                {
                    for (int y = -outlineSize; y <= outlineSize; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        Rect outlineRect = new Rect(iconRect.x + x, iconRect.y + y, iconRect.width, iconRect.height);
                        Color originalColor = GUI.color;
                        GUI.color = goldenColor;
                        GUI.DrawTexture(outlineRect, icon);
                        GUI.color = originalColor;
                    }
                }

                if (assetColor != Color.clear)
                {
                    Color originalColor = GUI.color;
                    GUI.color = assetColor;
                    GUI.DrawTexture(iconRect, icon);
                    GUI.color = originalColor;
                }
                else
                {
                    GUI.DrawTexture(iconRect, icon);
                }
            }
        }

        string emoji = settings.GetEmoji(path);
        if (!string.IsNullOrEmpty(emoji) && settings.globalShowEmojis && showEmoji)
        {
            bool isUnityIcon = emoji.Contains("Icon") || emoji.Contains("d_") || emoji.Contains("cs ") || emoji.Contains(".");

            if (isUnityIcon)
            {
                Texture2D emojiIcon = EditorGUIUtility.IconContent(emoji).image as Texture2D;
                if (emojiIcon != null)
                {
                    float iconSize = isSmallIcon ? 12 : 48;
                    Rect emojiRect = new Rect(iconRect.x + iconRect.width - iconSize - 2, iconRect.y + iconRect.height - iconSize - 2, iconSize, iconSize);
                    GUI.DrawTexture(emojiRect, emojiIcon);
                }
            }
            else
            {
                GUIStyle emojiStyle = new GUIStyle();
                emojiStyle.fontSize = isSmallIcon ? 10 : (int)(iconRect.width * 0.4f);
                emojiStyle.alignment = TextAnchor.LowerRight;
                emojiStyle.normal.textColor = Color.white;

                Rect emojiRect = new Rect(iconRect.x, iconRect.y, iconRect.width, iconRect.height);
                GUI.Label(emojiRect, emoji, emojiStyle);
            }
        }
    }

    private static void OnHierarchyWindowItemGUI(int instanceID, Rect selectionRect)
    {
        if (settings == null)
            return;

        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null)
            return;

        string objectPath = "hierarchy:" + obj.name;

        FolderColorData folderData = settings.GetFolderData(objectPath);
        if (folderData == null)
            return;

        bool showColor = folderData.showColor;
        bool showHighlight = folderData.showHighlight;
        bool showEmoji = folderData.showEmoji;

        Color assetColor = folderData.color;

        Rect iconRect = new Rect(selectionRect.x, selectionRect.y, 16, 16);

        if (assetColor != Color.clear && settings.globalShowHighlight && showHighlight)
        {
            Rect textRect = new Rect(selectionRect.x + 18, selectionRect.y, selectionRect.width - 18, selectionRect.height);
            Color highlightColor = new Color(assetColor.r, assetColor.g, assetColor.b, 0.3f);
            EditorGUI.DrawRect(textRect, highlightColor);
        }

        if (assetColor != Color.clear && settings.globalShowColors && showColor)
        {
            Texture2D icon = EditorGUIUtility.ObjectContent(obj, obj.GetType()).image as Texture2D;

            if (icon != null)
            {
                Color originalColor = GUI.color;
                GUI.color = assetColor;
                GUI.DrawTexture(iconRect, icon);
                GUI.color = originalColor;
            }
        }

        if (settings.IsFavorite(objectPath))
        {
            Color goldenColor = new Color(1f, 0.84f, 0f, 1f);
            int outlineSize = 1;

            Texture2D icon = EditorGUIUtility.ObjectContent(obj, obj.GetType()).image as Texture2D;
            if (icon != null)
            {
                for (int x = -outlineSize; x <= outlineSize; x++)
                {
                    for (int y = -outlineSize; y <= outlineSize; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        Rect outlineRect = new Rect(iconRect.x + x, iconRect.y + y, iconRect.width, iconRect.height);
                        Color originalColor = GUI.color;
                        GUI.color = goldenColor;
                        GUI.DrawTexture(outlineRect, icon);
                        GUI.color = originalColor;
                    }
                }

                if (assetColor != Color.clear)
                {
                    Color originalColor = GUI.color;
                    GUI.color = assetColor;
                    GUI.DrawTexture(iconRect, icon);
                    GUI.color = originalColor;
                }
                else
                {
                    GUI.DrawTexture(iconRect, icon);
                }
            }
        }

        string emoji = settings.GetEmoji(objectPath);
        if (!string.IsNullOrEmpty(emoji) && settings.globalShowEmojis && showEmoji)
        {
            bool isUnityIcon = emoji.Contains("Icon") || emoji.Contains("d_") || emoji.Contains("cs ") || emoji.Contains(".");

            if (isUnityIcon)
            {
                Texture2D emojiIcon = EditorGUIUtility.IconContent(emoji).image as Texture2D;
                if (emojiIcon != null)
                {
                    float iconSize = 16;
                    Rect emojiRect = new Rect(selectionRect.x + selectionRect.width - iconSize - 2, selectionRect.y, iconSize, iconSize);
                    GUI.DrawTexture(emojiRect, emojiIcon);
                }
            }
            else
            {
                GUIStyle emojiStyle = new GUIStyle();
                emojiStyle.fontSize = 10;
                emojiStyle.alignment = TextAnchor.MiddleRight;
                emojiStyle.normal.textColor = Color.white;

                Rect emojiRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.width - 2, selectionRect.height);
                GUI.Label(emojiRect, emoji, emojiStyle);
            }
        }
    }

    private static void SetColor(string path, Color color)
    {
        if (settings == null)
            return;

        settings.SetFolderColor(path, color);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
    }

    private static void RemoveColor(string path)
    {
        if (settings == null)
            return;

        settings.RemoveFolderColor(path);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
    }

    private static void OpenSettings()
    {
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    [MenuItem("Tools/Folder Colorizer/Open Settings")]
    private static void OpenSettingsMenu()
    {
        OpenSettings();
    }

    [MenuItem("Tools/Folder Colorizer/Clear All Colors")]
    private static void ClearAllColors()
    {
        if (settings == null)
            return;

        if (!EditorUtility.DisplayDialog("Clear All Folder Colors", "Are you sure you want to remove all folder colors?", "Yes", "Cancel"))
            return;

        settings.folderColors.Clear();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem("Assets/🎨 Folder Asset Tool", false, 19)]
    private static void OpenAssetTools()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!string.IsNullOrEmpty(path))
            AssetToolsWindow.ShowWindow(path);
    }

    [MenuItem("Assets/🎨 Folder Asset Tool", true)]
    private static bool ValidateOpenAssetTools()
    {
        return IsSelectedObjectValid();
    }

    [MenuItem("GameObject/🎨 Folder Asset Tool", false, 0)]
    private static void OpenAssetToolsFromGameObject()
    {
        if (Selection.activeGameObject != null)
        {
            string path = "hierarchy:" + Selection.activeGameObject.name;
            AssetToolsWindow.ShowWindow(path);
        }
    }

    [MenuItem("GameObject/🎨 Folder Asset Tool", true)]
    private static bool ValidateOpenAssetToolsFromGameObject()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/Folder Asset Tool")]
    private static void OpenAssetToolsFromMenu()
    {
        if (Selection.activeObject != null)
        {
            GameObject go = Selection.activeObject as GameObject;
            if (go != null && go.scene.IsValid())
            {
                AssetToolsWindow.ShowWindow("hierarchy:" + go.name);
                return;
            }

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(path))
            {
                AssetToolsWindow.ShowWindow(path);
                return;
            }
        }
        AssetToolsWindow.ShowWindow("");
    }

    private static bool IsSelectedObjectValid()
    {
        if (Selection.activeObject == null)
            return false;

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path);
    }
    }
}
