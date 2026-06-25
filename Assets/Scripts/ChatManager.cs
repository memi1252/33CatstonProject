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

    // InputField의 isFocused는 Enter 제출 시 같은 프레임에 풀리므로, 채팅 열림 상태를 직접 추적한다.
    private bool _chatOpen;

    // CloseChat()이 호출된 프레임을 기록해, 같은 프레임에 Update()의 Enter-열기 체크가
    // 다시 채팅을 여는 재오픈 레이스를 막는다 (OnSubmit과 Update가 같은 Enter를 같은 프레임에 처리할 때 발생).
    private int _lastCloseFrame = -1;


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
        if (UIManager.Instance.chatInput != null)
        {
            if (inputChat != null) inputChat.onEndEdit.RemoveListener(OnChatEndEdit);
            inputChat = UIManager.Instance.chatInput;
            // onEndEdit: Enter 제출이든 다른 곳 클릭으로 포커스를 잃든 입력이 끝나면 항상 호출된다.
            // (OnSubmit이 New Input System UI 모듈 바인딩에 따라 안 먹는 경우가 있어, 이걸 유일한 종료 경로로 둔다.)
            inputChat.onEndEdit.RemoveListener(OnChatEndEdit);
            inputChat.onEndEdit.AddListener(OnChatEndEdit);
        }
        if (UIManager.Instance.chatMessageText != null) messageText = UIManager.Instance.chatMessageText;
    }

    private void OnChatEndEdit(string text)
    {
        if (_chatOpen) SendChatMessage();
    }

    private void Update()
    {
        if (inputChat == null) return;

        bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!enterPressed) return;

        if (_chatOpen)
        {
            // Enter로 닫기/전송 (OnSubmit/onEndEdit이 같은 프레임에 이미 처리했어도 SendChatMessage는 안전하게 재호출 가능)
            SendChatMessage();
        }
        else if (Time.frameCount != _lastCloseFrame)
        {
            // 방금 같은 프레임에 닫힌 게 아니면 Enter로 채팅을 연다.
            OpenChat();
        }
    }

    private void OpenChat()
    {
        if (inputChat == null) return;
        _chatOpen = true;
        if (!inputChat.gameObject.activeSelf) inputChat.gameObject.SetActive(true);
        inputChat.ActivateInputField();
        inputChat.Select();
        SetPlayerInputEnabled(false);
    }

    private void CloseChat()
    {
        _chatOpen = false;
        _lastCloseFrame = Time.frameCount;
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
            // 래퍼를 거쳐야 누적 입력(_input)이 초기화되어 채팅 종료 후 이동이 정상 복구된다.
            if (inputEnabled) playerInput.EnableInput();
            else playerInput.DisableInput();
        }
    }

    public void SendChatMessage()
    {
        // 채팅 UI(DontDestroyOnLoad) 버튼의 OnClick/OnSubmit이 씬 전환으로 무효화된 ChatManager를
        // 가리킬 수 있으므로, 항상 현재 씬의 유효한 Instance를 통해 전송한다.
        ChatManager mgr = (Instance != null) ? Instance : this;
        if (mgr.Object == null || !mgr.Object.IsValid)
        {
            Debug.LogWarning("[ChatManager] 유효한 NetworkObject가 없어 채팅을 전송할 수 없습니다.");
            return;
        }

        if (mgr.inputChat != null && !string.IsNullOrEmpty(mgr.inputChat.text))
        {
            mgr.RPC_SendChatMessage(mgr.inputChat.text);
        }

        // 전송 여부와 무관하게 항상 채팅을 닫고 입력을 복구한다 (이 메서드가 닫기의 유일한 경로).
        mgr.CloseChat();
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
        if (string.IsNullOrEmpty(msg)) return;

        ChatManager mgr = (Instance != null) ? Instance : this;
        if (mgr.Object == null || !mgr.Object.IsValid)
        {
            Debug.LogWarning("[ChatManager] 유효한 NetworkObject가 없어 시스템 메시지를 전송할 수 없습니다.");
            return;
        }
        mgr.RPC_SendSystemMessage(msg, color);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendSystemMessage(string msg, Color color)
    {
        string senderName = "System";

        if (messageText != null)
            messageText.text += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{senderName} : {msg}</color> \n";
    }
}
