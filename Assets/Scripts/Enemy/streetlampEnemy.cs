using Projectiles;
using Projectiles.NetworkObjectExample;
using UnityEngine;
using Starter.Platformer;

public class streetlampEnemy : Enemy
{
    [HideInInspector] public float damage = 10f;
    public Light Light;
    public Color IdleColor = Color.blue;
    public Color attackIdleColor = Color.yellow;
    public Color attackColor = Color.red;

    public Enemy ownerEnemy;
    public NetworkObjectBuffer _projectileBuffer;
    public PhysicsProjectile _projectilePrefab;
    public Transform FireTransform;
    public TargetAttribute targetAttribute;
    public LayerMask raycastLayerMask;

    private MeshRenderer meshRenderer;


    private void Awake()
    {
        ownerEnemy = GetComponent<Enemy>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();
    }


    protected override void Start()
    {
        base.Start();

        // 부모(Enemy)의 Start()에서 enemyData를 통한 체력, 이동속도, 사거리 설정이 완료됩니다.
        // 자식 클래스에서는 추가적인 데미지 등만 동기화합니다.
        if (enemyData != null)
        {
            damage = enemyData.damage;
        }
        else
        {
            // SO가 없을 경우의 기본 폴백(기존 더미 스텟)
            startingHealth = 50f;
            attackRange = 5f;

            if (agent != null)
            {
                agent.speed = 10f;
            }
        }
    }

    private void Update()
    {
        if (CurrentState == EnemyState.Idle)
        {
            Light.color = IdleColor;
            meshRenderer.material.SetColor("_EmissionColor", IdleColor * 2f);
        }
        else
        {
            if (attackCooldown.ExpiredOrNotRunning(Runner))
            {
                Light.color = attackIdleColor;
                meshRenderer.material.SetColor("_EmissionColor", attackIdleColor * 2f);
            }
            else if (CurrentState == EnemyState.Attack)
            {
                Light.color = attackColor;
                meshRenderer.material.SetColor("_EmissionColor", attackColor * 2f);
            }
        }
        
        
    }

    protected override void PerformAttack()
    {
        if (!HasStateAuthority) return;

        Debug.Log($"더미(Dummy)가 플레이어를 공격했습니다! 데미지: {damage}");


        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null) damageable.TakeHit(damage, new RaycastHit());
    }

    private void FireSimple()
    {
        if (HasStateAuthority == false)
            return;

        var projectile = Runner.Spawn(_projectilePrefab, FireTransform.position, FireTransform.rotation,
            Object.InputAuthority);
        projectile.ownerEnemy = this.ownerEnemy; // 주인을 투사체에 전달
        projectile.Fire(FireTransform.position, FireTransform.rotation, damage, raycastLayerMask, targetAttribute);
    }

    private void FireWithBuffer()
    {
        var projectile = _projectileBuffer.Get<PhysicsProjectile>(FireTransform.position, FireTransform.rotation,
            Object.InputAuthority);
        if (projectile != null)
        {
            projectile.ownerEnemy = this.ownerEnemy; // 주인을 투사체에 전달
            projectile.Fire(FireTransform.position, FireTransform.rotation, damage, raycastLayerMask, targetAttribute);
        }
    }
}
