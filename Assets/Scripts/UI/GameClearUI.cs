using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameClearUI : MonoBehaviour
{
    [Header("UI 요소")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subText;
    public Button lobbyButton;
    public TMP_FontAsset galmuriFont;

    private void Awake()
    {
        gameObject.SetActive(false);

        if (lobbyButton != null)
            lobbyButton.onClick.AddListener(OnLobbyClicked);

        ApplyFont();
    }

    private void ApplyFont()
    {
        if (galmuriFont == null) return;
        if (titleText != null) titleText.font = galmuriFont;
        if (subText != null) subText.font = galmuriFont;

        if (lobbyButton != null)
        {
            var txt = lobbyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.font = galmuriFont;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "게임 클리어!";
        if (subText != null) subText.text = "최종 보스를 처치했습니다!";
    }

private void OnLobbyClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundManager.Instance?.sfxUIDisconnect);
        var runner = Fusion.NetworkRunner.Instances?.GetEnumerator();
        if (runner != null && runner.MoveNext())
        {
            var r = runner.Current;
            if (r != null) _ = r.Shutdown();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }
}
