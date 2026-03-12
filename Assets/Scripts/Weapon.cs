using System;
using System.Collections;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    public WeaponScriptableObject WeaponSO;
    public Transform fireTransform;

    private Animator Animator;
    private LineRenderer LineRenderer;
    private ParticleSystem laserBeamEffect;
    private GameObject projectilePrefab;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        TryGetComponent(out LineRenderer);
        gameObject.transform.GetChild(2).TryGetComponent(out laserBeamEffect);
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
                    StartCoroutine(FireLaser());
                }
                break;
            case WeaponType.FloorBoard :
                break;
        }
        
    }
    
    private IEnumerator FireLaser()
    {
        LineRenderer.enabled = true;
        laserBeamEffect.Play();
        LineRenderer.positionCount = 2;
        LineRenderer.SetPosition(0, fireTransform.position);
        LineRenderer.SetPosition(1, fireTransform.position);

        Ray ray = new Ray(fireTransform.position, transform.forward);
        Vector3 endPoint;

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            endPoint = hit.point;
        }
        else
        {
            endPoint = fireTransform.position + transform.forward * 50;
        }

        float timer = 0f;
        float duration = .5f; 
        Vector3 startPos = fireTransform.position;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            LineRenderer.SetPosition(1, Vector3.Lerp(startPos, endPoint, progress));
            yield return null;
        }
        LineRenderer.SetPosition(1, endPoint);
        
        yield return new WaitForSeconds(0.3f);
        laserBeamEffect.Stop();
        LineRenderer.enabled = false;
    }

    
    private void Update()
    {
        if (WeaponSO.weaponType == WeaponType.Laser)
        {
            Debug.DrawRay(fireTransform.position, transform.forward * 100, Color.red);
        }
    }
}
