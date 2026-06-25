using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using Hovl;

namespace Projectiles.NetworkObjectExample
{
    [RequireComponent(typeof(NetworkRigidbody3D))]
    public class PhysicsProjectile : NetworkBehaviour
    {
       // PRIVATE MEMBERS

       [SerializeField]
       private float _initialImpulse = 100f;
       [SerializeField]
       private float _lifeTime = 4f;
       [SerializeField]
       private GameObject _visualsRoot;
       [SerializeField]
       private GameObject _hitEffect;
       [SerializeField]
       private float _lifeTimeAfterHit = 2f;

       private bool _isDestroyedRender;

       private NetworkRigidbody3D _rigidbody;
       private Collider _collider;
       private Vector3 moveDirection;
       private float damageValue;
       private LayerMask collisionMask;
       private TargetAttribute weaponAttribute;

       [HideInInspector]
       public Starter.Platformer.Player ownerPlayer;
       [HideInInspector]
       public Enemy ownerEnemy;
       
       [Networked]
       private TickTimer _lifeCooldown { get; set; }
       [Networked]
       private NetworkBool _isDestroyed { get; set; }
       
       // --- 추가된 부분: 발사 직후 충돌 무시 타이머 ---
       [Networked]
       private TickTimer _ignoreCollisionTimer { get; set; }

       // PUBLIC METHODS

       public void Fire(Vector3 position, Quaternion rotation, float damageValue, LayerMask collisionMask, TargetAttribute weaponAttribute = TargetAttribute.Normal)
       {
          moveDirection = rotation * Vector3.forward;
          this.damageValue = damageValue;
          this.collisionMask = collisionMask;
          this.weaponAttribute = weaponAttribute;

          if (_rigidbody == null)
          {
             _rigidbody = GetComponent<NetworkRigidbody3D>();
          }
          
          if (_rigidbody == null || _rigidbody.Rigidbody == null || Runner == null)
          {
             Debug.LogError("[PhysicsProjectile] Cannot fire - _rigidbody, Rigidbody, or Runner is null!");
             return;
          }

          try
          {
             _rigidbody.Teleport(position, rotation);
          }
          catch (System.NullReferenceException)
          {
             transform.SetPositionAndRotation(position, rotation);
             _rigidbody.Rigidbody.position = position;
             _rigidbody.Rigidbody.rotation = rotation;
          }

          _rigidbody.Rigidbody.isKinematic = false;
          _rigidbody.Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
          _rigidbody.Rigidbody.linearVelocity = Vector3.zero;
          _rigidbody.Rigidbody.angularVelocity = Vector3.zero;
          transform.rotation = rotation;
          _rigidbody.Rigidbody.AddForce(transform.forward * _initialImpulse, ForceMode.Impulse);

          _isDestroyed = false;
          _isDestroyedRender = false;
          _collider.enabled = true;
          if (_visualsRoot != null) _visualsRoot.SetActive(true);
          if (_hitEffect != null) _hitEffect.SetActive(false);

          if (_lifeTime > 0f)
          {
             _lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTime);
          }

          // ✅ 발사 시 0.2초 충돌 무시 타이머 설정
          _ignoreCollisionTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);

