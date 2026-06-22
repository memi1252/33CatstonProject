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
    private Rigidbody _rb;
    private bool _landed = false;

    protected override void Awake()
    {
        base.Awake(); // Enemy.Awake() — rb 세팅 + enemyData 스탯 초기화
        ownerEnemy = GetComponent<Enemy>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        _rb = GetComponent<Rigidbody>();
    }

    public override void Spawned()
    {
        base.Spawned();
        // StateAuthority(호스트)에서만 물리 낙하 처리. 클라이언트는 NetworkPosition 보간으로 따라옴.
        if (HasStateAuthority && _rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
            StartCoroutine(LandingTimeout());
        }
    }

    // 바닥에 닿는 순간 kinematic으로 전환 — 투사체가 맞출 수 있는 정확한 위치에 고정
    private void OnCollisionEnter(Collision collision)
    {
        if (_landed || _rb == null || _rb.isKinematic) return;
        FreezeRigidbody();
    }

    private void FreezeRigidbody()
    {
        if (_rb == null || _landed) return;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _landed = true;
    }

    // OnCollisionEnter가 어떤 이유로 안 불릴 경우 대비한 안전 타임아웃 (5초)
    private System.Collections.IEnumerator LandingTimeout()
    {
        yield return new WaitForSeconds(5f);
        FreezeRigidbody();
    }

    protected override void Start()
    {
        base.Start();
        if (enemyData != null)
            damage = GetFinalDamage();
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
        if (damageable != null) damageable.TakeHit(damage, new RaycastHit(), this.gameObject);
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
