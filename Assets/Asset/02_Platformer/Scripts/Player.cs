using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using Starter.Platformer;
using UnityEngine.UIElements;

namespace Starter.Platformer
{
	/// <summary>
	/// Main player scrip - controls player movement and animations.
	/// </summary>
	public sealed class Player : NetworkBehaviour, IDamageable
	{
		[Header("References")]
		public SimpleKCC KCC;
		public PlayerInput PlayerInput;
		public Animator Animator;
		public Transform CameraPivot;
		public Transform CameraHandle;
		public Transform ScalingRoot;
		public UINameplate Nameplate;
		public GameObject RevivalUI;
		public float WalkSpeed = 2f;
		public float SprintSpeed = 5f;
		public float JumpImpulse = 10f;
		public float UpGravity = 25f;
		public float DownGravity = 40f;
		public float RotationSpeed = 8f;

		[Header("Movement Accelerations")]
		public float GroundAcceleration = 55f;
		public float GroundDeceleration = 25f;
		public float AirAcceleration = 25f;
		public float AirDeceleration = 1.3f;

		[Header("Sounds")]
		public AudioSource FootstepSound;
		public AudioClip JumpAudioClip;
		public AudioClip LandAudioClip;
		public AudioClip CoinCollectedAudioClip;

		[Header("VFX")]
		public ParticleSystem DustParticles;

		[Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
		public string Nickname { get; set; }
		[Networked, HideInInspector, OnChangedRender(nameof(OnCollectedCoinsChanged))]
		public int CollectedCoins { get; set; }

		[Networked, OnChangedRender(nameof(OnJumpingChanged))]
		private NetworkBool _isJumping { get; set; }

		[Networked, OnChangedRender(nameof(OnDeadChanged))]
		public NetworkBool dead { get; set; }

		[Header("Stats")]
		[Networked] public float hp { get; set; } = 100f;
		[Networked] public float maxHp { get; set; } = 100f;
		[Networked] public float mp { get; set; } = 50f;
		[Networked] public float maxMp { get; set; } = 50f;
		[Networked] public float damage { get; set; } = 5f; // damage * N% + WaeponDamage
		[Networked] public float attackSpeed { get; set; } = 1f; //100% + N%
		[Networked] public float moveSpeed { get; set; } = 10f; // 100% + N%
		[Networked] public float allDamage { get; set; } = 0f; // 스킬, 무기 데미지, 속설 추가뎀 전부 포함
		[Networked] public float damageReceived { get; set; } = 1f;
		[Networked] public float criticalChance { get; set; } = 0.1f;
		[Networked] public float criticalDamage { get; set; } = .5f; // damage * criticalDamage%

		[Header("Special Effects")]
		// 특수 효과 배열: SpecialEffectType의 int 값을 인덱스로 사용하여 확장성을 챙김
		[Networked, Capacity(64)] public NetworkArray<float> specialEffectValues => default;

		public void AddSpecialEffect(SpecialEffectType type, float value)
		{
			if (type == SpecialEffectType.None) return;
			specialEffectValues.Set((int)type, specialEffectValues[(int)type] + value);
		}

		public float GetSpecialEffectValue(SpecialEffectType type)
		{
			return specialEffectValues[(int)type];
		}

		public bool HasSpecialEffect(SpecialEffectType type)
		{
			return specialEffectValues[(int)type] > 0f;
		}

		// Animation IDs
		private int _animIDSpeed;
		private int _animIDGrounded;
		private int _animIDDie;
		private int _animIDRespawn;

		private Vector3 _moveVelocity;

		private GameManager _gameManager;

		public void Respawn(Vector3 position, bool resetCoins)
		{
			KCC.SetPosition(position);
			KCC.SetLookRotation(0f, 0f);

			_moveVelocity = Vector3.zero;

			if (resetCoins)
			{
				CollectedCoins = 0;
			}
		}

		public override async void Spawned()
		{
			if (HasStateAuthority)
			{
				_gameManager = FindObjectOfType<GameManager>();

				// Set player nickname that is saved in UIGameMenu
				Nickname = PlayerPrefs.GetString("PlayerName");

				while (GameManager.Instance == null) await System.Threading.Tasks.Task.Delay(100);

				GameManager.Instance.RPC_RegisterPlayerName(Runner.LocalPlayer, Nickname);
			}

			// In case the nickname is already changed,
			// we need to trigger the change manually
			OnNicknameChanged();
		}

		public override void FixedUpdateNetwork()
		{
			if (_gameManager.IsGameFinished)
			{
				// Let players fall even when game is finished (KCC.Move is called)
				ProcessInput(default);
				return;
			}

			if (KCC.Position.y < -15f)
			{
				// Player fell, let's respawn
				Respawn(_gameManager.GetSpawnPosition(), false);
			}

			// 네트워크를 통해 동기화된 입력 사용
			if (GetInput<GameplayInput>(out var input))
			{
				ProcessInput(input);
				
			}
			else
			{
				ProcessInput(default);
			}

			if (KCC.IsGrounded)
			{
				// Stop jumping
				_isJumping = false;
			}

			// Input Authority를 가진 클라이언트만 입력 리셋
			if (HasInputAuthority)
			{
				if (PlayerInput != null)
				{
					PlayerInput.ResetInput();
				}
			}
		}


        public override void Render()
		{
			RevivalUI.SetActive(dead);
			if (dead) return;
			
			Animator.SetFloat(_animIDSpeed, KCC.RealSpeed);
			Animator.SetBool(_animIDGrounded, KCC.IsGrounded);	

			FootstepSound.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
			FootstepSound.pitch = KCC.RealSpeed > moveSpeed + 3 - 1 ? 1.5f : 1f;

			ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 8f);

			var emission = DustParticles.emission;
			emission.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
		}

