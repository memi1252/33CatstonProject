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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
