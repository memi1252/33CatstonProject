using UnityEngine;
using Fusion;
using Starter.Platformer;
using Fusion.Sockets;
using System.Collections.Generic;
using System;


/// <summary>
/// Handles player connections (spawning of Player instances).
/// </summary>
public sealed class GameManager : NetworkBehaviour
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

	public override void Spawned()
	{
		LocalPlayer = Runner.Spawn(PlayerPrefab, GetSpawnPosition(), Quaternion.identity, Runner.LocalPlayer);
		Runner.SetPlayerObject(Runner.LocalPlayer, LocalPlayer.Object);

		// 스탯 UI 활성화 (모든 클라이언트가 각자 실행 → 클라이언트에서도 스탯창 표시됨)
		if (UIManager.Instance != null && UIManager.Instance.statsUI != null && UIManager.Instance.statsUI.HpUI != null)
		{
			UIManager.Instance.statsUI.HpUI.SetActive(true);
		}

		// LocalPlayer가 InputAuthority를 가진 경우에만 InputHandler 추가
		if (LocalPlayer.HasInputAuthority)
		{
			// PlayerInput 활성화
			var playerInput = LocalPlayer.GetComponent<PlayerInput>();
			if (playerInput != null)
			{
				playerInput.EnableInput();
			}

			// InputHandler 추가
			var inputHandler = LocalPlayer.gameObject.AddComponent<InputHandler>();
			inputHandler.Initialize(Runner);
			Debug.Log("[GameManager] InputHandler를 LocalPlayer에 추가했습니다.");
		}
		else
		{
			Debug.Log("[GameManager] LocalPlayer에 InputAuthority가 없어서 입력 시스템을 활성화하지 않았습니다.");
		}
	}

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

	public override void Despawned(NetworkRunner runner, bool hasState)
	{
		// Clear the reference because UI can try to access it even after despawn
		LocalPlayer = null;
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