		private void Awake()
		{
			AssignAnimationIDs();
		}

        

        private void LateUpdate()
		{
			// Only local player needs to update the camera
			if (HasStateAuthority == false)
				return;

			if (_gameManager.IsGameFinished)
				return;
		
			// UI표시
			UIManager.Instance.statsUI.hpImageView(hp / maxHp);
			UIManager.Instance.statsUI.mpImageView(mp / maxMp);
			
			// 카메라 흔들림 offset 가져오기
			Vector3 shakeOffset = GameManager.Instance != null && GameManager.Instance.cameraShack != null 
				? GameManager.Instance.cameraShack.GetShakeOffset() 
				: Vector3.zero;
			
			// Update camera pivot and transfer properties from camera handle to Main Camera.
			//CameraPivot.rotation = Quaternion.Euler(PlayerInput.CurrentInput.LookRotation);
			//Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
			Camera.main.transform.position = CameraHandle.position + new Vector3(0f, 0f, -10f) + shakeOffset;
			Camera.main.transform.rotation = CameraHandle.localRotation;
		}

		private void ProcessInput(GameplayInput input)
		{
			if(dead) return; // 죽었을떄 움직이지마
			
			float jumpImpulse = 0f;

			if (KCC.IsGrounded && input.Jump)
			{
				// Set world space jump vector
				jumpImpulse = JumpImpulse;
				_isJumping = true;
			}

			// It feels better when the player falls quicker
			KCC.SetGravity(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity);

			float speed = input.Sprint ? moveSpeed +3 : moveSpeed;

			// Calculate correct move direction from input (rotated based on camera look)
			var moveDirection = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
			var desiredMoveVelocity = moveDirection * speed;

			float acceleration;
			if (desiredMoveVelocity == Vector3.zero)
			{
				// No desired move velocity - we are stopping
				acceleration = KCC.IsGrounded ? GroundDeceleration : AirDeceleration;
			}
			else
			{
				acceleration = KCC.IsGrounded ? GroundAcceleration : AirAcceleration;
			}

			_moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

			KCC.Move(_moveVelocity, jumpImpulse);

			// 마우스 위치를 월드 좌표로 변환
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Plane groundPlane = new Plane(Vector3.up, transform.position);
			Vector3 mouseWorldPos = Vector3.zero;
			if (groundPlane.Raycast(ray, out float distance))
			{
				mouseWorldPos = ray.GetPoint(distance);
			}
			mouseWorldPos.y = transform.position.y; // 같은 높이로 맞춤
			// 캐릭터가 마우스 방향을 바라보도록 회전
			Vector3 lookDirection = (mouseWorldPos - transform.position).normalized;
			if (lookDirection != Vector3.zero)
			{
				Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

				KCC.SetLookRotation(Quaternion
					.Slerp(transform.rotation, targetRotation, RotationSpeed * Runner.DeltaTime).eulerAngles);
			}

			if (input.Attack)
			{
				// Debug.Log("Ddddddddddddddddd");
				// input.Attack = false; // 공격 입력 초기화
				GetComponent<WeaponController>().Attack(transform.forward, damage, criticalDamage);
			}
		}

