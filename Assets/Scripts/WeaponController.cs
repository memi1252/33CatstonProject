using System;
using Fusion;
using Projectiles.NetworkObjectExample;
using UnityEngine;

public class WeaponController : NetworkBehaviour
{
    public Transform weaponHold;
    public WeaponScriptableObject startWeapon;
    //private Weapon equippedWeapon;
    private Weapon_NetworkObject equippedWeapon;

    [Networked] private TickTimer attackCooldownTimer { get; set; }
    [Networked] private float currentMaxCooldown { get; set; }

    private StatsUI localStatsUI;

    public override void Spawned()
    {
        if (HasStateAuthority == false)
            return;

        if (startWeapon != null)
        {
            EquipWeapon(startWeapon);
        }
    }

    public override void Render()
    {
        // 자신의 무기 쿨타임만 UI에 반영 (로컬 플레이어 검사: 입력 권한이 있는 오브젝트만)
        if (HasInputAuthority)
        {
            if (localStatsUI == null)
            {
                localStatsUI = FindAnyObjectByType<StatsUI>();
            }

            if (localStatsUI != null)
            {
                if (attackCooldownTimer.IsRunning)
                {
                    float remaining = attackCooldownTimer.RemainingTime(Runner) ?? 0f;
                    // 남은 시간 비율을 반전시켜 0에서 1로 차오르게 변경
                    float fillPercentage = currentMaxCooldown > 0f ? 1f - (remaining / currentMaxCooldown) : 1f;
                    
                    localStatsUI.AttackCoolTimeView(fillPercentage);
                }
                else
                {
                    // 쿨타임이 끝났을 경우 (원한다면 1f로 채워진 상태를 유지하게 변경할 수도 있습니다)
                    localStatsUI.AttackCoolTimeView(1f);
                }
            }
        }
    }

    public void EquipWeapon(WeaponScriptableObject newWeapon)
    {
        if (HasStateAuthority == false)
            return;

        if (equippedWeapon != null)
        {
            if (equippedWeapon.Object != null)
            {
                Runner.Despawn(equippedWeapon.Object);
            }
        }

        NetworkObject weaponObject = Runner.Spawn(newWeapon.weaponPrefab, weaponHold.position, weaponHold.rotation, Object.InputAuthority);
        //equippedWeapon = weaponObject.GetComponent<Weapon>();
        equippedWeapon = weaponObject.GetComponent<Weapon_NetworkObject>();
        equippedWeapon.WeaponSO = newWeapon;
        // 임시
        FindAnyObjectByType<StatsUI>().Set(newWeapon.weaponType, newWeapon.grade, newWeapon.targetAttribute);
        // 네트워크 객체를 소유한 플레이어를 무기에 연결합니다 (필요 시 Player 참조용)
        equippedWeapon.ownerPlayer = GetComponent<Starter.Platformer.Player>(); 
        equippedWeapon.transform.parent = weaponHold;
        equippedWeapon.transform.localPosition = Vector3.zero;
        equippedWeapon.transform.localRotation = Quaternion.identity;

        // 무기를 장착하면 쿨타임 초기화
        attackCooldownTimer = TickTimer.None;
    }

    public void Attack(Vector3 Look, float damage, float criticalDamage)
    {
        if (HasStateAuthority == false)
            return;
        
        if (equippedWeapon != null && equippedWeapon.Object != null)
        {
            // 공격 쿨타임 체크
            if (attackCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                equippedWeapon.Fire(damage, criticalDamage);

                // 무기의 attackSpeed (쿨타임) (공격 속도 합산)
                Starter.Platformer.Player player = GetComponent<Starter.Platformer.Player>();
                float calculatedAttackTime = equippedWeapon.WeaponSO.attackSpeed;
                
                // Strike 무기일 때 5번 증강 기능(Strike 무기에 공격 속도 증가) 적용
                if (equippedWeapon.WeaponSO.weaponType == WeaponType.Strike && player.HasSpecialEffect(SpecialEffectType.StrikeWeaponSpeedUp))
                {
                    float speedBonus = player.GetSpecialEffectValue(SpecialEffectType.StrikeWeaponSpeedUp);
                    // 공속이 증가할수록 쿨타임은 줄어듦. (1 + 보너스)로 나누는 방식
                    calculatedAttackTime = calculatedAttackTime / (1f + speedBonus);
                }

                attackCooldownTimer = TickTimer.CreateFromSeconds(Runner, calculatedAttackTime);
                currentMaxCooldown = calculatedAttackTime;
            }
        }
    }
}
