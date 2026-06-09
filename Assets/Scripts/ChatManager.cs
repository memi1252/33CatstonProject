using System;
using UnityEngine;
using Fusion;
using Starter.Platformer;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Instance { get; private set; }
    
    public InputField inputChat; 
    public TextMeshProUGUI messageText;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ResolveUIReferences();
    }

    public override void Spawned()
    {
        ResolveUIReferences();
    }

    // 게임 씬에 배치된 ChatManager는 영구(DontDestroyOnLoad) 채팅 UI를 에디터에서 연결할 수 없으므로
    // 런타임에 UIManager에서 가져온다. (UIManager에 값이 있을 때만 덮어씀 → 로비 인스턴스는 자체 연결 유지)
    private void ResolveUIReferences()
    {
        if (UIManager.Instance == null) return;
        if (UIManager.Instance.chatInput != null) inputChat = UIManager.Instance.chatInput;
        if (UIManager.Instance.chatMessageText != null) messageText = UIManager.Instance.chatMessageText;
    }

    private void Update()
    {
        if (inputChat == null) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (inputChat.isFocused)
            {
                // 입력 중 Enter → 내용이 있으면 전송하고 채팅 닫기
                if (!string.IsNullOrEmpty(inputChat.text))
                {
                    SendChatMessage();
                }
                CloseChat();
            }
            else
            {
                // 닫힌 상태 Enter → 채팅 열고 포커스 (입력 중에는 플레이어 이동 입력을 막는다)
                OpenChat();
            }
        }
    }

    private void OpenChat()
    {
        if (inputChat == null) return;
        if (!inputChat.gameObject.activeSelf) inputChat.gameObject.SetActive(true);
        inputChat.ActivateInputField();
        inputChat.Select();
        SetPlayerInputEnabled(false);
    }

    private void CloseChat()
    {
        if (inputChat != null)
        {
            inputChat.text = "";
            inputChat.DeactivateInputField();
        }
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        SetPlayerInputEnabled(true);
    }

    // 채팅 입력 중에는 로컬 플레이어의 입력(이동 등)을 잠가 타이핑이 캐릭터를 움직이지 않게 한다.
    private void SetPlayerInputEnabled(bool inputEnabled)
    {
        if (Runner == null) return;
        NetworkObject playerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (playerObj != null && playerObj.TryGetComponent(out PlayerInput playerInput))
        {
            var unityInput = playerInput.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (unityInput != null) unityInput.enabled = inputEnabled;
        }
    }

    public void SendChatMessage()
    {
        if (!string.IsNullOrEmpty(inputChat.text))
        {
            RPC_SendChatMessage(inputChat.text);
            inputChat.text = "";
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendChatMessage(string msg, RpcInfo info = default)
    {
        string senderName = "추정불가";
        if (Runner.TryGetPlayerObject(info.Source, out NetworkObject player))
        {
            Player p = player.GetComponent<Player>();
            if (p != null && !string.IsNullOrEmpty(p.Nickname))
                senderName = p.Nickname;
        }

        if (messageText != null)
            messageText.text += $"{senderName} : {msg} \n";
    }

    public void SendSystemMessage(string msg, Color color)
    {
        if (!string.IsNullOrEmpty(msg))
        {
            RPC_SendSystemMessage(msg, color);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendSystemMessage(string msg, Color color)
    {
        string senderName = "System";

        if (messageText != null)
            messageText.text += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{senderName} : {msg}</color> \n";
    }
}
