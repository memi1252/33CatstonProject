using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Starter.Platformer;

/// <summary>
/// 팀원(아군)의 상태를 화면에 표시하는 UI
/// 각 플레이어의 HP, 닉네임, 상태를 실시간으로 업데이트합니다
/// </summary>
public class AlliedStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform _alliedContainer; // 팀원 UI 아이템들이 들어갈 컨테이너
    [SerializeField] private AlliedStatusItem _alliedStatusItemPrefab; // 팀원 상태 UI 프리팹

    [Header("Settings")]
    [SerializeField] private bool _showLocalPlayer = false; // 로컬 플레이어 표시 여부

    private Dictionary<Player, AlliedStatusItem> _alliedUIItems = new Dictionary<Player, AlliedStatusItem>();
    private Player _localPlayer;

    private void Start()
    {
        if (_alliedContainer == null)
        {
            _alliedContainer = transform;
        }

        // 디자인 단계에 컨테이너에 박혀있던 미리보기/placeholder 아이템들을 모두 제거
        for (int i = _alliedContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(_alliedContainer.GetChild(i).gameObject);
        }
    }

    private void Update()
    {
        // GameManager가 초기화될 때까지 대기
        if (GameManager.Instance == null || GameManager.Instance.LocalPlayer == null)
            return;

        if (_localPlayer == null)
        {
            _localPlayer = GameManager.Instance.LocalPlayer;
        }

        UpdateAlliedUI();
    }

    /// <summary>
    /// 모든 팀원의 상태를 업데이트합니다
    /// </summary>
    private void UpdateAlliedUI()
    {
        // 모든 플레이어 찾기
        Player[] allPlayers = FindObjectsOfType<Player>();

        // 현재 활성 플레이어들 추적
        HashSet<Player> activePlayerSet = new HashSet<Player>(allPlayers);

        // 죽은 플레이어 UI 제거
        List<Player> deadPlayers = new List<Player>();
        foreach (var player in _alliedUIItems.Keys)
        {
            if (player == null || !activePlayerSet.Contains(player))
            {
                deadPlayers.Add(player);
            }
        }

        foreach (var player in deadPlayers)
        {
            if (_alliedUIItems.TryGetValue(player, out var uiItem))
            {
                Destroy(uiItem.gameObject);
                _alliedUIItems.Remove(player);
            }
        }

        // 플레이어별로 UI 업데이트
        foreach (Player player in allPlayers)
        {
            // 로컬 플레이어 제외 (원하지 않으면)
            if (!_showLocalPlayer && player == _localPlayer)
                continue;

            // 새로운 플레이어라면 UI 아이템 생성
            if (!_alliedUIItems.ContainsKey(player))
            {
                CreateAlliedStatusItem(player);
            }

            // 기존 UI 아이템 업데이트
            if (_alliedUIItems.TryGetValue(player, out var uiItem))
            {
                uiItem.UpdateStatus(player);
            }
        }
    }

    /// <summary>
    /// 특정 플레이어의 상태 UI 아이템을 생성합니다
    /// </summary>
    private void CreateAlliedStatusItem(Player player)
    {
        AlliedStatusItem newItem = Instantiate(_alliedStatusItemPrefab, _alliedContainer);
        newItem.Initialize(player);
        _alliedUIItems[player] = newItem;
    }
}