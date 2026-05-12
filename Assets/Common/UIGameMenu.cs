using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Starter
{
	/// <summary>
	/// Shows in-game menu, handles player connecting/disconnecting to the network game and cursor locking.
	/// </summary>
	public class UIGameMenu : MonoBehaviour
	{
		[Header("Start Game Setup")]
		[Tooltip("Specifies which game mode player should join - e.g. Platformer, ThirdPersonCharacter")]
		public string GameModeIdentifier;
		public NetworkRunner RunnerPrefab;
		public int MaxPlayerCount = 8;

		[Header("Debug")]
		[Tooltip("For debug purposes it is possible to force single-player game (starts faster)")]
		public bool ForceSinglePlayer;

		[Header("UI Setup")]
		public CanvasGroup PanelGroup;
		public TMP_InputField RoomText;
		public TMP_InputField NicknameText;
		public TextMeshProUGUI StatusText;
		public GameObject StartGroup;
		public GameObject DisconnectGroup;

		private NetworkRunner _runnerInstance;
		private static string _shutdownStatus;

		private void Awake()
		{
			DontDestroyOnLoad(gameObject.transform.parent.gameObject);
		}

		public async void StartGame()
		{
			await StartGameAsync(null);
		}

		/// <summary>외부(룸 리스트 등)에서 특정 세션 이름으로 바로 입장.</summary>
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

			// Add listener for shutdowns so we can handle unexpected shutdowns
			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.AddListener(OnShutdown);

			var sceneInfo = new NetworkSceneInfo();
			sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

			// 외부에서 특정 세션 이름이 지정되면 그걸로, 아니면 닉네임 기반 자동 생성
			string sessionName;
			if (!string.IsNullOrEmpty(overrideSessionName))
			{
				sessionName = overrideSessionName;
			}
			else
			{
				var nick = !string.IsNullOrWhiteSpace(NicknameText.text) ? NicknameText.text.Trim() : "Player";
				sessionName = $"{nick}'s Room";
				if (RoomText != null)
					RoomText.text = sessionName;
			}

			var startArguments = new StartGameArgs()
			{
				GameMode = Application.isEditor && ForceSinglePlayer ? GameMode.Single : GameMode.Shared,
				SessionName = sessionName,
				PlayerCount = MaxPlayerCount,
				// We need to specify a session property for matchmaking to decide where the player wants to join.
				// Otherwise players from Platformer scene could connect to ThirdPersonCharacter game etc.

				
				SessionProperties = new Dictionary<string, SessionProperty> {["GameMode"] = GameModeIdentifier},
				Scene = sceneInfo,
				IsOpen = true,
				IsVisible = true,
			};

			StatusText.text = startArguments.GameMode == GameMode.Single ? "Starting single-player..." : "Connecting...";

			var startTask = _runnerInstance.StartGame(startArguments);
			await startTask;
			
			if (startTask.Result.Ok)
			{
				StatusText.text = "";
				PanelGroup.gameObject.SetActive(false);
			}
			else
			{
				StatusText.text = $"Connection Failed: {startTask.Result.ShutdownReason}";
			}
		}

		public async void DisconnectClicked()
		{
			await Disconnect();
		}

		public async void BackToMenu()
		{
			await Disconnect();
			Destroy(gameObject.transform.parent.gameObject);
			SceneManager.LoadScene(0);
		}

		public void TogglePanelVisibility()
		{
			if (PanelGroup.gameObject.activeSelf && _runnerInstance == null)
				return; // Panel cannot be hidden if the game is not running

			if (PanelGroup.gameObject.activeSelf)
			{
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
			else
			{
				//Cursor.lockState = CursorLockMode.Locked;
				//Cursor.visible = false;
			}

			PanelGroup.gameObject.SetActive(!PanelGroup.gameObject.activeSelf);
		}

		private void OnEnable()
		{
			var nickname = PlayerPrefs.GetString("PlayerName");
			if (string.IsNullOrEmpty(nickname))
			{
				nickname = "Player" + Random.Range(10000, 100000);
			}

			NicknameText.text = nickname;

			// Try to load previous shutdown status
			StatusText.text = _shutdownStatus != null ? _shutdownStatus : string.Empty;
			_shutdownStatus = null;
		}

		private void Update()
		{
			// Enter/Esc key is used for locking/unlocking cursor in game view.
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				TogglePanelVisibility();
			}

			if (PanelGroup.gameObject.activeSelf)
			{
				StartGroup.SetActive(_runnerInstance == null);
				DisconnectGroup.SetActive(_runnerInstance != null);
				RoomText.interactable = _runnerInstance == null;
				NicknameText.interactable = _runnerInstance == null;

				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
			else
			{
				//Cursor.lockState = CursorLockMode.Locked;
				//Cursor.visible = false;
			}
		}

		public async Task Disconnect()
		{
			if (_runnerInstance == null)
				return;

			StatusText.text = "Disconnecting...";
			PanelGroup.interactable = false;

			// Remove shutdown listener since we are disconnecting deliberately
			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.RemoveListener(OnShutdown);

			await _runnerInstance.Shutdown();
			_runnerInstance = null;

			// Reset of scene network objects is needed, reload the whole scene
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		private void OnShutdown(NetworkRunner runner, ShutdownReason reason)
		{
			// Unexpected shutdown happened (e.g. Host disconnected)

			// Save status into static variable, it will be used in OnEnable after scene load
			_shutdownStatus = $"Shutdown: {reason}";
			Debug.LogWarning(_shutdownStatus);

			// Reset of scene network objects is needed, reload the whole scene
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}
	}
}
