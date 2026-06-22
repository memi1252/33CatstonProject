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

    // N/B 키 입력 신뢰성: Update에서 감지하고 FixedUpdateNetwork에서 소비.
    // (FixedUpdateNetwork의 Input.GetKeyDown은 재시뮬레이션/틱 타이밍 때문에 누락될 수 있음)
    private bool _contractTriggerRequested;
    private bool _imprintTriggerRequested;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 주의: NetworkBehaviour는 DontDestroyOnLoad로 씬 간 이동시키면 NetworkObject가
            // 무효화되어 FixedUpdateNetwork/RPC가 동작하지 않는다. 증강이 일어나는 게임 씬에
            // BuffManager NetworkObject를 직접 배치할 것.
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Player GetLocalPlayer()
    {
        if (Runner == null) return null;
        NetworkObject obj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (obj != null && obj.TryGetComponent(out Player p)) return p;
        return null;
    }

    // 게임 씬에 배치된 BuffManager는 영구(DontDestroyOnLoad) UI를 에디터에서 연결할 수 없으므로
    // 런타임에 UIManager에서 UI 참조를 가져온다. (UIManager에 값이 있을 때만 덮어씀 → 로비 인스턴스는 자체 연결 유지)
    private void ResolveUIReferences()
    {
        if (UIManager.Instance == null) return;
        if (UIManager.Instance.buffTimerFillImage != null) timerFillImage = UIManager.Instance.buffTimerFillImage;
        if (UIManager.Instance.buffTimerText != null) timerText = UIManager.Instance.buffTimerText;
        if (UIManager.Instance.buffSlotParent != null) buffSlotParent = UIManager.Instance.buffSlotParent;
    }

    void Start()
    {
        ResolveUIReferences();
        if (UIManager.Instance != null && UIManager.Instance.buffUI != null)
            UIManager.Instance.buffUI.SetActive(false);
    }

    private void DisablePlayerInput()
    {
        NetworkObject playerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (playerObj != null && playerObj.TryGetComponent(out PlayerInput playerInput))
        {
            // 래퍼를 거쳐야 누적 입력(_input)이 초기화되어 UI 종료 후 이동이 정상 복구된다.
            playerInput.DisableInput();
            Debug.Log("[BuffManager] Player input disabled");
        }
    }

    private void EnablePlayerInput()
    {
        NetworkObject playerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (playerObj != null && playerObj.TryGetComponent(out PlayerInput playerInput))
        {
            playerInput.EnableInput();
            Debug.Log("[BuffManager] Player input enabled");
        }
    }

    // ===== 외부(StageManager 등) 연동 진입점 =====
    // 계약/각인 투표를 코드로 시작한다. 실제 시작은 씬 권한자의 FixedUpdateNetwork 에서 소비된다.
    public void RequestContractVote()
    {
        if (Runner != null && Runner.IsSceneAuthority) _contractTriggerRequested = true;
    }

    public void RequestImprintVote()
    {
        if (Runner != null && Runner.IsSceneAuthority) _imprintTriggerRequested = true;
    }

    // 계약/각인 투표가 진행 중인지 (네트워크 동기화 상태 기반). 종료되면 둘 다 false.
    public bool IsBuffVoteActive => isContractBuffActive || isImprintBuffActive;

    // Update is called once per frame
    void Update()
    {
        // 네트워크 객체가 스폰되지 않았으면 리턴
        if (!Object || !Object.IsValid) return;

        // N/B 트리거 감지 (씬 권한자만). 실제 시작은 FixedUpdateNetwork에서 소비.
        if (Runner.IsSceneAuthority)
        {
            if (Input.GetKeyDown(KeyCode.N)) _contractTriggerRequested = true;
            if (Input.GetKeyDown(KeyCode.B)) _imprintTriggerRequested = true;
        }

        if (isContractBuffActive)
        {
            contractVoteTime -= Time.deltaTime;
            if (timerFillImage != null) timerFillImage.fillAmount = contractVoteTime / contractVoteTimeMax;
            if (timerText != null) timerText.text = $"{contractVoteTime:F0}";
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
                    // 모든 플레이어의 선택이 수집되면 (contractChosenBuff는 씬 권한자에만 쌓이므로
                    // 이 조건은 사실상 씬 권한자에서만 참이 된다)
                    if (Runner.IsSceneAuthority && contractChosenBuff.Count == Runner.ActivePlayers.Count())
                    {
                        // 선택된 계약 인덱스를 모든 클라에 브로드캐스트 → 각 클라가 자기 플레이어에 적용 + UI 종료
                        contractApplyBuff();

                        // 권한자 전용 네트워크 상태 정리
                        isContractBuffActive = false;
                        playerVotes.Clear();
                        contractChosenBuff.Clear();
                    }
                }
            }
        }
        
        if (isImprintBuffActive)
        {
            imprintVoteTime -= Time.deltaTime;
            if (timerFillImage != null) timerFillImage.fillAmount = imprintVoteTime / imprintVoteTimeMax;
            if (timerText != null) timerText.text = $"{imprintVoteTime:F0}";
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
                        // 가장 많은 표를 받은 버프 + 조건 충족 버프를 모든 클라에 브로드캐스트 (각자 자기에게 적용)
                        ImprintFinishVoting();

                        // 권한자 전용 네트워크 상태 정리
                        isImprintBuffActive = false;
                        isVoteFinished = false;
                        playerVotes.Clear();
                    }

                    // 로컬 UI/타이머 초기화 (모든 클라)
                    imprintVoteTimeMax = 30f;
                    imprintVoteTime = imprintVoteTimeMax;
                    if (UIManager.Instance != null && UIManager.Instance.buffUI != null)
                        UIManager.Instance.buffUI.SetActive(false);
                    EnablePlayerInput(); // 플레이어 입력 다시 활성화
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

        // 적용할 버프들의 인덱스(imprintAvailableBuffs 기준)를 모아 브로드캐스트한다.
        List<int> conditionIndices = new List<int>();
        for (int i = 0; i < buffSlots.Count; i++)
        {
            BuffScripableObject buff = buffSlots[i].buffScripableObject;
            if (buff == null || !buff.isVotingCondition) continue;

            bool pass = false;
            switch (buff.Condition)
            {
                case VotingCondition.Count:
                    pass = voteCounts[i] == buff.votingValue;
                    break;
                case VotingCondition.Percent:
                    float percent = (float)voteCounts[i] / Runner.ActivePlayers.Count() * 100f;
                    pass = percent >= buff.votingValue;
                    break;
                case VotingCondition.MAX:
                    pass = voteCounts[i] == Runner.ActivePlayers.Count();
                    break;
            }

            if (pass)
            {
                int idx = System.Array.IndexOf(imprintAvailableBuffs, buff);
                if (idx >= 0) conditionIndices.Add(idx);
            }
        }

        int winnerIndex = -1;
        if (voteCounts.Count > 0 && buffSlots.Count > 0)
        {
            int maxValue = voteCounts.Max();
            int maxSlot = voteCounts.IndexOf(maxValue);
            if (maxSlot >= 0 && maxSlot < buffSlots.Count && buffSlots[maxSlot].buffScripableObject != null)
            {
                winnerIndex = System.Array.IndexOf(imprintAvailableBuffs, buffSlots[maxSlot].buffScripableObject);
            }
        }

        RPC_ApplyImprintBuffs(winnerIndex, conditionIndices.ToArray());
    }

    // 각인 버프 적용: 모든 클라가 받아 자기 플레이어에만 적용 (Shared 모드 권한 규칙)
