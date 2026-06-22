using System.Collections;
using Projectiles.NetworkObjectExample;
using UnityEngine;
using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using Projectiles.NetworkObjectExample;

public class AbandonedDroneEnemy : Enemy
{
    public int attackCount = 4;
    public Transform firePoint;
    public PhysicsProjectile bulletPrefab;
    public ParticleSystem fireParticles;
    public TargetAttribute targetAttribute;
    public LayerMask raycastLayerMask;
    
    protected override void PerformAttack()
    {
        base.PerformAttack();
        StartCoroutine(Attack());

    }

    IEnumerator Attack()
    {
        for (int i = 0; i < attackCount; i++)
        {
            var projectile = Runner.Spawn(bulletPrefab, firePoint.position, firePoint.rotation, Object.InputAuthority);
            projectile.ownerEnemy = this;
            projectile.Fire(firePoint.position, firePoint.rotation, GetFinalDamage(), raycastLayerMask, targetAttribute);
            fireParticles.Play();
            yield return new WaitForSeconds(0.4f);
        }
    }
}
