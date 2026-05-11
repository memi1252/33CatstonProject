using System.Collections;
using Fusion;
using Starter.Platformer;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Dead
}

public class Enemy : NetworkBehaviour , IDamageable
{
    public float startingHealth;
    public float attackRange = 2f;
    [Networked] public float health { get; set; }
    [Networked] public EnemyState CurrentState { get; set; }
    
    // 네트워크 동기화 속성
    [Networked] public NetworkBool isDead { get; set; }
    [Networked, OnChangedRender(nameof(OnPositionChanged))] public Vector3 NetworkPosition { get; set; }
    [Networked, OnChangedRender(nameof(OnRotationChanged))] public Quaternion NetworkRotation { get; set; }

    [Header("탐지 설정")]
    public float detectionMultiplier = 3f; // 감지 범위 배수 (사거리 * detectionMultiplier 반경을 탐지)

    [Header("적 데이터 설정")]
    public EnemyScriptableObject enemyData;
    protected EnemyType enemyType;
    
    public NavMeshAgent agent;
    protected Transform target;
    public GameObject dieEffect;

    // 무빙 어택을 위한 변수
    protected float strafeTimer;
    protected float strafeDirection = 1f;

    // 공격 속도 제어 타이머
    [Networked] protected TickTimer attackCooldown { get; set; }

    public bool dontMove = false;


    protected virtual void Start()
    {
        if (!dontMove)
        {
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false; // NavMeshAgent의 자동 회전을 끄고 수동으로 타겟을 바라보게 설정
        }
        

        if (enemyData != null)
        {
            startingHealth = enemyData.hp;
            attackRange = enemyData.range;
            enemyType = enemyData.enemyType;
            if (agent != null) agent.speed = enemyData.speed;
        }
        else
        {
            enemyType = EnemyType.Melee; // 기본값
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            health = startingHealth;
            CurrentState = EnemyState.Idle;
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
        }
    }

    public override void Render()
    {
        // State Authority가 아닌 경우 네트워크 동기화된 위치와 회전으로 업데이트
        if (!HasStateAuthority)
        {
            transform.position = Vector3.Lerp(transform.position, NetworkPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, NetworkRotation, Time.deltaTime * 10f);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isDead) return;

        // State Authority만이 위치와 상태를 업데이트함
        NetworkPosition = transform.position;
        NetworkRotation = transform.rotation;

        switch (CurrentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
            case EnemyState.Dead:
                break;
        }
    }

    protected virtual void UpdateIdleState()
    {
        if (target != null)
        {
            CurrentState = EnemyState.Chase;
        }
        else
        {
            // 사거리(attackRange) * 배수 를 실제 감지(시야) 거리로 사용합니다.
            float detectRange = attackRange * detectionMultiplier;
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            
            float minDistance = float.MaxValue;
            Transform closestPlayer = null;

            foreach (GameObject p in players)
            {
                float distance = Vector3.Distance(transform.position, p.transform.position);
                // 탐지 범위 안에 있고, 가장 가까운 플레이어인지 확인
                if (distance <= detectRange && distance < minDistance)
                {
                    minDistance = distance;
                    closestPlayer = p.transform;
                }
            }

            if (closestPlayer != null)
            {
                target = closestPlayer;
            }
        }
    }

    protected virtual void UpdateChaseState()
    {
        if (target == null)
        {
            CurrentState = EnemyState.Idle;
            if (dontMove) return;
            // 안전 검사 추가
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) 
                agent.ResetPath();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
    
        if (distanceToTarget > attackRange * detectionMultiplier)
        {
            target = null;
            CurrentState = EnemyState.Idle;
            if (dontMove) return;
            // 안전 검사 추가
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) 
                agent.ResetPath();
            return;
        }

        if (distanceToTarget <= attackRange)
        {
            CurrentState = EnemyState.Attack;
        }
        else
        {
            Vector3 targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
            if (!dontMove)
            {
                // 핵심 수정 부분: SetDestination 전 상태 확인
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.SetDestination(targetPosition);
                }
            }
       
