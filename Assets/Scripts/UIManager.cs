using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public StatsUI statsUI;
    public GameObject buffUI;
    public GameObject weaponUI;

    // 영구(DontDestroyOnLoad) UI를 게임 씬의 매니저들이 런타임에 참조하기 위한 핸들.
    // 이 필드들은 UIManager가 처음 생성되는 씬(로비)에서 한 번만 연결하면 됨.
    [Header("Buff UI (BuffManager가 런타임에 사용)")]
    public Image buffTimerFillImage;
    public TextMeshProUGUI buffTimerText;
    public Transform buffSlotParent;

    [Header("Chat UI (ChatManager가 런타임에 사용)")]
    public InputField chatInput;
    public TextMeshProUGUI chatMessageText;

    [Header("Weapon UI (WeaponManager가 런타임에 사용)")]
    public Transform weaponSelectPanel;
    public Image weaponTimerFillImage;
    public TextMeshProUGUI weaponTimerText;

    [Header("게임오버 / 클리어 UI")]
    public GameOverUI gameOverUI;
    public GameClearUI gameClearUI;

    [Header("카운트다운 UI")]
    public CountdownUI countdownUI;

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 로비/메인으로 돌아오면 게임 전용 UI 초기화
        if (scene.buildIndex == 0 || scene.buildIndex == 1)
        {
            if (gameOverUI != null) gameOverUI.gameObject.SetActive(false);
            if (gameClearUI != null) gameClearUI.gameObject.SetActive(false);
            if (countdownUI != null) countdownUI.gameObject.SetActive(false);
            if (buffUI != null) buffUI.SetActive(false);
            if (weaponUI != null) weaponUI.SetActive(false);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 씬 재진입 시 새 씬의 UI 레퍼런스를 기존 인스턴스에 갱신
            Instance.RefreshSceneReferences(this);
            Destroy(gameObject);
        }
    }

    // 씬 전환 후 씬에 새로 생성된 UI 오브젝트들로 참조 갱신
    private void RefreshSceneReferences(UIManager src)
    {
        if (src.statsUI != null)               statsUI = src.statsUI;
        if (src.buffUI != null)                buffUI = src.buffUI;
        if (src.weaponUI != null)              weaponUI = src.weaponUI;
        if (src.buffTimerFillImage != null)    buffTimerFillImage = src.buffTimerFillImage;
        if (src.buffTimerText != null)         buffTimerText = src.buffTimerText;
        if (src.buffSlotParent != null)        buffSlotParent = src.buffSlotParent;
        if (src.chatInput != null)             chatInput = src.chatInput;
        if (src.chatMessageText != null)       chatMessageText = src.chatMessageText;
        if (src.weaponSelectPanel != null)     weaponSelectPanel = src.weaponSelectPanel;
        if (src.weaponTimerFillImage != null)  weaponTimerFillImage = src.weaponTimerFillImage;
        if (src.weaponTimerText != null)       weaponTimerText = src.weaponTimerText;
        // gameOverUI / gameClearUI / countdownUI는 UIManager 자식으로 DontDestroyOnLoad와 함께 살아있으므로 갱신 불필요
    }
}
