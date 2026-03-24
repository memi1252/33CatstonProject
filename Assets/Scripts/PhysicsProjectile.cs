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

		private bool _isDestroyedRender;

		private NetworkRigidbody3D _rigidbody;
		private Collider _collider;
		private Vector3 moveDirection;
		private float damageValue;
		private LayerMask collisionMask;
		
		[Networked]
		private TickTimer _lifeCooldown { get; set; }
		[Networked]
		private NetworkBool _isDestroyed { get; set; }

		// PUBLIC METHODS

		public void Fire(Vector3 position, Quaternion rotation, float damageValue, LayerMask collisionMask)
		{
			moveDirection = rotation * Vector3.forward;
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

			_isDestroyed = false;
			_isDestroyedRender = false;
			_collider.enabled = true;
			if (_visualsRoot != null) _visualsRoot.SetActive(true);
			if (_hitEffect != null) _hitEffect.SetActive(false);

			if (_lifeTime > 0f)
			{
				_lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTime);
			}
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			// 버퍼에서 미리 생성만 되고 아직 활성화(Fire)되지 않은 객체일 경우, 네트워크 속성 접근을 방지
			// Object나 Runner가 제대로 할당되지 않았다면 아예 건너뛰게 강제 처리
			if (Object == null || Runner == null || !Object.IsValid)
				return;

			try
			{
				if (_lifeCooldown.IsRunning == false)
					return; // 아직 발사(Fire)되지 않은 대기 상태면 실행 생략
			}
			catch (System.InvalidOperationException)
			{
				return;
			}

			if (_isDestroyed) 
			{
				// 파괴된(폭발 이펙트 재생 중인) 상태라면 수명 타이머만 체크합니다.
				if (_lifeCooldown.Expired(Runner))
				{
					Runner.Despawn(Object);
				}
				return;
			}

			CheckCollisions(_rigidbody.Rigidbody.linearVelocity.magnitude * Runner.DeltaTime);
			
			// CheckCollisions 이후 객체가 Despawn 되었을 수 있으므로 다시 체크
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
				// Save destroyed flag so hit effects can be shown on other clients as well
				_isDestroyed = true;
				_lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTimeAfterHit);
			}
			catch (System.InvalidOperationException)
			{
				// 아직 Network 속성에 접근할 수 없는 경우 무시
			}

			// Stop the movement
			if (_rigidbody != null && _rigidbody.Rigidbody != null)
			{
				// kinematic 상태인 경우 velocity 설정이 에러를 발생시키므로 순서 변경
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
			// 현재 위치에서 한 틱 전 위치를 계산하여 좀 더 안전하게 레이캐스트 시작 (빠른 속도로 인한 판정 무시 방지)
			Vector3 startPos = transform.position - (moveDirection.normalized * moveDistance);
			Ray ray = new Ray(startPos, moveDirection.normalized);
			RaycastHit hit;
			
			// 충돌 판정 거리를 약간 여유 있게 처리
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
				damageableObject.TakeHit(damageValue, hit); // 데미지 입히기
			}
			
			ProcessHit(); // 시각 효과와 생명 주기 세팅 (여기서 수명을 Hit 후 지연 시간으로 재설정함)
			// Runner.Despawn(Object); // ❌ 즉시 없애면 이펙트가 안 보이므로 삭제!
		}
	}
}
