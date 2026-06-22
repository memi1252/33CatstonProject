using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비의 Ready 버튼 UI. 인원 < MinPlayers면 비활성화되고, 클릭 시 본인 준비상태 토글.
/// </summary>
public class LobbyReadyUI : MonoBehaviour
{
    [Header("References")]
    public Button ReadyButton;
    public TextMeshProUGUI ReadyButtonLabel;
    public TextMeshProUGUI StatusText;

    [Header("Labels")]
    public string LabelReady = "준비";
    public string LabelUnready = "준비 완료";
    public string LabelWaiting = "대기 중...";

    [Header("Colors")]
    public Color ColorReady = new Color(0.30f, 0.85f, 0.30f);
    public Color ColorUnready = new Color(0.95f, 0.80f, 0.20f);
    public Color ColorDisabled = new Color(0.50f, 0.50f, 0.50f);

    private NetworkRunner _runner;

    private void Awake()
    {
        if (ReadyButton != null)
            ReadyButton.onClick.AddListener(OnReadyClicked);
    }

    private void Update()
    {
        if (LobbyReadyManager.Instance == null)
        {
            SetDisabled(LabelWaiting);
            return;
        }

        if (_runner == null)
            _runner = LobbyReadyManager.Instance.Runner;
        if (_runner == null)
        {
            SetDisabled(LabelWaiting);
            return;
        }

        var local = _runner.LocalPlayer;
        bool canReady = LobbyReadyManager.Instance.CanReady();
        bool isReady = LobbyReadyManager.Instance.IsReady(local);

        if (ReadyButton != null)
            ReadyButton.interactable = canReady;

        if (ReadyButtonLabel != null)
        {
            if (!canReady)
            {
                ReadyButtonLabel.text = LabelWaiting;
                ReadyButtonLabel.color = ColorDisabled;
            }
            else
            {
                ReadyButtonLabel.text = isReady ? LabelUnready : LabelReady;
                ReadyButtonLabel.color = isReady ? ColorReady : ColorUnready;
            }
        }

        if (StatusText != null)
        {
            int playerCount = 0;
            int readyCount = 0;
            foreach (var p in _runner.ActivePlayers)
            {
                playerCount++;
                if (LobbyReadyManager.Instance.IsReady(p)) readyCount++;
            }
            StatusText.text = $"{readyCount} / {playerCount} 준비 (최소 {LobbyReadyManager.Instance.MinPlayers}명 필요)";
        }
    }

    private void SetDisabled(string label)
    {
        if (ReadyButton != null) ReadyButton.interactable = false;
        if (ReadyButtonLabel != null)
        {
            ReadyButtonLabel.text = label;
            ReadyButtonLabel.color = ColorDisabled;
        }
        if (StatusText != null)
            StatusText.text = string.Empty;
    }

private void OnReadyClicked()
    {
        if (LobbyReadyManager.Instance == null) return;
        if (_runner == null) return;
        if (!LobbyReadyManager.Instance.CanReady()) return;

        SoundManager.Instance?.PlaySFX(SoundManager.Instance?.sfxUIReady);
        bool current = LobbyReadyManager.Instance.IsReady(_runner.LocalPlayer);
        LobbyReadyManager.Instance.RPC_SetReady(_runner.LocalPlayer, !current);
    }
}