using UnityEngine;

public class SlimeEnemy : Enemy
{
    override protected void Start()
    {
        base.Start();
    }

    protected override void PerformAttack()
    {
        IDamageable damageable = target.transform.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeHit(enemyData.damage, new RaycastHit(), this.gameObject);
        }
    }
}
