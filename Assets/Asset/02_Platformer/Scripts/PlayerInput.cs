using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Starter.Platformer
{
	/// <summary>
	/// Structure holding player input.
	/// </summary>
	public struct GameplayInput : INetworkInput
	{
		//public Vector2 LookRotation;
		public Vector2 MoveDirection;
		public bool Jump;
		public bool Sprint;
		public bool Attack;
	}

	/// <summary>
	/// PlayerInput handles accumulating player input from Unity.
	/// </summary>
	[RequireComponent(typeof(UnityEngine.InputSystem.PlayerInput))]
	public sealed class PlayerInput : MonoBehaviour
	{
		public float InitialLookRotation = 18f;

		public GameplayInput CurrentInput => _input;
		private GameplayInput _input;
		
		private UnityEngine.InputSystem.PlayerInput _playerInputComponent;
		private NetworkObject _networkObject;

		/// <summary>
		/// Called by NetworkRunner to get input data for networking
		/// </summary>
		public void OnInput(NetworkRunner runner, NetworkInput input)
		{
			input.Set(_input);
		}

		/// <summary>
		/// Called when input is missing for a player
		/// </summary>
		public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
		{
			input.Set(default(GameplayInput));
		}

		public void ResetInput()
		{
			// Reset input after it was used to detect changes correctly again
			//_input.MoveDirection = default;
			_input.Jump = false;
			_input.Sprint = false;
			_input.Attack = false;
		}

		private void Awake()
		{
			// NetworkObject 찾기
			_networkObject = GetComponent<NetworkObject>();
			
			// Unity PlayerInput 컴포넌트 찾기 및 설정
			_playerInputComponent = GetComponent<UnityEngine.InputSystem.PlayerInput>();
			if (_playerInputComponent == null)
			{
				Debug.LogError("[PlayerInput] Unity PlayerInput 컴포넌트를 찾을 수 없습니다!");
				return;
			}

			// PlayerInput 컴포넌트가 Send Messages 방식으로 설정되어 있는지 확인
			if (_playerInputComponent.notificationBehavior != UnityEngine.InputSystem.PlayerNotifications.SendMessages)
			{
				Debug.LogWarning("[PlayerInput] PlayerInput 컴포넌트의 Behavior를 Send Messages로 설정합니다.");
				_playerInputComponent.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.SendMessages;
			}

			// 처음에는 모든 PlayerInput을 비활성화
			_playerInputComponent.enabled = false;
			Debug.Log("[PlayerInput] Unity PlayerInput 컴포넌트 초기 비활성화");
		}

		private void Start()
		{
			// Set initial camera rotation
			//_input.LookRotation = new Vector2(InitialLookRotation, 0f);
			
			// NetworkObject가 스폰되면 InputAuthority 확인
			if (_networkObject != null && _networkObject.HasInputAuthority)
			{
				EnableInput();
			}
		}

		public void EnableInput()
		{
			if (_playerInputComponent != null)
			{
				// 비활성화 동안 남아있던 stale 입력을 초기화한다.
				// Value형 Move 액션은 재활성화 시 초기 상태 체크로 현재 눌린 방향을 다시 보내므로
				// 이동이 정상 복구된다. (초기화하지 않으면 채팅 후 가끔 캐릭터가 안 움직임)
				_input = default;
				_playerInputComponent.enabled = true;
				Debug.Log($"[PlayerInput] Unity PlayerInput 활성화 - HasInputAuthority: {_networkObject?.HasInputAuthority}");
			}
		}

		public void DisableInput()
		{
			if (_playerInputComponent != null)
			{
				_playerInputComponent.enabled = false;
				// 입력 잠금 동안 마지막 이동값이 남아 캐릭터가 계속 미끄러지지 않도록 초기화한다.
				_input = default;
				Debug.Log("[PlayerInput] Unity PlayerInput 비활성화");
			}
		}

		private void Update()
		{
			// New Input System을 사용하므로 Update에서 직접 입력 처리하지 않음
			// 모든 입력은 OnMove, OnLook, OnJump, OnSprint 콜백으로 처리됨
		}

		// New Input System 콜백들
		void OnMove(InputValue value)
		{
			// InputAuthority를 가진 플레이어만 입력 처리
			if (_networkObject == null || !_networkObject.HasInputAuthority)
				return;

			var moveDirection = value.Get<Vector2>();
			_input.MoveDirection = moveDirection.normalized;
		}

		// void OnLook(InputValue value)
		// {
		// 	// InputAuthority를 가진 플레이어만 입력 처리
		// 	if (_networkObject == null || !_networkObject.HasInputAuthority)
		// 		return;
		// 	
		// 	// Look 입력 누적 처리 (마우스 델타값)
		// 	var lookDelta = value.Get<Vector2>();
		// 	lookDelta.x *= -1; // Y축 반전 (마우스 Y는 보통 반전해야 함)
		// 	_input.LookRotation = ClampLookRotation(_input.LookRotation + lookDelta);
		// }

		void OnJump(InputValue value)
		{
			// InputAuthority를 가진 플레이어만 입력 처리
			if (_networkObject == null || !_networkObject.HasInputAuthority)
				return;

			if (Cursor.lockState != CursorLockMode.Locked)
				return;
			_input.Jump |= value.isPressed;
		}

		void OnSprint(InputValue value)
		{
			// InputAuthority를 가진 플레이어만 입력 처리
			if (_networkObject == null || !_networkObject.HasInputAuthority)
				return;
			
			_input.Sprint |= value.isPressed;
		}

		void OnAttack(InputValue value)
		{
            if(_networkObject == null || !_networkObject.HasInputAuthority)

                return;

			// 마우스가 버튼 등 "클릭 가능한" UI 위에 있을 때만 공격을 막는다.
			// IsPointerOverGameObject()는 채팅창/증강 표시/팀원 정보 같은 비인터랙티브 표시용
			// 패널 위에 있어도 true가 되어 평소 화면 어디서든 공격이 막히는 문제가 있었다.
			if (value.isPressed && IsPointerOverInteractableUI())
			{
				return;
			}

			_input.Attack = value.isPressed;
		}

		private static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _uiRaycastResults
			= new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();

		private bool IsPointerOverInteractableUI()
		{
			var eventSystem = UnityEngine.EventSystems.EventSystem.current;
			if (eventSystem == null) return false;

			var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
			{
				position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero
			};

			_uiRaycastResults.Clear();
			eventSystem.RaycastAll(pointerData, _uiRaycastResults);

			foreach (var result in _uiRaycastResults)
			{
				// 버튼, 토글, 슬라이더, 입력창 등 실제로 클릭/조작 가능한 UI 위에 있을 때만 차단한다.
				if (result.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null)
					return true;
			}
			return false;
		}

		private Vector2 ClampLookRotation(Vector2 lookRotation)
		{
			lookRotation.x = Mathf.Clamp(lookRotation.x, -30f, 70f);
			return lookRotation;
		}
	}
}
