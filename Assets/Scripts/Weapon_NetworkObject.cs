using System.Collections;
using Fusion;
using UnityEngine;

namespace Projectiles.NetworkObjectExample
{
    public class Weapon_NetworkObject : WeaponBase
    {
        // PRIVATE MEMBERS

        [HideInInspector]
        public Starter.Platformer.Player ownerPlayer; // Player 참조 추가
        [HideInInspector]
        public Enemy ownerEnemy; // 적 참조 추가

        [SerializeField] private PhysicsProjectile _projectilePrefab;

        [SerializeField,
         Tooltip(
             "NetworkObjectBuffer will pre-spawn projectiles in advance to mitigate spawn delay on input authority")]
        private bool _useBuffer;

        [SerializeField] private NetworkObjectBuffer _projectileBuffer;

        [Networked] private int _fireCount { get; set; }

        public WeaponScriptableObject WeaponSO;

        // 무기 종류를 모든 클라이언트에 동기화하기 위한 인덱스 (WeaponDatabase 기준).
        // WeaponSO 자체는 ScriptableObject 참조라 네트워크로 못 보내므로 인덱스로 동기화 후 각 클라가 복원한다.
        [Networked] public int WeaponSOIndex { get; set; } = -1;

        private int _visibleFireCount;

        private Transform originParent;

        private float damage;
        private float criticalDamage;
        
        public LayerMask raycastLayerMask;

        private bool _isBoundToOwner;

        public float plusDelay;

        // WeaponBase INTERFACE



        private void Start()
        {
            if (VisualEffect)
                VisualEffect.Stop();
            if (attackScope)
                attackScope.SetActive(false);
        }

        public override void Fire(float damage, float criticalDamage)
        {
            this.damage = damage;
            this.criticalDamage = criticalDamage;
            switch (WeaponSO.weaponType)
            {
                case WeaponType.Projectile:
                    Animator.SetTrigger("Attack");
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
                    FireLaserLogic();
                    _fireCount++;
                    break;
                case WeaponType.Area:
                    AreaAttackLogic();
                    _fireCount++;
                    break;
                case WeaponType.Strike:
                    StrikeAttackLogic();
                    _fireCount++;
                    break;
            }
        }

        private void FireLaserLogic()
        {
            Ray ray = new Ray(FireTransform.position, transform.forward);
            Vector3 endPoint;
            float hitDistance = 50f;

            if (Physics.Raycast(ray, out RaycastHit hit, 50f, raycastLayerMask))
            {
                endPoint = hit.point;
                hitDistance = hit.distance;
                if (HasStateAuthority && hit.collider != null && hit.collider.transform.root.gameObject != originParent.root.gameObject)
                {
                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        float totalDamage = damage + WeaponSO.weaponDamage;
                        damageable.TakeHit(totalDamage, hit, GetAttackerGameObject());

                        // 속성 효과 적용
                        ApplyWeaponAttributeEffect(damageable, hit.point, totalDamage);
                    }
                }
            }
            else
            {
                endPoint = FireTransform.position + transform.forward * 50;
            }

            Rpc_PlayLaserEffect(endPoint, hitDistance);
        }

