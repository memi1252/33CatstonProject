using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;

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

          // ✅ 0.2초가 지난 후에만 레이캐스트 충돌 검사 수행
          if (_ignoreCollisionTimer.ExpiredOrNotRunning(Runner))
          {
             CheckCollisions(_rigidbody.Rigidbody.linearVelocity.magnitude * Runner.DeltaTime);
          }
          
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
          if (!_ignoreCollisionTimer.ExpiredOrNotRunning(Runner)) return;

          if (collision.rigidbody != null)
          {
             ProcessHit();
          }
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
                        explosionDamage = damageValue * 0.5f;
                        shouldExplode = true;
                    }

                    if (shouldExplode)
                    {
                        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius, collisionMask);
                        foreach (var hitCollider in hitColliders)
                        {
                            IDamageable damageableObject = hitCollider.GetComponentInParent<IDamageable>();
                            if (damageableObject != null)
                            {
                                damageableObject.TakeHit(explosionDamage, new RaycastHit());
                            }
                        }
                    }
                }
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
          if (_hitEffect != null) _hitEffect.SetActive(true);
          if (_visualsRoot != null) _visualsRoot.SetActive(false);
       }
       
       private void CheckCollisions(float moveDistance)
       {
          Vector3 startPos = transform.position - (moveDirection.normalized * moveDistance);
          Ray ray = new Ray(startPos, moveDirection.normalized);
          RaycastHit hit;
          
          if (Physics.Raycast(ray, out hit, moveDistance * 2f, collisionMask, QueryTriggerInteraction.Collide))
          {
             OnHitObject(hit);
          }
       }

        private void OnHitObject(RaycastHit hit)
        {
            if (HasStateAuthority == false)
                return;

            IDamageable damageableObject = hit.collider.GetComponent<IDamageable>();
            if (damageableObject != null)
            {
                damageableObject.TakeHit(damageValue, hit);

                if (AttributeEffectApplier.Instance != null)
                {
                    GameObject attacker = (ownerPlayer != null) ? ownerPlayer.gameObject :
                                          (ownerEnemy != null ? ownerEnemy.gameObject : null);

                    AttributeEffectApplier.Instance.ApplyAttributeEffect(
                        weaponAttribute,
                        damageableObject,
                        hit.point,
                        damageValue,
                        attacker
                    );
                }
            }
            
            // 레이캐스트 충돌 시에도 폭발 효과 등을 위해 ProcessHit 호출
            ProcessHit();
        }
    }
}