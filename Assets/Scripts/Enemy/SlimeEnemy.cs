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
            damageable.TakeHit(GetFinalDamage(), new RaycastHit(), this.gameObject);
        }
    }
}
