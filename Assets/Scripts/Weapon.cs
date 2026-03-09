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
        var ammo = Instantiate(projectilePrefab, fireTransform.position, Quaternion.Euler(Look));
        Rigidbody rb = ammo.GetComponent<Rigidbody>();
        rb.AddForce(Look * WeaponSO.projectileSpeed, ForceMode.Impulse);
        Destroy(ammo, 7);
    }
}
