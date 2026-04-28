using UnityEngine;
using System.Collections.Generic;

namespace FolderAssetTool
{
    [System.Serializable]
    public class FolderColorData
    {
        public string folderPath;
        public Color color;
        public string emoji;
        public bool isFavorite;
        public bool showColor = true;
        public bool showHighlight = true;
        public bool showEmoji = true;
    }

    [CreateAssetMenu(fileName = "FolderColorSettings", menuName = "Editor/Folder Color Settings")]
    public class FolderColorSettings : ScriptableObject
    {
        public List<FolderColorData> folderColors = new List<FolderColorData>();

        public bool globalShowColors = true;
        public bool globalShowHighlight = true;
        public bool globalShowEmojis = true;

        public Color GetFolderColor(string path)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path)
                    return data.color;
            }
            return Color.clear;
        }

        public void SetFolderColor(string path, Color color)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path)
                {
                    data.color = color;
                    return;
                }
            }

            folderColors.Add(new FolderColorData { folderPath = path, color = color });
        }

        public void RemoveFolderColor(string path)
        {
            folderColors.RemoveAll(data => data.folderPath == path);
        }

        public bool HasColor(string path)
        {
            return folderColors.Exists(data => data.folderPath == path);
        }

        public string GetEmoji(string path)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path)
                    return data.emoji;
            }
            return string.Empty;
        }

        public void SetEmoji(string path, string emoji)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path)
                {
                    data.emoji = emoji;
                    return;
                }
            }

            folderColors.Add(new FolderColorData { folderPath = path, emoji = emoji, color = Color.clear });
        }

        public bool HasEmoji(string path)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path && !string.IsNullOrEmpty(data.emoji))
                    return true;
            }
            return false;
        }

        public bool IsFavorite(string path)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path)
                    return data.isFavorite;
            }
            return false;
        }

        public void SetFavorite(string path, bool isFavorite)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path)
                {
                    data.isFavorite = isFavorite;
                    return;
                }
            }

            folderColors.Add(new FolderColorData { folderPath = path, isFavorite = isFavorite, color = Color.clear });
        }

        public FolderColorData GetFolderData(string path)
        {
            foreach (var data in folderColors)
            {
                if (data.folderPath == path)
                    return data;
            }
            return null;
        }

        public void SetFolderOption(string path, string optionName, bool value)
        {
            FolderColorData data = GetFolderData(path);
            if (data == null)
            {
                data = new FolderColorData { folderPath = path, color = Color.clear };
                folderColors.Add(data);
            }

            if (optionName == "showColor")
                data.showColor = value;
            else if (optionName == "showHighlight")
                data.showHighlight = value;
            else if (optionName == "showEmoji")
                data.showEmoji = value;
        }
    }
}
