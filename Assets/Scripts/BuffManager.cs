using UnityEngine;
using UnityEngine.UI;
using Fusion;
using ExitGames.Client.Photon.StructWrapping;
using System.Collections.Generic;
using Starter.Platformer;
using TMPro;
using System.Linq;
using Febucci.TextAnimatorForUnity;
using UnityEngine.Serialization;

public class BuffManager : NetworkBehaviour
{
    public static BuffManager Instance { get; private set; }
    public GameObject BuffUI;
    public GameObject bufffSlotPrefab;
    public Transform buffSlotParent;

    public float imprintVoteTime = 30f;
    public float contractVoteTime = 15f;

    [Header("UI")]
    public Image timerFillImage;
    public TextMeshProUGUI timerText;

    public BuffScripableObject[] imprintAvailableBuffs; // 각인 투표 가능한 버프 목록
    
    public ContractScriptableObject[] contractAvailableBuffs; // 계약으로 얻을 수 있는 버프 목록

    [Networked]
    public bool isImprintBuffActive { get; set; } = false; // 각인 버프 활성화 여부
    
    [Networked]
    public bool isContractBuffActive { get; set; } = false; // 계약 버프 활성화 여부

    [Networked]
    public bool isVoteFinished { get; set; } = false; // 투표 종료 여부

    [Networked, Capacity(4)]
    public NetworkDictionary<PlayerRef, int> playerVotes => default;

    private Dictionary<ContractScriptableObject, int> myContractBuff = new Dictionary<ContractScriptableObject, int>(); // 현제 보여지고있는 계약 
    
    private List<ContractScriptableObject> contractChosenBuff = new List<ContractScriptableObject>(); // 선택받은 계약 버프들 
    private List<ContractScriptableObject> archiveContractBuffs = new List<ContractScriptableObject>(); // 계약 버프 중복 방지 위한 아카이브
    private List<BuffScripableObject> archiveImprintBuffs = new List<BuffScripableObject>();
    private List<BuffSlot> buffSlots = new List<BuffSlot>();
    private ChangeDetector _changeDetector;

    private float imprintVoteTimeMax = 30f;
    private float contractVoteTimeMax = 15f;
    private float voteResultTime = 5f;

    private bool isContractBuffTransmission = false;

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
        // 네트워크 객체가 스폰되지 않았으면 리턴
        if (!Object || !Object.IsValid) return;

        if (isContractBuffActive)
        {
            contractVoteTime -= Time.deltaTime;
            timerFillImage.fillAmount = contractVoteTime / contractVoteTimeMax;
            timerText.text = $"{contractVoteTime:F0}";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (contractVoteTime <= 0)
            {
                if (!isContractBuffTransmission)
                {
                    isContractBuffTransmission = true;
                    playerVotes.TryGet(Runner.LocalPlayer, out int myVoteOrder);
                    foreach (var buff in myContractBuff)
                    {
                        if(myVoteOrder == buff.Value)
                        {
                            for(int i = 0; i < contractAvailableBuffs.Length; i++)
                            {
                                if(buff.Key == contractAvailableBuffs[i])
                                {
                                    RPC_ContractBuffTransmission(i);
                                    break;
                                }
                            }
                        }

                    }
                }
                else
                {
                    if (contractChosenBuff.Count == Runner.ActivePlayers.Count())
                    {
                        if (Runner.IsSceneAuthority)
                        {
                            contractApplyBuff();
                            isContractBuffActive  = false;
                        }
                        playerVotes.Clear();
                        contractVoteTime = contractVoteTimeMax;
                        //Cursor.lockState = CursorLockMode.Locked;
                        //Cursor.visible = false;
                        BuffUI.SetActive(false);
                    }
                }
            }
        }
        
