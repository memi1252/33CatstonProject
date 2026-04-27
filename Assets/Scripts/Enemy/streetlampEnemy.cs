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
    public Transform[] FireTransforms;
    public TargetAttribute targetAttribute;
    public LayerMask raycastLayerMask;
    public bool _useBuffer;

    private MeshRenderer meshRenderer;


    private void Awake()
    {
        ownerEnemy = GetComponent<Enemy>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();
    }


    protected override void Start()
    {
        base.Start();

        // ???(Enemy)?? Start()???? enemyData?? ???? ü??, ??????, ???? ?????? ??????.
        // ??? ??????????? ??????? ?????? ?? ?????????.
        if (enemyData != null)
        {
            damage = enemyData.damage;
        }
        else
        {
            // SO?? ???? ????? ?? ????(???? ???? ????)
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

        Debug.Log($"????(Dummy)?? ?÷???? ??????????! ??????: {damage}");
        if (_useBuffer == true)
        {
            FireWithBuffer();
        }
        else
        {
            FireSimple();
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null) damageable.TakeHit(damage, new RaycastHit());
    }

    private void FireSimple()
    {
        if (HasStateAuthority == false)
            return;

        foreach (var fireTransform in FireTransforms)
        {
            var projectile = Runner.Spawn(_projectilePrefab, fireTransform.position, fireTransform.rotation,
                Object.InputAuthority);
            projectile.ownerEnemy = this.ownerEnemy;
            projectile.Fire(fireTransform.position, fireTransform.rotation, damage, raycastLayerMask, targetAttribute);
        }
        
    }

    private void FireWithBuffer()
    {
        foreach (var fireTransform in FireTransforms)
        {
            var projectile = _projectileBuffer.Get<PhysicsProjectile>(fireTransform.position, fireTransform.rotation,
                Object.InputAuthority);
            if (projectile != null)
            {
                projectile.ownerEnemy = this.ownerEnemy; // ?????? ????ü?? ????
                projectile.Fire(fireTransform.position, fireTransform.rotation, damage, raycastLayerMask, targetAttribute);
            }
        }
    }
}
