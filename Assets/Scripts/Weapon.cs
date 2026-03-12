using UnityEngine;

public class Weapon : MonoBehaviour
{
    public WeaponScriptableObject WeaponSO;
    public Transform fireTransform;

    private Animator Animator;
    private LineRenderer LineRenderer;
    private GameObject projectilePrefab;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        TryGetComponent(out LineRenderer);
    }

    private void Start()
    {
        projectilePrefab = WeaponSO.projectilePrefab;
    }

    public void Attack(Vector3 Look)
    {
        Animator.SetTrigger("Attack");
        switch (WeaponSO.weaponType)
        {
            case WeaponType.Projectile:
                var ammoObj = Instantiate(projectilePrefab, fireTransform.position, Quaternion.Euler(Look));
                Rigidbody rb = ammoObj.GetComponent<Rigidbody>();
                rb.AddForce(Look * WeaponSO.projectileSpeed, ForceMode.Impulse);
                Ammo ammo = ammoObj.GetComponent<Ammo>();
                ammo.projectileDis = WeaponSO.projectileDis;
                ammoObj.transform.localScale = Vector3.one * WeaponSO.tileSize *0.1f;
                break;
            case WeaponType.Laser:
                if(LineRenderer != null)
                {
                    Ray ray = new Ray(fireTransform.position, Look);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        if(hit.distance < WeaponSO.projectileDis)
                        {
                            LineRenderer.SetPosition(1, hit.point);
                        }
                        else
                        {
                            LineRenderer.SetPosition(1, fireTransform.position + Look * WeaponSO.projectileDis);
                        }
                    }
                    LineRenderer.SetPosition(0, fireTransform.position);
                    LineRenderer.enabled = true;
                }
                break;
            case WeaponType.FloorBoard :
                break;
        }
        
    }
}
