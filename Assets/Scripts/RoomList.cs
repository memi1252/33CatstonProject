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
            // Shutdown 시 GameObject가 파괴되지 않도록 명시하거나, 
            // 아예 깔끔하게 Shutdown이 끝날 때까지 기다립니다.
            await _lobbyRunner.Shutdown(destroyGameObject: false); 
            
            // Shutdown 직후에는 컴포넌트가 아직 정리가 안 됐을 수 있으므로 
            // 잠시 null로 밀어줍니다.
            _lobbyRunner = null;
        }

        // 0.1초 정도 아주 잠깐 대기 (레이스 컨디션 방지)
        await Task.Delay(100);

        await ConnectToLobby();
    }

    private async Task ConnectToLobby()
    {
        // 여기서 this(컴포넌트)나 gameObject가 살아있는지 체크
        if (this == null || gameObject == null) return;

        if (_lobbyRunner == null)
        {
            // 기존에 남아있을지 모를 NetworkRunner 컴포넌트 제거 (중복 방지)
            var oldRunner = GetComponent<NetworkRunner>();
            if (oldRunner != null) DestroyImmediate(oldRunner);

            _lobbyRunner = gameObject.AddComponent<NetworkRunner>();
            _lobbyRunner.ProvideInput = false;
        }

        // 세션 접속 시도
        var result = await _lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);
        
        if (result.Ok) {
            Debug.Log("Fusion Lobby 접속 성공");
        } else {
            // Shutdown 사유가 'UserShutdown'이면 의도적인 것이므로 무시 가능
            if (result.ShutdownReason != ShutdownReason.Ok)
                Debug.LogError($"Lobby 접속 실패: {result.ShutdownReason}");
        }
    }

        private async void OnEnable()
        {
            await ConnectToLobby();
        }

        // 로비 접속 로직 분리
        

        private void ClearListUI()
        {
            foreach (Transform child in ContentParent)
            {
                Destroy(child.gameObject);
            }
        }

        private void OnDisable()
        {
            if (_lobbyRunner != null)
            {
                _lobbyRunner.Shutdown();
                _lobbyRunner = null;
            }
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            Debug.Log($"방 목록 업데이트: {sessionList.Count}개의 세션 발견");

            ClearListUI();

            foreach (var session in sessionList)
            {
                if (session.IsVisible)
                {
                    GameObject entryGo = Instantiate(RoomEntryPrefab, ContentParent);
                    var entry = entryGo.GetComponent<RoomEntry>();
                    
                    if (entry != null)
                    {
                        entry.Setup(session, (roomName) => {
                            JoinRoom(roomName);
                        });
                    }
                }
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
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            throw new NotImplementedException();
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            throw new NotImplementedException();
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}