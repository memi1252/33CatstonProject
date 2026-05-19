using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비 씬에서 플레이어 준비 상태를 동기화. 모든 플레이어가 준비 + 인원이 MinPlayers 이상이면 게임 씬으로 전환.
/// </summary>
public class LobbyReadyManager : NetworkBehaviour
{
    public static LobbyReadyManager Instance { get; private set; }

    [Header("Game Start")]
    [Tooltip("최소 시작 인원")]
    public int MinPlayers = 2;
    [Tooltip("게임 씬 빌드 인덱스. -1이면 GameSceneName 사용")]
    public int GameSceneBuildIndex = -1;
    [Tooltip("GameSceneBuildIndex가 -1일 때 사용할 씬 이름")]
    public string GameSceneName = "GameScene";
    [Tooltip("모두 준비 후 시작까지 대기 시간 (초)")]
    public float StartDelay = 1.0f;

    [Networked, Capacity(8)]
    public NetworkDictionary<PlayerRef, NetworkBool> Ready => default;

    [Networked]
    public NetworkBool GameStarting { get; set; }

    [Networked]
    public TickTimer StartTimer { get; set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void Spawned()
    {
        // 등록은 FixedUpdateNetwork에서 ActivePlayers 기준으로 일괄 처리.
        // 여기서 LocalPlayer를 미리 넣으면 클라이언트 입장 타이밍에 PlayerRef.None이 섞이는 경우가 있음.
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority == false) return;

        // 새로 들어온 플레이어 자동 등록
        foreach (var p in Runner.ActivePlayers)
        {
            if (!Ready.ContainsKey(p))
                Ready.Set(p, false);
        }

        // 떠난 플레이어 정리
        var toRemove = new System.Collections.Generic.List<PlayerRef>();
        foreach (var kv in Ready)
        {
            bool stillActive = false;
            foreach (var p in Runner.ActivePlayers)
            {
                if (p == kv.Key) { stillActive = true; break; }
            }
            if (!stillActive) toRemove.Add(kv.Key);
        }
        foreach (var p in toRemove) Ready.Remove(p);

        // 인원이 줄어들면 시작 취소
        if (GameStarting && CountActivePlayers() < MinPlayers)
        {
            GameStarting = false;
            StartTimer = TickTimer.None;
        }

        // 모두 준비 + 최소 인원 만족 → 시작 카운트다운
        if (!GameStarting && AllReady() && CountActivePlayers() >= MinPlayers)
        {
            GameStarting = true;
            StartTimer = TickTimer.CreateFromSeconds(Runner, StartDelay);
        }

        if (GameStarting && StartTimer.Expired(Runner))
        {
            StartTimer = TickTimer.None;
            LoadGameScene();
        }
    }

    private void LoadGameScene()
    {
        if (HasStateAuthority == false) return;

        SceneRef sceneRef = GameSceneBuildIndex >= 0
            ? SceneRef.FromIndex(GameSceneBuildIndex)
            : SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath(GameSceneName));

        if (sceneRef.IsValid)
        {
            UIManager.Instance.statsUI.HpUI.SetActive(true);
            Runner.LoadScene(sceneRef, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError($"[LobbyReadyManager] Cannot resolve game scene '{GameSceneName}' (idx {GameSceneBuildIndex})");
        }
    }

    private int CountActivePlayers()
    {
        int count = 0;
        foreach (var _ in Runner.ActivePlayers) count++;
        return count;
    }

    public bool AllReady()
    {
        int playerCount = CountActivePlayers();
        if (playerCount == 0) return false;

        int readyCount = 0;
        foreach (var kv in Ready)
            if (kv.Value) readyCount++;
        return readyCount >= playerCount;
    }

    public bool IsReady(PlayerRef p)
    {
        return Ready.TryGet(p, out var v) && v;
    }

    public bool CanReady()
    {
        return CountActivePlayers() >= MinPlayers;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReady(PlayerRef player, NetworkBool ready)
    {
        // 인원 부족이면 준비 자체를 막음
        if (ready && CountActivePlayers() < MinPlayers) return;
        Ready.Set(player, ready);
    }
}