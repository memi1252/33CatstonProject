using UnityEngine;

public class Weapon : MonoBehaviour
{
    public WeaponScriptableObject WeaponSO;
    public Transform fireTransform;

    private Animator Animator;
    private GameObject projectilePrefab;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
    }

    private void Start()
    {
        projectilePrefab = WeaponSO.projectilePrefab;
    }

    public void Attack(Vector3 Look)
    {
        Animator.SetTrigger("Attack");
        var ammoObj = Instantiate(projectilePrefab, fireTransform.position, Quaternion.Euler(Look));
        Rigidbody rb = ammoObj.GetComponent<Rigidbody>();
        rb.AddForce(Look * WeaponSO.projectileSpeed, ForceMode.Impulse);
        Ammo ammo = ammoObj.GetComponent<Ammo>();
        ammo.projectileDis = WeaponSO.projectileDis;
        ammoObj.transform.localScale = Vector3.one * WeaponSO.tileSize *0.1f;
    }
}