		public void TakeHit(float _damage, RaycastHit hit)
		{
			if (Object.HasStateAuthority)
			{
				TakeDamage(_damage);
			}
			else
			{
				Rpc_TakeDamage(_damage);
			}
		}

		[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
		public void Rpc_TakeDamage(float _damage)
		{
			TakeDamage(_damage);
		}

	public void TakeDamage(float _damage)
	{
		if (dead) return;

		float actualDamage = damageReceived * 0.01f * _damage;
		hp -= actualDamage;

		Debug.Log($"[Player 피격] 유저({Nickname})가 {actualDamage}의 데미지를 입었습니다. (남은 HP : {hp}/{maxHp})");

		// 로컬 플레이어만 카메라 흔들기
		if (HasStateAuthority && GameManager.Instance != null && GameManager.Instance.cameraShack != null)
		{
			float shakeIntensity = Mathf.Clamp01(actualDamage / maxHp);
			GameManager.Instance.cameraShack.Shake(0.3f, shakeIntensity * 0.5f);
		}

		if (hp <= 0)
		{
			dead = true;
			OnDeadChanged();
			Debug.Log($"[Player 사망] 유저({Nickname})가 사망했습니다!");
			// 본래라면 여기서 부활 로직, 혹은 쓰러짐 애니메이션 등을 호출합니다.
		}
	}

	[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
	public void RPC_Revive(string reviverName, float revivalHpPercent = 0.3f)
	{
		Revive(reviverName, revivalHpPercent);
	}

	public void Revive(string reviverName, float revivalHpPercent = 0.3f)
	{
		if (!dead) return;

		dead = false;
		hp = maxHp * revivalHpPercent;
		OnDeadChanged();

		Debug.Log($"[Player 부활] 유저({Nickname})가 {reviverName}에 의해 부활했습니다! (HP : {hp}/{maxHp})");
	}

		private void AssignAnimationIDs()
		{
			_animIDSpeed = Animator.StringToHash("Speed");
			_animIDGrounded = Animator.StringToHash("Grounded");
			_animIDRespawn = Animator.StringToHash("ReSpawn");
			_animIDDie = Animator.StringToHash("Die");
		}

		private void OnTriggerEnter(Collider other)
		{
			// Coins are collected only for local player
			if (HasStateAuthority == false)
				return;

			var coin = other.GetComponent<Coin>();
			if (coin != null)
			{
				coin.CoinCollected = OnCoinCollected;
				coin.RequestCollect();
			}
		}

		private void OnJumpingChanged()
		{
			if (_isJumping)
			{
				AudioSource.PlayClipAtPoint(JumpAudioClip, KCC.Position, 1f);
				ScalingRoot.localScale = new Vector3(0.5f, 1.5f, 0.5f);
			}
			else
			{
				AudioSource.PlayClipAtPoint(LandAudioClip, KCC.Position, 1f);
				ScalingRoot.localScale = new Vector3(1.25f, 0.75f, 1.25f);
			}
		}

		private void OnDeadChanged()
		{
			if (dead)
			{
				Animator.SetTrigger(_animIDDie);
			}
			else
			{
				Animator.SetTrigger(_animIDRespawn);
			}
		}

		private void OnCoinCollected()
		{
			CollectedCoins++;
		}

		private void OnCollectedCoinsChanged()
		{
			if (CollectedCoins <= 0)
				return; // Just coins reset

			AudioSource.PlayClipAtPoint(CoinCollectedAudioClip, KCC.Position, 1f);
		}

		private void OnNicknameChanged()
		{
			if (HasStateAuthority)
				return; // Do not show nickname for local player

			Nameplate.SetNickname(Nickname);
		}
	}
}
