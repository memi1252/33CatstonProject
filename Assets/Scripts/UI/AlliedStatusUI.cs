using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Starter.Platformer;
using Fusion;

/// <summary>
/// 팀원(아군)의 상태를 화면에 표시하는 UI
/// 게임 중: 각 플레이어의 HP, 닉네임, 상태
/// 로비 중: 모든 플레이어의 닉네임 + 준비 상태
/// </summary>
public class AlliedStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform _alliedContainer; // 팀원 UI 아이템들이 들어갈 컨테이너
    [SerializeField] private AlliedStatusItem _alliedStatusItemPrefab; // 팀원 상태 UI 프리팹

    [Header("Settings")]
    [SerializeField] private bool _showLocalPlayer = false; // 로컬 플레이어 표시 여부 (게임 중)

    // 게임 모드: Player 인스턴스 기준
    private Dictionary<Player, AlliedStatusItem> _alliedUIItems = new Dictionary<Player, AlliedStatusItem>();
    private Player _localPlayer;

    // 로비 모드: PlayerRef 기준
    private Dictionary<PlayerRef, AlliedStatusItem> _lobbyUIItems = new Dictionary<PlayerRef, AlliedStatusItem>();

    private void Start()
    {
        if (_alliedContainer == null)
        {
            _alliedContainer = transform;
        }
    }

    private void Update()
    {
        // 로비 매니저가 있으면 로비 모드 우선
        if (LobbyReadyManager.Instance != null && LobbyReadyManager.Instance.Object != null)
        {
            UpdateLobbyUI();
            return;
        }

        // GameManager가 초기화될 때까지 대기
        if (GameManager.Instance == null || GameManager.Instance.LocalPlayer == null)
            return;

        if (_localPlayer == null)
        {
            _localPlayer = GameManager.Instance.LocalPlayer;
        }

        // 로비에서 게임으로 전환되었으면 로비 UI 정리
        if (_lobbyUIItems.Count > 0)
            ClearLobbyUI();

        UpdateAlliedUI();
    }

    private void ClearLobbyUI()
    {
        foreach (var kv in _lobbyUIItems)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        _lobbyUIItems.Clear();
    }

    private void UpdateLobbyUI()
    {
        var manager = LobbyReadyManager.Instance;
        var runner = manager.Runner;
        if (runner == null) return;

        var activeSet = new HashSet<PlayerRef>();
        foreach (var p in runner.ActivePlayers) activeSet.Add(p);

        // 떠난 플레이어 정리
        var toRemove = new List<PlayerRef>();
        foreach (var kv in _lobbyUIItems)
        {
            if (!activeSet.Contains(kv.Key)) toRemove.Add(kv.Key);
        }
        foreach (var p in toRemove)
        {
            if (_lobbyUIItems.TryGetValue(p, out var item) && item != null)
                Destroy(item.gameObject);
            _lobbyUIItems.Remove(p);
        }

        // 새로 들어온 플레이어 추가 + 갱신
        foreach (var p in runner.ActivePlayers)
        {
            if (!_lobbyUIItems.TryGetValue(p, out var item) || item == null)
            {
                item = Instantiate(_alliedStatusItemPrefab, _alliedContainer);
                string nick = ResolveNickname(runner, p);
                item.InitializeForLobby(p, nick);
                _lobbyUIItems[p] = item;
            }

            item.UpdateReadyState(manager.IsReady(p));
        }
    }

    private string ResolveNickname(NetworkRunner runner, PlayerRef p)
    {
        if (runner.TryGetPlayerObject(p, out var obj) && obj != null)
        {
            var player = obj.GetComponent<Player>();
            if (player != null && !string.IsNullOrEmpty(player.Nickname.ToString()))
                return player.Nickname.ToString();
        }
        if (GameManager.Instance != null)
        {
            var name = GameManager.Instance.GetPlayerName(p);
            if (!string.IsNullOrEmpty(name) && name != "Unknown") return name;
        }
        return p.ToString();
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
                uiItem.HideReadyState();
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
