using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace Starter
{
    /// <summary>
    /// 포톤 퓨전 라운지에 연결해서 열린 세션 목록을 받아 RoomSlot으로 표시.
    /// 슬롯을 클릭하면 UIGameMenu.JoinRoom(sessionName)으로 입장.
    /// </summary>
    public class RoomListManager : MonoBehaviour
    {
        [Header("References")]
        public UIGameMenu GameMenu;
        public NetworkRunner LobbyRunnerPrefab;
        public Transform RoomListContent;
        public RoomSlot RoomSlotPrefab;

        [Header("Optional")]
        public GameObject EmptyLabel;
        public Button RefreshButton;

        [Header("Filter")]
        [Tooltip("같은 GameMode 세션만 표시. 비워두면 모든 세션 표시.")]
        public string GameModeFilter;

        [Header("Search")]
        [Tooltip("방 이름 검색에 연결할 InputField (RoomText)")]
        public TMPro.TMP_InputField SearchInputField;

        private string _searchKeyword = "";

        [Header("Lobby")]
        public string LobbyName = "default";

        [Header("Ping")]
        [Tooltip("핑 갱신 주기 (초)")]
        public float PingUpdateInterval = 1f;

        private NetworkRunner _lobbyRunner;
        private readonly List<SessionInfo> _filteredSessions = new();
        private readonly List<RoomSlot> _slots = new();
        private bool _isJoining;
        private float _pingTimer;

        private void Awake()
        {
            if (RefreshButton != null)
                RefreshButton.onClick.AddListener(Refresh);
            if (SearchInputField != null)
                SearchInputField.onValueChanged.AddListener(OnSearchChanged);
        }

        private void OnSearchChanged(string keyword)
        {
            _searchKeyword = keyword;
            Repaint();
        }

        // region → Photon NameServer 호스트 매핑 (ICMP 핑용)
        private static readonly Dictionary<string, string> RegionPingHosts = new()
        {
            { "kr", "ns.kr.photonengine.com" },
            { "asia", "ns.asia.photonengine.com" },
            { "jp", "ns.jp.photonengine.com" },
            { "us", "ns.us.photonengine.com" },
            { "usw", "ns.usw.photonengine.com" },
            { "eu", "ns.eu.photonengine.com" },
            { "sa", "ns.sa.photonengine.com" },
            { "in", "ns.in.photonengine.com" },
            { "au", "ns.au.photonengine.com" },
            { "ru", "ns.ru.photonengine.com" },
            { "tr", "ns.tr.photonengine.com" },
            { "za", "ns.za.photonengine.com" },
        };
        private const string FallbackPingHost = "ns.photonengine.io";

        private int _currentPingMs = -1;
        private bool _pingInFlight;

        private void Update()
        {
            if (_lobbyRunner == null || _slots.Count == 0) return;

            _pingTimer -= Time.unscaledDeltaTime;
            if (_pingTimer > 0f) return;
            _pingTimer = PingUpdateInterval;

            // 첫 슬롯의 region을 사용 (대개 모두 같은 리전)
            string host = ResolvePingHost();
            _ = PingHostAsync(host);

            // 마지막으로 측정된 값으로 모든 슬롯 갱신
            foreach (var slot in _slots)
            {
                if (slot != null) slot.SetPing(_currentPingMs);
            }
        }

        private string ResolvePingHost()
        {
            string region = null;
            if (_filteredSessions.Count > 0)
                region = _filteredSessions[0].Region;

            if (!string.IsNullOrEmpty(region) && RegionPingHosts.TryGetValue(region.ToLowerInvariant(), out var host))
                return host;
            return FallbackPingHost;
        }

        private async Task PingHostAsync(string host)
        {
            if (_pingInFlight) return;
            _pingInFlight = true;

            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(host, 2000);
                if (reply.Status == IPStatus.Success)
                    _currentPingMs = (int)reply.RoundtripTime;
                else
                    _currentPingMs = -1;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoomListManager] Ping failed to {host}: {e.Message}");
                _currentPingMs = -1;
            }
            finally
            {
                _pingInFlight = false;
            }
        }

        private void OnEnable()
        {
            _ = JoinLobbyAsync();
        }

        private void OnDisable()
        {
            _ = LeaveLobbyAsync();
        }

        public void Refresh()
        {
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            await LeaveLobbyAsync();
            await JoinLobbyAsync();
        }

        private async Task JoinLobbyAsync()
        {
            if (_lobbyRunner != null) return;
            if (LobbyRunnerPrefab == null)
            {
                Debug.LogError("[RoomListManager] LobbyRunnerPrefab is not assigned");
                return;
            }

            _lobbyRunner = Instantiate(LobbyRunnerPrefab);
            _lobbyRunner.name = "LobbyRunner";

            var events = _lobbyRunner.GetComponent<NetworkEvents>();
            if (events == null)
                events = _lobbyRunner.gameObject.AddComponent<NetworkEvents>();
            events.OnSessionListUpdate.AddListener(OnSessionListUpdated);

            var result = await _lobbyRunner.JoinSessionLobby(SessionLobby.Shared, LobbyName);
            if (!result.Ok)
            {
                Debug.LogWarning($"[RoomListManager] Lobby join failed: {result.ShutdownReason}");
            }
        }

        private async Task LeaveLobbyAsync()
        {
            if (_lobbyRunner == null) return;

            var events = _lobbyRunner.GetComponent<NetworkEvents>();
            if (events != null)
                events.OnSessionListUpdate.RemoveListener(OnSessionListUpdated);

            var runner = _lobbyRunner;
            _lobbyRunner = null;
            await runner.Shutdown();
        }

        private void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessions)
        {
            _filteredSessions.Clear();
            foreach (var s in sessions)
            {
                if (s == null) continue;
                if (!s.IsVisible) continue;
                if (!string.IsNullOrEmpty(GameModeFilter))
                {
                    if (!s.Properties.TryGetValue("GameMode", out var prop)) continue;
                    if ((string)prop != GameModeFilter) continue;
                }
                _filteredSessions.Add(s);
            }

            Repaint();
        }

        private void Repaint()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i] != null) Destroy(_slots[i].gameObject);
            }
            _slots.Clear();

            bool hasKeyword = !string.IsNullOrWhiteSpace(_searchKeyword);
            int shown = 0;
            foreach (var s in _filteredSessions)
            {
                if (RoomSlotPrefab == null || RoomListContent == null) break;
                if (hasKeyword && !s.Name.Contains(_searchKeyword, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var slot = Instantiate(RoomSlotPrefab, RoomListContent);
                slot.Bind(s, OnRoomClicked);
                _slots.Add(slot);
                shown++;
            }

            if (EmptyLabel != null)
                EmptyLabel.SetActive(shown == 0);
        }

        private async void OnRoomClicked(SessionInfo session)
        {
            if (_isJoining) return;
            if (GameMenu == null)
            {
                Debug.LogWarning("[RoomListManager] GameMenu reference is missing");
                return;
            }

            _isJoining = true;
            // 라운지 러너는 종료하고 GameMenu가 본 게임 러너를 새로 띄움
            await LeaveLobbyAsync();
            GameMenu.JoinRoom(session.Name);
            _isJoining = false;
        }
    }
}