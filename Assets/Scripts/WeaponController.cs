using System;
using Fusion;
using UnityEngine;

public class WeaponController : NetworkBehaviour
{
    public Transform weaponHold;
    public WeaponScriptableObject startWeapon;
    private Weapon equippedWeapon;

    private void Start()
    {
        if (startWeapon != null)
        {
            EquipWeapon(startWeapon);
        }
    }


    public void EquipWeapon(WeaponScriptableObject newWeapon)
    {
        if (equippedWeapon != null)
        {
            Runner.Despawn(equippedWeapon.Object);
        }

        equippedWeapon = Instantiate(newWeapon.weaponPrefab, weaponHold.position, weaponHold.rotation).GetComponent<Weapon>();
        equippedWeapon.WeaponSO = newWeapon;
        equippedWeapon.transform.parent = weaponHold;
    }

    public void Attack(Vector3 Look, float damage, float criticalDamage)
    {
        if (equippedWeapon != null)
        {
            equippedWeapon.Attack(Look, damage, criticalDamage );
        }
    }
}
