using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class TaxiEnemy : Enemy
{
    [Header("시각 효과 설정")]
    public ParticleSystem[] wheelDustParticles; // 바퀴 먼지 파티클 2개 할당
    public GameObject collisionEffectPrefab;    // 충돌 시 생성될 이펙트 프리팹

    [Header("택시 사거리 및 공격 설정")]
    public float taxiAttackRange = 7f;
    public float chargeDamage = 20f;
    public float chargeSpeedMultiplier = 3f;
    public float maxChargeDistance = 10f; 
    public float cooldownAfterCharge = 2.0f;

    [Networked] private TickTimer chargeTimer { get; set; }
    [Networked] private NetworkBool isCharging { get; set; }
    [Networked] private Vector3 chargeDirection { get; set; }
    [Networked] private Vector3 chargeStartPosition { get; set; }

    private System.Collections.Generic.HashSet<GameObject> hitObjects = new System.Collections.Generic.HashSet<GameObject>();

    protected override void Start()
    {
        base.Start();
        attackRange = taxiAttackRange;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isDead) return;
        base.FixedUpdateNetwork();
    }

    // [핵심] 렌더링 프레임마다 파티클 상태 업데이트
    public override void Render()
    {
        base.Render(); // 부모의 보간 로직 유지
        UpdateWheelParticles();
    }

    private void UpdateWheelParticles()
    {
        // 1. 움직이거나 돌진 중일 때만 파티클 켜기
        // 에이전트 속도가 일정 이상이거나, 돌진 중일 때
        bool shouldEmit = isCharging || (agent != null && agent.velocity.magnitude > 0.1f);

        foreach (var ps in wheelDustParticles)
        {
            if (ps == null) continue;
            var emission = ps.emission;
            
            // 현재 상태와 파티클 방출 상태가 다를 때만 갱신
            if (emission.enabled != shouldEmit)
            {
                emission.enabled = shouldEmit;
            }
        }
    }

    protected override void UpdateAttackState()
    {
        if (target == null) { CurrentState = EnemyState.Idle; return; }

        if (isCharging)
        {
            float movedDistance = Vector3.Distance(chargeStartPosition, transform.position);
            if (chargeTimer.ExpiredOrNotRunning(Runner) || movedDistance >= maxChargeDistance)
            {
                StopCharge();
                return;
            }
            
            float baseSpeed = (enemyData != null) ? enemyData.speed : 5f;
            transform.position += chargeDirection * (baseSpeed * chargeSpeedMultiplier) * Runner.DeltaTime;
            NetworkPosition = transform.position;
            return;
        }

        if (!attackCooldown.ExpiredOrNotRunning(Runner))
        {
            FaceTarget();
            if (Vector3.Distance(transform.position, target.position) > attackRange) 
                CurrentState = EnemyState.Chase;
            return;
        }

        if (Vector3.Distance(transform.position, target.position) <= attackRange) StartCharge();
        else CurrentState = EnemyState.Chase;
    }

    private void StartCharge()
    {
        if (isCharging) return;
        isCharging = true;
        hitObjects.Clear();
        chargeDirection = transform.forward;
        var vector3 = chargeDirection;
        vector3.y = 0;
        chargeDirection = vector3;
        chargeDirection.Normalize();
        chargeStartPosition = transform.position;
        chargeTimer = TickTimer.CreateFromSeconds(Runner, 5.0f); 

        if (agent != null) agent.enabled = false; 
        NetworkRotation = transform.rotation;
    }

    private void StopCharge()
    {
        isCharging = false;
        if (agent != null)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
                transform.position = hit.position;

            agent.enabled = true;
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }
        }
        attackCooldown = TickTimer.CreateFromSeconds(Runner, cooldownAfterCharge);
        CurrentState = EnemyState.Chase;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isCharging || !HasStateAuthority || hitObjects.Contains(other.gameObject)) return;
        if (other.gameObject == this.gameObject) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeHit(chargeDamage, new RaycastHit());
            hitObjects.Add(other.gameObject);

            // [추가] 충돌 이펙트 생성
            if (collisionEffectPrefab != null)
            {
                // 충돌 지점 계산 (가장 가까운 점)
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                // 이펙트 생성 (네트워크 동기화가 필요한 중요한 이펙트라면 Runner.Spawn, 단순 시각용이면 Instantiate)
                GameObject effect = Instantiate(collisionEffectPrefab, contactPoint, Quaternion.identity);
                Destroy(effect, 2.0f); // 2초 뒤 삭제
            }
        }
    }
}