        if (isImprintBuffActive)
        {
            imprintVoteTime -= Time.deltaTime;
            timerFillImage.fillAmount = imprintVoteTime / imprintVoteTimeMax;
            timerText.text = $"{imprintVoteTime:F0}";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (imprintVoteTime <= 0)
            {
                if (!isVoteFinished)
                {
                    if (Runner.IsSceneAuthority)
                    {
                        isVoteFinished = true;
                    }
                    imprintVoteTimeMax = voteResultTime;
                    imprintVoteTime = voteResultTime; // 결과 발표 시간
                    UpdateVoteVisuals();
                }
                else
                {
                    if (Runner.IsSceneAuthority)
                    {
                        // 버프 적용 로직은 여기서 구현 (예: 가장 많은 표를 받은 버프 적용, 조건에 맞으면 버프 적용)
                        ImprintFinishVoting();
                    }
                    

                    //투표 종료 처리
                    isImprintBuffActive = false;
                    isVoteFinished = false;
                    playerVotes.Clear();
                    imprintVoteTimeMax = 30f;
                    imprintVoteTime = imprintVoteTimeMax;
                    //Cursor.lockState = CursorLockMode.Locked;
                    //Cursor.visible = false;
                    BuffUI.SetActive(false);
                }
            }
        }
    }
    
    private void ImprintFinishVoting()
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

        for (int i = 0; i < buffSlots.Count; i++)
        {
            BuffScripableObject buff = buffSlots[i].buffScripableObject;
            if (buff.isVotingCondition)
            {
                switch (buff.Condition)
                {
                    case VotingCondition.Count:
                        if (voteCounts[i] == buff.votingValue)
                        {
                            imprintConditionApplyBuff(i);
                        }
                        break;
                    case VotingCondition.Percent:
                        float percent = (float)voteCounts[i] / Runner.ActivePlayers.Count() * 100f;
                        if (percent >= buff.votingValue)
                        {
                            imprintConditionApplyBuff(i);
                        }
                        break;
                    case VotingCondition.MAX:
                        if (voteCounts[i] == Runner.ActivePlayers.Count())
                        {
                            imprintConditionApplyBuff(i);
                        }
                        break;
                }
            }
        }
        
        if (voteCounts.Count > 0)
        {
            int maxValue = voteCounts.Max();
            int maxIndex = voteCounts.IndexOf(maxValue);
            imprintApplyBuff(maxIndex);
        }

        
    }

    private void imprintConditionApplyBuff(int maxIndex)
    {
        var conditionBuffAsset = buffSlots[maxIndex].buffScripableObject;

        if (conditionBuffAsset == null) return;
        
        foreach (var playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObj = Runner.GetPlayerObject(playerRef);

            if (playerObj != null)
            {
                if (playerObj.TryGetComponent(out Player player))
                {

                    for (int i = 0; i < conditionBuffAsset.votingAbility.Length; i++)
                    {
                        var props = conditionBuffAsset.votingAbility[i].targetAbilities;
                        player.maxHp += props.maxHp;
                        player.maxMp += props.maxMp;
                        player.damage *= props.damage;
                        player.attackSpeed = (props.attackSpeed / (player.attackSpeed/100f));
                        player.moveSpeed += props.moveSpeed;
                        player.allDamage += props.allDamage;
                        player.damageReceived += props.damageReceived;
                        player.criticalDamage += props.criticalDamage;
                        player.criticalChance += props.criticalChance;
                    }
                    

                    Debug.Log($"[Buff] {player.Nickname}에게 {conditionBuffAsset.buffName} 적용 완료!");
                }
            }
        }
    }

    private void imprintApplyBuff(int maxIndex)
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

                    for (int i = 0; i < winnerBuffAsset.buffProperties.Length; i++)
                    {
                        var props = winnerBuffAsset.buffProperties[i].targetAbilities;
                        if (winnerBuffAsset.Condition == VotingCondition.Fixed)
                        {
                            player.maxHp += props.maxHp;
                            player.maxMp += props.maxMp;
                        }else if( winnerBuffAsset.Condition == VotingCondition.Percent)
                        {
                            player.maxHp *= (1 + props.maxHp);
                            player.maxMp *= (1 + props.maxMp);
                        }
                        player.damage *= props.damage;
                        player.attackSpeed = (player.attackSpeed + props.attackSpeed);
                        player.moveSpeed = player.WalkSpeed * (1 + props.moveSpeed);
                        player.allDamage += props.allDamage;
                        player.damageReceived += props.damageReceived;
                        player.criticalDamage += props.criticalDamage;
                        player.criticalChance += props.criticalChance;
                    }
                    Debug.Log($"[Buff] {player.Nickname}에게 {winnerBuffAsset.buffName} 적용 완료!");
                }
            }
        }
    }
    
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ContractBuffTransmission(int buffIndex)
    {
        ContractScriptableObject chosenBuff = contractAvailableBuffs[buffIndex];
        contractChosenBuff.Add(chosenBuff);
        Debug.Log($"[Buff] {chosenBuff.contractName}");
    }
    
    
    private void contractApplyBuff()
    {
        // 게약은 각인과 달리 무기에 적용시키는 증강이 있아서 나중에 무기를 만든후 무기에적용시키는 증강 만들어야할듯
        foreach (var buff in contractChosenBuff)
        {
            foreach (var playerRef in Runner.ActivePlayers)
            {
                NetworkObject playerObj = Runner.GetPlayerObject(playerRef);
        
                if (playerObj != null)
                {
                    if (playerObj.TryGetComponent(out Player player))
                    {
                        for (int i = 0; i < buff.contractBuffs.Length; i++)
                        {
                            var props = buff.contractBuffs[i].targetAbilities;
                            if(buff.valueType == ValueType.Percent)
                            {
                                player.maxHp *= (1 + props.maxHp);
                                player.maxMp *= (1 + props.maxMp);
                            }
                            player.damage *= props.damage;
                            player.attackSpeed = (player.attackSpeed + props.attackSpeed);
                            player.moveSpeed = player.WalkSpeed * (1 + props.moveSpeed);
                            player.allDamage += props.allDamage;
                            player.damageReceived += props.damageReceived;
                            player.criticalDamage += props.criticalDamage;
                            player.criticalChance += props.criticalChance;
                        }
                        
                        Debug.Log($"[Buff] {player.Nickname}에게 {buff.contractName} 적용 완료!");
                    }
                }
            }
        }
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
        //계약 증강 시작 임시
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (!isContractBuffActive)
            {
                int neededBuffCount = Runner.ActivePlayers.Count() * 3;
                int[] buffIndices = new int[neededBuffCount];
                
                // 사용 가능한 버프 인덱스 리스트 생성
                List<int> availableIndices = new List<int>();
                for (int j = 0; j < contractAvailableBuffs.Length; j++)
                {
                    if (!archiveContractBuffs.Contains(contractAvailableBuffs[j]))
                    {
                        availableIndices.Add(j);
                    }
                }
                
                // 사용 가능한 버프가 부족하면 archiveBuffs 초기화
                if (availableIndices.Count < neededBuffCount)
                {
                    Debug.Log($"[BuffManager] 계약 버프 풀 초기화 (필요: {neededBuffCount}, 남음: {availableIndices.Count})");
                    //archiveBuffs.Clear();
                    availableIndices.Clear();
                    for (int j = 0; j < contractAvailableBuffs.Length; j++)
                    {
                        availableIndices.Add(j);
                    }
                }
                
                // 랜덤하게 선택
                for (int i = 0; i < neededBuffCount; i++)
                {
                    int randomListIndex = Random.Range(0, availableIndices.Count);
                    int randomIndex = availableIndices[randomListIndex];
                    buffIndices[i] = randomIndex;
                    archiveContractBuffs.Add(contractAvailableBuffs[randomIndex]);
                    availableIndices.RemoveAt(randomListIndex);
                }
                RPC_ContractBuffVote(buffIndices);
            }
        }
        
        //각인 증강 시작 임시
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (imprintAvailableBuffs.Length == 0)
                return;
            if (!isImprintBuffActive)
            {
                int[] buffIndices = new int[3];
                
                // 사용 가능한 버프 인덱스 리스트 생성
                List<int> availableIndices = new List<int>();
                for (int j = 0; j < imprintAvailableBuffs.Length; j++)
                {
                    if (!archiveImprintBuffs.Contains(imprintAvailableBuffs[j]))
                    {
                        availableIndices.Add(j);
                    }
                }
                
                // 사용 가능한 버프가 부족하면 archiveBuffs 초기화
                if (availableIndices.Count < 3)
                {
                    Debug.Log($"[BuffManager] 각인 버프 풀 초기화 (필요: 3, 남음: {availableIndices.Count})");
                    //archiveBuffs.Clear();
                    availableIndices.Clear();
                    for (int j = 0; j < imprintAvailableBuffs.Length; j++)
                    {
                        availableIndices.Add(j);
                    }
                }
                
                // 랜덤하게 선택
                for (int i = 0; i < 3; i++)
                {
                    int randomListIndex = Random.Range(0, availableIndices.Count);
                    int randomIndex = availableIndices[randomListIndex];
                    buffIndices[i] = randomIndex;
                    archiveImprintBuffs.Add(imprintAvailableBuffs[randomIndex]);
                    availableIndices.RemoveAt(randomListIndex);
                }
                RPC_ImprintBuffVote(buffIndices);
            }
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ContractBuffVote(int[] buffIndices)
    {
        isContractBuffActive = true;
        if (buffSlotParent.childCount > 0) foreach (Transform child in buffSlotParent) Destroy(child.gameObject);
        buffSlots.Clear();

        BuffUI.SetActive(true);
        
        // 현재 접속한 플레이어들을 ID 순서대로 정렬하여 리스트로 만듭니다.
        var sortedPlayers = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();

        // 전체 인원수
        int totalCount = sortedPlayers.Count;

        // 내 현재 순서 (0부터 시작하므로 +1)
        int myCurrentOrder = sortedPlayers.IndexOf(Runner.LocalPlayer) + 1;
        
        int Order = 1;
        switch (myCurrentOrder)
        {
            case 1:
                for(int i = 0; i < 3; i++)
                {
                    ContractScriptableObject buff = contractAvailableBuffs[i];
                    var slot = Instantiate(bufffSlotPrefab, buffSlotParent);
                    BuffSlot buffSlot = slot.GetComponent<BuffSlot>();
                    buffSlot.UpdateVotePlayer("");
                    buffSlots.Add(buffSlot);
                    myContractBuff.Add(buff, Order);
                    buffSlot.Set(buff);
                    buffSlot.Order = Order++;
                }
                break;
            case 2:
                for(int i = 3; i < 6; i++)
                {
                    ContractScriptableObject buff = contractAvailableBuffs[i];
                    var slot = Instantiate(bufffSlotPrefab, buffSlotParent);
                    BuffSlot buffSlot = slot.GetComponent<BuffSlot>();
                    buffSlot.UpdateVotePlayer("");
                    buffSlots.Add(buffSlot);
                    myContractBuff.Add(buff, Order);
                    buffSlot.Set(buff);
                    buffSlot.Order = Order++;
                }
                break;
            case 3:
                for(int i = 6; i < 9; i++)
                {
                    ContractScriptableObject buff = contractAvailableBuffs[i];
                    var slot = Instantiate(bufffSlotPrefab, buffSlotParent);
                    BuffSlot buffSlot = slot.GetComponent<BuffSlot>();
                    buffSlot.UpdateVotePlayer("");
                    buffSlots.Add(buffSlot);
                    myContractBuff.Add(buff, Order);
                    buffSlot.Set(buff);
                    buffSlot.Order = Order++;
                }
                break;
            case 4:
                for(int i = 9; i < 12; i++)
                {
                    ContractScriptableObject buff = contractAvailableBuffs[i];
                    var slot = Instantiate(bufffSlotPrefab, buffSlotParent);
                    BuffSlot buffSlot = slot.GetComponent<BuffSlot>();
                    buffSlot.UpdateVotePlayer("");
                    buffSlots.Add(buffSlot);
                    myContractBuff.Add(buff, Order);
                    buffSlot.Set(buff);
                    buffSlot.Order = Order++;
                }
                break;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ImprintBuffVote(int[] buffIndices)
    {
        isImprintBuffActive = true;
        if (buffSlotParent.childCount > 0) foreach (Transform child in buffSlotParent) Destroy(child.gameObject);
        buffSlots.Clear();

        BuffUI.SetActive(true);

        int Order = 1;
        foreach (int index in buffIndices)
        {

            BuffScripableObject buffData = imprintAvailableBuffs[index];
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
