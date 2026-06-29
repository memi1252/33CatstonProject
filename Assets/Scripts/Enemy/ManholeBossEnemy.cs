using System.Collections;
using System.Collections.Generic;
using Fusion;
using Projectiles.NetworkObjectExample;
using Starter.Platformer;
using UnityEngine;

public enum ManholeBossPhase
{
    Hidden,      // 바닥에 숨어있음. 무적, 잡몹 소환
    Emerging,    // 떠오르는 중 (텔레그래프, 아직 무적)
    Vulnerable,  // 노출. 피해 적용 + 증기 공격
    Submerging   // 다시 내려가는 중 (무적)
}

/// <summary>
/// 맨홀 보스.
/// - 평소엔 바닥(맨홀) 안에 숨어 무적
/// - 주기적으로 맨홀 뚜껑 중 하나에서 떠오름 → 일정 시간 피해 가능
/// - 노출 동안: 모든 플레이어에게 증기 다단 발사 + 주변 AoE 증기 분사
/// - 숨어있을 때: 맨홀 위치에서 잡몹(SlimeEnemy 등) 소환
/// 보스는 NavMesh 로 이동하지 않고, 맨홀 위치 사이를 텔레포트한다.
/// </summary>
public class ManholeBossEnemy : Enemy
{
    [Header("맨홀 위치")]
    [Tooltip("설정하면 보스가 스폰될 때 자신이 속한 보스 스테이지의 enemySpawnPoints(이미 벽 안 끼도록 검증된 " +
             "적 스폰 위치들) 전부에 맨홀을 직접 생성해서 등록한다 (manholeCoverPrefab 필요). 보스 자신도 그 " +
             "지점들 중 하나에서 스폰되므로 임의 오프셋과 달리 벽에 낄 위험이 없다. 씬에 별도 배치 불필요. " +
             "비워두면 아래 manholeCovers 인스펙터 값을 쓰고, 그것도 비어있으면 씬에 미리 배치된 ManholeCover를 " +
             "전부 자동 수집한다(구버전 호환).")]
    public ManholeCover manholeCoverPrefab;

    [Tooltip("씬에 배치된 맨홀 뚜껑들(구버전 호환). 비워두면 ManholeCover 가 붙은 모든 오브젝트를 자동 수집한다.")]
    public List<ManholeCover> manholeCovers = new List<ManholeCover>();

    // manholeSpawnPoints로 직접 스폰한 맨홀들 — 보스가 사라질 때 같이 정리한다.
    private readonly List<GameObject> _spawnedManholeObjects = new List<GameObject>();

    [Header("외형(숨김/노출 토글 대상)")]
    [Tooltip("Hidden 상태에서 비활성화될 시각/콜라이더 루트. 비워두면 자신을 사용한다.")]
    public GameObject visualRoot;

    [Header("페이즈 길이(초)")]
    public float emergingDuration = 1.0f;
    public float vulnerableDuration = 6.0f;
    public float submergingDuration = 0.8f;
    public float hiddenDuration = 4.0f;
    [Tooltip("스폰 직후 첫 등장까지 대기 시간(초)")]
    public float initialDelay = 1.5f;

    [Header("증기 발사 (다단/다중 타겟)")]
    public PhysicsProjectile steamProjectilePrefab;
    [Tooltip("증기가 발사될 위치(보스 위쪽 빈 트랜스폼).")]
    public Transform steamFirePoint;
    public int steamShotsPerBurst = 3;
    public float steamShotInterval = 0.35f;
    public float steamBurstCooldown = 1.4f;
    public LayerMask steamRaycastMask = ~0;
    public TargetAttribute steamAttribute = TargetAttribute.Normal;
    public ParticleSystem steamMuzzleParticles;

