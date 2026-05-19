using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 텔레포트 확인창과 페이드 오버레이를 관리하는 씬 싱글톤.
/// 씬에 한 개만 존재해야 합니다.
/// </summary>
public class TeleportManager : MonoBehaviour
{
    public static TeleportManager Instance { get; private set; }

    [Header("Confirm Panel")]
    [Tooltip("Yes/No 버튼을 가진 확인 패널. 시작 시 자동 비활성화.")]
    [SerializeField] private GameObject confirmPanel;
    [Tooltip("확인창 메시지 텍스트. {0}이 포털 이름으로 치환됩니다.")]
    [SerializeField] private TextMeshProUGUI confirmMessageText;
    [SerializeField] private string messageFormat = "{0}(으)로 이동하시겠습니까?";
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Fade Overlay")]
    [Tooltip("풀스크린 검정 Image에 붙은 CanvasGroup. alpha 0 시작.")]
    [SerializeField] private CanvasGroup fadeOverlay;

    private Action _onYes;
    private Action _onNo;

    /// <summary>확인창이 열려 있거나 텔레포트가 진행 중이면 true.</summary>
    public bool IsBusy { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
            fadeOverlay.interactable = false;
        }

        if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
        if (noButton != null) noButton.onClick.AddListener(OnNoClicked);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowConfirm(Portal portal, Action onYes, Action onNo)
    {
        if (IsBusy) return;
        IsBusy = true;
        _onYes = onYes;
        _onNo = onNo;

        if (confirmMessageText != null && portal != null)
            confirmMessageText.text = string.Format(messageFormat, portal.PortalName);

        if (confirmPanel != null) confirmPanel.SetActive(true);
        else { OnYesClicked(); } // 패널이 없으면 즉시 동의 처리
    }

    private void OnYesClicked()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        var cb = _onYes;
        _onYes = null;
        _onNo = null;
        // IsBusy 는 Player 코루틴이 끝날 때 NotifyTeleportFinished 로 해제
        cb?.Invoke();
    }

    private void OnNoClicked()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        IsBusy = false;
        var cb = _onNo;
        _onYes = null;
        _onNo = null;
        cb?.Invoke();
    }

    /// <summary>확인창 없이 바로 텔레포트를 시작할 때 사용. IsBusy를 켜 중복 트리거 방지.</summary>
    public void BeginTeleport()
    {
        IsBusy = true;
    }

    /// <summary>Player가 텔레포트 코루틴 끝에서 호출.</summary>
    public void NotifyTeleportFinished()
    {
        IsBusy = false;
    }

    public IEnumerator FadeOut(float duration) => FadeRoutine(0f, 1f, duration, true);
    public IEnumerator FadeIn(float duration) => FadeRoutine(1f, 0f, duration, false);

    private IEnumerator FadeRoutine(float from, float to, float duration, bool blockRaycasts)
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.blocksRaycasts = blockRaycasts;

        if (duration <= 0f)
        {
            fadeOverlay.alpha = to;
            if (!blockRaycasts) fadeOverlay.blocksRaycasts = false;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fadeOverlay.alpha = to;
        if (!blockRaycasts) fadeOverlay.blocksRaycasts = false;
    }
}