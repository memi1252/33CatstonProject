using UnityEngine;
using UnityEngine.UI;
using Starter.Platformer;
using TMPro;
using Fusion;

/// <summary>
/// 개별 팀원의 상태를 표시하는 UI 아이템
/// 닉네임, HP 게이지, 상태(생존/사망), 로비에서는 준비 상태 등을 표시합니다
/// </summary>
public class AlliedStatusItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _nicknameText; // 플레이어 닉네임
    [SerializeField] private Image _hpBarImage; // HP 게이지
    [SerializeField] private TextMeshProUGUI _hpText; // HP 텍스트 (예: 100/100)
    [SerializeField] private Image _statusIndicator; // 상태 표시 이미지 (생존/사망)
    [SerializeField] private Sprite _aliveSprite;
    [SerializeField] private Sprite _deadSprite;

    [Header("Lobby Ready Indicator")]
    [SerializeField] private GameObject _readyRoot; // 로비에서만 활성화되는 준비 표시 루트 (옵션)
    [SerializeField] private TextMeshProUGUI _readyText; // "READY" / "..." 텍스트
    [SerializeField] private Image _readyBackground; // 준비 상태 배경
    [SerializeField] private Color _readyColor = new Color(0.30f, 0.85f, 0.30f);
    [SerializeField] private Color _unreadyColor = new Color(0.60f, 0.60f, 0.60f);

    private Player _player;
    private PlayerRef _playerRef;
    private CanvasGroup _canvasGroup; // 페이드 효과를 위한 CanvasGroup

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 이 UI 아이템을 초기화합니다
    /// </summary>
    public void Initialize(Player player)
    {
        _player = player;
        _playerRef = player != null && player.Object != null ? player.Object.StateAuthority : default;

        if (_nicknameText != null)
        {
            _nicknameText.text = _player.Nickname;
        }
    }

    /// <summary>로비 모드에서 PlayerRef로 직접 초기화 (Player 프리팹이 없는 시점)</summary>
    public void InitializeForLobby(PlayerRef playerRef, string nickname)
    {
        _player = null;
        _playerRef = playerRef;

        if (_nicknameText != null)
            _nicknameText.text = string.IsNullOrEmpty(nickname) ? playerRef.ToString() : nickname;

        // HP/상태는 게임 중에만 의미가 있으므로 숨김
        if (_hpBarImage != null) _hpBarImage.gameObject.SetActive(false);
        if (_hpText != null) _hpText.gameObject.SetActive(false);
        if (_statusIndicator != null) _statusIndicator.gameObject.SetActive(false);
    }

    /// <summary>로비 준비 상태 표시 갱신</summary>
    public void UpdateReadyState(bool isReady)
    {
        if (_readyRoot != null) _readyRoot.SetActive(true);
        if (_readyText != null) _readyText.text = isReady ? "READY" : "...";
        if (_readyBackground != null) _readyBackground.color = isReady ? _readyColor : _unreadyColor;
    }

    /// <summary>준비 표시 숨김 (게임 중)</summary>
    public void HideReadyState()
    {
        if (_readyRoot != null) _readyRoot.SetActive(false);
    }

    public PlayerRef PlayerRef => _playerRef;

    /// <summary>
    /// 플레이어의 현재 상태로 UI를 업데이트합니다
    /// </summary>
    public void UpdateStatus(Player player)
    {
        if (_player == null || player != _player)
            return;

        // HP 게이지 업데이트
        if (_hpBarImage != null)
        {
            float hpPercent = _player.maxHp > 0 ? _player.hp / _player.maxHp : 0f;
            _hpBarImage.fillAmount = Mathf.Clamp01(hpPercent);
        }

        // HP 텍스트 업데이트
        if (_hpText != null)
        {
            _hpText.text = $"{Mathf.Max(0, _player.hp):F0}/{_player.maxHp:F0}";
        }

        // 상태 표시 업데이트
        if (_statusIndicator != null)
        {
            if (_player.dead)
            {
                _statusIndicator.sprite = _deadSprite;
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0.5f; // 사망 시 투명도 낮춤
                }
            }
            else
            {
                _statusIndicator.sprite = _aliveSprite;
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f; // 생존 시 투명도 정상
                }
            }
        }
    }
}


