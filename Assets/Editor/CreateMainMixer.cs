
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using System.Reflection;
using System.IO;

public static class CreateMainMixer
{
    [MenuItem("Tools/Create Main Audio Mixer")]
    public static void Create()
    {
        const string folder  = "Assets/Audio";
        const string outPath = "Assets/Audio/MainMixer.mixer";

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        if (AssetDatabase.LoadAssetAtPath<Object>(outPath) != null)
        {
            Debug.Log("[SoundManager] MainMixer.mixer 이미 존재합니다.");
            // 이미 있으면 expose만 재시도
            ExposeMixerParameters(outPath);
            return;
        }

        // AudioMixer 생성 (UnityEditor.AudioMixerController 내부 API)
        System.Type t = null;
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            t = a.GetType("UnityEditor.AudioMixerController");
            if (t != null) break;
        }

        if (t == null)
        {
            // Fallback: 빈 믹서를 직접 생성할 수 없으면 안내만
            Debug.LogWarning("[SoundManager] AudioMixerController 타입을 찾을 수 없습니다.\n" +
                "Unity 에디터에서 수동으로: Assets > Create > Audio > Audio Mixer");
            return;
        }

        var mixer = ScriptableObject.CreateInstance(t);
        AssetDatabase.CreateAsset(mixer, outPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ExposeMixerParameters(outPath);
        Debug.Log("[SoundManager] MainMixer.mixer 생성 완료! Assets/Audio/MainMixer.mixer");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(outPath));
    }

    private static void ExposeMixerParameters(string path)
    {
        // AudioMixer를 로드해서 BGM/SFX 그룹 볼륨을 Expose
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
        if (mixer == null) { Debug.LogWarning("믹서 로드 실패"); return; }

        // Master 볼륨 expose
        mixer.SetFloat("MasterVolume", 0f);

        string[] exposedNames = { "MasterVolume", "BGMVolume", "SFXVolume" };
        foreach (var name in exposedNames)
        {
            // AudioMixer.SetFloat으로 파라미터를 expose하려면 먼저 그룹을 만들어야 함
            // 실제 Expose는 Inspector에서 우클릭 > Expose Parameter 로 해야 함
        }

        Debug.Log("[SoundManager] 믹서 로드 완료. Inspector에서 각 그룹 볼륨을 우클릭 → Expose Parameter 하고\n" +
                  "이름을 MasterVolume / BGMVolume / SFXVolume 으로 설정해주세요.");
    }
}
#endif
