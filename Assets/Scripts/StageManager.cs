using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 한 런(run)의 스테이지 진행을 관리한다.
/// - 한 씬 안에 아레나 구역들을 두고, 미리 만든 Portal 로 다음 아레나로 이동(맵 재사용 가능).
/// - 스테이지 목록은 인스펙터에서 직접 설계한다(타입/적/보상/출구 포탈).
/// - 전투 스테이지: 스폰 포인트에 적 3~5마리 스폰 → 전멸 시 클리어.
/// - 클리어 직후 그 자리에서 보상(계약/각인/무기) 오픈 → 완료되면 출구 포탈 해제.
/// - 보스 스테이지: 보스 처치 시 런 클리어.
///
/// 주의: 이 NetworkBehaviour 는 스테이지가 진행되는 게임 씬에 직접 배치해야 한다.
/// (DontDestroyOnLoad 로 옮기면 NetworkObject 가 무효화되어 FixedUpdateNetwork/RPC 가 동작하지 않음 — BuffManager 참고)
/// </summary>
public class StageManager : NetworkBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum StageReward { None, Contract, Imprint, Weapon }
    private enum StagePhase { NotStarted, Combat, Reward, Cleared }

    [System.Serializable]
    public class StageDefinition
    {
        public string stageName = "Stage";

        [Tooltip("전투가 있는 스테이지인지. 무기 변경/보상 전용 방이면 끄세요.")]
        public bool hasCombat = true;
        [Tooltip("최종 보스 스테이지")]
        public bool isBoss = false;

        [Header("전투")]
        [Tooltip("이 아레나에 미리 배치한 적 스폰 위치들")]
        public Transform[] enemySpawnPoints;
        [Tooltip("이 스테이지에서 스폰할 적 프리팹 후보(랜덤). 정예 스테이지는 강한 적으로 채우세요.")]
        public Enemy[] enemyPool;
        public int minEnemies = 3;
        public int maxEnemies = 5;
        [Tooltip("보스 스테이지에서 스폰할 보스 프리팹")]
        public Enemy bossPrefab;

        [Header("보상 (전투 클리어 직후 그 자리에서 오픈)")]
        public StageReward rewardOnClear = StageReward.None;

        [Header("출구")]
        [Tooltip("이 스테이지 클리어 시 활성화되어 다음 스테이지로 이동하는 포탈. 보스 스테이지는 비워둬도 됨.")]
        public Portal exitPortal;
        [Tooltip("위 포탈을 통해 도착할 지점. 지정하면 포탈의 도착 지점을 이 위치로 덮어쓴다. 비워두면 포탈 자체에 설정된 도착 지점 사용.")]
        public Transform exitDestination;

        public bool HasSpawnData =>
            enemySpawnPoints != null && enemySpawnPoints.Length > 0 &&
            enemyPool != null && enemyPool.Length > 0;

        public Enemy RandomEnemy() =>
            (enemyPool != null && enemyPool.Length > 0) ? enemyPool[Random.Range(0, enemyPool.Length)] : null;
    }

    [Header("플레이어 스폰 위치")]
    [Tooltip("이 씬에서 플레이어가 스폰될 기준점들. 비워두면 GameManager 위치를 사용. 여러 개면 무작위로 하나를 골라 사용한다. 씬마다 원하는 위치에 빈 Transform 을 두고 연결하면 된다.")]
    public Transform[] playerSpawnPoints;

    [Header("스테이지 목록 (순서대로 진행)")]
    public List<StageDefinition> stages = new List<StageDefinition>();

    [Header("타이밍")]
    [Tooltip("무기 선택 스테이지 보상 대기 시간(초). WeaponManager.weaponSelectVoteTime 와 맞추세요.")]
    public float weaponRewardWait = 16f;
    [Tooltip("보상(투표)이 비정상 종료될 때를 대비한 최대 대기 시간(초).")]
    public float rewardTimeout = 60f;

    [Networked, OnChangedRender(nameof(OnStageChanged))]
    public int CurrentStageIndex { get; set; } = -1;

    // === 호스트 전용 진행 상태 (StateAuthority 만 FixedUpdateNetwork 로직을 돌림) ===
    private StagePhase _phase = StagePhase.NotStarted;
    private bool _combatActive;
    private readonly List<Enemy> _aliveEnemies = new List<Enemy>();

    private StageReward _rewardKind;
    private bool _rewardSawActive;     // 보상 투표가 실제로 시작됨(active==true)을 확인했는지
    private TickTimer _rewardTimeout;  // 보상 단계 안전 타임아웃
    private TickTimer _weaponRewardTimer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void Spawned()
    {
        // 모든 클라이언트: 시작 시 각 출구 포탈의 도착 지점을 적용하고, 포탈을 잠근다(숨김).
        for (int i = 0; i < stages.Count; i++)
        {
            ApplyExitDestinationLocal(i);
            SetPortalLockedLocal(i, true);
        }

        if (HasStateAuthority)
        {
            // 첫 스테이지 시작
            if (stages.Count > 0)
                BeginStage(0);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        switch (_phase)
        {
            case StagePhase.Combat: TickCombat(); break;
            case StagePhase.Reward: TickReward(); break;
        }
    }

    // ===== 스테이지 시작 =====
    private void BeginStage(int index)
    {
        if (index < 0 || index >= stages.Count) return;

        // 추적 목록 + 씬 전체 Enemy를 모두 디스폰 (_aliveEnemies 누락분까지 보장)
        DespawnAllEnemies();

        // 포탈 재사용 시 이전에 열린 포탈을 다시 잠근다
        RPC_LockAllPortals();

        CurrentStageIndex = index;
        StageDefinition def = stages[index];

        _aliveEnemies.Clear();
        _combatActive = false;

        Debug.Log($"[StageManager] BeginStage {index} ({def.stageName}) " +
                  $"hasCombat={def.hasCombat} isBoss={def.isBoss} reward={def.rewardOnClear} " +
                  $"spawnPts={def.enemySpawnPoints?.Length ?? 0} pool={def.enemyPool?.Length ?? 0}");

        Announce($"[{def.stageName}] 시작");

        if (def.isBoss)
        {
            SpawnBoss(def);
            if (_aliveEnemies.Count > 0)
            {
                _phase = StagePhase.Combat;
                _combatActive = true;
            }
            else
            {
                Debug.LogError($"[StageManager] {def.stageName}: 보스 스테이지인데 보스가 스폰되지 않았습니다. bossPrefab/스폰포인트를 확인하세요.");
                OnBossDefeated();
            }
        }
        else if (def.hasCombat)
        {
            if (!def.HasSpawnData)
            {
                Debug.LogError($"[StageManager] {def.stageName}: 전투 스테이지인데 스폰 포인트 또는 적 프리팹(enemyPool)이 비어 있습니다. " +
                               $"(spawnPoints={(def.enemySpawnPoints?.Length ?? 0)}, enemyPool={(def.enemyPool?.Length ?? 0)})");
            }

            SpawnEnemies(def);

            if (_aliveEnemies.Count > 0)
            {
                _phase = StagePhase.Combat;
                _combatActive = true;
            }
            else
            {
                // 전투 스테이지인데 적이 한 마리도 스폰되지 않음 → 설정 오류. 게임이 막히지 않게 보상으로 넘기되 경고.
                Debug.LogWarning($"[StageManager] {def.stageName}: 적이 0마리 스폰되어 전투를 건너뜁니다. 설정을 확인하세요.");
                StartReward(def);
            }
        }
        else
        {
            // 전투 없는 스테이지(무기 변경 방 등): 의도적으로 바로 보상으로.
            Debug.Log($"[StageManager] {def.stageName}: 전투 없는 스테이지 → 바로 보상.");
            StartReward(def);
        }
    }

    private void DespawnAllEnemies()
    {
        if (!HasStateAuthority) return;
        // _aliveEnemies 추적 목록에 있는 것
        foreach (var e in _aliveEnemies)
        {
            if (e != null && e.Object != null && e.Object.IsValid)
                Runner.Despawn(e.Object);
        }
        // 씬에서 직접 찾아서 누락분도 처리 (NavMesh 오류 등으로 비정상 상태인 적 포함)
        foreach (var e in FindObjectsOfType<Enemy>())
        {
            if (e.Object != null && e.Object.IsValid)
                Runner.Despawn(e.Object);
        }
    }

    private void SpawnEnemies(StageDefinition def)
    {
        int count = Mathf.Clamp(Random.Range(def.minEnemies, def.maxEnemies + 1), 1, 99);
        // 전봇대(streetlampEnemy)는 위치가 겹치면 안 되므로 점유된 스폰 포인트 인덱스를 따로 추적
        var lampOccupied = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            Enemy prefab = def.RandomEnemy();
            if (prefab == null) continue;

            Transform pt;
            if (prefab is streetlampEnemy)
            {
                // 아직 전봇대가 없는 포인트를 순서대로 찾는다
                pt = null;
                for (int k = 0; k < def.enemySpawnPoints.Length; k++)
                {
                    int idx = (i + k) % def.enemySpawnPoints.Length;
                    if (!lampOccupied.Contains(idx))
                    {
                        lampOccupied.Add(idx);
                        pt = def.enemySpawnPoints[idx];
                        break;
                    }
                }
                if (pt == null)
                {
                    Debug.LogWarning($"[StageManager] {def.stageName}: 전봇대를 스폰할 남은 포인트가 없어 건너뜁니다.");
                    continue;
                }
            }
            else
            {
                pt = def.enemySpawnPoints[i % def.enemySpawnPoints.Length];
            }

            Enemy e = Runner.Spawn(prefab, pt.position, pt.rotation);
            if (e != null) _aliveEnemies.Add(e);
        }
        Debug.Log($"[StageManager] {def.stageName}: 적 {_aliveEnemies.Count}마리 스폰");
    }

    private void SpawnBoss(StageDefinition def)
    {
        if (def.bossPrefab == null)
        {
            Debug.LogWarning($"[StageManager] {def.stageName}: bossPrefab 이 비어있습니다.");
            return;
        }
        Vector3 pos = (def.enemySpawnPoints != null && def.enemySpawnPoints.Length > 0)
            ? def.enemySpawnPoints[0].position : transform.position;
        Quaternion rot = (def.enemySpawnPoints != null && def.enemySpawnPoints.Length > 0)
            ? def.enemySpawnPoints[0].rotation : Quaternion.identity;

        Enemy boss = Runner.Spawn(def.bossPrefab, pos, rot);
        if (boss != null) _aliveEnemies.Add(boss);
    }

    // ===== 전투 진행 =====
    private void TickCombat()
    {
        if (!_combatActive) return;

        CleanDeadEnemies();
        if (_aliveEnemies.Count > 0) return;

        // 전멸 → 클리어
        _combatActive = false;
        StageDefinition def = stages[CurrentStageIndex];

        if (def.isBoss)
        {
            OnBossDefeated();
            return;
        }

        StartReward(def);
    }

    private void CleanDeadEnemies()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            Enemy e = _aliveEnemies[i];
            if (e == null || e.Object == null || !e.Object.IsValid || e.isDead)
                _aliveEnemies.RemoveAt(i);
        }
    }

    // ===== 보상 =====
    private void StartReward(StageDefinition def)
    {
        _rewardKind = def.rewardOnClear;
        _rewardSawActive = false;
        _rewardTimeout = TickTimer.CreateFromSeconds(Runner, rewardTimeout);

        switch (def.rewardOnClear)
        {
            case StageReward.Contract:
                BuffManager.Instance?.RequestContractVote();
                _phase = StagePhase.Reward;
                break;

            case StageReward.Imprint:
                BuffManager.Instance?.RequestImprintVote();
                _phase = StagePhase.Reward;
                break;

            case StageReward.Weapon:
                WeaponManager.Instance?.RequestWeaponSelect();
                _weaponRewardTimer = TickTimer.CreateFromSeconds(Runner, weaponRewardWait);
                _phase = StagePhase.Reward;
                break;

            default:
                // 보상 없음 → 바로 클리어 처리
                OnStageFullyCleared();
                break;
        }
    }

    private void TickReward()
    {
        // 안전 타임아웃: 투표가 비정상 종료돼도 진행을 막지 않는다.
        if (_rewardTimeout.Expired(Runner)) { OnStageFullyCleared(); return; }

        switch (_rewardKind)
        {
            case StageReward.Contract:
            case StageReward.Imprint:
            {
                bool active = BuffManager.Instance != null && BuffManager.Instance.IsBuffVoteActive;
                if (active) _rewardSawActive = true;             // 투표 시작 확인
                if (_rewardSawActive && !active) OnStageFullyCleared(); // 시작 후 종료됨
                break;
            }
            case StageReward.Weapon:
                if (_weaponRewardTimer.Expired(Runner)) OnStageFullyCleared();
                break;
            default:
                OnStageFullyCleared();
                break;
        }
    }

    private void OnStageFullyCleared()
    {
        _phase = StagePhase.Cleared;

        // 다음 스테이지로 가는 출구 포탈 해제 (모든 클라이언트)
        RPC_SetPortalLocked(CurrentStageIndex, false);
        Announce($"[{stages[CurrentStageIndex].stageName}] 클리어! 포탈이 열렸습니다.");
    }

    private void OnBossDefeated()
    {
        _phase = StagePhase.Cleared;
        if (CurrentStageIndex >= stages.Count - 1)
        {
            // 최종 보스 처치 → 런 클리어
            RPC_RunComplete();
        }
        else
        {
            // 중간 보스 처치 → 포탈 열고 다음 스테이지로 계속
            RPC_SetPortalLocked(CurrentStageIndex, false);
            Announce($"[{stages[CurrentStageIndex].stageName}] 보스 처치! 포탈이 열렸습니다.");
        }
    }

    // ===== 다음 스테이지 진입 (출구 포탈 사용 시 호출) =====
    public void NotifyExitPortalUsed(Portal portal)
    {
        // 현재 클리어된 스테이지의 포탈인지만 확인 (같은 포탈이 여러 스테이지에서 재사용되므로 인덱스 검색 금지)
        if (CurrentStageIndex < 0 || CurrentStageIndex >= stages.Count) return;
        if (stages[CurrentStageIndex].exitPortal != portal) return;
        RPC_RequestBeginStage(CurrentStageIndex + 1);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestBeginStage(int index)
    {
        // 클리어된 현재 스테이지의 바로 다음만 허용 → 중복/순서 꼬임 방지
        if (_phase == StagePhase.Cleared && index == CurrentStageIndex + 1 && index < stages.Count)
            BeginStage(index);
    }

    // ===== 출구 포탈 잠금/해제 =====

    // 모든 고유 포탈을 강제로 잠근다 (포탈 재사용 시 이전 스테이지에서 열린 것을 닫기 위해)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_LockAllPortals()
    {
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (var def in stages)
        {
            if (def.exitPortal == null) continue;
            if (seen.Add(def.exitPortal.GetInstanceID()))
                def.exitPortal.gameObject.SetActive(false);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetPortalLocked(int stageIndex, NetworkBool locked)
    {
        SetPortalLockedLocal(stageIndex, locked);
    }

    private void SetPortalLockedLocal(int stageIndex, bool locked)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count) return;
        Portal p = stages[stageIndex].exitPortal;
        if (p != null && p.gameObject.activeSelf == locked)
            p.gameObject.SetActive(!locked);
    }

    // 스테이지에 지정한 도착 지점을 해당 출구 포탈에 적용한다.
    private void ApplyExitDestinationLocal(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count) return;
        StageDefinition def = stages[stageIndex];
        if (def.exitPortal != null && def.exitDestination != null)
            def.exitPortal.SetDestination(def.exitDestination);
    }

    // ===== 런 클리어 =====
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RunComplete()
    {
        if (ChatManager.Instance != null)
            ChatManager.Instance.SendSystemMessage("최종 보스 처치! 스테이지 클리어!", Color.yellow);
        if (GameManager.Instance != null)
            GameManager.Instance.ReviveAllDeadPlayers();
    }

    private void OnStageChanged()
    {
        // 필요 시 UI(현재 스테이지 표시) 갱신 훅. 현재는 로그만.
        Debug.Log($"[StageManager] 현재 스테이지: {CurrentStageIndex}");
    }

    // ===== 플레이어 스폰 위치 (GameManager 가 호출) =====
    // playerSpawnPoints 중 하나를 무작위로 골라 radius 만큼 흩뿌린 위치를 반환한다.
    // 지정된 스폰 포인트가 없으면 false 를 반환해 GameManager 폴백을 쓰게 한다.
    public bool TryGetPlayerSpawnPosition(float radius, out Vector3 position)
    {
        Transform chosen = PickPlayerSpawnPoint();
        if (chosen == null) { position = default; return false; }

        Vector2 offset = Random.insideUnitCircle * radius;
        position = chosen.position + new Vector3(offset.x, 0f, offset.y);
        return true;
    }

    private Transform PickPlayerSpawnPoint()
    {
        if (playerSpawnPoints == null || playerSpawnPoints.Length == 0) return null;

        // null 항목을 제외하고 무작위로 하나 선택
        int valid = 0;
        foreach (var t in playerSpawnPoints) if (t != null) valid++;
        if (valid == 0) return null;

        int pick = Random.Range(0, valid);
        foreach (var t in playerSpawnPoints)
        {
            if (t == null) continue;
            if (pick == 0) return t;
            pick--;
        }
        return null;
    }

    private void Announce(string msg)
    {
        Debug.Log($"[StageManager] {msg}");
        if (ChatManager.Instance != null)
            ChatManager.Instance.SendSystemMessage(msg, Color.white);
    }
}
