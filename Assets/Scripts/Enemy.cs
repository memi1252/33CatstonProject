using System;
using System.Collections;
using Fusion;
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
    
    public bool dead;

    [Header("탐지 설정")]
    public float detectionMultiplier = 3f; // 감지 범위 배수 (사거리 * detectionMultiplier 반경을 탐지)

    [Header("적 데이터 설정")]
    public EnemyScriptableObject enemyData;
    protected EnemyType enemyType;
    
    protected NavMeshAgent agent;
    protected Transform target;

    // 무빙 어택을 위한 변수
    protected float strafeTimer;
    protected float strafeDirection = 1f;

    // 공격 속도 제어 타이머
    [Networked] protected TickTimer attackCooldown { get; set; }

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false; // NavMeshAgent의 자동 회전을 끄고 수동으로 타겟을 바라보게 설정

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
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || dead) return;

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
            agent.ResetPath();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        
        // 탐지 거리 밖으로 플레이어가 도망가면 타겟을 포기하고 Idle로 돌아감
        if (distanceToTarget > attackRange * detectionMultiplier)
        {
            target = null;
            CurrentState = EnemyState.Idle;
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
            agent.SetDestination(targetPosition);
            FaceTarget(); // 추적 중일 때 타겟 바라보기
        }
    }

    protected virtual void UpdateAttackState()
    {
        if (target == null)
        {
            CurrentState = EnemyState.Idle;
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
                // 자폭형: 목표를 향해 돌진 (무빙 필요 없음)
                agent.SetDestination(target.position);
                // 자폭 처리 로직은 자폭 사거리 내에 들어오면 실행
                if (distanceToTarget <= 1.5f)
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
        if (dead) return;
        Debug.Log("자폭 공격 수행!");
        // 폭발 이펙트, 주변 데미지 처리 후 사망
        ApplyDamage(health); 
    }

    protected void FaceTarget()
    {
        if (target == null) return;

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
        if (health <= 0 && !dead)
        {
            CurrentState = EnemyState.Dead;
            Die();
        }
    }

    public virtual void Die()
    {
        if (!Object.HasStateAuthority) return;

        dead = true;
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
}
