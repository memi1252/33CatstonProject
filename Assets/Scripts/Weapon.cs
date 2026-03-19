using System;
using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

public class Weapon : MonoBehaviour
{
    public WeaponScriptableObject WeaponSO;
    public Transform fireTransform;
    public GameObject attackScope;

    public float damage;

    private Animator Animator;
    private LineRenderer LineRenderer;
    private ParticleSystem ParticleEffect;
    private VisualEffect VisualEffect;
    private GameObject projectilePrefab;
    
    private Transform originParent;
    

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        TryGetComponent(out LineRenderer);
        TryGetComponentInChildren(gameObject, out ParticleEffect);
        TryGetComponentInChildren(gameObject, out VisualEffect);
        if (attackScope != null)
        {
            attackScope.transform.localPosition = Vector3.zero;
        }
    }
    
    
    
    public static bool TryGetComponentInChildren<T>(GameObject obj, out T component) where T : Component
    {
        component = obj.GetComponentInChildren<T>();
        return component != null;
    }

    private void Start()
    {
        originParent = transform.parent;
        projectilePrefab = WeaponSO.projectilePrefab;
    }

    public void Attack(Vector3 Look, float damage, float criticalDamage)
    {
        
        switch (WeaponSO.weaponType)
        {
            case WeaponType.Projectile:
                Animator.SetTrigger("Attack");
                var ammoObj = Instantiate(projectilePrefab, fireTransform.position, Quaternion.Euler(Look));
                Rigidbody rb = ammoObj.GetComponent<Rigidbody>();
                rb.AddForce(Look * WeaponSO.projectileSpeed, ForceMode.Impulse);
                Ammo ammo = ammoObj.GetComponent<Ammo>();
                ammo.projectileDis = WeaponSO.projectileDis;
                ammoObj.transform.localScale = Vector3.one * WeaponSO.tileSize *0.1f;
                Destroy(ammoObj, 10);
                break;
            case WeaponType.Laser:
                StartCoroutine(FireLaser());
                break;
            case WeaponType.Area :
                StartCoroutine(AreaAttack());
                break;
            case WeaponType.Strike:
                break;
        }
        
    }

    private IEnumerator AreaAttack()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, transform.root.position);
        Vector3 mouseWorldPos = Vector3.zero;
        if (groundPlane.Raycast(ray, out float distance))
        {
            mouseWorldPos = ray.GetPoint(distance);
        }
        mouseWorldPos.y = originParent.position.y;
        transform.parent = null;
        transform.position = mouseWorldPos;
        float scopeSize = WeaponSO.tileSize;
        attackScope.transform.localScale = new Vector3(scopeSize, scopeSize, scopeSize);
        yield return new WaitForSeconds(1f);
        attackScope.transform.localScale = Vector3.zero;
        VisualEffect.enabled = true;
        float lifeTime = WeaponSO.projectileSpeed * .08f;
        VisualEffect.SetFloat("Size", WeaponSO.tileSize*0.1f);
        VisualEffect.SetFloat("LifeTime", lifeTime);
        VisualEffect.Play();
        yield return new WaitForSeconds(lifeTime);
        VisualEffect.enabled = false;
        transform.parent = originParent;
        transform.localPosition = Vector3.zero;
        yield return  null;
    }
    
    private IEnumerator FireLaser()
    {
        if (LineRenderer != null)
        {
            LineRenderer.enabled = true;
            if (ParticleEffect != null)
                ParticleEffect.Play();
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
            if (ParticleEffect != null)
                ParticleEffect.Stop();
            LineRenderer.enabled = false;
        }
        else if (VisualEffect != null)
        {
            VisualEffect.Play();
            if (ParticleEffect != null)
                ParticleEffect.Play();
            Ray ray = new Ray(fireTransform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.DrawRay(fireTransform.position, transform.forward * hit.distance, Color.red, 5f);
                VisualEffect.SetVector3("TargetPosition", new Vector3(0, hit.distance * 0.5f, 0));
            }
            else
            {
                VisualEffect.SetVector3("TargetPosition", new Vector3(0, 50, 0));
            }
            
            yield return new WaitForSeconds(0.3f);
            VisualEffect.Stop();
            if (ParticleEffect != null)
                ParticleEffect.Stop();
        }
        
    }
}
