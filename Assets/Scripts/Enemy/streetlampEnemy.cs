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

    [Tooltip("바닥을 찾기 위한 레이캐스트 레이어. 비워두면 Default+Building을 기본값으로 사용한다.")]
    public LayerMask groundLayerMask;

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
        // StateAuthority(호스트)에서만 처리. 클라이언트는 NetworkPosition 보간으로 따라옴.
        if (HasStateAuthority && _rb != null)
        {
            // 예전엔 물리로 낙하시키다가 바닥과 충돌을 못 감지하면(콜라이더 누락/타이밍 문제)
            // 5초 타임아웃에 걸려 공중에 뜬 채로 멈췄다 — 데미지 판정 콜라이더도 그 높이에 고정되어
            // 플레이어가 때릴 수 없는 버그로 이어졌다. 레이캐스트로 바닥을 즉시 찾아 그 자리에 고정한다.
            if (groundLayerMask.value == 0)
                groundLayerMask = (1 << LayerMask.NameToLayer("Default")) | (1 << LayerMask.NameToLayer("Building"));

            Vector3 origin = transform.position + Vector3.up * 10f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f, groundLayerMask))
            {
                transform.position = hit.point;
            }
            else
            {
                Debug.LogWarning($"[streetlampEnemy] {name}: 아래쪽에서 바닥을 못 찾아 스폰 위치 그대로 둡니다.");
            }

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _landed = true;
        }
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
