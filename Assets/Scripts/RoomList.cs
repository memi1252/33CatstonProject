using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace Starter
{
    // MonoBehaviour를 상속받고 Fusion의 콜백 인터페이스를 구현합니다.
    public class RoomList : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("References")]
        public UIGameMenu GameMenu;          // 기존 코드와 연결
        public GameObject RoomEntryPrefab;   // 방 정보를 표시할 UI 프리팹
        public Transform ContentParent;      // ScrollView의 Content

        private NetworkRunner _lobbyRunner;

        public async void RefreshLobby()
        {
            Debug.Log("방 목록 새로고침 시도...");

            ClearListUI();

            if (_lobbyRunner != null)
            {
                await _lobbyRunner.Shutdown(destroyGameObject: false);
                _lobbyRunner = null;
            }

            await Task.Delay(100);

            await ConnectToLobby();
        }

        private async Task ConnectToLobby()
        {
            if (this == null || gameObject == null) return;

            // UI 참조 체크
            if (ContentParent == null)
            {
                Debug.LogError("[RoomList] ContentParent가 null입니다. Inspector에서 설정하세요!");
                return;
            }

            if (RoomEntryPrefab == null)
            {
                Debug.LogError("[RoomList] RoomEntryPrefab이 null입니다. Inspector에서 설정하세요!");
                return;
            }

            if (_lobbyRunner == null)
            {
                var oldRunner = GetComponent<NetworkRunner>();
                if (oldRunner != null) DestroyImmediate(oldRunner);

                _lobbyRunner = gameObject.AddComponent<NetworkRunner>();
                _lobbyRunner.ProvideInput = false;
                _lobbyRunner.AddCallbacks(this); // 콜백 등록
                Debug.Log("[RoomList] NetworkRunner 생성 및 콜백 등록 완료");
            }

            Debug.Log("[RoomList] 로비 접속 시도 중...");
            var result = await _lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);

            if (result.Ok)
            {
                Debug.Log("[RoomList] ✓ Fusion Lobby 접속 성공! 세션 목록 대기 중...");
            }
            else
            {
                if (result.ShutdownReason != ShutdownReason.Ok)
                    Debug.LogError($"[RoomList] ✗ Lobby 접속 실패: {result.ShutdownReason}");
            }
        }

        private async void OnEnable()
        {
            await ConnectToLobby();
        }

        private void ClearListUI()
        {
            if (ContentParent == null) return;

            int childCount = ContentParent.childCount;
            foreach (Transform child in ContentParent)
            {
                Destroy(child.gameObject);
            }

            if (childCount > 0)
            {
                Debug.Log($"[RoomList] {childCount}개의 방 UI 항목 제거됨");
            }
        }

        private async void OnDisable()
        {
            if (_lobbyRunner != null)
            {
                Debug.Log("[RoomList] 로비 러너 종료 중...");
                await _lobbyRunner.Shutdown();
                _lobbyRunner = null;
            }
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            Debug.Log($"[RoomList] ★ 방 목록 업데이트 콜백 호출됨! 총 {sessionList.Count}개의 세션");

            if (ContentParent == null)
            {
                Debug.LogError("[RoomList] ContentParent가 null입니다!");
                return;
            }

            if (RoomEntryPrefab == null)
            {
                Debug.LogError("[RoomList] RoomEntryPrefab이 null입니다!");
                return;
            }

            ClearListUI();

            int visibleCount = 0;
            foreach (var session in sessionList)
            {
                Debug.Log($"[RoomList] 세션 발견: {session.Name}, IsVisible: {session.IsVisible}, PlayerCount: {session.PlayerCount}/{session.MaxPlayers}");

                if (session.IsVisible)
                {
                    visibleCount++;
                    GameObject entryGo = Instantiate(RoomEntryPrefab, ContentParent);
                    var entry = entryGo.GetComponent<RoomEntry>();

                    if (entry != null)
                    {
                        entry.Setup(session, (roomName) => {
                            JoinRoom(roomName);
                        });
                        Debug.Log($"[RoomList] 방 UI 생성 완료: {session.Name}");
                    }
                    else
                    {
                        Debug.LogError($"[RoomList] RoomEntry 컴포넌트를 찾을 수 없음! Prefab을 확인하세요.");
                    }
                }
            }

            if (visibleCount == 0)
            {
                Debug.LogWarning("[RoomList] 표시 가능한 방이 없습니다. 방을 먼저 생성해야 합니다.");
            }
            else
            {
                Debug.Log($"[RoomList] ✓ {visibleCount}개의 방 UI 생성 완료");
            }
        }

        private void JoinRoom(string roomName)
        {
            GameMenu.RoomText.text = roomName;
            GameMenu.StartGame();
        }

        #region Unused Callbacks (필수 구현)
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) 
        {
            Debug.Log($"[RoomList] 로비 러너 종료됨: {shutdownReason}");
        }
        public void OnConnectedToServer(NetworkRunner runner) 
        {
            Debug.Log("[RoomList] 서버에 연결됨");
        }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) 
        {
            Debug.Log($"[RoomList] 서버에서 연결 해제됨: {reason}");
        }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        #endregion
    }
}
