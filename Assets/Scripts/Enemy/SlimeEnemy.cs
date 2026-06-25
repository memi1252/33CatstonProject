using System;
using Fusion;
using UnityEngine;

public class SlimeEnemy : Enemy
{
    
    public Animator animator;
    
    
    override protected void Start()
    {
        base.Start();
    }

    private void Update()
    {
        if (CurrentState == EnemyState.Idle)
        {
            animator.SetBool("Move", false);
        }
        else if (CurrentState == EnemyState.Chase)
        {
            animator.SetBool("Move", true);
        }
    }

    protected override void PerformAttack()
    {
        IDamageable damageable = target.transform.GetComponent<IDamageable>();
        if (damageable != null)
        {
            animator.SetTrigger("Attack");
            damageable.TakeHit(GetFinalDamage(), new RaycastHit(), this.gameObject);
        }
    }

    protected override void ApplyDamage(float damage, NetworkObject attackerObj = default)
    {
        if (isDead) return;

        float finalDamage = EnemyGlobalBuffs.ScaledReceived(damage, isBoss);
        Debug.Log(finalDamage);
        Rpc_ShowDamagePopup(finalDamage, attackerObj != null ? attackerObj.transform.position : default);
        health -= finalDamage;
        animator.SetTrigger("hit");
        SoundManager.Instance?.PlayEnemyHit();
        if (health <= 0 && !isDead)
        {
            CurrentState = EnemyState.Dead;
            Die();
            return;
        }

        if (CurrentState != EnemyState.Dead)
        {
            AggroOnHit(attackerObj != null ? attackerObj.gameObject : null);
        }
    }

    public override void Die()
    {
        animator.SetTrigger("Die");
        base.Die();
    }
}
