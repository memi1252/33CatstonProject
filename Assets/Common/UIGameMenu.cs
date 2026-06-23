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
			string[] roomOnly = { "RoomNameText", "PlayerCountText", "LeaveRoomButton", "MainMenuButton", "Divider" };
			foreach (var n in roomOnly)
				t.Find(n)?.gameObject.SetActive(inRoom);
		}

		private void OnDestroy()
		{
			if (_instance == this) _instance = null;
		}

		public async void StartGame()
		{
			await StartGameAsync(null);
		}

		public async void JoinRoom(string sessionName)
		{
			if (!string.IsNullOrEmpty(sessionName) && RoomText != null)
				RoomText.text = sessionName;
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
				if (RoomText != null) RoomText.text = sessionName;
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
			NicknameText.text = nickname;
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
