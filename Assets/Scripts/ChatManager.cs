using System;
using UnityEngine;
using Fusion;
using Starter.Platformer;
using TMPro;
using UnityEngine.UI;

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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (inputChat.text != "")
            {
                SendChatMessage();
            }
            else
            {
                inputChat.ActivateInputField();
            }
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
            senderName = player.GetComponent<Player>().Nickname;
        }

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
        
        messageText.text += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{senderName} : {msg}</color> \n";
    }
}
