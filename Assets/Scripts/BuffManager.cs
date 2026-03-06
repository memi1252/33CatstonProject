using UnityEngine;
using UnityEngine.UI;
using Fusion;
using ExitGames.Client.Photon.StructWrapping;
using System.Collections.Generic;
using Starter.Platformer;
using TMPro;
using System.Linq;

public class BuffManager : NetworkBehaviour
{
    public static BuffManager Instance { get; private set; }
    public GameObject BuffUI;
    public GameObject bufffSlotPrefab;
    public Transform buffSlotParent;

    public float voteTime = 30;
    public float timerFillAmount = 1f;

    [Header("UI")]
    public Image timerFillImage;
    public TextMeshProUGUI timerText;

    public BuffScripableObject[] availableBuffs; // 투표 가능한 버프 목록

    [Networked]
    public bool isBuffActive { get; set; } = false; // 버프 활성화 여부

    [Networked]
    public bool isVoteFinished { get; set; } = false; // 투표 종료 여부

    [Networked, Capacity(4)]
    public NetworkDictionary<PlayerRef, int> playerVotes => default;

    private List<BuffScripableObject> archiveBuffs = new List<BuffScripableObject>();
    private List<BuffSlot> buffSlots = new List<BuffSlot>();
    private ChangeDetector _changeDetector;

    private float voteTimeMax = 30f;

    private float voteResultTime = 5f;

    void Awake()
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

    void Start()
    {
        BuffUI.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (isBuffActive)
        {
            voteTime -= Time.deltaTime;
            timerFillImage.fillAmount = voteTime / voteTimeMax;
            timerText.text = $"{voteTime:F0}";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (voteTime <= 0)
            {
                if (!isVoteFinished)
                {
                    isVoteFinished = true;
                    voteTimeMax = voteResultTime;
                    voteTime = voteResultTime; // 결과 발표 시간
                    UpdateVoteVisuals();
                }
                else
                {
                    // 버프 적용 로직은 여기서 구현 (예: 가장 많은 표를 받은 버프 적용, 조건에 맞으면 버프 적용)
                    FinishVoting();

                    //투표 종료 처리
                    isBuffActive = false;
                    isVoteFinished = false;
                    playerVotes.Clear();
                    archiveBuffs.Clear();
                    voteTimeMax = 30f;
                    voteTime = voteTimeMax;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    BuffUI.SetActive(false);
                }
            }


        }
    }

    private void FinishVoting()
    {
        //if (!Runner.IsServer) return; 
        Debug.Log("[BuffManager] Finish Voting!");
        List<int> voteCounts = new List<int> { 0, 0, 0 }; // 각 버프에 대한 투표 수

        foreach (var key in playerVotes)
        {
            if(key.Value == 1)
            {
                voteCounts[0]++;
            }
            else if(key.Value == 2)
            {
                voteCounts[1]++;
            }
            else if(key.Value == 3)
            {
                voteCounts[2]++;
            }
            Debug.Log(voteCounts[0] + " / " + voteCounts[1] + " / " + voteCounts[2]);
        }
        
        if (voteCounts.Count > 0)
        {
            int maxValue = voteCounts.Max();
            int maxIndex = voteCounts.IndexOf(maxValue);
            RPC_ApplyBuff(maxIndex);
        }
    }

    //[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyBuff(int maxIndex)
    {
        var winnerBuffAsset = buffSlots[maxIndex].buffScripableObject;

        if (winnerBuffAsset == null) return;

        foreach (var playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObj = Runner.GetPlayerObject(playerRef);

            if (playerObj != null)
            {
                if (playerObj.TryGetComponent(out Player player))
                {
                    
                    //if (Runner.IsServer)
                    {
                        var props = winnerBuffAsset.buffProperties;
                    
                        player.maxHp += props.maxHp;
                        player.speed += props.speed;
                        player.damage += props.damage;
                        player.criticalDamage += props.criticalDamage;
                        player.criticalChance += props.criticalChance;
                        player.attackRange += props.attackRange;
                        player.meleeDefense += props.meleeDefense;
                        player.magicDefense += props.magicDefense;
                    }

                    Debug.Log($"[Buff] {player.Nickname}에게 {winnerBuffAsset.buffName} 적용 완료!");
                }
            }
        }

        // 3. 버프 적용이 끝났으니 UI를 닫습니다.
        BuffUI.SetActive(false);
    }


