using System.Collections;
using Fusion;
using UnityEngine;

namespace Projectiles.NetworkObjectExample
{
    public class Weapon_NetworkObject : WeaponBase
    {
        // PRIVATE MEMBERS

        [SerializeField] private PhysicsProjectile _projectilePrefab;

        [SerializeField,
         Tooltip(
             "NetworkObjectBuffer will pre-spawn projectiles in advance to mitigate spawn delay on input authority")]
        private bool _useBuffer;

        [SerializeField] private NetworkObjectBuffer _projectileBuffer;

        [Networked] private int _fireCount { get; set; }

        public WeaponScriptableObject WeaponSO;

        private int _visibleFireCount;

        private Transform originParent;

        private float damage;
        private float criticalDamage;
        public LayerMask raycastLayerMask;

        private bool _isBoundToOwner;

        // WeaponBase INTERFACE

        public override void Fire(float damage, float criticalDamage)
        {
            this.damage = damage;
            this.criticalDamage = criticalDamage;
            switch (WeaponSO.weaponType)
            {
                case WeaponType.Projectile:
                    if (_useBuffer == true)
                    {
                        FireWithBuffer();
                    }
                    else
                    {
                        FireSimple();
                    }

                    _fireCount++;
                    break;
                case WeaponType.Laser:
                    StartCoroutine(FireLaser());
                    break;
                case WeaponType.Area:
                    StartCoroutine(AreaAttack());
                    break;
                case WeaponType.Strike:
                    break;
            }
        }

        public override void Spawned()
        {
            // In case of late join (and other scenarios) this object can be spawned
            // with fire count larger than zero. To prevent unwanted fire effects triggered in Render method
            // we consider all fire that happened before the Spawn as already visible.
            _visibleFireCount = _fireCount;
            originParent = transform.parent;
            _isBoundToOwner = BindToOwnerWeaponHold();
        }

        public override void FixedUpdateNetwork()
        {
            if (_isBoundToOwner == false)
            {
                _isBoundToOwner = BindToOwnerWeaponHold();
            }
        }

        private bool BindToOwnerWeaponHold()
        {
            if (Runner == null || Object == null)
                return false;

            if (Runner.TryGetPlayerObject(Object.InputAuthority, out NetworkObject ownerObject) == false)
                return false;

            WeaponController weaponController = ownerObject.GetComponent<WeaponController>();
            if (weaponController == null || weaponController.weaponHold == null)
                return false;

            transform.SetParent(weaponController.weaponHold, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            originParent = transform.parent;
            return true;
        }

        public override void Render()
        {
            if (_visibleFireCount < _fireCount)
            {
                PlayFireEffect();
            }

            _visibleFireCount = _fireCount;
        }

        // PRIVATE METHODS

        private void FireSimple()
        {
            // Spawn can be called only on state authority
            if (HasStateAuthority == false)
                return;

            var projectile = Runner.Spawn(_projectilePrefab, FireTransform.position, FireTransform.rotation,
                Object.InputAuthority);
            projectile.Fire(FireTransform.position, FireTransform.rotation, damage + WeaponSO.weaponDamage, raycastLayerMask);
        }

        private void FireWithBuffer()
        {
            // In Fusion 2 there is no longer a predicted spawn. We can go around this to have a buffer of pre-spawned
            // objects that are already living inside simulation but inactive. Check NetworkObjectBuffer component for more info.
            var projectile = _projectileBuffer.Get<PhysicsProjectile>(FireTransform.position, FireTransform.rotation,
                Object.InputAuthority);
            if (projectile != null)
            {
                projectile.Fire(FireTransform.position, FireTransform.rotation, damage + WeaponSO.weaponDamage, raycastLayerMask);
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
            float lifeTime = WeaponSO.projectileSpeed * .08f;
            VisualEffect.SetFloat("Size", WeaponSO.tileSize * 0.1f);
            VisualEffect.SetFloat("LifeTime", lifeTime);
            VisualEffect.Play();
            yield return new WaitForSeconds(lifeTime);
            VisualEffect.enabled = false;
            transform.parent = originParent;
            transform.localPosition = Vector3.zero;
            yield return null;
        }

        private IEnumerator FireLaser()
        {
            if (LineRenderer != null)
            {
                LineRenderer.enabled = true;
                if (ParticleEffect != null)
                    ParticleEffect.Play();
                LineRenderer.positionCount = 2;
                LineRenderer.SetPosition(0, FireTransform.position);
                LineRenderer.SetPosition(1, FireTransform.position);

                Ray ray = new Ray(FireTransform.position, transform.forward);
                Vector3 endPoint;

                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    endPoint = hit.point;
                    if (hit.collider != null && hit.collider.TryGetComponent<IDamageable>(out IDamageable damageableObject))
                    {
                        damageableObject.TakeHit(damage + WeaponSO.weaponDamage, hit); // 데미지 입히기
                    }
                }
                else
                {
                    endPoint = FireTransform.position + transform.forward * 50;
                }

                float timer = 0f;
                float duration = .5f;
                Vector3 startPos = FireTransform.position;

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
                Ray ray = new Ray(FireTransform.position, transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Debug.DrawRay(FireTransform.position, transform.forward * hit.distance, Color.red, 5f);
                    VisualEffect.SetVector3("TargetPosition", new Vector3(0, hit.distance * 0.5f, 0));
                    
                    if (hit.collider != null && hit.collider.TryGetComponent<IDamageable>(out IDamageable damageableObject))
                    {
                        damageableObject.TakeHit(damage + WeaponSO.weaponDamage, hit); // 데미지 입히기
                    }
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
}