private void RPC_ApplyImprintBuffs(int winnerIndex, int[] conditionIndices)
    {
        Player me = GetLocalPlayer();
        if (me == null) return;

        if (conditionIndices != null)
        {
            foreach (int idx in conditionIndices)
            {
                if (idx >= 0 && idx < imprintAvailableBuffs.Length)
                {
                    var buff = imprintAvailableBuffs[idx];
                    me.ApplyImprintConditionBuff(buff);
                    // EnemyGlobalBuffs는 씬 권한자(호스트)에서만 1회 적용 — 멀티플레이어에서 중복 stacking 방지
                    if (Runner.IsSceneAuthority && buff.votingAbility != null)
                        foreach (var entry in buff.votingAbility)
                            EnemyGlobalBuffs.Apply(entry.targetAbilities);
                }
            }
        }

        if (winnerIndex >= 0 && winnerIndex < imprintAvailableBuffs.Length)
        {
            var winner = imprintAvailableBuffs[winnerIndex];
            me.ApplyImprintBuff(winner);
            if (Runner.IsSceneAuthority && winner.buffProperties != null)
                foreach (var entry in winner.buffProperties)
                    EnemyGlobalBuffs.Apply(entry.targetAbilities);
        }

        SoundManager.Instance?.PlayBuffApply();
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ContractBuffTransmission(int buffIndex)
    {
        if (buffIndex < 0 || buffIndex >= contractAvailableBuffs.Length) return;
        ContractScriptableObject chosenBuff = contractAvailableBuffs[buffIndex];
        contractChosenBuff.Add(chosenBuff);
        Debug.Log($"[Buff] {chosenBuff.contractName}");
    }


    // 씬 권한자에서만 호출됨. 선택된 계약 인덱스를 모든 클라에 브로드캐스트.
    private void contractApplyBuff()
    {
        List<int> indices = new List<int>();
        foreach (var buff in contractChosenBuff)
        {
            int idx = System.Array.IndexOf(contractAvailableBuffs, buff);
            if (idx >= 0) indices.Add(idx);
        }
        RPC_FinishContractVote(indices.ToArray());
    }

    // 계약 버프 적용 + UI/입력 종료: 모든 클라가 받아 자기 플레이어에만 적용 (Shared 모드 권한 규칙)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FinishContractVote(int[] contractIndices)
    {
        Player me = GetLocalPlayer();
        if (me != null && contractIndices != null)
        {
            foreach (int idx in contractIndices)
            {
                if (idx >= 0 && idx < contractAvailableBuffs.Length)
                {
                    var contract = contractAvailableBuffs[idx];
                    me.ApplyContractBuff(contract);
                    // 계약에 적 관련 속성이 있으면 전역에 누적 — 씬 권한자에서만 1회 적용
                    if (Runner.IsSceneAuthority && contract.contractBuffs != null)
                        foreach (var entry in contract.contractBuffs)
                            EnemyGlobalBuffs.Apply(entry.targetAbilities);
                }
            }
        }

        // 모든 클라 UI/입력/로컬 상태 초기화
        isContractBuffTransmission = false;
        contractVoteTime = contractVoteTimeMax;
        if (UIManager.Instance != null && UIManager.Instance.buffUI != null)
            UIManager.Instance.buffUI.SetActive(false);
        EnablePlayerInput();
    }


    public override void Spawned()
    {
        // 2. 데이터 변화를 감지하기 위한 디텍터 초기화
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        ResolveUIReferences();
        if (UIManager.Instance != null && UIManager.Instance.buffUI != null)
            UIManager.Instance.buffUI.SetActive(false);

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
        //계약 증강 시작 임시 - StateAuthority만 처리
        bool contractTrigger = _contractTriggerRequested;
        _contractTriggerRequested = false;
        if (contractTrigger && Runner.IsSceneAuthority)
        {
            if (!isContractBuffActive)
            {
                isContractBuffActive = true;  // 먼저 true로 설정하여 중복 호출 방지
                
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
                    archiveContractBuffs.Clear();
                    availableIndices.Clear();
                    for (int j = 0; j < contractAvailableBuffs.Length; j++)
                    {
                        availableIndices.Add(j);
                    }
                }
                
                // 랜덤하게 선택
                for (int i = 0; i < neededBuffCount; i++)
                {
                    if (availableIndices.Count == 0) break;
                    
                    int randomListIndex = Random.Range(0, availableIndices.Count);
                    int randomIndex = availableIndices[randomListIndex];
                    buffIndices[i] = randomIndex;
                    archiveContractBuffs.Add(contractAvailableBuffs[randomIndex]);
                    availableIndices.RemoveAt(randomListIndex);
                }
                RPC_ContractBuffVote(buffIndices);
            }
        }
        
        //각인 증강 시작 임시 - StateAuthority만 처리
        bool imprintTrigger = _imprintTriggerRequested;
        _imprintTriggerRequested = false;
        if (imprintTrigger && Runner.IsSceneAuthority)
        {
            if (imprintAvailableBuffs.Length == 0)
                return;
            if (!isImprintBuffActive)
            {
                isImprintBuffActive = true;  // 먼저 true로 설정
                
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
                    archiveImprintBuffs.Clear();
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
        myContractBuff.Clear(); // 기존 UI 데이터만 초기화
        if (buffSlotParent != null && buffSlotParent.childCount > 0) foreach (Transform child in buffSlotParent) Destroy(child.gameObject);
        buffSlots.Clear();

        if (UIManager.Instance != null && UIManager.Instance.buffUI != null)
            UIManager.Instance.buffUI.SetActive(true);
        DisablePlayerInput(); // 플레이어 입력 비활성화

        // 현재 접속한 플레이어들을 ID 순서대로 정렬하여 리스트로 만듭니다.
        var sortedPlayers = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();

        // 내 현재 순서 (0부터 시작하므로 +1)
        int myCurrentOrder = sortedPlayers.IndexOf(Runner.LocalPlayer) + 1;
        
        // 각 플레이어가 보여줄 3개 버프의 시작 인덱스
        int startIndex = (myCurrentOrder - 1) * 3;
        int endIndex = startIndex + 3;
        
        Debug.Log($"[BuffManager] 플레이어 {myCurrentOrder}: buffIndices[{startIndex}:{endIndex}]");
        
        int Order = 1;
        for (int i = startIndex; i < endIndex && i < buffIndices.Length; i++)
        {
            int buffIndex = buffIndices[i];
            if (buffIndex >= 0 && buffIndex < contractAvailableBuffs.Length)
            {
                ContractScriptableObject buff = contractAvailableBuffs[buffIndex];
                var slot = Instantiate(bufffSlotPrefab, buffSlotParent);
                BuffSlot buffSlot = slot.GetComponent<BuffSlot>();
                buffSlot.UpdateVotePlayer("");
                buffSlots.Add(buffSlot);
                myContractBuff[buff] = Order;  // Add 대신 직접 할당 (중복 키 자동 처리)
                buffSlot.Set(buff);
                buffSlot.Order = Order++;
                
                Debug.Log($"[BuffManager] 버프 표시: {buff.contractName} (Order: {buffSlot.Order})");
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ImprintBuffVote(int[] buffIndices)
    {
        isImprintBuffActive = true;
        if (buffSlotParent != null && buffSlotParent.childCount > 0) foreach (Transform child in buffSlotParent) Destroy(child.gameObject);
        buffSlots.Clear();

        if (UIManager.Instance != null && UIManager.Instance.buffUI != null)
            UIManager.Instance.buffUI.SetActive(true);
        DisablePlayerInput(); // 플레이어 입력 비활성화

        int Order = 1;
        foreach (int index in buffIndices)
        {
            if (index < 0 || index >= imprintAvailableBuffs.Length) continue;
            BuffScripableObject buffData = imprintAvailableBuffs[index];
            if (buffData == null) continue;

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
