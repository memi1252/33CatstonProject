using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Starter
{
	public class UIGameMenu : MonoBehaviour
	{
		[Header("Start Game Setup")]
		public string GameModeIdentifier;
		public NetworkRunner RunnerPrefab;
		public int MaxPlayerCount = 8;

		[Header("Debug")]
		public bool ForceSinglePlayer;

		[Header("방 선택/생성 패널 (방 밖에서 표시)")]
		public CanvasGroup PanelGroup;
		public TMP_InputField RoomText;
		public TMP_InputField NicknameText;
		public TextMeshProUGUI StatusText;
		public GameObject StartGroup;
		public GameObject DisconnectGroup;
		public GameObject Connecting;
		public TextMeshProUGUI ConnectingText;

		[Header("방 안 메뉴 패널")]
		[Tooltip("방 안에서 메뉴버튼을 눌렀을 때 표시되는 패널")]
		public GameObject RoomMenuPanel;
		[Tooltip("방 안에서만 보이는 메뉴 버튼")]
		public GameObject MenuButton;
		public TextMeshProUGUI RoomNameText;
		public TextMeshProUGUI PlayerCountText;

		[Header("Connecting UI Settings")]
		public float ConnectingDotInterval = 0.4f;
		public float ConnectFailedHideDelay = 2f;

		private NetworkRunner _runnerInstance;
		private static string _shutdownStatus;
		private Coroutine _connectingDotsCoroutine;
		private Coroutine _hideConnectingCoroutine;

		public static UIGameMenu _instance;

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject.transform.parent.gameObject);
				return;
			}
			_instance = this;
			DontDestroyOnLoad(gameObject.transform.parent.gameObject);
			WireRoomMenuButtons();
		}

		private void WireRoomMenuButtons()
		{
			if (RoomMenuPanel == null) return;
			var t = RoomMenuPanel.transform;

			var resume = t.Find("ResumeButton")?.GetComponent<UnityEngine.UI.Button>();
			if (resume != null) resume.onClick.AddListener(ToggleRoomMenu);

			var leave = t.Find("LeaveRoomButton")?.GetComponent<UnityEngine.UI.Button>();
			if (leave != null) leave.onClick.AddListener(DisconnectClicked);

			var mainMenu = t.Find("MainMenuButton")?.GetComponent<UnityEngine.UI.Button>();
			if (mainMenu != null) mainMenu.onClick.AddListener(BackToMenu);

			var settings = t.Find("SettingsButton")?.GetComponent<UnityEngine.UI.Button>();
			if (settings != null) settings.onClick.AddListener(OpenSettings);

			var quit = t.Find("QuitGameButton")?.GetComponent<UnityEngine.UI.Button>();
			if (quit != null) quit.onClick.AddListener(QuitGame);
		}

		public async void QuitGame()
		{
			await Disconnect(loadLobby: false);
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		public void OpenSettings()
		{
			var ui = UnityEngine.Object.FindObjectOfType<SettingsUI>(true);
			ui?.ToggleSettings();
		}

		private void UpdateRoomMenuContent(bool inRoom)
		{
			if (RoomMenuPanel == null) return;
			var t = RoomMenuPanel.transform;
			// 메인 메뉴로 가는 버튼은 방에 있든 로비에 있든 항상 보여야 한다.
			string[] roomOnly = { "RoomNameText", "PlayerCountText", "LeaveRoomButton", "Divider" };
			foreach (var n in roomOnly)
				t.Find(n)?.gameObject.SetActive(inRoom);

			// 방에 입장 안 했을 땐 게임 종료, 입장했으면 방 나가기만 보이게 (같은 자리, 겹치지 않게)
			t.Find("QuitGameButton")?.gameObject.SetActive(!inRoom);
		}

		private void OnDestroy()
		{
			if (_instance == this) _instance = null;
		}

		public async void StartGame()
		{
			await StartGameAsync(null);
		}

		// 개발자 모드 치트: 로비 방의 LobbyReadyManager.SoloTestMode를 켜서 혼자(1명)여도
		// 준비/시작이 가능하게 한다. 방에 들어가기 전엔 아직 LobbyReadyManager가 없으니 적용 대상 없음 →
		// 방에 들어간 뒤(대기 화면)에 눌러야 한다.
		public void DevMode_EnableSoloTest()
		{
			if (LobbyReadyManager.Instance == null) return;
			// 방장이 아니어도 적용되도록 RPC로 요청한다 (직접 대입하면 방장이 아닌 인스턴스에서는 무시됨).
			LobbyReadyManager.Instance.RequestSoloTestMode();
			Debug.Log("[Cheat] F4: SoloTestMode 활성화 요청 — 혼자서도 준비/시작 가능");
		}

		// 개발자 모드일 때 화면에 사용 가능한 치트키 목록을 항상 보여준다.
		private void OnGUI()
		{
			// Invincible 자체는 F5로 껐다 켰다 할 수 있으므로 메뉴 표시 여부는 항상 유지되는
			// PendingInvincibleCheat(GBSWM으로 켠 개발자 모드 활성화 여부)로만 판단한다.
			if (!GameManager.PendingInvincibleCheat) return;

			bool devModeInGame = GameManager.Instance != null && GameManager.Instance.LocalPlayer != null;

			bool invincibleOn = devModeInGame && GameManager.Instance.LocalPlayer.Invincible;
			string text = devModeInGame
				? $"[개발자 모드]\nF1 : 전원 체력 완전 회복\nF2 : 스테이지 강제 클리어 (낀 적 처치)\nF3 : 멈춘 증강/무기 선택 UI 강제 종료\nF4 : 혼자서도 진행 가능 (SoloTestMode)\nF5 : 전원 무적모드 {(invincibleOn ? "ON" : "OFF")} 토글\nF6 : 상태이상 꼬임(둔화 등) 강제 해제\nF7 : 보스 HP UI 강제 갱신\nF8 : 전원 최대 체력 +20\nF9 : 전원 공격력 +5"
				: "[개발자 모드 대기 중]";

			GUIStyle style = new GUIStyle(GUI.skin.box)
			{
				alignment = TextAnchor.UpperLeft,
				fontSize = 16,
				normal = { textColor = Color.yellow }
			};
			// 항목이 늘어난 만큼(F8/F9 추가) 박스 높이도 같이 늘린다.
			GUI.Box(new Rect(10, 10, 320, 230), text, style);
		}

		public async void JoinRoom(string sessionName)
		{
			// .text = 로 바꾸면 onValueChanged가 발생해서 타이핑용 통통 효과(UIInputBump)와
			// 방 검색 필터(RoomListManager)가 의도치 않게 같이 트리거된다. 코드에서 값만 바꿀 땐 알림 없이 설정.
			if (!string.IsNullOrEmpty(sessionName) && RoomText != null)
				RoomText.SetTextWithoutNotify(sessionName);
			await StartGameAsync(sessionName);
		}

		private async Task StartGameAsync(string overrideSessionName)
		{
			await Disconnect();

			PlayerPrefs.SetString("PlayerName", NicknameText.text);

			_runnerInstance = Instantiate(RunnerPrefab);

			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.AddListener(OnShutdown);

			var sceneInfo = new NetworkSceneInfo();
			sceneInfo.AddSceneRef(SceneRef.FromIndex(1));

			string sessionName;
			if (!string.IsNullOrEmpty(overrideSessionName))
			{
				sessionName = overrideSessionName;
			}
			else
			{
				var nick = !string.IsNullOrWhiteSpace(NicknameText.text) ? NicknameText.text.Trim() : "Player";
				sessionName = $"{nick}'s Room";
				if (RoomText != null) RoomText.SetTextWithoutNotify(sessionName);
			}

			var startArguments = new StartGameArgs()
			{
				GameMode = Application.isEditor && ForceSinglePlayer ? GameMode.Single : GameMode.Shared,
				SessionName = sessionName,
				PlayerCount = MaxPlayerCount,
				SessionProperties = new Dictionary<string, SessionProperty> { ["GameMode"] = GameModeIdentifier },
				Scene = sceneInfo,
				IsOpen = true,
				IsVisible = true,
			};

			StatusText.text = startArguments.GameMode == GameMode.Single ? "Starting single-player..." : "Connecting...";
			ShowConnecting();

			var startTask = _runnerInstance.StartGame(startArguments);
			await startTask;

			if (startTask.Result.Ok)
			{
				StatusText.text = "";
				HideConnecting();
				// 방 선택 패널 숨김, 방 안 메뉴버튼 표시
				PanelGroup.gameObject.SetActive(false);
				if (MenuButton != null) MenuButton.SetActive(true);
				if (RoomMenuPanel != null) RoomMenuPanel.SetActive(false);
			}
			else
			{
				StatusText.text = $"Connection Failed: {startTask.Result.ShutdownReason}";
				ShowConnectFailed(ConnectFailedHideDelay);
			}
		}

		// ─── 방 안 메뉴 토글 ───────────────────────────────────────
		public void ToggleRoomMenu()
		{
			if (RoomMenuPanel == null) return;
			bool next = !RoomMenuPanel.activeSelf;
			RoomMenuPanel.SetActive(next);
			if (next) UpdateRoomMenuContent(_runnerInstance != null);
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		private void Update()
		{
			bool inRoom = _runnerInstance != null;

			// 메뉴버튼은 항상 표시
			if (MenuButton != null) MenuButton.SetActive(true);

			// ESC → 항상 RoomMenuPanel 토글
			if (Input.GetKeyDown(KeyCode.Escape))
				ToggleRoomMenu();

			// F4: 게임 안에서만 허용 (로비 대기실은 LobbyReadyManager가 살아있으므로 거기선 차단)
			// 인게임 F4 처리는 GameManager.Update()에서 담당한다.

			// 방 안 메뉴 패널이 열려있을 때 방 정보 갱신
			if (inRoom && RoomMenuPanel != null && RoomMenuPanel.activeSelf)
			{
				if (RoomNameText != null)
					RoomNameText.text = $"방: {_runnerInstance.SessionInfo.Name}";
				if (PlayerCountText != null)
				{
					int count = 0;
					foreach (var _ in _runnerInstance.ActivePlayers) count++;
					PlayerCountText.text = $"플레이어: {count} / {_runnerInstance.SessionInfo.MaxPlayers}";
				}
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
		}

		// ─── Disconnect / BackToMenu ───────────────────────────────
		public async void DisconnectClicked()
		{
			if (RoomMenuPanel != null) RoomMenuPanel.SetActive(false);
			UIManager.Instance.buffUI.SetActive(false);
			UIManager.Instance.weaponUI.SetActive(false);
			await Disconnect(loadLobby: true);
		}

		public async void BackToMenu()
		{
			if (RoomMenuPanel != null) RoomMenuPanel.SetActive(false);
			UIManager.Instance.buffUI.SetActive(false);
			UIManager.Instance.weaponUI.SetActive(false);
			await Disconnect(loadLobby: false);
			Destroy(gameObject.transform.parent.gameObject);
			SceneManager.LoadScene(0);
		}

		public async Task Disconnect(bool loadLobby = true)
		{
			if (_runnerInstance == null) return;

			StatusText.text = "Disconnecting...";
			PanelGroup.interactable = false;

			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.RemoveListener(OnShutdown);

			await _runnerInstance.Shutdown();
			_runnerInstance = null;

			if (MenuButton != null) MenuButton.SetActive(false);
			if (RoomMenuPanel != null) RoomMenuPanel.SetActive(false);

			PanelGroup.gameObject.SetActive(true);
			PanelGroup.interactable = true;
			StatusText.text = "";

			if (loadLobby) SceneManager.LoadScene(1);
			Destroy(UIManager.Instance.gameObject);
			Destroy(transform.root.gameObject);
		}

		private void OnShutdown(NetworkRunner runner, ShutdownReason reason)
		{
			_runnerInstance = null;
			_shutdownStatus = $"Shutdown: {reason}";
			Debug.LogWarning(_shutdownStatus);

			if (MenuButton != null) MenuButton.SetActive(false);
			if (RoomMenuPanel != null) RoomMenuPanel.SetActive(false);

			PanelGroup.gameObject.SetActive(true);
			PanelGroup.interactable = true;

			SceneManager.LoadScene(1);

			// 정상적으로 나갈 때(Disconnect())와 동일하게 영구 UI를 통째로 정리한다.
			// 안 그러면 게임 시작 도중 예기치 않게 끊겼을 때(방장이 방 닫음 등) 낡은 UI가 그대로 남아
			// 로비로 돌아와도 새로 생성되지 않고 깨진 상태로 유지된다.
			if (UIManager.Instance != null) Destroy(UIManager.Instance.gameObject);
			Destroy(transform.root.gameObject);
		}

		// ─── 방 선택 패널용 기존 토글 (MenuButton 없을 때 호환) ────
		public void TogglePanelVisibility()
		{
			if (_runnerInstance != null)
			{
				ToggleRoomMenu();
				return;
			}
			PanelGroup.gameObject.SetActive(!PanelGroup.gameObject.activeSelf);
		}

		private void OnEnable()
		{
			var nickname = PlayerPrefs.GetString("PlayerName");
			if (string.IsNullOrEmpty(nickname))
				nickname = "Player" + Random.Range(10000, 100000);
			// .text = 로 바꾸면 onValueChanged가 발생해서 UIInputBump의 통통 효과가 트리거된다.
			// 방을 나갔다가 다시 들어올 때 OnEnable이 다시 호출되며 이게 반복/중첩 트리거되어
			// 스프링 값이 Infinity/NaN까지 깨지는 버그가 있었다(닉네임 칸이 이상하게 표시/터지는 원인).
			NicknameText.SetTextWithoutNotify(nickname);
			StatusText.text = _shutdownStatus != null ? _shutdownStatus : string.Empty;
			_shutdownStatus = null;
		}

		// ─── Connecting 애니메이션 ────────────────────────────────
		private void ShowConnecting()
		{
			if (_hideConnectingCoroutine != null) { StopCoroutine(_hideConnectingCoroutine); _hideConnectingCoroutine = null; }
			if (Connecting != null) Connecting.SetActive(true);
			StopConnectingDotsAnimation();
			_connectingDotsCoroutine = StartCoroutine(AnimateConnectingDots());
		}

		private void HideConnecting()
		{
			StopConnectingDotsAnimation();
			if (_hideConnectingCoroutine != null) { StopCoroutine(_hideConnectingCoroutine); _hideConnectingCoroutine = null; }
			if (Connecting != null) Connecting.SetActive(false);
		}

		private void ShowConnectFailed(float hideDelay)
		{
			StopConnectingDotsAnimation();
			if (Connecting != null) Connecting.SetActive(true);
			if (ConnectingText != null) ConnectingText.text = "접속 실패";
			if (_hideConnectingCoroutine != null) StopCoroutine(_hideConnectingCoroutine);
			_hideConnectingCoroutine = StartCoroutine(HideConnectingAfterDelay(hideDelay));
		}

		private void StopConnectingDotsAnimation()
		{
			if (_connectingDotsCoroutine != null) { StopCoroutine(_connectingDotsCoroutine); _connectingDotsCoroutine = null; }
		}

		private IEnumerator AnimateConnectingDots()
		{
			int dotCount = 0;
			var wait = new WaitForSeconds(ConnectingDotInterval > 0f ? ConnectingDotInterval : 0.4f);
			while (true)
			{
				if (ConnectingText != null) ConnectingText.text = "접속중" + new string('.', dotCount);
				dotCount = (dotCount + 1) % 4;
				yield return wait;
			}
		}

		private IEnumerator HideConnectingAfterDelay(float seconds)
		{
			yield return new WaitForSeconds(seconds);
			if (Connecting != null) Connecting.SetActive(false);
			_hideConnectingCoroutine = null;
		}
	}
}
