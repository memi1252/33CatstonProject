using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Fusion;
using System;

namespace Starter
{
    public class RoomEntry : MonoBehaviour
    {
        public TextMeshProUGUI RoomNameText;
        public TextMeshProUGUI PlayerCountText;
        public Button JoinButton;

        private string _roomName;
        private Action<string> _onJoin;

        public void Setup(SessionInfo session, Action<string> onJoin)
        {
            _roomName = session.Name;
            _onJoin = onJoin;

            RoomNameText.text = session.Name;
            PlayerCountText.text = $"{session.PlayerCount} / {session.MaxPlayers}";

            JoinButton.onClick.RemoveAllListeners();
            JoinButton.onClick.AddListener(() => _onJoin?.Invoke(_roomName));
        }
    }
}