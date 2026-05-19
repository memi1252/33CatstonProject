using UnityEngine;
using Fusion;

public class DummyEnemy : Enemy
{
    [HideInInspector] public float damage = 10f;

    protected override void Start()
    {
        base.Start();
        
        // 부모(Enemy)의 Start()에서 enemyData를 통한 체력, 이동속도, 사거리 설정이 완료됩니다.
        // 자식 클래스에서는 추가적인 데미지 등만 동기화합니다.
        if (enemyData != null)
        {
            damage = enemyData.damage;
        }
        else
        {
            // SO가 없을 경우의 기본 폴백(기존 더미 스텟)
            startingHealth = 50f;
            attackRange = 5f;
            
            if (agent != null)
            {
                agent.speed = 10f;
            }
        }
    }

    protected override void PerformAttack()
    {
        if (!HasStateAuthority) return;
        
        Debug.Log($"더미(Dummy)가 플레이어를 공격했습니다! 데미지: {damage}");
        
        
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null) damageable.TakeHit(damage, new RaycastHit(), this.gameObject);
    }
}