    public override void Spawned()
    {
        // 2. 데이터 변화를 감지하기 위한 디텍터 초기화
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        BuffUI.SetActive(false);
    }

    public override void Render()
    {
        // 3. 딕셔너리 값이 바뀔 때마다 업데이트 감지
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(playerVotes))
            {
                UpdateVoteVisualsSelf();
            }
        }
    }

    private void UpdateVoteVisualsSelf()
    {
        if (buffSlots == null || buffSlots.Count == 0) return;

        foreach (var slot in buffSlots)
        {
            string names = "";
            playerVotes.TryGet(Runner.LocalPlayer, out int myVoteOrder);
            if (myVoteOrder == slot.Order)
            {
                PlayerRef playerRef = Runner.LocalPlayer;
                string playerName = GameManager.Instance.GetPlayerName(playerRef);

                names = playerName;
            }
            // names = names.TrimEnd(',', ' '); // 마지막 쉼표와 공백 제거
            slot.UpdateVotePlayer(names);
        }
    }

    private void UpdateVoteVisuals()
    {
        foreach (var slot in buffSlots)
        {
            string names = "";
            int count = 0;
            foreach (var kvp in playerVotes)
            {
                if (kvp.Value == slot.Order)
                {
                    count++;
                    PlayerRef playerRef = kvp.Key;
                    string playerName = GameManager.Instance.GetPlayerName(playerRef);
                    if (count % 2 == 0) // 2명마다 줄바꿈
                    {
                        names += playerName + "\n";
                    }
                    else
                    {
                        names += playerName + ", ";
                    }
                }
            }
            names = names.TrimEnd(',', ' '); // 마지막 쉼표와 공백 제거
            slot.UpdateVotePlayer(names);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (!isBuffActive)
            {
                int[] buffIndices = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    int randomIndex = Random.Range(0, availableBuffs.Length);
                    while (archiveBuffs.Contains(availableBuffs[randomIndex]))
                    {
                        randomIndex = Random.Range(0, availableBuffs.Length);
                    }
                    buffIndices[i] = randomIndex;
                    archiveBuffs.Add(availableBuffs[randomIndex]);
                }
                RPC_BuffVote(buffIndices);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BuffVote(int[] buffIndices)
    {
        isBuffActive = true;
        if (buffSlotParent.childCount > 0) foreach (Transform child in buffSlotParent) Destroy(child.gameObject);
        buffSlots.Clear();

        BuffUI.SetActive(true);

        int Order = 1;
        foreach (int index in buffIndices)
        {

            BuffScripableObject buffData = availableBuffs[index];
            var slot = Instantiate(bufffSlotPrefab, buffSlotParent);
            BuffSlot buffSlot = slot.GetComponent<BuffSlot>();
            buffSlot.UpdateVotePlayer("");
            buffSlots.Add(buffSlot);
            buffSlot.Set(buffData);
            buffSlot.Order = Order++;
        }
    }

    public void OnVoteButtonClicked(int buffOrder)
    {
        RPC_SubmitVote(Runner.LocalPlayer, buffOrder);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitVote(PlayerRef player, int buffOrder)
    {
        // 1. 이미 이 버프에 투표한 상태에서 또 누르면 -> 투표 취소
        if (playerVotes.TryGet(player, out int currentVote) && currentVote == buffOrder)
        {
            playerVotes.Remove(player);
            
            Debug.Log($"[BuffManager] {player} 투표 취소");
        }
        else
        {
            // 2. 처음 투표하거나 다른 버프를 선택하면 -> 값 갱신 (덮어쓰기)
            playerVotes.Set(player, buffOrder);
            Debug.Log($"[BuffManager] {player}가 {buffOrder}번으로 투표 변경/등록");
        }
    }

}
