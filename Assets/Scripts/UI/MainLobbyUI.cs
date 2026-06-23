using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainLobbyUI : MonoBehaviour
{
    public Button gameStartButton;
    public Button gameExitButton;

    [Header("로딩 화면")]
    public TMPro.TMP_FontAsset loadingFont;

    private GameObject _loadingOverlay;

    private void Awake()
    {
        gameStartButton.onClick.AddListener(OnGameStartButtonClicked);
        gameExitButton.onClick.AddListener(OnGameExitButtonClicked);
        BuildLoadingOverlay();
    }

    private void BuildLoadingOverlay()
    {
        var canvas = GetComponent<Canvas>();

        _loadingOverlay = new GameObject("LoadingOverlay");
        _loadingOverlay.transform.SetParent(transform, false);

        var rt = _loadingOverlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var bg = _loadingOverlay.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);

        // 로딩 텍스트
        var textGO = new GameObject("LoadingText");
        textGO.transform.SetParent(_loadingOverlay.transform, false);
        var tRT = textGO.AddComponent<RectTransform>();
        tRT.anchorMin = tRT.anchorMax = new Vector2(0.5f, 0.5f);
        tRT.sizeDelta = new Vector2(400, 80);
        tRT.anchoredPosition = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "로딩 중...";
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (loadingFont != null) tmp.font = loadingFont;

        _loadingOverlay.transform.SetAsLastSibling();
        _loadingOverlay.SetActive(false);
    }

    private void OnGameStartButtonClicked()
    {
        gameStartButton.interactable = false;
        StartCoroutine(LoadLobbyAsync());
    }

    private IEnumerator LoadLobbyAsync()
    {
        _loadingOverlay.SetActive(true);
        var bg = _loadingOverlay.GetComponent<Image>();
        var tmp = _loadingOverlay.GetComponentInChildren<TextMeshProUGUI>();

        // 로딩과 페이드 인을 동시에 시작
        var op = SceneManager.LoadSceneAsync("LobbyScene");
        op.allowSceneActivation = false;

        float t = 0f;
        int dots = 0;
        float dotTimer = 0f;

        while (true)
        {
            t += Time.deltaTime;
            dotTimer += Time.deltaTime;

            // 배경 페이드 인 (0.4초)
            bg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.85f, t / 0.4f));

            // 점 애니메이션
            if (dotTimer >= 0.35f) { dotTimer = 0f; dots = (dots + 1) % 4; }
            tmp.text = "로딩 중" + new string('.', dots);

            // 로드 완료 + 페이드 인 끝나면 전환
            if (op.progress >= 0.9f && t >= 0.4f)
                break;

            yield return null;
        }

        op.allowSceneActivation = true;
    }

    private void OnGameExitButtonClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundManager.Instance?.sfxUIDisconnect);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
