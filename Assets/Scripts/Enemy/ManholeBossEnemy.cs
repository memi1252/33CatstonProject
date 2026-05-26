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

    [Header("잡몹 소환 (Hidden 동안)")]
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
        if (minionPrefab != null && minionSpawnTimer.ExpiredOrNotRunning(Runner))
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

        // 살아있는 플레이어가 있는 맨홀 우선, 없으면 랜덤
        ManholeCover cover = PickManholeNearAnyPlayer() ?? manholeCovers[Random.Range(0, manholeCovers.Count)];
        if (cover == null) return;

        var minion = Runner.Spawn(minionPrefab, cover.transform.position + minionSpawnOffset, Quaternion.identity);
        if (minion != null) _aliveMinions.Add(minion);
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
        Vector3 center = transform.position;
        Rpc_PlaySprayVfx(center);

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
    private void Rpc_PlaySprayVfx(Vector3 pos)
    {
        if (sprayVfxPrefab == null) return;
        var go = Instantiate(sprayVfxPrefab, pos, Quaternion.identity);
        Destroy(go, 3f);
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