            FaceTarget();
        }
    }

    protected virtual void UpdateAttackState()
    {
        if (target == null)
        {
            CurrentState = EnemyState.Idle;
            return;
        }

        if (target.GetComponent<Player>().dead)
        {
            float detectRange = attackRange * detectionMultiplier;
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            
            float minDistance = float.MaxValue;
            Transform closestPlayer = null;

            foreach (GameObject p in players)
            {
                if (p.GetComponent<Player>().dead) continue;
                float distance = Vector3.Distance(transform.position, p.transform.position);
                // 탐지 범위 안에 있고, 가장 가까운 플레이어인지 확인
                if (distance <= detectRange && distance < minDistance)
                {
                    
                    minDistance = distance;
                    closestPlayer = p.transform;
                }
            }

            if (closestPlayer != null)
            {
                target = closestPlayer;
            }
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // 거리가 멀어지면 다시 추격 모드로 변경
        if (distanceToTarget > attackRange)
        {
            CurrentState = EnemyState.Chase;
            return;
        }

        switch (enemyType)
        {
            case EnemyType.Melee:
                if (dontMove) break;
                // 근거리는 타겟과 살짝 거리를 유지하며 멈추거나 조금 다가가기
                float meleeStoppingDistance = attackRange * 0.85f; // 사거리의 85% 정도에서 멈춤
                if (distanceToTarget > meleeStoppingDistance)
                {
                    agent.SetDestination(target.position);
                }
                else
                {
                    // 거리가 충분히 가까우면 제자리에 정지
                    agent.ResetPath();
                }
                break;
            case EnemyType.Ranged:
                if (dontMove) break;
                // 원거리는 사거리 내에서 거리를 유지하며 횡이동(Strafing)
                strafeTimer -= Runner.DeltaTime;
                if (strafeTimer <= 0)
                {
                    strafeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                    strafeTimer = UnityEngine.Random.Range(0.4f, 0.8f);
                }

                Vector3 dirToTarget = (target.position - transform.position).normalized;
                dirToTarget.y = 0;
                Vector3 rightDir = Vector3.Cross(dirToTarget, Vector3.up).normalized;
                
                float maintainDistance = attackRange * 0.8f;
                Vector3 targetPosition = target.position - (dirToTarget * maintainDistance) + (rightDir * strafeDirection * 2f);
                agent.SetDestination(targetPosition);
                break;
            case EnemyType.destruct:
                if (dontMove) break;
                // 자폭형: 목표를 향해 돌진 (무빙 필요 없음)
                agent.SetDestination(target.position);
                // 자폭 처리 로직은 자폭 사거리 내에 들어오면 실행
                if (distanceToTarget <= 3f)
                {
                    PerformSelfDestruct();
                }
                break;
        }

        // 공격 중일 때 타겟 바라보기
        FaceTarget();

        // 쿨타임 체크 후 공격 수행
        if (enemyType != EnemyType.destruct && attackCooldown.ExpiredOrNotRunning(Runner))
        {
            PerformAttack();
            float atkSpeed = enemyData != null ? enemyData.attackSpeed : 1f;
            // 기획서에 있는 attackSpeed 값을 그대로 쿨타임(초 단위)으로 적용
            attackCooldown = TickTimer.CreateFromSeconds(Runner, atkSpeed);
        }
    }

    protected virtual void PerformAttack()
    {
        // 자식 클래스에서 구체적인 공격 로직 구현 (근거리 공격, 발사체 생성 등)
        Debug.Log("기본 공격 수행");
    }

    protected virtual void PerformSelfDestruct()
    {
        if (!HasStateAuthority || isDead) return;
        Debug.Log("자폭 공격 수행!");
        // 폭발 이펙트, 주변 데미지 처리 후 사망
        Instantiate(dieEffect, transform.position, Quaternion.identity);
        Collider[] cols = Physics.OverlapSphere(transform.position, enemyData.range);
        foreach (Collider c in cols)
        {
            if (c.transform.parent == transform) continue;
            if (c.transform.parent.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeHit(enemyData.damage, new RaycastHit());
            }
        }
        ApplyDamage(health); 
    }
    

    protected void FaceTarget()
    {
        if (target == null) return;
        if (isDead) return;
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        dirToTarget.y = 0;

        if (dirToTarget != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dirToTarget);
            // 부드럽게 타겟을 바라보도록 회전 (deltaTime을 곱하여 프레임 보정)
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Runner.DeltaTime * 8f);
        }
    }

    public virtual void TakeHit(float damage, RaycastHit hit)
    {
        // 적이 이미 Despawn되었으면 데미지 적용 무시
        if (Object == null || !Object.IsValid)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            ApplyDamage(damage);
        }
        else
        {
            Rpc_ApplyDamage(damage);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_ApplyDamage(float damage)
    {
        ApplyDamage(damage);
    }

    protected virtual void ApplyDamage(float damage)
    {
        Debug.Log(damage);
        health -= damage;
        if (health <= 0 && !isDead)
        {
            CurrentState = EnemyState.Dead;
            Die();
        }
    }

    public virtual void Die()
    {
        if (!Object.HasStateAuthority) return;

        isDead = true;
        CurrentState = EnemyState.Dead;
        Runner.Despawn(Object);
    }

    private void OnDrawGizmosSelected()
    {
        // 씬 뷰에서 적을 선택했을 때만 범위가 보이도록 설정
        float currentAttackRange = (Application.isPlaying || enemyData == null) ? attackRange : enemyData.range;

        // 공격 사거리 (빨간색 원)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, currentAttackRange);

        // 탐지 사거리 (노란색 원)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, currentAttackRange * detectionMultiplier);
    }

    // 네트워크 동기화 콜백
    private void OnPositionChanged()
    {
        if (HasStateAuthority) return;
        
        // State Authority가 아닌 클라이언트에서만 업데이트 (원격 플레이어의 위치 반영)
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, Time.deltaTime * 10f);
    }

    private void OnRotationChanged()
    {
        if (HasStateAuthority) return;
        
        // State Authority가 아닌 클라이언트에서만 업데이트 (원격 플레이어의 회전 반영)
        transform.rotation = Quaternion.Lerp(transform.rotation, NetworkRotation, Time.deltaTime * 10f);
    }
}
