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
    [Tooltip("씬에 배치된 맨홀 뚜껑들. 비워두면 ManholeCover 가 붙은 모든 오브젝트를 자동 수집한다.")]
    public List<ManholeCover> manholeCovers = new List<ManholeCover>();

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

        if (manholeCovers == null || manholeCovers.Count == 0)
        {
            manholeCovers = new List<ManholeCover>(ManholeCover.All);
        }

        if (visualRoot == null) visualRoot = gameObject;
        _bodyColliders = visualRoot.GetComponentsInChildren<Collider>(true);

        // 초기 비주얼: 숨김 상태로 시작
        ApplyVisualForPhase(ManholeBossPhase.Hidden);
    }

    public override void Spawned()
    {
        base.Spawned();

        // 중간 참여 플레이어도 현재 페이즈 비주얼 맞도록 한번 적용
        ApplyVisualForPhase(Phase);

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
        if (minion != null) _aliveMinions.Add(minion);
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
                float dmg = enemyData != null ? enemyData.damage : 10f;
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
    }

    public override void Die()
    {
        if (HasStateAuthority)
        {
            CleanDeadMinions();
            foreach (var m in _aliveMinions)
            {
                if (m != null && m.Object != null && m.Object.IsValid)
                    Runner.Despawn(m.Object);
            }
            _aliveMinions.Clear();
        }
        base.Die();
    }

    // ===== 비주얼/콜라이더 토글 =====
    private void OnPhaseChanged()
    {
        ApplyVisualForPhase(Phase);
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
