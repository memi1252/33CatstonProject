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

    public override void Spawned()
    {
        if (HasStateAuthority == false)
            return;

        if (startWeapon != null)
        {
            EquipWeapon(startWeapon);
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
        equippedWeapon.transform.parent = weaponHold;
        equippedWeapon.transform.localPosition = Vector3.zero;
        equippedWeapon.transform.localRotation = Quaternion.identity;

        // 무기를 장착할 때 쿨타임 초기화
        attackCooldownTimer = TickTimer.None;
    }

    public void Attack(Vector3 Look, float damage, float criticalDamage)
    {
        if (HasStateAuthority == false)
            return;
        
        if (equippedWeapon != null && equippedWeapon.Object != null)
        {
            // 공격 쿨타임 체크 (TickTimer가 만료되었거나 설정되지 않은 경우만 공격 허용)
            if (attackCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                //equippedWeapon.Attack(Look, damage, criticalDamage );
                equippedWeapon.Fire(damage, criticalDamage);

                // 무기의 attackSpeed (쿨타임)만큼 타이머 설정
                attackCooldownTimer = TickTimer.CreateFromSeconds(Runner, equippedWeapon.WeaponSO.attackSpeed);
            }
        }
    }
}
