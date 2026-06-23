using System.Collections;
using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using Starter.Platformer;
using UnityEngine.UIElements;
using DamageNumbersPro;
using UnityEngine.SceneManagement;

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
		public DamageNumber damagePopup;

		[Header("Camera Follow")]
		public float CameraFollowSpeed = 5f;
		private Vector3 _cameraBasePos;
		private bool _cameraInitialized;

		[Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
		public string Nickname { get; set; }
		[Networked, HideInInspector, OnChangedRender(nameof(OnCollectedCoinsChanged))]
		public int CollectedCoins { get; set; }

		[Networked, OnChangedRender(nameof(OnJumpingChanged))]
		private NetworkBool _isJumping { get; set; }

		[Networked]
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

		// === Teleport ===
		// 텔레포트 중 비주얼 숨김 상태(전 클라이언트 공유). OnChangedRender로 메시 토글.
		[Networked, OnChangedRender(nameof(OnTeleportVisuallyHiddenChanged))]
		public NetworkBool TeleportVisuallyHidden { get; set; }
		// VFX가 재생될 위치(출발 또는 도착). 위치 동기화 지연을 우회.
		[Networked] public Vector3 TeleportVfxPosition { get; set; }
		// VFX 트리거: 값이 바뀔 때마다 OnChangedRender로 prefab을 인스턴스화.
		[Networked, OnChangedRender(nameof(OnTeleportDepartTick))] public int TeleportDepartTick { get; set; }
		[Networked, OnChangedRender(nameof(OnTeleportArriveTick))] public int TeleportArriveTick { get; set; }

		[Header("Teleport VFX/SFX (선택)")]
		[Tooltip("출발 효과 GameObject. 트리거 시 해당 위치에 복제 생성되고 잠시 후 자동 파괴.\n프리팹 또는 비활성화된 자식 템플릿 모두 가능.")]
		public GameObject teleportDepartVfx;
		[Tooltip("도착 효과 GameObject. 트리거 시 해당 위치에 복제 생성되고 잠시 후 자동 파괴.")]
		public GameObject teleportArriveVfx;
		public AudioClip teleportDepartSfx;
		public AudioClip teleportArriveSfx;
		[Tooltip("VFX가 생성될 기준 Transform(보통 캐릭터 허리 위치의 빈 자식). 비워두면 Player 루트 위치 사용.")]
		public Transform teleportVfxAnchor;
		[Tooltip("기준 위치에서 더할 추가 오프셋(월드 기준)")]
		public Vector3 teleportVfxOffset = Vector3.zero;
		[Tooltip("스폰된 VFX 인스턴스가 자동 파괴되기까지의 시간(초)")]
		public float teleportVfxLifetime = 3f;

		private bool _cameraFollowFrozen;
		private Coroutine _teleportCoroutine;
		// KCC.SetPosition은 반드시 FixedUpdateNetwork에서 호출되어야 동기화가 안 풀린다.
		// 코루틴에서는 아래 플래그만 켜고, FixedUpdateNetwork이 실제 이동을 수행.
		private bool _hasPendingTeleport;
		private Vector3 _pendingTeleportPosition;
		private Quaternion _pendingTeleportRotation;

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

		// === 증강(버프) 적용 ===
		// Shared 모드에서는 각 클라이언트가 자기 Player의 State Authority만 가지므로,
		// 버프는 반드시 "자기 자신"에게만 적용해야 동기화된다. (마스터가 남의 스탯을 쓰면 무시됨)
		// BuffManager가 RpcTargets.All 로 호출 → 각 클라가 자기 플레이어에 적용.

		public void ApplyContractBuff(ContractScriptableObject buff)
		{
			if (HasStateAuthority == false) return;
			if (buff == null || buff.contractBuffs == null) return;

			// 무기 타입 조건 체크 (TargetType.None(4) 이면 모든 무기에 적용)
			if (buff.targetType != TargetType.None)
			{
				var weaponSO = GetComponent<WeaponController>()?.CurrentWeaponSO;
				if (weaponSO == null || (WeaponType)buff.targetType != weaponSO.weaponType) return;
			}

			// 무기 속성 조건 체크 (TargetAttribute.None(0) 이면 모든 속성에 적용)
			if (buff.targetAttribute != TargetAttribute.None)
			{
				var weaponSO = GetComponent<WeaponController>()?.CurrentWeaponSO;
				if (weaponSO == null || buff.targetAttribute != weaponSO.targetAttribute) return;
			}

			for (int i = 0; i < buff.contractBuffs.Length; i++)
			{
				var props = buff.contractBuffs[i].targetAbilities;
				if (buff.valueType == global::ValueType.Percent)
				{
					maxHp *= (1 + props.maxHp);
					maxMp *= (1 + props.maxMp);
				}
				else
				{
					maxHp += props.maxHp;
					maxMp += props.maxMp;
				}
				damage += props.damage;
				attackSpeed = (attackSpeed + props.attackSpeed);
				moveSpeed = WalkSpeed * (1 + props.moveSpeed);
				allDamage += props.allDamage;
				damageReceived += props.damageReceived;
				criticalDamage += props.criticalDamage;
				criticalChance += props.criticalChance;
			}

			if (buff.specialEffect != SpecialEffectType.None)
			{
				AddSpecialEffect(buff.specialEffect, buff.specialEffectValue);
			}

			ActiveBuffDisplayUI.Instance?.AddBuff(buff.contractIcon, buff.contractName);
			Debug.Log($"[ContractBuff] {buff.contractName}: dmgReceived={damageReceived} dmg={damage} allDmg={allDamage}");
		}

		public void ApplyImprintBuff(BuffScripableObject buff)
		{
			if (HasStateAuthority == false) return;
			if (buff == null || buff.buffProperties == null) return;

			for (int i = 0; i < buff.buffProperties.Length; i++)
			{
				var props = buff.buffProperties[i].targetAbilities;
				if (buff.Condition == VotingCondition.Fixed)
				{
					maxHp += props.maxHp;
					maxMp += props.maxMp;
				}
				else if (buff.Condition == VotingCondition.Percent)
				{
					maxHp *= (1 + props.maxHp);
					maxMp *= (1 + props.maxMp);
				}
				damage += props.damage;
				attackSpeed = (attackSpeed + props.attackSpeed);
				moveSpeed = WalkSpeed * (1 + props.moveSpeed);
				allDamage += props.allDamage;
				damageReceived += props.damageReceived;
				criticalDamage += props.criticalDamage;
				criticalChance += props.criticalChance;
			}

			ActiveBuffDisplayUI.Instance?.AddBuff(buff.buffIcon, buff.buffName);
			Debug.Log($"[Buff] {Nickname}에게 {buff.buffName} 적용 완료!");
		}

		public void ApplyImprintConditionBuff(BuffScripableObject buff)
		{
			if (HasStateAuthority == false) return;
			if (buff == null || buff.votingAbility == null) return;

			for (int i = 0; i < buff.votingAbility.Length; i++)
			{
				var props = buff.votingAbility[i].targetAbilities;
				maxHp += props.maxHp;
				maxMp += props.maxMp;
				damage += props.damage;
				attackSpeed = (props.attackSpeed / (attackSpeed / 100f));
				moveSpeed += props.moveSpeed;
				allDamage += props.allDamage;
				damageReceived += props.damageReceived;
				criticalDamage += props.criticalDamage;
				criticalChance += props.criticalChance;
			}

			ActiveBuffDisplayUI.Instance?.AddBuff(buff.buffIcon, buff.buffName);
			Debug.Log($"[Buff] {Nickname}에게 {buff.buffName} 적용 완료!");
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
			Debug.Log($"[Player.Spawned] damageReceived={damageReceived} damage={damage} allDamage={allDamage} HasStateAuthority={HasStateAuthority}");
			if (HasStateAuthority)
			{
				_gameManager = FindObjectOfType<GameManager>();

				// Set player nickname that is saved in UIGameMenu
				Nickname = PlayerPrefs.GetString("PlayerName");

				while (GameManager.Instance == null) await System.Threading.Tasks.Task.Delay(100);

				GameManager.Instance.RPC_RegisterPlayerName(Runner.LocalPlayer, Nickname);
			}

			// 로컬 플레이어(InputAuthority) 스폰 완료 시점에 StatsUI 활성화
			// GameManager.Spawned()보다 이 시점이 더 안정적 (Player 오브젝트가 실제로 준비됨)
			if (HasInputAuthority)
			{
				if (UIManager.Instance != null && UIManager.Instance.statsUI != null && UIManager.Instance.statsUI.HpUI != null)
				{
					UIManager.Instance.statsUI.HpUI.SetActive(true);
				}
				else
				{
					Debug.LogWarning("[Player] StatsUI를 찾을 수 없습니다. UIManager 설정을 확인하세요.");
				}
			}

			// In case the nickname is already changed,
			// we need to trigger the change manually
			OnNicknameChanged();
		}

		public override void FixedUpdateNetwork()
		{
			// 텔레포트 적용은 FixedUpdateNetwork 안에서 해야 KCC/네트워크 시뮬레이션이 안 풀린다.
			if (_hasPendingTeleport && HasStateAuthority)
			{
				KCC.SetPosition(_pendingTeleportPosition);
				KCC.SetLookRotation(_pendingTeleportRotation.eulerAngles.y, 0f);
				_moveVelocity = Vector3.zero;
				_hasPendingTeleport = false;
				return; // 같은 틱에 입력 처리하지 않음
			}

			if (ChatManager.Instance.inputChat.isFocused)
			{
				// 채팅입력중이면 움직임 X
				return;
			}

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
			

			FootstepSound.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
			FootstepSound.pitch = KCC.RealSpeed > moveSpeed + 3 - 1 ? 1.5f : 1f;

			ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 8f);

			var emission = DustParticles.emission;
			emission.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
		}

        

        private void LateUpdate()
		{
			// Only local player needs to update the camera
			if (HasStateAuthority == false)
				return;

			if (_gameManager == null || _gameManager.IsGameFinished)
				return;

			// UI표시
			if (UIManager.Instance != null && UIManager.Instance.statsUI != null)
			{
				UIManager.Instance.statsUI.hpImageView(hp / maxHp);
				UIManager.Instance.statsUI.mpImageView(mp / maxMp);
			}

			// 텔레포트 중에는 카메라를 그대로 정지
			if (_cameraFollowFrozen)
				return;

			// 카메라 흔들림 offset 가져오기
			Vector3 shakeOffset = GameManager.Instance != null && GameManager.Instance.cameraShack != null
				? GameManager.Instance.cameraShack.GetShakeOffset()
				: Vector3.zero;

			// Update camera pivot and transfer properties from camera handle to Main Camera.
			//CameraPivot.rotation = Quaternion.Euler(PlayerInput.CurrentInput.LookRotation);
			//Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
			Vector3 desiredCameraPos = CameraHandle.position + new Vector3(0f, 0f, -10f);
			if (!_cameraInitialized)
			{
				_cameraBasePos = desiredCameraPos;
				_cameraInitialized = true;
			}
			_cameraBasePos = Vector3.Lerp(_cameraBasePos, desiredCameraPos, CameraFollowSpeed * Time.deltaTime);
			Camera.main.transform.position = _cameraBasePos + shakeOffset;
			Camera.main.transform.rotation = CameraHandle.localRotation;
		}

		private void ProcessInput(GameplayInput input)
		{
			if(dead) return; // 죽었을떄 움직이지마
			if(TeleportVisuallyHidden) return; // 텔레포트 중 입력 차단

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

		public void TakeHit(float _damage, RaycastHit hit, GameObject attackerGameObject)
		{
			// 로비씬에서는 플레이어끼리 피해 없음 (build index 1 = LobbyScene)
			if (SceneManager.GetActiveScene().buildIndex == 1) return;

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

		[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
		private void Rpc_ShowDamagePopup(float _damage)
		{
			if (damagePopup == null) return;
			damagePopup.Spawn(transform.position + Vector3.up, _damage);
		}

public void TakeDamage(float _damage)
		{
			if (dead) return;

			float actualDamage = damageReceived * _damage;
			Debug.Log($"[TakeDamage] _damage={_damage} damageReceived={damageReceived} actual={actualDamage}");
			hp -= actualDamage;

			SoundManager.Instance?.PlayPlayerHit();
			Rpc_ShowDamagePopup(actualDamage);

			Debug.Log($"[Player 피격] 유저({Nickname})가 {actualDamage}의 데미지를 입었습니다. (남은 HP : {hp}/{maxHp})");

			if (HasStateAuthority && GameManager.Instance != null && GameManager.Instance.cameraShack != null)
			{
				float shakeIntensity = Mathf.Clamp01(actualDamage / maxHp);
				GameManager.Instance.cameraShack.Shake(0.3f, shakeIntensity * 0.5f);
			}

			if (hp <= 0)
			{
				dead = true;
				SoundManager.Instance?.PlayPlayerDeath();
				if (ChatManager.Instance != null)
					ChatManager.Instance.SendSystemMessage(Nickname + "님이 사망했습니다.", Color.red);
				Debug.Log($"[Player 사망] 유저({Nickname})가 사망했습니다!");
			}
		}

		public void Revive(string reviverName, float revivalHpPercent = 0.3f)
		{
			if (!dead) return;

			dead = false;
			hp = maxHp * revivalHpPercent;
			SoundManager.Instance?.PlayPlayerRevive();

			Debug.Log($"[Player 부활] 유저({Nickname})가 {reviverName}에 의해 부활했습니다! (HP : {hp}/{maxHp})");
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
				SoundManager.Instance?.PlayPlayerJump();
				if (JumpAudioClip != null) AudioSource.PlayClipAtPoint(JumpAudioClip, KCC.Position, 1f);
				ScalingRoot.localScale = new Vector3(0.5f, 1.5f, 0.5f);
			}
			else
			{
				SoundManager.Instance?.PlayPlayerLand();
				if (LandAudioClip != null) AudioSource.PlayClipAtPoint(LandAudioClip, KCC.Position, 1f);
				ScalingRoot.localScale = new Vector3(1.25f, 0.75f, 1.25f);
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

		// ===== Teleport =====

		/// <summary>로컬 플레이어가 Portal을 통해 텔레포트를 시작.</summary>
		public void StartTeleport(Portal portal)
		{
			if (portal == null) return;
			if (_teleportCoroutine != null) return;
			if (!HasStateAuthority) return; // 본인만 시작 (Shared 모드에서 본인 객체에 StateAuthority)
			if (TeleportManager.Instance != null) TeleportManager.Instance.BeginTeleport();
			_teleportCoroutine = StartCoroutine(TeleportSequence(portal));
		}

		private IEnumerator TeleportSequence(Portal portal)
		{
			Vector3 destPos = portal.DestinationPosition;
			Quaternion destRot = portal.DestinationRotation;

			// 1) 출발 VFX는 Player의 teleportVfxAnchor(+offset) 위치에서 재생
			TeleportVfxPosition = GetTeleportVfxSpawnPosition();
			TeleportDepartTick = unchecked(TeleportDepartTick + 1);

			// 2) 캐릭터 비주얼 숨김 (네트워크 동기화)
			TeleportVisuallyHidden = true;

			// 3) 출발 VFX/사운드 보여줄 시간 대기 (카메라는 아직 원래 위치)
			yield return new WaitForSeconds(portal.DepartHoldDuration);

			// 4) 위치 이동 요청 (실제 SetPosition은 FixedUpdateNetwork에서 처리)
			//    카메라는 frozen 상태가 아니므로 CameraHandle이 따라간 새 위치로 부드럽게 lerp.
			_pendingTeleportPosition = destPos;
			_pendingTeleportRotation = destRot;
			_hasPendingTeleport = true;

			int guard = 30;
			while (_hasPendingTeleport && guard-- > 0)
				yield return null;

			// 5) 카메라가 새 위치로 이동하는 동안 캐릭터는 계속 숨겨둠
			if (portal.CameraTravelDuration > 0f)
				yield return new WaitForSeconds(portal.CameraTravelDuration);

			// 6) 캐릭터 비주얼 복원
			TeleportVisuallyHidden = false;

			// 7) 도착 VFX도 동일한 anchor 기준
			TeleportVfxPosition = GetTeleportVfxSpawnPosition();
			TeleportArriveTick = unchecked(TeleportArriveTick + 1);

			// 8) 도착 VFX/사운드 잠깐 보여줄 시간
			if (portal.ArriveHoldDuration > 0f)
				yield return new WaitForSeconds(portal.ArriveHoldDuration);

			// 9) 매니저에 종료 통지
			if (TeleportManager.Instance != null)
				TeleportManager.Instance.NotifyTeleportFinished();

			_teleportCoroutine = null;
		}

		// 모든 클라이언트에서 호출되는 콜백 (Networked 프로퍼티 변경 시)
		private void OnTeleportVisuallyHiddenChanged()
		{
			// ScalingRoot 자체를 SetActive(false) 하면 자식 VFX(ParticleSystem)도 같이 꺼지므로
			// 메시 렌더러만 토글해서 캐릭터만 보이지 않게 한다.
			if (ScalingRoot != null)
			{
				var renderers = ScalingRoot.GetComponentsInChildren<Renderer>(true);
				bool show = !TeleportVisuallyHidden;
				for (int i = 0; i < renderers.Length; i++)
				{
					// VFX 파티클 렌더러는 끄지 않음
					if (renderers[i] is ParticleSystemRenderer) continue;
					renderers[i].enabled = show;
				}
			}

			// 텔레포트 중 부활 UI 표시 방지
			if (RevivalUI != null && TeleportVisuallyHidden)
				RevivalUI.SetActive(false);
		}

		private void OnTeleportDepartTick()
		{
			PlayTeleportVfx(teleportDepartVfx, teleportDepartSfx, TeleportVfxPosition);
		}

		private void OnTeleportArriveTick()
		{
			PlayTeleportVfx(teleportArriveVfx, teleportArriveSfx, TeleportVfxPosition);
		}

		private Vector3 GetTeleportVfxSpawnPosition()
		{
			Vector3 basePos = teleportVfxAnchor != null ? teleportVfxAnchor.position : transform.position;
			return basePos + teleportVfxOffset;
		}

		private void PlayTeleportVfx(GameObject vfxPrefab, AudioClip sfx, Vector3 pos)
		{
			if (vfxPrefab != null)
			{
				// 매번 새 인스턴스 생성 → 부모 없이 월드에 떠 있다가 자동 파괴
				// (참조 대상이 비활성 자식 템플릿이어도 복제본은 활성화)
				var go = Instantiate(vfxPrefab, pos, Quaternion.identity);
				if (!go.activeSelf) go.SetActive(true);
				Destroy(go, Mathf.Max(0.1f, teleportVfxLifetime));
			}
			if (sfx != null)
			{
				AudioSource.PlayClipAtPoint(sfx, pos, 1f);
			}
		}
	}
}
