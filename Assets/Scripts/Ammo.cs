using System;
using Fusion;
using UnityEngine;
using UnityEngine.VFX;

public class Ammo : NetworkBehaviour
{
    public LayerMask collisionMask;
    
    public float speed;
    public float projectileDis = 3f;
    public Transform weaponTransform;
    
    public ParticleSystem projectileParticles;
    public VisualEffect projectileVisualEffect;

    [Networked] public Vector3 MoveDirection { get; set; }
    [Networked] public float DamageValue { get; set; }
    [Networked]  public float MoveSpeed { get; set; }
    [Networked] public float MaxDistance { get; set; }
    [Networked] public Vector3 SpawnPosition { get; set; }
    [Networked] public NetworkBool IsInitialized { get; set; }
    
    [Networked] private TickTimer spawnInvincibility { get; set; }
    
    public override void Spawned() {
        // 스폰 후 0.2초 동안은 무적
        spawnInvincibility = TickTimer.CreateFromSeconds(Runner, 0.2f);
    }
    
    public void Initialize(Vector3 spawnPosition, Vector3 direction, float damageValue, float moveSpeed, float maxDistance)
    {
        SpawnPosition = spawnPosition;
        MoveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        DamageValue = damageValue;
        MoveSpeed = moveSpeed;
        MaxDistance = maxDistance;
        IsInitialized = true;
    }
    
    public void SetDamage(float damage)
    {
        DamageValue = damage;
    }
    
    public void SetLookDirection(Vector3 lookDirection)
    {
        MoveDirection = lookDirection.sqrMagnitude > 0.0001f ? lookDirection.normalized : transform.forward;
    }


    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority == false)
            return;
        
        float moveDistance = MoveSpeed *Runner.DeltaTime;
        transform.position += MoveDirection * moveDistance;
        CheckCollisions(moveDistance);
        
        if (Vector3.Distance(transform.position, SpawnPosition) > MaxDistance)
        {
            Runner.Despawn(Object);
        }
    }

    private void Update()
    {
        
    }

    private void CheckCollisions(float moveDistance)
    {
        Ray ray = new Ray(transform.position, MoveDirection);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, moveDistance, collisionMask, QueryTriggerInteraction.Collide))
        {
            OnHitObject(hit);
        }
    }

    private void OnHitObject(RaycastHit hit)
    {
        if (HasStateAuthority == false)
            return;

        IDamageable damageableObject = hit.collider.GetComponent<IDamageable>();
        if (damageableObject != null)
        {
            damageableObject.TakeHit(DamageValue, hit); // 데미지 입히기
        }
        if (Object.HasStateAuthority && spawnInvincibility.ExpiredOrNotRunning(Runner)) {
            // 무적 시간이 끝난 후에만 삭제 실행
            Runner.Despawn(Object);
        }
    }

    // // 트리거 충돌 감지
    // private void OnCollisionEnter(Collision other)
    // {
    //     if (!HasStateAuthority) return;
    //     
    //     ContactPoint contatPoint = other.contacts[0];
    //     Vector2 hitPoint = contatPoint.point;
    //     
    //     // if (other.gameObject.TryGetComponent<Enemy>(out var enemy))
    //     // {
    //     //     if(projectileParticles != null)
    //     //     {
    //     //         projectileParticles.transform.position = hitPoint;
    //     //         projectileParticles.Play();
    //     //     }
    //     //     else if(projectileVisualEffect != null)
    //     //     {
    //     //         projectileVisualEffect.transform.position = hitPoint;
    //     //         projectileVisualEffect.Play();
    //     //     }
    //     //     enemyTakeDamage(10f); // 데미지 입히기
    //     //     Runner.Despawn(Object); // 충돌 후 총알 삭제
    //     // }
    // }
}