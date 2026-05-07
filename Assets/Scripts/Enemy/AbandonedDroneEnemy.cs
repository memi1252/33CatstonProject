using Projectiles.NetworkObjectExample;
using UnityEngine;

public class AbandonedDroneEnemy : Enemy
{
    public Transform firePoint;
    public GameObject bulletPrefab;
    public ParticleSystem fireParticles;
    public TargetAttribute targetAttribute;
    public LayerMask raycastLayerMask;
    
    protected override void PerformAttack()
    {
        base.PerformAttack();
        var bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        var physicsProjectile = bullet.GetComponent<PhysicsProjectile>();
        physicsProjectile.Fire(firePoint.position, firePoint.rotation, enemyData.damage, raycastLayerMask, targetAttribute);
        fireParticles.Play();
    }
}
