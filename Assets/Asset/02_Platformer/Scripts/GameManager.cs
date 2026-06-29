using UnityEngine;
using Fusion;
using Starter.Platformer;
using Fusion.Sockets;
using System.Collections.Generic;
using System;


/// <summary>
/// Handles player connections (spawning of Player instances).
/// </summary>
public sealed class GameManager : NetworkBehaviour, INetworkRunnerCallbacks
{
	public static GameManager Instance { get; private set; }
	public int MinCoinsToWin = 10;
	public float GameOverTime = 4f;
	public Player PlayerPrefab;
	public float SpawnRadius = 3f;
	public CameraShack cameraShack;

	public Player LocalPlayer { get; private set; }
	public bool IsGameFinished => GameOverTimer.IsRunning;


	[Networked] public PlayerRef Winner { get; set; }
	[Networked] public TickTimer GameOverTimer { get; set; }

	[Networked, Capacity(4)] public NetworkDictionary<PlayerRef, NetworkString<_16>> PlayerNames => default;

	void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_RegisterPlayerName(PlayerRef player, string name)
	{
		if (!string.IsNullOrEmpty(name))
		{
			PlayerNames.Set(player, name);
			Debug.Log($"[GameManager] {player} 이름 등록: {name}");
		}
	}



	// 플레이어 번호(Ref)로 이름을 찾는 도우미 함수
	public string GetPlayerName(PlayerRef player)
	{
		if (PlayerNames.TryGet(player, out var name))
		{
			return name.ToString();
		}

		return "Unknown";
	}

	// Called from UnityEvent on Flag gameobject
	public void OnFlagReached(Player player)
	{
		if (HasStateAuthority == false)
			return;

		if (Winner != PlayerRef.None)
			return; // Someone was faster

		if (player.CollectedCoins < MinCoinsToWin)
			return; // Not enough coins

		Winner = player.Object.StateAuthority;
		GameOverTimer = TickTimer.CreateFromSeconds(Runner, GameOverTime);
	}

	public Vector3 GetSpawnPosition()
	{
		// 스폰 위치는 StageManager 가 지정한다. 없거나 지정 안 됐으면 GameManager 위치로 폴백.
		if (StageManager.Instance != null &&
		    StageManager.Instance.TryGetPlayerSpawnPosition(SpawnRadius, out Vector3 stagePos))
		{
			return stagePos;
		}

		var randomPositionOffset = UnityEngine.Random.insideUnitCircle * SpawnRadius;
		return transform.position + new Vector3(randomPositionOffset.x, 0f, randomPositionOffset.y);
	}

	private void SpawnLocalPlayer()
	{
		if (LocalPlayer != null) return; // 이미 스폰됨

		LocalPlayer = Runner.Spawn(PlayerPrefab, GetSpawnPosition(), Quaternion.identity, Runner.LocalPlayer);

		if (LocalPlayer == null)
		{
			Debug.LogError("[GameManager] Runner.Spawn() 실패 - LocalPlayer가 null입니다. 네트워크 연결 상태를 확인하세요.");
			return;
		}

		Runner.SetPlayerObject(Runner.LocalPlayer, LocalPlayer.Object);

		if (LocalPlayer.HasInputAuthority)
		{
			var playerInput = LocalPlayer.GetComponent<PlayerInput>();
			if (playerInput != null) playerInput.EnableInput();

			var inputHandler = LocalPlayer.gameObject.AddComponent<InputHandler>();
			inputHandler.Initialize(Runner);
			Debug.Log("[GameManager] InputHandler를 LocalPlayer에 추가했습니다.");
		}
	}

	[Networked] public PlayerRef HostPlayerRef { get; set; }

	public override void Spawned()
	{
		Runner.AddCallbacks(this);
		SpawnLocalPlayer();

		// 씬에 배치된 이 GameManager의 State Authority를 가진 클라이언트가 곧 방장(씬 권한자)이다.
		// HostMigration이 꺼져있어 방장이 나가면 씬 권한이 아무에게도 없는 상태가 되어 적/스테이지 진행이
		// 멈출 수 있으므로, 누가 방장인지 기록해뒀다가 OnPlayerLeft에서 감지해 전원을 로비로 내보낸다.
		if (HasStateAuthority)
		{
			HostPlayerRef = Runner.LocalPlayer;
		}
	}

	public override void Despawned(NetworkRunner runner, bool hasState)
	{
		if (runner != null) runner.RemoveCallbacks(this);
		LocalPlayer = null;
	}

	// 뒤늦게 방에 입장한 경우 자신의 캐릭터를 스폰
	public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
	{
		if (player == runner.LocalPlayer && LocalPlayer == null)
		{
			SpawnLocalPlayer();
		}
	}

	// 방장(씬 권한자)이 나가면 나머지 플레이어들도 전부 로비로 내보낸다.
	public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
	{
		if (player == HostPlayerRef)
		{
			RPC_HostLeftKickAll();
		}
	}

	// RpcSources.All: 방장이 나간 시점엔 씬 권한자가 없을 수 있어(HostMigration 비활성) StateAuthority 제약 없이 보낸다.
	[Rpc(RpcSources.All, RpcTargets.All)]
	private void RPC_HostLeftKickAll()
	{
		if (ChatManager.Instance != null)
			ChatManager.Instance.SendSystemMessage("방장이 나가서 로비로 돌아갑니다.", Color.red);

		if (Starter.UIGameMenu._instance != null)
			_ = Starter.UIGameMenu._instance.Disconnect(true);
	}
	public void OnInput(NetworkRunner runner, NetworkInput input) { }
	public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
	public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
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

	private void Update()
	{
		// 테스트용: T 키로 모든 죽은 플레이어 부활 (로컬 플레이어만)
		if (LocalPlayer != null && LocalPlayer.HasInputAuthority && Input.GetKeyDown(KeyCode.T))
		{
			ReviveAllDeadPlayers();
			Debug.Log("[TEST] 모든 죽은 플레이어 부활 요청");
		}
	}

	public override void FixedUpdateNetwork()
	{
		if (GameOverTimer.Expired(Runner))
		{
			// Restart the game
			Winner = PlayerRef.None;

			// Prepare players for next round
			foreach (var playerRef in Runner.ActivePlayers)
			{
				RPC_RespawnPlayer(playerRef, GetSpawnPosition(), true);
			}

			// Reset timer
			GameOverTimer = default;
		}
	}

	[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
	private void RPC_RespawnPlayer([RpcTarget] PlayerRef playerRef, Vector3 position, bool resetCoins)
	{
		LocalPlayer.Respawn(position, resetCoins);
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.DrawWireSphere(transform.position, SpawnRadius);
	}

	/// <summary>
	/// 정예/보스 클리어 시 모든 죽은 플레이어 부활 (보스 스크립트에서 호출)
	/// </summary>
	public void ReviveAllDeadPlayers()
	{
		RPC_ReviveAllDeadPlayers();
		if (ChatManager.Instance != null)
			ChatManager.Instance.SendSystemMessage("모든 플레이어가 부활했습니다!", Color.cyan);
	}

	[Rpc(RpcSources.All, RpcTargets.All)]
	private void RPC_ReviveAllDeadPlayers()
	{
		foreach (var playerRef in Runner.ActivePlayers)
		{
			if (Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObj))
			{
				Player player = playerObj.GetComponent<Player>();
				if (player != null && player.dead)
				{
					player.Revive("보스 클리어", 0.25f);
				}
			}
		}
	}
}

