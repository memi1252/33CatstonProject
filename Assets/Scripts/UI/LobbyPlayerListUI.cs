using System.Collections.Generic;
using Fusion;
using Starter.Platformer;
using UnityEngine;

/// <summary>
/// 로비에서 입장한 플레이어 목록 + 준비 상태를 보여주는 UI.
/// LobbyReadyManager 인스턴스가 있을 때만 동작하고, 없으면 자동으로 비움.
/// </summary>
public class LobbyPlayerListUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _container;
    [SerializeField] private LobbyPlayerListItem _itemPrefab;

    private readonly Dictionary<PlayerRef, LobbyPlayerListItem> _items = new();

    private void Start()
    {
        if (_container == null) _container = transform;

        // 디자인 단계 placeholder 제거
        for (int i = _container.childCount - 1; i >= 0; i--)
            Destroy(_container.GetChild(i).gameObject);
    }

    private void Update()
    {
        var manager = LobbyReadyManager.Instance;

        // 매니저가 사라졌으면(씬 전환 등) 모두 정리
        if (manager == null || manager.Object == null)
        {
            ClearAll();
            return;
        }

        var runner = manager.Runner;
        if (runner == null) return;

        // 활성 플레이어 집합
        var activeSet = new HashSet<PlayerRef>();
        foreach (var p in runner.ActivePlayers) activeSet.Add(p);

        // 떠난 플레이어 정리
        var toRemove = new List<PlayerRef>();
        foreach (var kv in _items)
        {
            if (!activeSet.Contains(kv.Key)) toRemove.Add(kv.Key);
        }
        foreach (var p in toRemove)
        {
            if (_items.TryGetValue(p, out var item) && item != null)
                Destroy(item.gameObject);
            _items.Remove(p);
        }

        // 새로 들어온 플레이어 추가 + 갱신
        foreach (var p in runner.ActivePlayers)
        {
            string nick = ResolveNickname(runner, p);
            bool isReady = manager.IsReady(p);

            if (!_items.TryGetValue(p, out var item) || item == null)
            {
                item = Instantiate(_itemPrefab, _container);
                item.Bind(p, nick, isReady);
                _items[p] = item;
            }
            else
            {
                item.SetNickname(nick);
                item.SetReady(isReady);
            }
        }
    }

    private void ClearAll()
    {
        foreach (var kv in _items)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        _items.Clear();
    }

    private string ResolveNickname(NetworkRunner runner, PlayerRef p)
    {
        if (runner.TryGetPlayerObject(p, out var obj) && obj != null)
        {
            var player = obj.GetComponent<Player>();
            if (player != null)
            {
                string nick = player.Nickname.ToString();
                if (!string.IsNullOrEmpty(nick)) return nick;
            }
        }
        if (GameManager.Instance != null)
        {
            string name = GameManager.Instance.GetPlayerName(p);
            if (!string.IsNullOrEmpty(name) && name != "Unknown") return name;
        }
        return string.Empty;
    }
}