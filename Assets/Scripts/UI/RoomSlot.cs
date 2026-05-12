using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomSlot : MonoBehaviour
{
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI roomPingText;
    public TextMeshProUGUI roomPlayerCountText;
    public Button joinButton;

    [Header("Ping Color Thresholds (ms)")]
    public int goodPingMs = 50;
    public int okPingMs = 150;
    public Color goodColor = new Color(0.30f, 0.85f, 0.30f);
    public Color okColor = new Color(0.95f, 0.80f, 0.20f);
    public Color badColor = new Color(0.90f, 0.30f, 0.30f);

    private SessionInfo _session;
    private Action<SessionInfo> _onClick;

    private void Awake()
    {
        if (joinButton == null)
            joinButton = GetComponent<Button>();
        if (joinButton != null)
            joinButton.onClick.AddListener(HandleClick);
    }

    public void Bind(SessionInfo session, Action<SessionInfo> onClick)
    {
        _session = session;
        _onClick = onClick;

        if (roomNameText != null)
            roomNameText.text = string.IsNullOrEmpty(session.Name) ? "(no name)" : session.Name;

        if (roomPlayerCountText != null)
            roomPlayerCountText.text = $"{session.PlayerCount} / {session.MaxPlayers}";

        if (joinButton != null)
            joinButton.interactable = session.IsOpen && session.IsVisible && session.PlayerCount < session.MaxPlayers;

        SetPing(-1); // 초기엔 측정 전
    }

    public void SetPing(int pingMs)
    {
        if (roomPingText == null) return;

        if (pingMs < 0)
        {
            roomPingText.text = "-- ms";
            roomPingText.color = okColor;
            return;
        }

        roomPingText.text = $"{pingMs}ms";
        if (pingMs <= goodPingMs)
            roomPingText.color = goodColor;
        else if (pingMs <= okPingMs)
            roomPingText.color = okColor;
        else
            roomPingText.color = badColor;
    }

    private void HandleClick()
    {
        _onClick?.Invoke(_session);
    }
}