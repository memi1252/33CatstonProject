using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Player")]
    public AudioClip sfxPlayerHit;
    public AudioClip sfxPlayerDeath;
    public AudioClip sfxPlayerRevive;
    public AudioClip sfxPlayerJump;
    public AudioClip sfxPlayerLand;

    [Header("Weapon")]
    public AudioClip sfxWeaponWaterWand;
    public AudioClip sfxWeaponLaser;
    public AudioClip sfxWeaponExplosion;
    public AudioClip sfxWeaponCriticalHit;
    public AudioClip sfxWeaponEquip;

    [Header("Enemy")]
    public AudioClip sfxEnemyHit;
    public AudioClip sfxEnemySlimeDeath;
    public AudioClip sfxEnemyDroneDeath;
    public AudioClip sfxEnemyBossRoar;
    public AudioClip sfxEnemyBossSpawn;
    public AudioClip sfxEnemyTaxiCharge;
    public AudioClip sfxEnemyBossSteam;
    public AudioClip sfxEnemyMinionSpawn;

    [Header("UI / Stage")]
    public AudioClip sfxUIGameStart;
    public AudioClip sfxUIDisconnect;
    public AudioClip sfxUIRoomJoin;
    public AudioClip sfxUIReady;
    public AudioClip sfxUIClick;
    public AudioClip sfxUIMenuOpen;
    public AudioClip sfxUIBuffApply;
    public AudioClip sfxUIContractSelect;
    public AudioClip sfxUILevelUp;
    public AudioClip sfxUIWeaponEquip;
    public AudioClip sfxStageClear;
    public AudioClip sfxGameClear;
    public AudioClip sfxGameOver;
    public AudioClip sfxPortalTeleport;
    public AudioClip sfxProjectileImpact;
    public AudioClip sfxRevivalPrompt;

    [Header("BGM")]
    public AudioClip bgmLobby;
    public AudioClip bgmGame;
    public AudioClip bgmBoss;

    private const string MasterVolumeParam = "MasterVolume";
    private const string BGMVolumeParam = "BGMVolume";
    private const string SFXVolumeParam = "SFXVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadVolumeSettings();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 인덱스: 0=MainScene, 1=LobbyScene, 2=GameScene
        switch (scene.buildIndex)
        {
            case 0:
            case 1:
                PlayBGM(bgmLobby);
                break;
            case 2:
                PlayBGM(bgmGame);
                break;
        }
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (clip == null || (bgmSource.clip == clip && bgmSource.isPlaying)) return;
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    public void PlayBossBGM() => PlayBGM(bgmBoss);
    public void StopBGM() => bgmSource.Stop();

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    // 편의 메서드
    public void PlayPlayerHit()       => PlaySFX(sfxPlayerHit);
    public void PlayPlayerDeath()     => PlaySFX(sfxPlayerDeath);
    public void PlayPlayerRevive()    => PlaySFX(sfxPlayerRevive);
    public void PlayPlayerJump()      => PlaySFX(sfxPlayerJump);
    public void PlayPlayerLand()      => PlaySFX(sfxPlayerLand);

    public void PlayEnemyHit()        => PlaySFX(sfxEnemyHit);
    public void PlayEnemySlimeDeath() => PlaySFX(sfxEnemySlimeDeath);
    public void PlayEnemyDroneDeath() => PlaySFX(sfxEnemyDroneDeath);
    public void PlayEnemyBossRoar()   => PlaySFX(sfxEnemyBossRoar);
    public void PlayEnemyBossSpawn()  => PlaySFX(sfxEnemyBossSpawn);
    public void PlayEnemyTaxiCharge() => PlaySFX(sfxEnemyTaxiCharge);
    public void PlayEnemyBossSteam()  => PlaySFX(sfxEnemyBossSteam);
    public void PlayEnemyMinionSpawn()=> PlaySFX(sfxEnemyMinionSpawn);

    public void PlayWeaponShoot(AudioClip clip) => PlaySFX(clip);
    public void PlayCriticalHit()     => PlaySFX(sfxWeaponCriticalHit);
    public void PlayExplosion()       => PlaySFX(sfxWeaponExplosion);

    public void PlayUIClick()         => PlaySFX(sfxUIClick);
    public void PlayUIMenuOpen()      => PlaySFX(sfxUIMenuOpen);
    public void PlayBuffApply()       => PlaySFX(sfxUIBuffApply);
    public void PlayContractSelect()  => PlaySFX(sfxUIContractSelect);
    public void PlayWeaponEquip()     => PlaySFX(sfxUIWeaponEquip);
    public void PlayStageClear()      => PlaySFX(sfxStageClear);
    public void PlayGameClear()       => PlaySFX(sfxGameClear);
    public void PlayGameOver()        => PlaySFX(sfxGameOver);
    public void PlayPortalTeleport()  => PlaySFX(sfxPortalTeleport);
    public void PlayProjectileImpact()=> PlaySFX(sfxProjectileImpact);
    public void PlayRevivalPrompt()   => PlaySFX(sfxRevivalPrompt);

    public void SetMasterVolume(float v) { SetVolume(MasterVolumeParam, v); PlayerPrefs.SetFloat(MasterVolumeParam, v); }
    public void SetBGMVolume(float v)    { SetVolume(BGMVolumeParam, v);    PlayerPrefs.SetFloat(BGMVolumeParam, v); }
    public void SetSFXVolume(float v)    { SetVolume(SFXVolumeParam, v);    PlayerPrefs.SetFloat(SFXVolumeParam, v); }

    private void SetVolume(string paramName, float sliderValue)
    {
        float db = sliderValue > 0.0001f ? Mathf.Log10(sliderValue) * 20f : -80f;
        audioMixer.SetFloat(paramName, db);
    }

    private void LoadVolumeSettings()
    {
        SetVolume(MasterVolumeParam, PlayerPrefs.GetFloat(MasterVolumeParam, 1f));
        SetVolume(BGMVolumeParam,    PlayerPrefs.GetFloat(BGMVolumeParam, 1f));
        SetVolume(SFXVolumeParam,    PlayerPrefs.GetFloat(SFXVolumeParam, 1f));
    }

    public float GetMasterVolume() => PlayerPrefs.GetFloat(MasterVolumeParam, 1f);
    public float GetBGMVolume()    => PlayerPrefs.GetFloat(BGMVolumeParam, 1f);
    public float GetSFXVolume()    => PlayerPrefs.GetFloat(SFXVolumeParam, 1f);
}
