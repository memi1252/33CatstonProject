using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine.EventSystems;

namespace Projectiles.NetworkObjectExample
{
	// PhysicsProjectile is a NetworkObject spawned in a scene. It uses NetworkRigidbody to synchronize
	// its position and rotation constantly to all clients. This is inefficient and should really be used only
	// for special scenarios (e.g. large rolling projectile that needs to use Rigidbody) or when simplicity is the key.
	// See Projectiles Advanced how even grenades can be done without spawning separate NetworkObjects.
	// For a simple projectile example jump directly to the Example 3.
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

		[Networked]
		private TickTimer _lifeCooldown { get; set; }
		[Networked]
		private NetworkBool _isDestroyed { get; set; }

		private bool _isDestroyedRender;

		private NetworkRigidbody3D _rigidbody;
		private Collider _collider;
		private Vector3 moveDirection;
		private float damageValue;
		private LayerMask collisionMask;

		// PUBLIC METHODS

		public void Fire(Vector3 position, Quaternion rotation, float damageValue, LayerMask collisionMask)
		{
			moveDirection = rotation.eulerAngles;
			this.damageValue = damageValue;
			this.collisionMask = collisionMask;
			// ✅ null 체크 추가
			if (_rigidbody == null)
			{
				_rigidbody = GetComponent<NetworkRigidbody3D>();
			}
			
			if (_rigidbody == null || _rigidbody.Rigidbody == null || Runner == null)
			{
				Debug.LogError("[PhysicsProjectile] Cannot fire - _rigidbody, Rigidbody, or Runner is null!");
				return;
			}

			// NetworkRigidbody의 Simulator가 아직 초기화되지 않은 경우를 대비하여 try-catch 사용 및 위치 직접 설정
			try
			{
				_rigidbody.Teleport(position, rotation);
			}
			catch (System.NullReferenceException)
			{
				// Teleport 내부에서 _physicsSimulator가 null일 경우 발생할 수 있음
				transform.SetPositionAndRotation(position, rotation);
				_rigidbody.Rigidbody.position = position;
				_rigidbody.Rigidbody.rotation = rotation;
			}

			// 발사 전 물리 상태 초기화 (버퍼 재사용 시 기존 힘 제거)
			_rigidbody.Rigidbody.isKinematic = false;
			_rigidbody.Rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.Rigidbody.angularVelocity = Vector3.zero;
			transform.rotation = rotation; // 방향 확실히 지정
			_rigidbody.Rigidbody.AddForce(transform.forward * _initialImpulse, ForceMode.Impulse);

			// Set cooldown after which the projectile should be despawned
			if (_lifeTime > 0f)
			{
				_lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTime);
			}
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			_collider.enabled = _isDestroyed == false;

			if (_lifeCooldown.IsRunning == true && _lifeCooldown.Expired(Runner) == true)
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
			if (collision.rigidbody != null && Object != null)
			{
				ProcessHit();
			}
		}

		// PRIVATE METHODS

		private void ProcessHit()
		{
			// Save destroyed flag so hit effects can be shown on other clients as well
			_isDestroyed = true;

			_lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTimeAfterHit);

			// Stop the movement
			_rigidbody.Rigidbody.isKinematic = true;
			_collider.enabled = false;
		}

		private void ShowDestroyEffect()
		{
			if (_hitEffect != null)
			{
				_hitEffect.SetActive(true);
			}

			// Hide projectile visual
			if (_visualsRoot != null)
			{
				_visualsRoot.SetActive(false);
			}
		}
		
		private void CheckCollisions(float moveDistance)
		{
			Ray ray = new Ray(transform.position, moveDirection);
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, moveDistance, collisionMask, QueryTriggerInteraction.Collide))
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
				damageableObject.TakeHit(damageValue, hit); // 데미지 입히기
			}
			Runner.Despawn(Object);
		}
	}
}
