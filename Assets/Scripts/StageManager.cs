using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public int minEnemies = 6;
        public int maxEnemies = 12;
        [Tooltip("보스 스테이지에서 스폰할 보스 프리팹")]
        public Enemy bossPrefab;
        [Tooltip("보스 HP UI에 표시할 이름. 비워두면 stageName 사용.")]
        public string bossDisplayName;

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
    [Tooltip("보스 스테이지: 사전 잡몹 전멸 후 보스 등장까지 대기 시간(초).")]
    public float preBossDelay = 2f;

    [Header("게임 시작 무기 선택")]
    [Tooltip("게임 씬 진입 시 첫 스테이지 전에 무기 선택 UI를 열지 여부")]
    public bool weaponSelectOnStart = true;

    [Networked, OnChangedRender(nameof(OnStageChanged))]
    public int CurrentStageIndex { get; set; } = -1;

    // === 호스트 전용 진행 상태 (StateAuthority 만 FixedUpdateNetwork 로직을 돌림) ===
    private StagePhase _phase = StagePhase.NotStarted;
    private bool _combatActive;
    private readonly List<Enemy> _aliveEnemies = new List<Enemy>();

    // 포탈을 통과한 플레이어 추적 (전원 통과 시 다음 스테이지 시작)
    private readonly System.Collections.Generic.HashSet<PlayerRef> _playersPassedPortal =
        new System.Collections.Generic.HashSet<PlayerRef>();

    // DespawnAllEnemies 후 1틱 기다렸다가 스폰 (이전 스테이지 잡몹 잔존 방지)
    private int _pendingSpawnStageIndex = -1;
    private TickTimer _spawnDelayTimer;

    // 보스 스테이지: 맨홀 스폰 후 보스 등장 대기
    private TickTimer _preBossDelayTimer;
    private readonly List<ManholeCover> _pendingManholes = new List<ManholeCover>();

    private StageReward _rewardKind;
    private bool _rewardSawActive;     // 보상 투표가 실제로 시작됨(active==true)을 확인했는지
    private TickTimer _rewardTimeout;  // 보상 단계 안전 타임아웃
    private TickTimer _weaponRewardTimer;

    // 게임 시작 시 무기 선택 대기
    private bool _initialWeaponSelectPending;
    private TickTimer _initialWeaponSelectTimer;

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

        // 게임 시작 시 적 글로벌 버프 초기화 (이전 런 데이터 잔류 방지)
        EnemyGlobalBuffs.Reset();

        if (HasStateAuthority)
        {
            if (stages.Count > 0)
            {
                if (weaponSelectOnStart)
                {
                    // 첫 스테이지 전 무기 선택 UI 오픈
                    RPC_OpenInitialWeaponSelect();
                    _initialWeaponSelectTimer = TickTimer.CreateFromSeconds(Runner, weaponRewardWait);
                    _initialWeaponSelectPending = true;
                }
                else
                {
                    BeginStage(0);
                }
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (_initialWeaponSelectPending)
        {
            // 전원이 이미 무기를 골랐으면 타이머가 남아있어도 바로 카운트다운 시작 (TickReward의 Weapon 케이스와 동일한 패턴).
            bool allVoted = WeaponManager.Instance != null &&
                             WeaponManager.Instance.playerWeaponVotes.Count >= Runner.ActivePlayers.Count();
            if (allVoted || _initialWeaponSelectTimer.Expired(Runner))
            {
                _initialWeaponSelectPending = false;
                RPC_StartCountdown();
            }
            return; // 무기 선택 중에는 다른 로직 정지
        }

        // 전투 중뿐 아니라 보상/증강 투표 중(팀킬 등으로) 전원이 죽는 경우도 게임오버로 잡아야 한다.
        // 기존엔 TickCombat 안에서만 전멸 체크를 해서, 스테이지 클리어 후 각인/계약 투표 중에
        // 전원이 사망하면 게임오버 화면이 영영 뜨지 않는 문제가 있었다.
        if ((_phase == StagePhase.Combat || _phase == StagePhase.Reward) && AllPlayersDead())
        {
            _combatActive = false;
            _phase = StagePhase.Cleared;
            RPC_GameOver();
            return;
        }

        TickPendingSpawn();

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

        // 포탈 통과 집계 초기화 (새 스테이지)
        _playersPassedPortal.Clear();

        CurrentStageIndex = index;
        StageDefinition def = stages[index];

        _aliveEnemies.Clear();
        _combatActive = false;

        Debug.Log($"[StageManager] BeginStage {index} ({def.stageName}) " +
                  $"hasCombat={def.hasCombat} isBoss={def.isBoss} reward={def.rewardOnClear} " +
                  $"spawnPts={def.enemySpawnPoints?.Length ?? 0} pool={def.enemyPool?.Length ?? 0}");

        Announce($"[{def.stageName}] 시작");

        if (!def.hasCombat)
        {
            // 전투 없는 스테이지(무기 변경 방 등): 바로 보상
            Debug.Log($"[StageManager] {def.stageName}: 전투 없는 스테이지 → 바로 보상.");
            StartReward(def);
        }
        else
        {
            // 전투 스테이지: 이전 스테이지 잡몹 소환 타이머가 완전히 정지하도록
            // 1틱 기다렸다가 스폰 (DespawnAllEnemies 직후 즉시 스폰 시 잔존 몬스터 방지)
            _pendingSpawnStageIndex = index;
            _spawnDelayTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
            _phase = StagePhase.NotStarted; // FixedUpdateNetwork가 스폰 대기를 처리
        }
    }

    // 지연된 스폰 실행 (FixedUpdateNetwork에서 호출)
    private void TickPendingSpawn()
    {
        if (_pendingSpawnStageIndex < 0) return;
        if (!_spawnDelayTimer.Expired(Runner)) return;

        int index = _pendingSpawnStageIndex;
        _pendingSpawnStageIndex = -1;

        if (index < 0 || index >= stages.Count) return;
        StageDefinition def = stages[index];

        if (def.isBoss)
        {
            // 스폰 포인트에 맨홀 먼저 생성 → 딜레이 후 보스 스폰
            SpawnManholes(def);
            _preBossDelayTimer = TickTimer.CreateFromSeconds(Runner, preBossDelay);
            _phase = StagePhase.Combat; // TickCombat에서 타이머 감시
        }
        else
        {
            if (!def.HasSpawnData)
            {
                Debug.LogError($"[StageManager] {def.stageName}: 스폰 포인트 또는 enemyPool이 비어 있습니다.");
            }

            SpawnEnemies(def);

            if (_aliveEnemies.Count > 0)
            {
                _phase = StagePhase.Combat;
                _combatActive = true;
            }
            else
            {
                Debug.LogWarning($"[StageManager] {def.stageName}: 적이 0마리 스폰되어 전투를 건너뜁니다.");
                StartReward(def);
            }
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
        int baseCount = Random.Range(def.minEnemies, def.maxEnemies + 1);
        // 스테이지를 진행할수록 적 수도 같이 늘어나도록 — 스테이지 2칸마다 적 1마리 추가.
        int bonusCount = CurrentStageIndex / 2;
        int count = Mathf.Clamp(baseCount + bonusCount, 1, 99);
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

    // 보스 스테이지 1단계: 스폰 포인트마다 맨홀 생성 (모든 클라이언트에서 동일하게 로컬 생성)
    private void SpawnManholes(StageDefinition def)
    {
        _pendingManholes.Clear();
        if (def.bossPrefab == null) return;

        // 보스 프리팹에서 manholeCoverPrefab 읽기
        var bossPrefabScript = def.bossPrefab as ManholeBossEnemy;
        if (bossPrefabScript == null || bossPrefabScript.manholeCoverPrefab == null) return;
        if (def.enemySpawnPoints == null || def.enemySpawnPoints.Length == 0) return;

        RPC_SpawnManholes(CurrentStageIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnManholes(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count) return;
        StageDefinition def = stages[stageIndex];
        if (def.bossPrefab == null) return;

        var bossPrefabScript = def.bossPrefab as ManholeBossEnemy;
        if (bossPrefabScript == null || bossPrefabScript.manholeCoverPrefab == null) return;

        _pendingManholes.Clear();
        foreach (var pt in def.enemySpawnPoints)
        {
            if (pt == null) { _pendingManholes.Add(null); continue; }
            ManholeCover cover = Instantiate(bossPrefabScript.manholeCoverPrefab, pt.position, pt.rotation);
            _pendingManholes.Add(cover);
        }
        Debug.Log($"[StageManager] 보스 스테이지 맨홀 {_pendingManholes.Count}개 생성");
    }

    // 보스 스테이지 2단계: 맨홀 중 하나에 보스 스폰 후 맨홀 리스트 등록
    private void SpawnBossAtManhole(StageDefinition def)
    {
        if (def.bossPrefab == null)
        {
            Debug.LogWarning($"[StageManager] {def.stageName}: bossPrefab 이 비어있습니다.");
            return;
        }

        // 스폰 위치: 스폰 포인트 중 랜덤 (없으면 [0], 없으면 자기 위치)
        Vector3 pos = transform.position;
        Quaternion rot = Quaternion.identity;
        if (def.enemySpawnPoints != null && def.enemySpawnPoints.Length > 0)
        {
            int pick = Random.Range(0, def.enemySpawnPoints.Length);
            pos = def.enemySpawnPoints[pick].position;
            rot = def.enemySpawnPoints[pick].rotation;
        }

        // onBeforeSpawned: Spawned() 호출 전에 isBoss=true를 설정 → 체력 계산 시 보스 배율 적용
        Enemy boss = Runner.Spawn(def.bossPrefab, pos, rot,
            onBeforeSpawned: (runner, obj) =>
            {
                var e = obj.GetComponent<Enemy>();
                if (e != null) e.isBoss = true;
            });

        if (boss != null)
        {
            _aliveEnemies.Add(boss);

            // 미리 생성된 맨홀 리스트를 보스에게 등록 (보스의 Start()에서 중복 생성하지 않도록)
            if (boss is ManholeBossEnemy manholeBoss && _pendingManholes.Count > 0)
            {
                manholeBoss.SetManholeCovers(_pendingManholes);
            }

            SoundManager.Instance?.PlayEnemyBossSpawn();
            SoundManager.Instance?.PlayBossBGM();

            // 보스 HP UI 표시. 이 메서드 자체가 StateAuthority(호스트)의 FixedUpdateNetwork에서만
            // 실행되므로, RPC 없이 직접 ShowBoss를 부르면 호스트 화면에만 떠서 다른 클라이언트는 보스
            // HP 바를 영영 못 보는 문제가 있었다 (보스방마다 누가 호스트인지에 따라 다르게 보였던 원인).
            // → RPC로 모든 클라이언트에 전파한다.
            string displayName = string.IsNullOrEmpty(def.bossDisplayName) ? def.stageName : def.bossDisplayName;
            RPC_ShowBossHP(boss.Object.Id, displayName);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowBossHP(NetworkId bossId, string bossName)
    {
        // Shared 모드에서는 이 RPC가 보스 NetworkObject의 스폰 복제보다 클라이언트에 먼저 도착할 수
        // 있다. 그 경우 FindObject가 그 순간 null을 반환해 ShowBoss가 조용히 스킵되고, 이후로는
        // 다시 시도하지 않아 클라이언트 화면에 보스 HP UI가 영영 안 뜨는 문제가 있었다.
        // → 오브젝트가 실제로 나타날 때까지 몇 프레임 재시도한다.
        StartCoroutine(ShowBossHPWhenReady(bossId, bossName));
    }

    private IEnumerator ShowBossHPWhenReady(NetworkId bossId, string bossName)
    {
        float timeout = 3f;
        while (timeout > 0f)
        {
            NetworkObject bossObj = Runner.FindObject(bossId);
            Enemy boss = bossObj != null ? bossObj.GetComponent<Enemy>() : null;
            if (boss != null)
            {
                UIManager.Instance?.bossHPUI?.ShowBoss(boss, bossName);
                yield break;
            }
            yield return null;
            timeout -= Time.deltaTime;
        }
        Debug.LogWarning($"[StageManager] RPC_ShowBossHP: {bossId} 보스 오브젝트를 찾지 못해 보스 HP UI를 표시하지 못했습니다.");
    }

    // ===== 전투 진행 =====
    private bool AllPlayersDead()
    {
        int total = 0, dead = 0;
        foreach (var playerRef in Runner.ActivePlayers)
        {
            total++;
            if (Runner.TryGetPlayerObject(playerRef, out var obj))
            {
                var p = obj.GetComponent<Starter.Platformer.Player>();
                if (p != null && p.dead) dead++;
            }
        }
        return total > 0 && dead >= total;
    }

    private void TickCombat()
    {
        // 보스 스테이지: 맨홀 스폰 후 딜레이 → 보스 등장
        if (_preBossDelayTimer.IsRunning)
        {
            if (!_preBossDelayTimer.Expired(Runner)) return;
            _preBossDelayTimer = default;
            StageDefinition bsDef = stages[CurrentStageIndex];
            SpawnBossAtManhole(bsDef);
            if (_aliveEnemies.Count > 0)
            {
                _combatActive = true;
            }
            else
            {
                Debug.LogError($"[StageManager] {bsDef.stageName}: 보스가 스폰되지 않았습니다.");
                OnBossDefeated();
            }
            return;
        }

        if (!_combatActive) return;

        // 전멸 체크는 FixedUpdateNetwork 상단에서 전 단계 공통으로 처리한다 (보상/투표 중 전멸 대응).

        CleanDeadEnemies();
        if (_aliveEnemies.Count > 0) return;

        // 전멸
        _combatActive = false;
        StageDefinition def = stages[CurrentStageIndex];

        if (def.isBoss)
        {
            OnBossDefeated();
            return;
        }

        StartReward(def);
    }

    /// <summary>
    /// 치트(F2): 현재 스테이지의 적을 전부 즉사시켜 클리어를 강제한다. 기존 전멸 처리 흐름(TickCombat)을 그대로 탄다.
    /// 방장이 아니어도 동작하도록 StateAuthority(방장)에게 RPC로 요청한다 (_aliveEnemies는 방장만 갖고 있음).
    /// </summary>
    public void CheatForceClearStage()
    {
        RPC_CheatForceClearStage();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_CheatForceClearStage()
    {
        foreach (var e in _aliveEnemies.ToArray())
        {
            if (e != null) e.CheatKill();
        }
    }

    /// <summary>
    /// 치트(F7): 보스 HP UI가 (네트워크 타이밍 등으로) 안 떴을 때 로컬에서 강제로 다시 띄운다.
    /// _aliveEnemies는 방장만 채워져 있으므로, 어떤 클라이언트에서든 동작하도록 씬에서 직접 살아있는
    /// 보스를 찾는다. RPC가 필요 없다 — 보스 HP UI는 각자 자기 화면에 표시하는 로컬 UI이기 때문이다.
    /// </summary>
    public void CheatRefreshBossUI()
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var e in all)
        {
            if (e == null || !e.isBoss || e.isDead) continue;

            string displayName = (CurrentStageIndex >= 0 && CurrentStageIndex < stages.Count)
                ? (string.IsNullOrEmpty(stages[CurrentStageIndex].bossDisplayName) ? stages[CurrentStageIndex].stageName : stages[CurrentStageIndex].bossDisplayName)
                : "Boss";
            UIManager.Instance?.bossHPUI?.ShowBoss(e, displayName);
            Debug.Log($"[Cheat] F7: 보스 HP UI 강제 갱신 ({e.name})");
            return;
        }
        Debug.Log("[Cheat] F7: 살아있는 보스를 찾지 못했습니다.");
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
                // 전원이 무기를 다 골랐으면 타이머가 남아있어도 바로 진행한다.
                bool allVoted = WeaponManager.Instance != null &&
                                 WeaponManager.Instance.playerWeaponVotes.Count >= Runner.ActivePlayers.Count();
                if (allVoted || _weaponRewardTimer.Expired(Runner)) OnStageFullyCleared();
                break;
            default:
                OnStageFullyCleared();
                break;
        }
    }

    [Header("클리어 보상")]
    [Tooltip("스테이지/보스 클리어 시 최대 체력 대비 회복 비율")]
    public float clearHealPercent = 0.5f;

    private void OnStageFullyCleared()
    {
        _phase = StagePhase.Cleared;

        // 다음 스테이지로 가는 출구 포탈 해제 (모든 클라이언트)
        RPC_SetPortalLocked(CurrentStageIndex, false);
        RPC_HealAllPlayers(clearHealPercent);
        Announce($"[{stages[CurrentStageIndex].stageName}] 클리어! 포탈이 열렸습니다.");
    }

    private void OnBossDefeated()
    {
        _phase = StagePhase.Cleared;

        // 보스 BGM 정지 + HP UI 숨김 (모든 클라이언트)
        RPC_OnBossDefeatedVisuals();

        if (CurrentStageIndex >= stages.Count - 1)
        {
            RPC_RunComplete();
        }
        else
        {
            RPC_SetPortalLocked(CurrentStageIndex, false);
            RPC_HealAllPlayers(clearHealPercent);
            Announce($"[{stages[CurrentStageIndex].stageName}] 보스 처치! 포탈이 열렸습니다.");
            SoundManager.Instance?.PlayStageClear();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnBossDefeatedVisuals()
    {
        SoundManager.Instance?.StopBGM();
        UIManager.Instance?.bossHPUI?.HideBoss();
    }

    // 스테이지/보스 클리어 보상: 모든 클라이언트가 각자 자기 플레이어에게만 회복을 적용한다 (Shared 모드 권한 규칙).
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HealAllPlayers(float percent)
    {
        if (Runner == null) return;
        NetworkObject playerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (playerObj != null && playerObj.TryGetComponent(out Starter.Platformer.Player player))
            player.HealPercent(percent);
    }

    // ===== 다음 스테이지 진입 (출구 포탈 사용 시 호출) =====
    // Portal에서 로컬 플레이어 정보를 넘겨주면 서버에서 전원 통과 여부를 집계한다.
    public void NotifyExitPortalUsed(Portal portal, PlayerRef player)
    {
        if (CurrentStageIndex < 0 || CurrentStageIndex >= stages.Count) return;
        if (stages[CurrentStageIndex].exitPortal != portal) return;
        RPC_NotifyPortalUsed(player, CurrentStageIndex);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_NotifyPortalUsed(PlayerRef player, int stageIndex)
    {
        // 클리어 상태이고, 현재 스테이지의 포탈 통과인지 확인
        if (_phase != StagePhase.Cleared) return;
        if (stageIndex != CurrentStageIndex) return;

        _playersPassedPortal.Add(player);

        // 접속 중인 전체 플레이어 수 집계
        int total = 0;
        foreach (var _ in Runner.ActivePlayers) total++;

        int passed = _playersPassedPortal.Count;
        Announce($"포탈 통과 {passed}/{total}명");

        // 전원 통과 시 다음 스테이지 시작
        if (passed >= total)
        {
            int next = CurrentStageIndex + 1;
            if (next < stages.Count)
                BeginStage(next);
        }
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
    // [Rpc] 어트리뷰트가 빠져있어서 실제로는 일반 메서드 호출이었다. 호출부(OnBossDefeated)가
    // 호스트(StateAuthority)에서만 실행되므로 이 메서드도 호스트에서만 돌아, 최종 보스를 잡아도
    // 클리어 UI가 호스트한테만 뜨고 클라이언트에는 전혀 안 뜨는 버그의 원인이었다.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RunComplete()
    {
        // ChatManager.SendSystemMessage / GameManager.ReviveAllDeadPlayers는 그 자체가 내부적으로
        // RpcTargets.All 브로드캐스트라서, 이 RPC가 모든 클라이언트에서 실행되는 것과 합쳐지면
        // 인원수만큼 채팅 메시지/부활 처리가 중복 실행된다. 호스트에서 한 번만 트리거한다.
        if (HasStateAuthority)
        {
            if (ChatManager.Instance != null)
                ChatManager.Instance.SendSystemMessage("최종 보스 처치! 스테이지 클리어!", Color.yellow);
            if (GameManager.Instance != null)
                GameManager.Instance.ReviveAllDeadPlayers();
        }

        if (UIManager.Instance != null && UIManager.Instance.gameClearUI != null)
            UIManager.Instance.gameClearUI.Show();
        SoundManager.Instance?.PlayGameClear();
    }

    // ===== 게임오버 =====
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GameOver()
    {
        Debug.Log($"[GameOver] RPC 수신 - UIManager={UIManager.Instance != null}, gameOverUI={UIManager.Instance?.gameOverUI != null}");
        if (ChatManager.Instance != null)
            ChatManager.Instance.SendSystemMessage("모든 플레이어가 사망했습니다. 게임 오버!", Color.red);
        if (UIManager.Instance != null && UIManager.Instance.gameOverUI != null)
            UIManager.Instance.gameOverUI.Show();
        else
            Debug.LogWarning("[GameOver] UIManager 또는 gameOverUI가 null입니다!");
        SoundManager.Instance?.PlayGameOver();
    }

    private void OnStageChanged()
    {
        // [Networked] 콜백이라 모든 클라이언트에서 동일하게 호출된다 — 스테이지 진행에 따라
        // 적이 자연스럽게 강해지도록 자동 난이도 상승분을 갱신한다.
        EnemyGlobalBuffs.SetStageScaling(CurrentStageIndex);
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OpenInitialWeaponSelect()
    {
        WeaponManager.Instance?.WeaponSelect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartCountdown()
    {
        var countdownUI = CountdownUI.GetOrCreate();
        if (countdownUI != null)
            countdownUI.StartCountdown(3, () => { if (HasStateAuthority) BeginStage(0); });
        else if (HasStateAuthority)
            BeginStage(0);
    }

    private void Announce(string msg)
    {
        Debug.Log($"[StageManager] {msg}");
        if (ChatManager.Instance != null)
            ChatManager.Instance.SendSystemMessage(msg, Color.white);
    }
}
