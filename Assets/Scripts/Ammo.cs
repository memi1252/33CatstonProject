using Fusion;
using UnityEngine;
using UnityEngine.VFX;

public class Ammo : NetworkBehaviour
{
    public float projectileDis = 3f;
    public Transform weaponTransform;
    
    public ParticleSystem projectileParticles;
    public VisualEffect projectileVisualEffect;

    

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

    // 트리거 충돌 감지
    private void OnCollisionEnter(Collision other)
    {
        if (!HasStateAuthority) return;
        
        ContactPoint contatPoint = other.contacts[0];
        Vector2 hitPoint = contatPoint.point;
        
        // if (other.gameObject.TryGetComponent<Enemy>(out var enemy))
        // {
        //     if(projectileParticles != null)
        //     {
        //         projectileParticles.transform.position = hitPoint;
        //         projectileParticles.Play();
        //     }
        //     else if(projectileVisualEffect != null)
        //     {
        //         projectileVisualEffect.transform.position = hitPoint;
        //         projectileVisualEffect.Play();
        //     }
        //     enemyTakeDamage(10f); // 데미지 입히기
        //     Runner.Despawn(Object); // 충돌 후 총알 삭제
        // }
    }
}