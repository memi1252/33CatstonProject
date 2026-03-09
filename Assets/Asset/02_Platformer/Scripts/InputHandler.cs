using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

namespace Starter.Platformer
{
    /// <summary>
    /// 입력 처리를 담당하는 클래스. NetworkRunner의 콜백을 직접 받아서 PlayerInput의 데이터를 전송합니다.
    /// </summary>
    public class InputHandler : MonoBehaviour, INetworkRunnerCallbacks
    {
        private PlayerInput _playerInput;
        private NetworkRunner _runner;
        
        public void Initialize(NetworkRunner runner)
        {
            _runner = runner;
            
            // 같은 GameObject 또는 자식에서 PlayerInput 찾기
            _playerInput = GetComponentInChildren<PlayerInput>();
            if (_playerInput == null)
            {
                // 전역에서 찾기 (마지막 수단)
                _playerInput = FindFirstObjectByType<PlayerInput>();
            }
            
            // NetworkRunner에 콜백 등록
            runner.AddCallbacks(this);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_playerInput != null)
            {
                var gameplayInput = _playerInput.CurrentInput;
                input.Set(gameplayInput);
            }
            else
            {
                input.Set(default(GameplayInput));
            }
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
            input.Set(default(GameplayInput));
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
            }
        }

        #region Unused INetworkRunnerCallbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        #endregion
    }
}