          // ⭐ 씬에 있는 모든 IDamageable의 콜라이더와 물리 충돌을 미리 무시 → 적/플레이어를 밀어내지 않음
          // (벽/땅과는 그대로 충돌)
          IgnorePhysicsWithDamageables();
       }

       private void IgnorePhysicsWithDamageables()
       {
          if (_collider == null) return;

          // 자식 콜라이더가 여러 개 있을 수 있으니 모두 수집
          Collider[] myColliders = GetComponentsInChildren<Collider>(true);

          // 씬에 있는 모든 IDamageable을 찾음
          var damageables = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
          foreach (var mb in damageables)
          {
             if (mb is not IDamageable) continue;

             Collider[] targetCols = mb.GetComponentsInChildren<Collider>(true);
             foreach (var myCol in myColliders)
             {
                if (myCol == null) continue;
                foreach (var targetCol in targetCols)
                {
                   if (targetCol == null || targetCol == myCol) continue;
                   Physics.IgnoreCollision(myCol, targetCol, true);
                }
             }
          }
       }

       // NetworkBehaviour INTERFACE

       public override void FixedUpdateNetwork()
       {
          if (Object == null || Runner == null || !Object.IsValid)
             return;

          try
          {
             if (_lifeCooldown.IsRunning == false)
                return;
          }
          catch (System.InvalidOperationException)
          {
             return;
          }

          if (_isDestroyed) 
          {
             if (_lifeCooldown.Expired(Runner))
             {
                Runner.Despawn(Object);
             }
             return;
          }

          CheckCollisions(_rigidbody.Rigidbody.linearVelocity.magnitude * Runner.DeltaTime);
          
          if (Object == null || !Object.IsValid) return;

          if (_lifeCooldown.Expired(Runner))
          {
             Runner.Despawn(Object);
          }
       }

       public override void Render()
       {
          if (_isDestroyed == true && _isDestroyedRender == false)
          {
             _isDestroyedRender = true;
             ShowDestroyEffect();
          }
       }

       // MONOBEHAVIOUR

       protected void Awake()
       {
          _rigidbody = GetComponent<NetworkRigidbody3D>();
          _collider = GetComponentInChildren<Collider>();

          _collider.enabled = false;

          if (_hitEffect != null)
          {
             _hitEffect.SetActive(false);
          }
       }

       protected void OnCollisionEnter(Collision collision)
       {
          if (Object == null || !Object.IsValid) return;

          // ✅ 0.2초가 지나지 않았다면 물리 엔진 충돌 무시
          //if (!_ignoreCollisionTimer.ExpiredOrNotRunning(Runner)) return;

          ContactPoint contactPoint = default;
          bool hasContact = collision.contactCount > 0;
          if (hasContact)
          {
             contactPoint = collision.GetContact(0);

             var mover = GetComponentInChildren<HS_ProjectileMover>();
             if (mover != null)
             {
                mover.TriggerHit(contactPoint.point, contactPoint.normal);
             }
          }

          string colliderName = collision.collider != null ? collision.collider.name : "null";
          int colliderLayer = collision.collider != null ? collision.collider.gameObject.layer : -1;
          Debug.Log($"[PhysicsProjectile] OnCollisionEnter -> {colliderName} (layer {colliderLayer}), HasStateAuthority={HasStateAuthority}, owner=Player:{ownerPlayer != null}/Enemy:{ownerEnemy != null}, damageValue={damageValue}");

          if (HasStateAuthority && collision.collider != null)
          {
             IDamageable damageableObject = collision.collider.GetComponentInParent<IDamageable>();
             Debug.Log($"[PhysicsProjectile] Direct hit IDamageable on '{colliderName}': {(damageableObject != null ? damageableObject.GetType().Name : "null")}");
             if (damageableObject != null)
             {
                GameObject attacker = (ownerPlayer != null) ? ownerPlayer.gameObject :
                                      (ownerEnemy != null ? ownerEnemy.gameObject : null);
                damageableObject.TakeHit(damageValue, new RaycastHit(), attacker);

                // 타격감: 내가 쏜 투사체가 적을 맞추면 내 카메라를 살짝 흔든다 (적이 나를 맞췄을 때와는 별개)
                if (ownerPlayer != null && ownerPlayer.HasInputAuthority && GameManager.Instance != null && GameManager.Instance.cameraShack != null)
                {
                    GameManager.Instance.cameraShack.Shake(0.12f, 0.1f);
                }

                if (AttributeEffectApplier.Instance != null)
                {

                   Vector3 hitPoint = hasContact ? contactPoint.point : transform.position;
                   AttributeEffectApplier.Instance.ApplyAttributeEffect(
                      weaponAttribute,
                      damageableObject,
                      hitPoint,
                      damageValue,
                      attacker
                   );
                }
             }
          }

          ProcessHit();
       }

       // PRIVATE METHODS

       private void ProcessHit()
       {
          try
          {
                _isDestroyed = true;
                _lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTimeAfterHit);

                if (HasStateAuthority)
                {
                    float explosionDamage = 0f;
                    float explosionRadius = 3f;
                    bool shouldExplode = false;

                    if (ownerPlayer != null && ownerPlayer.HasSpecialEffect(SpecialEffectType.ExplosiveProjectiles))
                    {
                        explosionDamage = damageValue * ownerPlayer.GetSpecialEffectValue(SpecialEffectType.ExplosiveProjectiles);
                        shouldExplode = true;
                    }
                    else if (ownerEnemy != null)
                    {
                        // 적 투척물: 떨어진 위치에서 폭발
                        explosionDamage = damageValue * 0.15f;
                        explosionRadius = 3.5f;
                        shouldExplode = true;
                    }

                    if (shouldExplode)
                    {
                        // collisionMask에 의존하면 prefab 설정에 따라 놓치는 경우가 생기므로 모든 레이어 검사 후 IDamageable로 필터
                        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius, ~0, QueryTriggerInteraction.Collide);
                        Debug.Log($"[PhysicsProjectile] Explosion at {transform.position}, radius={explosionRadius}, foundColliders={hitColliders.Length}, dmg={explosionDamage}");
                        var damaged = new System.Collections.Generic.HashSet<IDamageable>();
                        GameObject explosionAttacker = (ownerPlayer != null) ? ownerPlayer.gameObject :
                                                       (ownerEnemy != null ? ownerEnemy.gameObject : null);
                        foreach (var hitCollider in hitColliders)
                        {
                            IDamageable damageableObject = hitCollider.GetComponentInParent<IDamageable>();
                            if (damageableObject == null) continue;
                            if (!damaged.Add(damageableObject)) continue; // 같은 타겟 중복 적용 방지

                            // 자기 자신/주인 제외
                            if (ownerPlayer != null && hitCollider.transform.IsChildOf(ownerPlayer.transform)) continue;
                            if (ownerEnemy != null && hitCollider.transform.IsChildOf(ownerEnemy.transform)) continue;

                            Debug.Log($"[PhysicsProjectile]   - hit {hitCollider.name} -> {damageableObject.GetType().Name} dmg={explosionDamage}");
                            damageableObject.TakeHit(explosionDamage, new RaycastHit(), explosionAttacker);
                        }

                        // 폭발 한 번당 한 번만 흔들기 (맞은 대상 수와 무관)
                        if (damaged.Count > 0 && ownerPlayer != null && ownerPlayer.HasInputAuthority && GameManager.Instance != null && GameManager.Instance.cameraShack != null)
                        {
                            GameManager.Instance.cameraShack.Shake(0.18f, 0.18f);
                        }
                    }
                }
                Destroy(gameObject, 5);
          }
          catch (System.InvalidOperationException) { }

          if (_rigidbody != null && _rigidbody.Rigidbody != null)
          {
             _rigidbody.Rigidbody.linearVelocity = Vector3.zero;
             _rigidbody.Rigidbody.angularVelocity = Vector3.zero;
             _rigidbody.Rigidbody.isKinematic = true;
          }
          if (_collider != null)
          {
             _collider.enabled = false;
          }
       }

       private void ShowDestroyEffect()
       {
          SoundManager.Instance?.PlayProjectileImpact();
          if (_hitEffect != null) _hitEffect.SetActive(true);
          if (_visualsRoot != null) _visualsRoot.SetActive(false);
       }
       
       private void CheckCollisions(float moveDistance)
       {
          if (HasStateAuthority == false) return;

          // 콜라이더 반경 계산
          float sphereRadius = 0.3f;
          if (_collider is SphereCollider sc)
             sphereRadius = sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

          // ⭐ collisionMask는 무시하고 모든 레이어를 검사 — 부모에서 IDamageable을 찾는 방식이라 레이어 필터에 의존하지 않음
          // (플레이어 capsule이 layer 0(Default)에 있어서 mask 필터가 layer 6/8만 잡으면 놓침)
          int allLayers = ~0;

          // 1단계: 투척물 주변 OverlapSphere로 IDamageable 직접 탐지
          Collider[] overlaps = Physics.OverlapSphere(transform.position, sphereRadius * 1.5f, allLayers, QueryTriggerInteraction.Collide);
          foreach (var col in overlaps)
          {
             if (TryHandleHit(col, col.ClosestPoint(transform.position), (transform.position - col.bounds.center).normalized))
                return;
          }

          // 2단계: 현재 속도 방향으로 SphereCast (빠른 투척물 터널링 방지)
          Vector3 currentVelocity = _rigidbody.Rigidbody.linearVelocity;
          if (currentVelocity.sqrMagnitude < 0.0001f)
             return;

          Vector3 dir = currentVelocity.normalized;
          Vector3 startPos = transform.position - (dir * moveDistance);

          RaycastHit[] hits = Physics.SphereCastAll(startPos, sphereRadius, dir, moveDistance * 2f, allLayers, QueryTriggerInteraction.Collide);
          System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

          foreach (var hit in hits)
          {
             if (hit.collider == null) continue;
             if (TryHandleHit(hit.collider, hit.point, hit.normal))
                return;
          }
       }

       private bool TryHandleHit(Collider col, Vector3 hitPoint, Vector3 hitNormal)
       {
          if (col == null) return false;
          if (col.transform.IsChildOf(transform)) return false; // 자신 콜라이더
          if (ownerPlayer != null && col.transform.IsChildOf(ownerPlayer.transform)) return false;
          if (ownerEnemy != null && col.transform.IsChildOf(ownerEnemy.transform)) return false;

          IDamageable dmg = col.GetComponentInParent<IDamageable>();
          if (dmg == null) return false;

          GameObject attacker = (ownerPlayer != null) ? ownerPlayer.gameObject :
                                (ownerEnemy != null ? ownerEnemy.gameObject : null);
          dmg.TakeHit(damageValue, default, attacker);

          if (ownerPlayer != null && ownerPlayer.HasInputAuthority && GameManager.Instance != null && GameManager.Instance.cameraShack != null)
          {
             GameManager.Instance.cameraShack.Shake(0.12f, 0.1f);
          }

          if (AttributeEffectApplier.Instance != null)
          {
             AttributeEffectApplier.Instance.ApplyAttributeEffect(weaponAttribute, dmg, hitPoint, damageValue, attacker);
          }

          var mover = GetComponentInChildren<HS_ProjectileMover>();
          if (mover != null)
             mover.TriggerHit(hitPoint, hitNormal);

          if (_collider != null)
             _collider.enabled = false;

          ProcessHit();
          return true;
       }
    }
}