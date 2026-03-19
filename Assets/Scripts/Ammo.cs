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

    private Vector3 lookDirection;
    private float damage;
    
    public void SetDamage(float damage)
    {
        this.damage = damage;
    }
    
    public void SetLookDirection(Vector3 lookDirection)
    {
        this.lookDirection = lookDirection.normalized;
    }


    public void Start()
    {
        
        
    }


    public override void FixedUpdateNetwork()
    {

        // 무기와 거리가 일정 이상이면 총알 삭제
         
        if (HasStateAuthority)
        {
            if (Vector3.Distance(transform.position, weaponTransform.position) > projectileDis)
            {
                Runner.Despawn(Object);
            }
            
        }
    }

    private void Update()
    {
        float moveDistance = speed * Time.deltaTime;
        transform.Translate(lookDirection * moveDistance);
        CheckCollisions(moveDistance);
    }

    private void CheckCollisions(float moveDistance)
    {
        Ray ray = new Ray(transform.position, lookDirection);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, moveDistance, collisionMask, QueryTriggerInteraction.Collide))
        {
            OnHitObject(hit);
        }
    }

    private void OnHitObject(RaycastHit hit)
    {
        IDamageable damageableObject = hit.collider.GetComponent<IDamageable>();
        if (damageableObject != null)
        {
            damageableObject.TakeHit(damage, hit); // 데미지 입히기
        }
        Destroy(gameObject);
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