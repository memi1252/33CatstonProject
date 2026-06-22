using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingManager : NetworkBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Transform playerSlotParent;
    [SerializeField] private GameObject playerSlotPrefab;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TMP_FontAsset galmuriFont;

    // 각 플레이어의 로딩 완료 여부 (PlayerRef → bool)
    [Networked, Capacity(4)]
    private NetworkDictionary<PlayerRef, NetworkBool> _readyMap => default;

    private readonly Dictionary<PlayerRef, GameObject> _slotObjects = new();
    private readonly Dictionary<PlayerRef, string> _playerNames = new();
    private bool _allReadySent = false;

    private void Awake()
    {
        Instance = this;
        if (galmuriFont != null)
        {
            if (titleText != null) titleText.font = galmuriFont;
        }
    }

    public override void Spawned()
    {
        // 자신의 로딩 완료 신고
        StartCoroutine(ReportReady());
    }

    private IEnumerator ReportReady()
    {
        yield return null;
        string myName = PlayerPrefs.GetString("PlayerName", $"플레이어 {Runner.LocalPlayer.PlayerId}");
        RPC_SetReady(Runner.LocalPlayer, myName);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_SetReady(PlayerRef player, string playerName)
    {
        _playerNames[player] = playerName;
        if (HasStateAuthority)
            _readyMap.Set(player, true);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // 모든 플레이어 확인
        var players = Runner.ActivePlayers;
        int total = 0, ready = 0;
        foreach (var p in players)
        {
            total++;
            if (_readyMap.TryGet(p, out NetworkBool isReady) && isReady)
                ready++;
        }

        if (!_allReadySent && total > 0 && ready >= total)
        {
            _allReadySent = true;
            RPC_AllReady();
        }
    }

    public override void Render()
    {
        UpdateSlotUI();
    }

    private void UpdateSlotUI()
    {
        if (playerSlotParent == null || playerSlotPrefab == null) return;

        foreach (var p in Runner.ActivePlayers)
        {
            bool isReady = _readyMap.TryGet(p, out NetworkBool r) && r;

            if (!_slotObjects.TryGetValue(p, out var slot) || slot == null)
            {
                slot = Instantiate(playerSlotPrefab, playerSlotParent);
                _slotObjects[p] = slot;
            }

            // 슬롯 텍스트와 색상 업데이트
            var texts = slot.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length > 0)
            {
                if (galmuriFont != null) texts[0].font = galmuriFont;
                _playerNames.TryGetValue(p, out string displayName);
                if (string.IsNullOrEmpty(displayName))
                    displayName = $"플레이어 {p.PlayerId}";
                texts[0].text = displayName;
            }
            if (texts.Length > 1)
            {
                if (galmuriFont != null) texts[1].font = galmuriFont;
                texts[1].text = isReady ? "로딩 완료 ✓" : "로딩 중...";
                texts[1].color = isReady ? Color.green : Color.yellow;
            }

            var img = slot.GetComponentInChildren<Image>();
            if (img != null)
                img.color = isReady ? new Color(0.2f, 0.8f, 0.2f, 0.3f) : new Color(0.8f, 0.8f, 0.2f, 0.3f);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AllReady()
    {
        // 게임씬으로 이동 (build index 2)
        StartCoroutine(LoadGameScene());
    }

    private IEnumerator LoadGameScene()
    {
        yield return new WaitForSeconds(0.5f);
        Runner.LoadScene(SceneRef.FromIndex(3));
    }
}
