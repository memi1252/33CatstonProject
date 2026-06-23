using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainLobbyUI : MonoBehaviour
{
    public Button gameStartButton;
    public Button gameExitButton;

    private void Awake()
    {
        gameStartButton.onClick.AddListener(OnGameStartButtonClicked);
        gameExitButton.onClick.AddListener(OnGameExitButtonClicked);
    }

private void OnGameStartButtonClicked()
    {
        //SoundManager.Instance?.PlaySFX(SoundManager.Instance?.sfxUIGameStart);
        UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }

private void OnGameExitButtonClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundManager.Instance?.sfxUIDisconnect);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