    [Header("증기 뿜기 (AoE)")]
    public float sprayInterval = 3.5f;
    public float sprayRadius = 5f;
    public float sprayDamage = 15f;
    public GameObject sprayVfxPrefab;
    [Tooltip("스프레이 VFX 유지 시간(초). 노출이 이 시간보다 적게 남으면 분사를 생략해 맨홀 들어간 뒤 효과가 남지 않게 한다.")]
    public float sprayVfxLifetime = 2f;
    [Tooltip("VFX 분사 시작 위치(보스 로컬 기준 오프셋, 예: 입 높이).")]
    public Vector3 sprayVfxOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("VFX 분사 축 보정(도). 프리팹이 +Z가 아닌 다른 축으로 분사하면 조정.")]
    public Vector3 sprayVfxEulerOffset = Vector3.zero;

    [Header("잡몹 소환 (Hidden 동안)")]
    [Tooltip("소환할 잡몹 종류들. 이 중 하나를 랜덤으로 스폰한다.")]
    public List<Enemy> minionPrefabs = new List<Enemy>();
    [Tooltip("(구버전 호환) minionPrefabs 가 비어있으면 이 단일 프리팹을 사용한다.")]
    public Enemy minionPrefab;
    public int maxAliveMinions = 4;
    public float minionSpawnInterval = 1.5f;
    public Vector3 minionSpawnOffset = new Vector3(0f, 0.5f, 0f);

    [Header("애니메이션")]
    [Tooltip("보스 애니메이터. 비워두면 visualRoot 또는 자식에서 자동으로 찾는다. (모든 파라미터는 Bool)")]
    public Animator animator;
    [Tooltip("죽는 애니메이션(Dies) 재생 시간(초). 이 시간 후 디스폰한다.")]
    public float deathAnimDuration = 2f;
    [Tooltip("단발 애니메이션(공격/피격/리액션) 유지 시간(초).")]
    public float momentaryAnimDuration = 0.6f;

    // 애니메이터 Bool 파라미터 이름
    private const string A_IdleOne        = "IdleOne";
    private const string A_IdleAlert      = "IdleAlert";
    private const string A_Sleeps         = "Sleeps";
    private const string A_AngryReaction  = "AngryReaction";
    private const string A_Hit            = "Hit";
    private const string A_AnkleBite      = "AnkleBite";
    private const string A_CrochBite      = "CrochBite";
    private const string A_Dies           = "Dies";
    private const string A_HushLittleBaby = "HushLittleBaby";
    private const string A_Run            = "Run";

    // 페이즈에 따라 하나만 켜지는 '상태' 애니메이션들. (보스는 텔레포트로 이동하므로 Run 은 평소 사용 안 함)
    private static readonly string[] _stateBools =
        { A_IdleOne, A_IdleAlert, A_Sleeps, A_HushLittleBaby, A_Run };

    // Rpc_PlayMomentary 식별자
    private const int ANIM_ANKLEBITE = 0;
    private const int ANIM_CROCHBITE = 1;
    private const int ANIM_HIT       = 2;

    // 애니메이터를 지연 해석 (Spawned/Start 순서와 무관하게 사용 가능)
    private Animator Anim
    {
        get
        {
            if (animator == null)
            {
                if (visualRoot != null) animator = visualRoot.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = GetComponentInChildren<Animator>(true);
            }
            return animator;
        }
    }

    [Networked, OnChangedRender(nameof(OnPhaseChanged))]
    public ManholeBossPhase Phase { get; set; }

    [Networked] private TickTimer phaseTimer { get; set; }
    [Networked] private TickTimer steamBurstTimer { get; set; }
    [Networked] private TickTimer sprayTimer { get; set; }
    [Networked] private TickTimer minionSpawnTimer { get; set; }
    [Networked] private int currentManholeIndex { get; set; } = -1;

    private readonly List<Enemy> _aliveMinions = new List<Enemy>();
    private Collider[] _bodyColliders;

    protected override void Start()
    {
        dontMove = true; // 보스는 직접 이동하지 않음
        base.Start();

        // 보스가 스폰될 때 자신이 속한 스테이지의 enemySpawnPoints(이미 벽 안 끼는 위치로 검증된 지점들)에
        // 맨홀도 같이 만든다. 임의 오프셋이 아니라 기존 적 스폰 포인트를 그대로 재사용하므로 벽 안에 생기는
        // 문제가 없고, 보스 자신도 그 지점들 중 하나에서 스폰된다(StageManager.SpawnBoss). 위치 동기화가
        // 필요 없는 순수 비주얼 마커라 Runner.Spawn 없이 각 클라이언트가 로컬로 만든다 — 스폰 포인트가
        // 스테이지 정의(씬 데이터)로 모든 클라이언트에 동일하므로 결과 순서도 일치해 Rpc_NotifyCover(index)가
        // 모든 클라에서 같은 맨홀을 가리킨다.
        Transform[] spawnPoints = GetCurrentStageSpawnPoints();
        if (spawnPoints != null && spawnPoints.Length > 0 && manholeCoverPrefab != null)
        {
            manholeCovers = new List<ManholeCover>();
            foreach (var pt in spawnPoints)
            {
                if (pt == null) { manholeCovers.Add(null); continue; }
                ManholeCover cover = Instantiate(manholeCoverPrefab, pt.position, pt.rotation);
                _spawnedManholeObjects.Add(cover.gameObject);
                manholeCovers.Add(cover);
            }
        }
        else if (manholeCovers == null || manholeCovers.Count == 0)
        {
            manholeCovers = new List<ManholeCover>(ManholeCover.All);
        }

        if (visualRoot == null) visualRoot = gameObject;
        _bodyColliders = visualRoot.GetComponentsInChildren<Collider>(true);

        // 초기 비주얼: 숨김 상태로 시작
        ApplyVisualForPhase(ManholeBossPhase.Hidden);
    }

    // 현재 진행 중인(이 보스가 속한) 스테이지의 enemySpawnPoints를 가져온다.
    // StageManager.stages/CurrentStageIndex는 모든 클라이언트에서 동일한 값을 보므로
    // (stages는 씬 데이터로 동일, CurrentStageIndex는 [Networked]) 클라이언트마다 다른 결과가 나오지 않는다.
    private Transform[] GetCurrentStageSpawnPoints()
    {
        var stageManager = StageManager.Instance;
        if (stageManager == null) return null;

        int index = stageManager.CurrentStageIndex;
        if (index < 0 || index >= stageManager.stages.Count) return null;

        return stageManager.stages[index].enemySpawnPoints;
    }

    public override void Spawned()
    {
        base.Spawned();

        // 중간 참여 플레이어도 현재 페이즈 비주얼/애니메이션 맞도록 한번 적용
        ApplyVisualForPhase(Phase);
        ApplyAnimForPhase(Phase);

        if (HasStateAuthority)
        {
            Phase = ManholeBossPhase.Hidden;
            phaseTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, initialDelay));
            minionSpawnTimer = TickTimer.CreateFromSeconds(Runner, minionSpawnInterval);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isDead) return;

        NetworkPosition = transform.position;
        NetworkRotation = transform.rotation;

        switch (Phase)
        {
            case ManholeBossPhase.Hidden:     UpdateHidden();     break;
            case ManholeBossPhase.Emerging:   UpdateEmerging();   break;
            case ManholeBossPhase.Vulnerable: UpdateVulnerable(); break;
            case ManholeBossPhase.Submerging: UpdateSubmerging(); break;
        }
    }

    // ===== Phase: Hidden =====
    private void UpdateHidden()
    {
        // 잡몹 소환
        if (HasMinionPrefab() && minionSpawnTimer.ExpiredOrNotRunning(Runner))
        {
            TrySpawnMinion();
            minionSpawnTimer = TickTimer.CreateFromSeconds(Runner, minionSpawnInterval);
        }

        if (phaseTimer.ExpiredOrNotRunning(Runner))
            BeginEmerge();
    }

    private void TrySpawnMinion()
    {
        CleanDeadMinions();
        if (_aliveMinions.Count >= maxAliveMinions) return;
        if (manholeCovers.Count == 0) return;

        Enemy prefab = PickRandomMinionPrefab();
        if (prefab == null) return;

        // 살아있는 플레이어가 있는 맨홀 우선, 없으면 랜덤
        ManholeCover cover = PickManholeNearAnyPlayer() ?? manholeCovers[Random.Range(0, manholeCovers.Count)];
        if (cover == null) return;

        var minion = Runner.Spawn(prefab, cover.transform.position + minionSpawnOffset, Quaternion.identity);
        if (minion != null)
        {
            _aliveMinions.Add(minion);
            SoundManager.Instance?.PlayEnemyMinionSpawn();
        }
    }

    // 소환할 잡몹 종류가 하나라도 있는지
    private bool HasMinionPrefab()
    {
        if (minionPrefabs != null)
        {
            foreach (var p in minionPrefabs)
                if (p != null) return true;
        }
        return minionPrefab != null;
    }

    // 잡몹 종류 중 하나를 랜덤으로 선택 (리스트가 비어있으면 단일 프리팹 사용)
    private Enemy PickRandomMinionPrefab()
    {
        if (minionPrefabs != null && minionPrefabs.Count > 0)
        {
            // null 항목을 제외하고 랜덤 선택
            int valid = 0;
            foreach (var p in minionPrefabs) if (p != null) valid++;
            if (valid > 0)
            {
                int pick = Random.Range(0, valid);
                foreach (var p in minionPrefabs)
                {
                    if (p == null) continue;
                    if (pick == 0) return p;
                    pick--;
                }
            }
        }
        return minionPrefab;
    }

    // ===== Phase: Emerging =====
    private void UpdateEmerging()
    {
        if (phaseTimer.ExpiredOrNotRunning(Runner))
        {
            Phase = ManholeBossPhase.Vulnerable;
            phaseTimer = TickTimer.CreateFromSeconds(Runner, vulnerableDuration);
            steamBurstTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            sprayTimer = TickTimer.CreateFromSeconds(Runner, sprayInterval);
        }
    }

    private void BeginEmerge()
    {
        if (manholeCovers.Count == 0)
        {
            // 맨홀이 없으면 그냥 현재 위치에서 노출
            Phase = ManholeBossPhase.Vulnerable;
            phaseTimer = TickTimer.CreateFromSeconds(Runner, vulnerableDuration);
            steamBurstTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            sprayTimer = TickTimer.CreateFromSeconds(Runner, sprayInterval);
            return;
        }

        // 가장 가까운 살아있는 플레이어 근처 맨홀을 선택 (없으면 랜덤)
        int chosen = PickManholeIndexNearClosestPlayer();
        if (chosen < 0) chosen = Random.Range(0, manholeCovers.Count);

        currentManholeIndex = chosen;
        var cover = manholeCovers[chosen];
        if (cover != null)
        {
            transform.position = cover.transform.position;
            NetworkPosition = transform.position;
            Rpc_NotifyCover(chosen, true);
        }

        Phase = ManholeBossPhase.Emerging;
        phaseTimer = TickTimer.CreateFromSeconds(Runner, emergingDuration);
    }

    // ===== Phase: Vulnerable =====
    private void UpdateVulnerable()
    {
        // 가장 가까운 플레이어를 바라보도록 회전 (브레쓰가 플레이어 쪽으로 향하게)
        FaceClosestPlayer();

        // 증기 다단 발사 (모든 살아있는 플레이어에게)
        if (steamBurstTimer.ExpiredOrNotRunning(Runner))
        {
            FireSteamAtAllPlayers();
            steamBurstTimer = TickTimer.CreateFromSeconds(Runner, steamBurstCooldown);
        }

        // 주기적 AoE 증기 분사
        if (sprayTimer.ExpiredOrNotRunning(Runner))
        {
            PerformSteamSpray();
            sprayTimer = TickTimer.CreateFromSeconds(Runner, sprayInterval);
        }

        if (phaseTimer.ExpiredOrNotRunning(Runner))
            BeginSubmerge();
    }

    private void FireSteamAtAllPlayers()
    {
        if (steamProjectilePrefab == null || steamFirePoint == null) return;

        SoundManager.Instance?.PlayEnemyBossSteam();
        Rpc_PlayMomentary(ANIM_CROCHBITE); // 증기 발사 모션

        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            var player = p.GetComponent<Player>();
            if (player == null || player.dead) continue;
            StartCoroutine(BurstAt(player.transform));
        }
    }

    private IEnumerator BurstAt(Transform target)
    {
        for (int i = 0; i < steamShotsPerBurst; i++)
        {
            if (target == null) yield break;
            if (Object == null || !Object.IsValid) yield break;
            if (Phase != ManholeBossPhase.Vulnerable) yield break;

            Vector3 dir = (target.position + Vector3.up * 0.8f - steamFirePoint.position).normalized;
            if (dir == Vector3.zero) dir = transform.forward;
            Quaternion rot = Quaternion.LookRotation(dir);

            var proj = Runner.Spawn(steamProjectilePrefab, steamFirePoint.position, rot, Object.InputAuthority);
            if (proj != null)
            {
                proj.ownerEnemy = this;
                float dmg = GetFinalDamage();
                proj.Fire(steamFirePoint.position, rot, dmg, steamRaycastMask, steamAttribute);
            }
            if (steamMuzzleParticles != null) steamMuzzleParticles.Play();

            yield return new WaitForSeconds(steamShotInterval);
        }
    }

    private void PerformSteamSpray()
    {
        // 노출이 곧 끝나면 분사 생략 → VFX가 맨홀 들어간 뒤까지 남는 것을 방지
        float? remain = phaseTimer.RemainingTime(Runner);
        if (remain.HasValue && remain.Value < sprayVfxLifetime)
            return;

        Rpc_PlayMomentary(ANIM_ANKLEBITE); // 근접 분사 모션
        Rpc_PlaySprayVfx();

        Vector3 center = transform.position;
        var damaged = new HashSet<IDamageable>();
        Collider[] cols = Physics.OverlapSphere(center, sprayRadius);
        foreach (var c in cols)
        {
            var player = c.GetComponentInParent<Player>();
            if (player == null || player.dead) continue;
            if (!damaged.Add(player)) continue;
            player.TakeHit(sprayDamage, default, gameObject);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlaySprayVfx()
    {
        if (sprayVfxPrefab == null) return;
        // 보스에 자식으로 부착 → 보스가 매 틱 플레이어를 바라보므로 브레스도 따라 회전한다.
        var go = Instantiate(sprayVfxPrefab, transform);
        go.transform.localPosition = sprayVfxOffset;
        go.transform.localRotation = Quaternion.Euler(sprayVfxEulerOffset);
        Destroy(go, Mathf.Max(0.5f, sprayVfxLifetime));
    }

    // ===== Phase: Submerging =====
    private void UpdateSubmerging()
    {
        if (phaseTimer.ExpiredOrNotRunning(Runner))
        {
            Phase = ManholeBossPhase.Hidden;
            phaseTimer = TickTimer.CreateFromSeconds(Runner, hiddenDuration);
            minionSpawnTimer = TickTimer.CreateFromSeconds(Runner, 0.3f);
        }
    }

    private void BeginSubmerge()
    {
        if (currentManholeIndex >= 0 && currentManholeIndex < manholeCovers.Count)
            Rpc_NotifyCover(currentManholeIndex, false);

        Phase = ManholeBossPhase.Submerging;
        phaseTimer = TickTimer.CreateFromSeconds(Runner, submergingDuration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyCover(int index, NetworkBool emerging)
    {
        if (index < 0 || index >= manholeCovers.Count) return;
        var cover = manholeCovers[index];
        if (cover == null) return;
        if (emerging) cover.NotifyBossEmerge();
        else          cover.NotifyBossSubmerge();
    }

    // ===== 피격 처리 =====
    public override void TakeHit(float damage, RaycastHit hit, GameObject attacker = null)
    {
        if (Phase != ManholeBossPhase.Vulnerable) return; // 무적
        base.TakeHit(damage, hit, attacker);
    }

    protected override void ApplyDamage(float damage, NetworkObject attackerObj = default)
    {
        if (Phase != ManholeBossPhase.Vulnerable) return; // RPC 경로 보호
        base.ApplyDamage(damage, attackerObj);
        if (!isDead) Rpc_PlayMomentary(ANIM_HIT); // 죽지 않았으면 피격 모션 (죽으면 Die 가 Dies 재생)
    }

    public override void Die()
    {
        if (!HasStateAuthority) return;
        if (isDead) return;

        isDead = true;
        CurrentState = EnemyState.Dead;

        // 잡몹 정리
        CleanDeadMinions();
        foreach (var m in _aliveMinions)
        {
            if (m != null && m.Object != null && m.Object.IsValid)
                Runner.Despawn(m.Object);
        }
        _aliveMinions.Clear();

        // 즉시 Despawn 하면 Dies 모션이 안 보이므로, 애니메이션 재생 후 디스폰한다.
        Rpc_PlayDeathAnim();
        StartCoroutine(DespawnAfterDeath());
    }

    // 보스가 디스폰/파괴될 때(승리, 강제 처치 등 모든 경로) 직접 스폰했던 맨홀들도 같이 정리한다.
    private void OnDestroy()
    {
        foreach (var go in _spawnedManholeObjects)
            if (go != null) Destroy(go);
        _spawnedManholeObjects.Clear();
    }

    private IEnumerator DespawnAfterDeath()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, deathAnimDuration));
        if (Object != null && Object.IsValid && HasStateAuthority)
            Runner.Despawn(Object);
    }

    // ===== 비주얼/콜라이더 토글 =====
    private void OnPhaseChanged()
    {
        ApplyVisualForPhase(Phase);
        ApplyAnimForPhase(Phase);
    }

    // ===== 애니메이션 =====
    private void ApplyAnimForPhase(ManholeBossPhase phase)
    {
        var a = Anim;
        // Hidden 등 visualRoot 가 꺼진 동안에는 애니메이터도 비활성 → 스킵 (재등장 시 다시 설정됨)
        if (a == null || !a.isActiveAndEnabled) return;

        // 상태 bool 모두 끄고 페이즈에 맞는 것만 켠다.
        foreach (var b in _stateBools) a.SetBool(b, false);

        switch (phase)
        {
            case ManholeBossPhase.Hidden:
                a.SetBool(A_Sleeps, true);          // 맨홀 안에서 잠듦
                break;
            case ManholeBossPhase.Emerging:
                a.SetBool(A_IdleAlert, true);        // 경계하며 등장
                PlayMomentary(A_AngryReaction);      // 등장 순간 분노 리액션
                break;
            case ManholeBossPhase.Vulnerable:
                a.SetBool(A_IdleOne, true);          // 노출 중 기본 자세 (공격 시 단발 모션이 위에 덮임)
                break;
            case ManholeBossPhase.Submerging:
                a.SetBool(A_HushLittleBaby, true);   // 다시 숨으며 자장가
                break;
        }
    }

    // 단발(공격/피격/리액션) 애니메이션: bool 을 잠깐 켰다가 끈다.
    private void PlayMomentary(string param)
    {
        var a = Anim;
        if (a == null || !a.isActiveAndEnabled || string.IsNullOrEmpty(param)) return;
        a.SetBool(param, true);
        StartCoroutine(ResetBoolAfter(param, momentaryAnimDuration));
    }

    private IEnumerator ResetBoolAfter(string param, float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
        if (animator != null) animator.SetBool(param, false);
    }

    // 단발 애니메이션을 모든 클라이언트에서 재생 (StateAuthority 가 호출)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayMomentary(int id)
    {
        switch (id)
        {
            case ANIM_ANKLEBITE: PlayMomentary(A_AnkleBite); break;
            case ANIM_CROCHBITE: PlayMomentary(A_CrochBite); break;
            case ANIM_HIT:       PlayMomentary(A_Hit);       break;
        }
    }

    // 사망 애니메이션을 모든 클라이언트에서 재생
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayDeathAnim()
    {
        var a = Anim;
        if (a == null) return;
        foreach (var b in _stateBools) a.SetBool(b, false);
        if (a.isActiveAndEnabled) a.SetBool(A_Dies, true);
    }

    private void ApplyVisualForPhase(ManholeBossPhase phase)
    {
        bool visible = phase != ManholeBossPhase.Hidden;
        bool hittable = phase == ManholeBossPhase.Vulnerable;

        if (visualRoot != null && visualRoot.activeSelf != visible)
            visualRoot.SetActive(visible);

        if (_bodyColliders != null)
        {
            foreach (var c in _bodyColliders)
            {
                if (c == null) continue;
                c.enabled = hittable;
            }
        }
    }

    // ===== 유틸 =====
    private void CleanDeadMinions()
    {
        for (int i = _aliveMinions.Count - 1; i >= 0; i--)
        {
            if (_aliveMinions[i] == null || _aliveMinions[i].isDead)
                _aliveMinions.RemoveAt(i);
        }
    }

    private int PickManholeIndexNearClosestPlayer()
    {
        Transform closestPlayer = FindClosestLivingPlayer();
        if (closestPlayer == null) return -1;

        int best = -1;
        float min = float.MaxValue;
        for (int i = 0; i < manholeCovers.Count; i++)
        {
            if (manholeCovers[i] == null) continue;
            float d = Vector3.Distance(manholeCovers[i].transform.position, closestPlayer.position);
            if (d < min) { min = d; best = i; }
        }
        return best;
    }

    private ManholeCover PickManholeNearAnyPlayer()
    {
        Transform closestPlayer = FindClosestLivingPlayer();
        if (closestPlayer == null) return null;

        ManholeCover best = null;
        float min = float.MaxValue;
        foreach (var cover in manholeCovers)
        {
            if (cover == null) continue;
            float d = Vector3.Distance(cover.transform.position, closestPlayer.position);
            if (d < min) { min = d; best = cover; }
        }
        return best;
    }

    [Header("회전")]
    [Tooltip("플레이어를 바라보는 회전 속도(도/초). 0 이하면 즉시 바라봄.")]
    public float faceTurnSpeed = 360f;

    private void FaceClosestPlayer()
    {
        Transform closest = FindClosestLivingPlayer();
        if (closest == null) return;

        Vector3 dir = closest.position - transform.position;
        dir.y = 0f; // 수평 회전만 (브레쓰가 좌우로 향하게)
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = faceTurnSpeed > 0f
            ? Quaternion.RotateTowards(transform.rotation, want, faceTurnSpeed * Runner.DeltaTime)
            : want;

        NetworkRotation = transform.rotation;
    }

    private Transform FindClosestLivingPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float min = float.MaxValue;
        Transform closest = null;
        foreach (var p in players)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.dead) continue;
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < min) { min = d; closest = p.transform; }
        }
        return closest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, sprayRadius);
    }
}
