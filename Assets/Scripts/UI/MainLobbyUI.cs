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
        SceneManager.LoadScene("LobbyScene");
    }

    private void OnGameExitButtonClicked()
    {
        #if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
        #else
        Application.Quit();
        #endif
    }
}
