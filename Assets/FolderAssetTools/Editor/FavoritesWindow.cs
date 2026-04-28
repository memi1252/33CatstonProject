using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace FolderAssetTool
{
    public class FavoritesWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private FolderColorSettings settings;
    private string searchFilter = "";

    [MenuItem("Tools/Asset Favorites")]
    public static void ShowWindow()
    {
        FavoritesWindow window = GetWindow<FavoritesWindow>("Favorites");
        window.minSize = new Vector2(300, 400);
        window.Show();
    }

    private void OnEnable()
    {
        LoadSettings();
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
            {
                EditorGUILayout.HelpBox("Settings not found. Create a favorite to initialize.", MessageType.Info);
                return;
            }
        }

        DrawHeader();
        DrawSearchBar();
        DrawFavoritesList();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("⭐ Favorite Assets", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
            searchFilter = "";
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawFavoritesList()
    {
        var favorites = GetFavorites();

        if (favorites.Count == 0)
        {
            EditorGUILayout.HelpBox("No favorites yet. Right-click on any asset and select 'Add to Favorites'.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var fav in favorites)
        {
            if (!string.IsNullOrEmpty(searchFilter) && !fav.ToLower().Contains(searchFilter.ToLower()))
                continue;

            DrawFavoriteItem(fav);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawFavoriteItem(string path)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        Texture2D icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
        if (icon != null)
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

        string assetName = System.IO.Path.GetFileName(path);
        if (string.IsNullOrEmpty(assetName))
            assetName = path;

        if (GUILayout.Button(assetName, EditorStyles.label))
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        GUILayout.FlexibleSpace();

        Color guiColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("✕", GUILayout.Width(25)))
        {
            RemoveFavorite(path);
            GUIUtility.ExitGUI();
        }
        GUI.backgroundColor = guiColor;

        EditorGUILayout.EndHorizontal();
    }

    private List<string> GetFavorites()
    {
        if (settings == null)
            return new List<string>();

        return settings.folderColors
            .Where(data => data.isFavorite)
            .Select(data => data.folderPath)
            .ToList();
    }

    private void RemoveFavorite(string path)
    {
        if (settings == null)
            return;

        settings.SetFavorite(path, false);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
    }
    }
}
