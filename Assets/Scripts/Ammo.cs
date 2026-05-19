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
    public float MaxDistance { get; set; }
    public Vector3 SpawnPosition { get; set; }
    [Networked] public NetworkBool IsInitialized { get; set; }

    private void Start()
    {
            SpawnPosition = transform.position;
    }

    public void Initialize( Vector3 direction, float damageValue, float moveSpeed, float maxDistance)
    {
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
        if (IsInitialized == false)
            return;
        
        float moveDistance = MoveSpeed * Runner.DeltaTime;
        
        // 모든 클라이언트가 동일한 방향으로 이동 (지연 최소)
        transform.position += MoveDirection * moveDistance;
        
        // 충돌 감지와 Despawn은 InputAuthority (총알 쏜 플레이어)만 처리
        if (HasInputAuthority)
        {
            CheckCollisions(moveDistance);
            
            if (Vector3.Distance(transform.position, SpawnPosition) > MaxDistance)
            {
                Runner.Despawn(Object);
            }
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
            // 발사한 플레이어(InputAuthority의 PlayerObject)를 공격자로 전달 → 어그로 판정
            GameObject attacker = null;
            if (Runner != null && Runner.TryGetPlayerObject(Object.InputAuthority, out NetworkObject shooterObj) && shooterObj != null)
            {
                attacker = shooterObj.gameObject;
            }
            damageableObject.TakeHit(DamageValue, hit, attacker); // 데미지 입히기
        }
        Runner.Despawn(Object);
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