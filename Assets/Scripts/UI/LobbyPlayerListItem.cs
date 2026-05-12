using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 플레이어 목록의 한 행. 닉네임 + 준비 상태 표시.
/// </summary>
public class LobbyPlayerListItem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private TextMeshProUGUI _readyText;
    [SerializeField] private Image _readyBackground;

    [Header("Labels")]
    [SerializeField] private string _readyLabel = "READY";
    [SerializeField] private string _unreadyLabel = "...";

    [Header("Colors")]
    [SerializeField] private Color _readyColor = new Color(0.30f, 0.85f, 0.30f);
    [SerializeField] private Color _unreadyColor = new Color(0.60f, 0.60f, 0.60f);

    public PlayerRef PlayerRef { get; private set; }

    public void Bind(PlayerRef playerRef, string nickname, bool isReady)
    {
        PlayerRef = playerRef;

        if (_nicknameText != null)
            _nicknameText.text = string.IsNullOrEmpty(nickname) ? playerRef.ToString() : nickname;

        SetReady(isReady);
    }

    public void SetNickname(string nickname)
    {
        if (_nicknameText != null && !string.IsNullOrEmpty(nickname))
            _nicknameText.text = nickname;
    }

    public void SetReady(bool isReady)
    {
        if (_readyText != null)
            _readyText.text = isReady ? _readyLabel : _unreadyLabel;
        if (_readyBackground != null)
            _readyBackground.color = isReady ? _readyColor : _unreadyColor;
    }
}