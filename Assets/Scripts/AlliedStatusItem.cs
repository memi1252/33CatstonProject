using UnityEngine;
using UnityEngine.UI;
using Starter.Platformer;
using TMPro;

/// <summary>
/// 개별 팀원의 상태를 표시하는 UI 아이템
/// 닉네임, HP 게이지, 상태(생존/사망) 등을 표시합니다
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

    private Player _player;
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

        if (_nicknameText != null)
        {
            _nicknameText.text = _player.Nickname;
        }
    }

    /// <summary>
    /// 플레이어의 현재 상태로 UI를 업데이트합니다
    /// </summary>
    public void UpdateStatus(Player player)
    {
        if (_player == null || player != _player)
            return;

        // 닉네임도 매 프레임 갱신 (스폰 직후 빈 값 → 동기화 후 채워지는 케이스 대응)
        if (_nicknameText != null && !string.IsNullOrEmpty(_player.Nickname.ToString()))
        {
            _nicknameText.text = _player.Nickname.ToString();
        }

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