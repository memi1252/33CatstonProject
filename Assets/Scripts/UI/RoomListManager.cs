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

        // 방이 새로 생성되면 Photon이 짧은 시간 안에 세션 속성을 여러 번 연달아 갱신해서 보낸다.
        // 매번 그대로 Repaint()하면 슬롯이 계속 지워졌다 다시 생기면서(슬롯의 등장 애니메이션도 매번 재생되어)
        // 깜빡이듯 커졌다 작아졌다 하는 것처럼 보인다. 짧은 시간 안의 중복 갱신은 묶어서 한 번만 그린다.
        private bool _repaintPending;
        private float _repaintCooldown;
        private const float RepaintMinInterval = 0.4f;

        // 방 검색창에 이 문자열을 입력하고 엔터를 치면 무적모드 치트가 켜진다(다음에 방을 만들거나 들어갈 때 적용).
        private const string InvincibleCheatCode = "GBSWM";

        private void Awake()
        {
            if (RefreshButton != null)
                RefreshButton.onClick.AddListener(Refresh);
            if (SearchInputField != null)
            {
                SearchInputField.onValueChanged.AddListener(OnSearchChanged);
                SearchInputField.onEndEdit.AddListener(OnSearchEndEdit);
            }
        }

        private void OnSearchEndEdit(string text)
        {
            if (!string.Equals(text, InvincibleCheatCode, System.StringComparison.OrdinalIgnoreCase))
                return;

            GameManager.PendingInvincibleCheat = true;
            SearchInputField.SetTextWithoutNotify("");
            _searchKeyword = "";
            RequestRepaint();
            Debug.Log("[Cheat] 무적모드 치트 활성화 — 다음에 방을 만들거나 들어가면 적용됩니다.");
        }

        private void OnSearchChanged(string keyword)
        {
            _searchKeyword = keyword;
            RequestRepaint();
        }

        private void RequestRepaint()
        {
            _repaintPending = true;
            _repaintCooldown = RepaintMinInterval;
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
            if (_repaintPending)
            {
                _repaintCooldown -= Time.unscaledDeltaTime;
                if (_repaintCooldown <= 0f)
                {
                    _repaintPending = false;
                    DoRepaint();
                }
            }

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

            RequestRepaint();
        }

        // 세션 이름 -> 슬롯. 매번 전부 파괴/재생성하지 않고 기존 슬롯을 재사용해서
        // (방 생성 직후 Photon이 세션 정보를 연달아 갱신할 때) 깜빡이거나 순간적으로 커지는 걸 막는다.
        private readonly Dictionary<string, RoomSlot> _slotsByName = new();

        private void DoRepaint()
        {
            if (RoomSlotPrefab == null || RoomListContent == null) return;

            bool hasKeyword = !string.IsNullOrWhiteSpace(_searchKeyword);
            var wanted = new HashSet<string>();

            foreach (var s in _filteredSessions)
            {
                if (hasKeyword && !s.Name.Contains(_searchKeyword, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                wanted.Add(s.Name);

                if (_slotsByName.TryGetValue(s.Name, out var existing) && existing != null)
                {
                    existing.Bind(s, OnRoomClicked); // 기존 슬롯 재사용, 내용만 갱신
                }
                else
                {
                    var slot = Instantiate(RoomSlotPrefab, RoomListContent);
                    slot.Bind(s, OnRoomClicked);
                    _slotsByName[s.Name] = slot;
                    _slots.Add(slot);
                }
            }

            // 더 이상 목록에 없는 방의 슬롯만 제거
            var staleKeys = new List<string>();
            foreach (var kv in _slotsByName)
            {
                if (!wanted.Contains(kv.Key)) staleKeys.Add(kv.Key);
            }
            foreach (var key in staleKeys)
            {
                var slot = _slotsByName[key];
                _slotsByName.Remove(key);
                _slots.Remove(slot);
                if (slot != null) Destroy(slot.gameObject);
            }

            if (EmptyLabel != null)
                EmptyLabel.SetActive(_slots.Count == 0);
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