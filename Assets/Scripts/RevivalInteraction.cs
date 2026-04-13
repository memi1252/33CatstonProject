using UnityEngine;
using Fusion;
using Starter.Platformer;


	/// <summary>
	/// 플레이어 부활 상호작용 시스템
	/// 살아있는 플레이어만 죽은 플레이어를 부활시킬 수 있습니다
	/// </summary>
	public class RevivalInteraction : NetworkBehaviour
	{
		[Header("Settings")]
		public float detectionRange = 5f;
		public float revivalDuration = 3f; // 3초 동안 키를 눌러야 부활
		public KeyCode revivalKey = KeyCode.E;
		public float revivalHpPercent = 0.3f; // 부활 시 최대 HP의 30%로 회복

		[Header("UI References")]
		public RevivalUI revivalUI;

		private Player _player;
		private Player _targetDeadPlayer = null;
		private float _revivalProgress = 0f;
		private bool _isReviving = false;

		private void Start()
		{
			_player = GetComponent<Player>();

		}

		private void Update()
		{
			if (!HasInputAuthority)
				return;
			if (_player.dead)
			{
				if (Input.GetKey(revivalKey))
				{
					GameManager.Instance.cameraShack.Shake();
				}
				revivalUI = _player.RevivalUI.GetComponent<RevivalUI>();
				if (revivalUI == _player.RevivalUI.GetComponent<RevivalUI>())
				{
					revivalUI.PlayerDieShow();
				}
				foreach (Player player in FindObjectsOfType<Player>())
				{
					if (player == _player) continue;
					// UI 표시
					if (player != null && player.dead)
					{
						revivalUI = player.RevivalUI.GetComponent<RevivalUI>();
						if (revivalUI == player.RevivalUI.GetComponent<RevivalUI>())
						{
							revivalUI.OtherPlayerDie();
						}
					}
				}
			}
			else
			{
				foreach (Player player in FindObjectsOfType<Player>())
				{
					// UI 표시
					if (player != null && player.dead)
					{
						revivalUI = player.RevivalUI.GetComponent<RevivalUI>();
						if (revivalUI == player.RevivalUI.GetComponent<RevivalUI>())
						{
							if(player != _player)
								revivalUI.Show(player.Nickname);
						}
					}
				}
			}



			// 죽은 플레이어는 부활시킬 수 없음
			if (_player.dead)
			{
				return;
			}





			// 주변의 죽은 플레이어 감지
			DetectDeadPlayers();

			// 죽은 플레이어가 범위 내에 있으면
			if (_targetDeadPlayer != null && !_targetDeadPlayer.dead)
			{
				// 대상 플레이어가 부활했으면 초기화
				_targetDeadPlayer = null;
				_revivalProgress = 0f;
				_isReviving = false;
				if (revivalUI != null)
					revivalUI.Hide();
				return;
			}

			if (_targetDeadPlayer != null)
			{
				// 부활 키를 누르고 있는지 확인
				if (Input.GetKey(revivalKey))
				{
					_isReviving = true;
					_revivalProgress += Time.deltaTime / revivalDuration;

					if (revivalUI != null)
						revivalUI.SetProgress(_revivalProgress);

					// 부활 완료
					if (_revivalProgress >= 1f)
					{
						ExecuteRevival();
						_revivalProgress = 0f;
						_isReviving = false;
					}
				}
				else
				{
					// 키를 뗌
					if (_isReviving)
					{
						_revivalProgress = 0f;
						_isReviving = false;
						if (revivalUI != null)
							revivalUI.SetProgress(0f);
					}
				}
			}
			else
			{
				// 범위 내에 죽은 플레이어가 없으면 UI 숨김
				if (revivalUI != null)
					revivalUI.Hide();
			}
		}

		/// <summary>
		/// 주변의 죽은 플레이어를 감지합니다
		/// </summary>
		private void DetectDeadPlayers()
		{
			_targetDeadPlayer = null;
			float closestDistance = detectionRange;

			// 모든 플레이어 찾기
			Player[] allPlayers = FindObjectsOfType<Player>();

			foreach (Player player in allPlayers)
			{
				// 자신은 제외
				if (player == _player)
					continue;

				// 죽은 플레이어만 확인
				if (!player.dead)
					continue;

				float distance = Vector3.Distance(_player.transform.position, player.transform.position);

				// 범위 내에 있는 가장 가까운 플레이어 선택
				if (distance < closestDistance)
				{
					closestDistance = distance;
					_targetDeadPlayer = player;
				}
			}
		}

		/// <summary>
		/// 부활을 실행합니다
		/// </summary>
		private void ExecuteRevival()
		{
			if (_targetDeadPlayer == null)
				return;

			// State Authority를 가진 플레이어만 RPC 호출 가능
			if (!_player.Object.HasStateAuthority)
				return;

			// 네트워크 RPC로 부활 요청
			_targetDeadPlayer.RPC_Revive(_player.Nickname, revivalHpPercent);

			Debug.Log($"[부활] {_player.Nickname}이(가) {_targetDeadPlayer.Nickname}을(를) 부활시켰습니다!");
		}
	}