        private void AreaAttackLogic()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.root.position);
            Vector3 mouseWorldPos = Vector3.zero;
            if (groundPlane.Raycast(ray, out float distance))
            {
                mouseWorldPos = ray.GetPoint(distance);
            }

            mouseWorldPos.y = originParent.position.y;

            if (HasStateAuthority)
            {
                StartCoroutine(DealAreaDamage(mouseWorldPos));
            }

            Rpc_PlayAreaEffect(mouseWorldPos, WeaponSO.tileSize);
        }

        private void StrikeAttackLogic()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.root.position);
            Vector3 mouseWorldPos = Vector3.zero;
            if (groundPlane.Raycast(ray, out float distance))
            {
                mouseWorldPos = ray.GetPoint(distance);
            }

            mouseWorldPos.y = originParent.position.y;

            if (HasStateAuthority)
            {
                StartCoroutine(DealStrikeDamage(mouseWorldPos));
            }

            // 스코프 크기도 함께 넘겨주기 위해 Rpc 파라미터 수정
            Rpc_PlayStrikeEffect(mouseWorldPos, WeaponSO.tileSize);
        }

        private IEnumerator DealAreaDamage(Vector3 targetPos)
        {
            // 이펙트가 터지는 시간에 맞춰 1초 대기
            yield return new WaitForSeconds(1f);

            // attackScope의 크기가 tileSize이므로, 반지름은 절반인 tileSize * 0.5f
            float radius = WeaponSO.tileSize * 0.5f;
            float lifeTime = WeaponSO.projectileSpeed * .08f;
            float tickRate = 0.1f; // 도중에 들어온 적도 잡기 위해 짧은 주기로 검사
            float timer = 0f;

            // 이펙트 1회 동안 이미 데미지를 입은 대상은 다시 맞지 않도록 추적
            var alreadyHit = new System.Collections.Generic.HashSet<IDamageable>();

            while (timer < lifeTime)
            {
                Collider[] hitColliders = Physics.OverlapSphere(targetPos, radius, raycastLayerMask);

                foreach (var hitCollider in hitColliders)
                {
                    IDamageable damageableObject = hitCollider.GetComponentInParent<IDamageable>();
                    if (damageableObject == null) continue;
                    if (!alreadyHit.Add(damageableObject)) continue;

                    float totalDamage = damage + WeaponSO.weaponDamage;
                    damageableObject.TakeHit(totalDamage, new RaycastHit(), GetAttackerGameObject());
                    ApplyWeaponAttributeEffect(damageableObject, targetPos, totalDamage);
                }

                // 다음 데미지 틱까지 대기
                yield return new WaitForSeconds(tickRate);
                timer += tickRate;
            }
        }

        private IEnumerator DealStrikeDamage(Vector3 targetPos)
        {
            // 투사체가 땅에 떨어져서 터지기까지의 시간 대기 (VisualStrikeAttack 시간과 동일하게 맞춤)
            yield return new WaitForSeconds(1.5f);

            // attackScope의 크기 기반 반경
            float radius = WeaponSO.tileSize * 0.5f;

            Collider[] hitColliders = Physics.OverlapSphere(targetPos, radius, raycastLayerMask);

            foreach (var hitCollider in hitColliders)
            {
                IDamageable damageableObject = hitCollider.GetComponentInParent<IDamageable>();
                if (damageableObject != null)
                {
                    float totalDamage = damage + WeaponSO.weaponDamage;
                    damageableObject.TakeHit(totalDamage, new RaycastHit(), GetAttackerGameObject());

                    // 속성 효과 적용
                    ApplyWeaponAttributeEffect(damageableObject, targetPos, totalDamage);
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void Rpc_PlayLaserEffect(Vector3 endPoint, float hitDistance)
        {
            StartCoroutine(VisualFireLaser(endPoint, hitDistance));
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void Rpc_PlayAreaEffect(Vector3 targetPos, float scopeSize)
        {
            StartCoroutine(VisualAreaAttack(targetPos, scopeSize));
        }

        // 스코프 크기를 매개변수로 추가
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void Rpc_PlayStrikeEffect(Vector3 targetPos, float scopeSize)
        {
            StartCoroutine(VisualStrikeAttack(targetPos, scopeSize));
        }

        public override void Spawned()
        {
            _visibleFireCount = _fireCount;
            originParent = transform.parent;
            _isBoundToOwner = BindToOwnerWeaponHold();
            ResolveWeaponSO();
        }

        public override void FixedUpdateNetwork()
        {
            if (_isBoundToOwner == false)
            {
                _isBoundToOwner = BindToOwnerWeaponHold();
            }

            // 인덱스가 늦게 동기화되거나 권한자가 나중에 세팅한 경우 대비해 복원 재시도
            if (WeaponSO == null && WeaponSOIndex >= 0)
            {
                ResolveWeaponSO();
            }
        }

        // 네트워크로 받은 인덱스로 WeaponSO를 복원 (모든 클라이언트에서 실행)
        private void ResolveWeaponSO()
        {
            if (WeaponSOIndex < 0) return;
            if (WeaponDatabase.Instance == null) return;

            WeaponScriptableObject resolved = WeaponDatabase.Instance.Get(WeaponSOIndex);
            if (resolved != null)
            {
                WeaponSO = resolved;
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
                //PlayFireEffect();
            }

            _visibleFireCount = _fireCount;
        }

        // PRIVATE METHODS

        private void FireSimple()
        {
            if (HasStateAuthority == false) return;

            var projectile = Runner.Spawn(_projectilePrefab, FireTransform.position, FireTransform.rotation, Object.InputAuthority);

            // 주인 정보 전달 (둘 중 있는 것을 전달)
            projectile.ownerPlayer = this.ownerPlayer;
            projectile.ownerEnemy = this.ownerEnemy;

            projectile.Fire(FireTransform.position, FireTransform.rotation, damage + WeaponSO.weaponDamage, raycastLayerMask, WeaponSO.targetAttribute);
        }

        private void FireWithBuffer()
        {
            var projectile = _projectileBuffer.Get<PhysicsProjectile>(FireTransform.position, FireTransform.rotation, Object.InputAuthority);
            if (projectile != null)
            {
                projectile.ownerPlayer = this.ownerPlayer;
                projectile.ownerEnemy = this.ownerEnemy; // 적 주인 전달

                projectile.Fire(FireTransform.position, FireTransform.rotation, damage + WeaponSO.weaponDamage, raycastLayerMask, WeaponSO.targetAttribute);
            }
        }

        // 디태치한 이펙트를 다시 무기 밑으로 붙이기 전 호출.
        // 코루틴 대기 중 무기가 디스폰/풀 반환되면 transform이 씬이 아닌 프리팹 에셋 상태가 되어
        // reparent 시 "resides in a Prefab asset" 예외가 발생하므로, 살아있는 씬 인스턴스일 때만 허용한다.
        private bool CanReparentToSelf()
        {
            return this != null && gameObject != null && gameObject.scene.IsValid();
        }

        private IEnumerator VisualAreaAttack(Vector3 mouseWorldPos, float scopeSize)
        {
            Quaternion scopeOriginalRot = Quaternion.identity;
            Quaternion vfxOriginalRot = Quaternion.identity;

            if (attackScope != null)
            {
                scopeOriginalRot = attackScope.transform.localRotation;
                attackScope.transform.parent = null;
                attackScope.transform.position = mouseWorldPos;
                attackScope.SetActive(true);

                // Rpc로 전달받은 크기를 그대로 적용
                attackScope.transform.localScale = new Vector3(scopeSize, scopeSize, scopeSize);
            }

            if (VisualEffect != null)
            {
                vfxOriginalRot = VisualEffect.transform.localRotation;
                VisualEffect.transform.parent = null;
                VisualEffect.transform.position = mouseWorldPos + new Vector3(0, .2f, 0);
            }


            if (ParticleEffect != null)
            {
                vfxOriginalRot = ParticleEffect.transform.localRotation;
                ParticleEffect.transform.parent = null;
                ParticleEffect.transform.position = mouseWorldPos;
            }

            yield return new WaitForSeconds(1f);

            if (attackScope != null)
            {
                attackScope.SetActive(false);
                attackScope.transform.localScale = Vector3.zero;
            }

            float lifeTime = 1f;
            if (WeaponSO != null)
            {
                lifeTime = WeaponSO.projectileSpeed * 0.08f;
                if (VisualEffect != null)
                {
                    VisualEffect.SetFloat("Size", scopeSize * 0.1f);
                }
            }

            if (VisualEffect != null)
            {
                VisualEffect.SetFloat("LifeTime", lifeTime);
                VisualEffect.Stop();
                VisualEffect.Play();
            }

            if (ParticleEffect != null)
            {
                if (ParticleEffect.transform.childCount > 0 && ParticleEffect.transform.GetChild(0).GetComponent<SpriteRenderer>())
                {
                    ParticleEffect.transform.GetChild(0).gameObject.SetActive(true);
                }
                ParticleEffect.transform.localScale = Vector3.one * (scopeSize + ParticelEffectPlugRange); // WeaponSO가 null일 수 있으므로 scopeSize를 사용
                ParticleEffect.Play();
            }

            yield return new WaitForSeconds(lifeTime  +plusDelay);

            if (VisualEffect != null)
            {
                VisualEffect.Stop();
                if (CanReparentToSelf())
                {
                    VisualEffect.transform.parent = transform;
                    VisualEffect.transform.localPosition = Vector3.zero;
                    VisualEffect.transform.localRotation = vfxOriginalRot;
                }
            }

            if (ParticleEffect != null)
            {
                // if (ParticleEffect.transform.childCount > 0 && ParticleEffect.transform.GetChild(0).GetComponent<SpriteRenderer>())
                // {
                //     ParticleEffect.transform.GetChild(0).gameObject.SetActive(false);
                // }

                ParticleEffect.Stop();
                if (CanReparentToSelf())
                {
                    ParticleEffect.transform.parent = transform;
                    ParticleEffect.transform.localPosition = Vector3.zero;
                    ParticleEffect.transform.localRotation = vfxOriginalRot;
                }
            }

            if (attackScope != null && CanReparentToSelf())
            {
                attackScope.transform.parent = transform;
                attackScope.transform.localPosition = Vector3.zero;
                attackScope.transform.localRotation = scopeOriginalRot;
            }
        }

        private IEnumerator VisualStrikeAttack(Vector3 targetPos, float scopeSize)
        {
            Quaternion scopeOriginalRot = Quaternion.identity;

            if (attackScope != null)
            {
                scopeOriginalRot = attackScope.transform.localRotation;
                attackScope.transform.parent = null;
                attackScope.transform.position = targetPos;
                attackScope.SetActive(true);

                // Rpc로 전달받은 크기를 그대로 적용
                attackScope.transform.localScale = new Vector3(scopeSize, scopeSize, scopeSize);
            }

            // 투사체가 땅에 떨어질 때까지 걸리는 대략적인 시간 대기 (적절히 조정 가능)
            yield return new WaitForSeconds(1.5f  +plusDelay);

            if (attackScope != null)
            {
                attackScope.SetActive(false);
                attackScope.transform.localScale = Vector3.zero;
                if (CanReparentToSelf())
                {
                    attackScope.transform.parent = transform;
                    attackScope.transform.localPosition = Vector3.zero;
                    attackScope.transform.localRotation = scopeOriginalRot;
                }
            }

            // 스코프가 사라졌을 때 파티클 재생
            if (ParticleEffect != null)
            {
                ParticleEffect.transform.parent = null; // 타겟 위치로 보내기 위해 부모 해제
                ParticleEffect.transform.position = targetPos;
                ParticleEffect.transform.rotation = Quaternion.LookRotation(Vector3.right);

                // 스코프 크기만큼 파티클 크기 키우기 (Strike)
                ParticleEffect.transform.localScale = new Vector3(scopeSize +ParticelEffectPlugRange, scopeSize +ParticelEffectPlugRange, scopeSize+ParticelEffectPlugRange);

                ParticleEffect.Play();
            }

            // 파티클 지속 시간 대기
            // 현재 Area와 비슷하게 LifeTime과 관련된 값이 없으므로 임의의 파티클 지속시간 1초 대기
            yield return new WaitForSeconds(1f);

            if (ParticleEffect != null)
            {
                ParticleEffect.Stop();
                if (CanReparentToSelf())
                {
                    ParticleEffect.transform.parent = transform; // 다시 무기 밑으로 복귀
                    ParticleEffect.transform.localPosition = Vector3.zero;
                    ParticleEffect.transform.localRotation = Quaternion.identity;

                    // 줄였던 크기 원상 복귀
                    ParticleEffect.transform.localScale = Vector3.one;
                }
            }
        }

        private IEnumerator VisualFireLaser(Vector3 endPoint, float hitDistance)
        {
            if (LineRenderer != null)
            {
                LineRenderer.enabled = true;
                if (ParticleEffect != null)
                    ParticleEffect.Play();
                LineRenderer.positionCount = 2;
                LineRenderer.SetPosition(0, FireTransform.position);
                LineRenderer.SetPosition(1, FireTransform.position);

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

                if (hitDistance < 50f)
                {
                    VisualEffect.SetVector3("TargetPosition", new Vector3(0, hitDistance * 0.5f, 0));
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

        /// <summary>
        /// 이 무기로 공격한 주체(플레이어 또는 적)의 GameObject를 반환. 어그로 등 식별용.
        /// </summary>
        private GameObject GetAttackerGameObject()
        {
            if (ownerPlayer != null) return ownerPlayer.gameObject;
            if (ownerEnemy != null) return ownerEnemy.gameObject;
            return null;
        }

        /// <summary>
        /// 무기의 속성에 맞는 효과 적용
        /// </summary>
        private void ApplyWeaponAttributeEffect(IDamageable target, Vector3 targetPosition, float totalDamage)
        {
            AttributeEffectApplier effectApplier = AttributeEffectApplier.Instance;
            if (effectApplier != null && WeaponSO != null)
            {
                // 공격자(GameObject) 판별
                GameObject attacker = (ownerPlayer != null) ? ownerPlayer.gameObject :
                                      (ownerEnemy != null ? ownerEnemy.gameObject : null);

                effectApplier.ApplyAttributeEffect(WeaponSO.targetAttribute, target, targetPosition, totalDamage, attacker);
            }
        }
    }
}

