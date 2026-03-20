using System;
using Fusion;
using UnityEngine;

public class WeaponController : NetworkBehaviour
{
    public Transform weaponHold;
    public WeaponScriptableObject startWeapon;
    private Weapon equippedWeapon;

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
        equippedWeapon = weaponObject.GetComponent<Weapon>();
        equippedWeapon.WeaponSO = newWeapon;
        equippedWeapon.transform.parent = weaponHold;
        equippedWeapon.transform.localPosition = Vector3.zero;
        equippedWeapon.transform.localRotation = Quaternion.identity;
    }

    public void Attack(Vector3 Look, float damage, float criticalDamage)
    {
        if (HasStateAuthority == false)
            return;

        if (equippedWeapon != null && equippedWeapon.Object != null)
        {
            equippedWeapon.Attack(Look, damage, criticalDamage );
        }
    }
}
