using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponManager : NetworkBehaviour
{
    public static WeaponManager Instance { get; private set; }

    public GameObject weaponUI;
    public GameObject weaponSelectPrefab;
    public Transform weaponSelectPanel;
    
    [Header("무기종류")] 
    public WeaponScriptableObject[] weaponSOs;

    [Header("UI")]
    public Image timerFillImage;
    public TextMeshProUGUI timerText;

    public float weaponSelectVoteTime = 15f;

    [Networked, Capacity(4)]
    public NetworkDictionary<PlayerRef, int> playerWeaponVotes => default;

    private List<WeaponSelectSlot> _weaponSelectSlots = new List<WeaponSelectSlot>();

    private float _weaponSelectVoteTimeMax = 15f;
    
    // 로컬 플레이어용
    private float _localWeaponSelectTimer;
    private bool _localUIActive;
    private int[] _randomWeaponIndices = new int[3]; // 로컬 플레이어가 보는 랜덤 무기 인덱스

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

    void Start()
    {
        weaponUI.SetActive(false);
    }

    public override void FixedUpdateNetwork()
    {
        if (Input.GetKeyDown(KeyCode.M) && Runner.IsSceneAuthority)
        {
            // 호스트가 누른 경우 모든 클라이언트에게 전파
            RPC_OpenWeaponSelectUI();
        }
    }

    void Update()
    {
        if (!Object || !Object.IsValid) return;

        // 로컬 플레이어가 무기 선택 UI를 보고 있을 때만 타이머 처리
        if (_localUIActive)
        {
            _localWeaponSelectTimer -= Time.deltaTime;
            
            if (timerFillImage != null)
                timerFillImage.fillAmount = Mathf.Max(0, _localWeaponSelectTimer / _weaponSelectVoteTimeMax);
            
            if (timerText != null)
                timerText.text = $"{Mathf.Max(0, _localWeaponSelectTimer):F0}";
            
            // 타이머 종료 시 자동으로 랜덤 무기 선택
            if (_localWeaponSelectTimer <= 0)
            {
                AutoSelectRandomWeapon();
                CloseWeaponUI();
            }
        }
    }

    private void AutoSelectRandomWeapon()
    {
        // 표시된 3개 무기 중 랜덤 선택 (1, 2, 3)
        int randomOrder = Random.Range(1, 4);
        int weaponIndex = _randomWeaponIndices[randomOrder - 1];
        WeaponScriptableObject selectedWeapon = weaponSOs[weaponIndex];
        
        Debug.Log($"[WeaponManager] Auto selected weapon: {selectedWeapon.weaponName} (index: {weaponIndex})");
        
        // 로컬 플레이어의 무기 장착
        EquipWeaponToLocalPlayer(selectedWeapon);
        
        // 네트워크에 투표 등록
        RPC_RegisterWeaponVote(Runner.LocalPlayer, weaponIndex);
    }

    private void CloseWeaponUI()
    {
        _localUIActive = false;
        if (weaponUI != null)
        {
            weaponUI.SetActive(false);
        }
        
        // 플레이어 입력 다시 활성화
        EnablePlayerInput();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OpenWeaponSelectUI()
    {
        // 호스트 호출 시 모든 클라이언트가 이 메서드 실행
        WeaponSelect();
    }

    public void WeaponSelect()
    {
        if (_localUIActive)
        {
            return;
        }

        // 로컬 플레이어를 위해 UI 표시
        _localUIActive = true;
        _localWeaponSelectTimer = _weaponSelectVoteTimeMax;
        
        if (weaponUI != null)
        {
            weaponUI.SetActive(true);
        }
        
        // 플레이어 입력 비활성화
        DisablePlayerInput();
        
        ShowWeaponOptions();
    }

    private void DisablePlayerInput()
    {
        NetworkObject playerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (playerObj != null && playerObj.TryGetComponent(out Starter.Platformer.PlayerInput playerInput))
        {
            playerInput.gameObject.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled = false;
            Debug.Log("[WeaponManager] Player input disabled");
        }
    }

    private void EnablePlayerInput()
    {
        NetworkObject playerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (playerObj != null && playerObj.TryGetComponent(out Starter.Platformer.PlayerInput playerInput))
        {
            playerInput.gameObject.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled = true;
            Debug.Log("[WeaponManager] Player input enabled");
        }
    }

    private void ShowWeaponOptions()
    {
        // 기존 슬롯 삭제
        foreach (Transform child in weaponSelectPanel)
        {
            Destroy(child.gameObject);
        }
        _weaponSelectSlots.Clear();

        // 무기 옵션 표시 (최대 3개) - 각 플레이어마다 랜덤
        int optionCount = Mathf.Min(3, weaponSOs.Length);
        
        // 랜덤 무기 인덱스 생성 (중복 없음)
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < weaponSOs.Length; i++)
        {
            availableIndices.Add(i);
        }
        
        Debug.Log($"[WeaponManager] Showing {optionCount} random weapon options");
        
        for (int i = 0; i < optionCount; i++)
        {
            // 남은 인덱스 중에서 랜덤 선택
            int randomIdx = Random.Range(0, availableIndices.Count);
            int weaponIndex = availableIndices[randomIdx];
            availableIndices.RemoveAt(randomIdx);
            
            _randomWeaponIndices[i] = weaponIndex;
            
            GameObject slotObj = Instantiate(weaponSelectPrefab, weaponSelectPanel);
            WeaponSelectSlot slot = slotObj.GetComponent<WeaponSelectSlot>();
            if (slot != null)
            {
                slot.Order = i + 1;
                slot.Set(weaponSOs[weaponIndex]);
                _weaponSelectSlots.Add(slot);
                Debug.Log($"[WeaponManager] Slot {i + 1}: {weaponSOs[weaponIndex].weaponName}");
            }
            else
            {
                Debug.LogWarning($"[WeaponManager] weaponSelectPrefab does not have WeaponSelectSlot component!");
            }
        }
    }

    public void OnWeaponSelectButtonClicked(int order)
    {
        if (!_localUIActive) return;

        // 선택한 무기의 실제 인덱스 가져오기
        int weaponIndex = _randomWeaponIndices[order - 1];
        WeaponScriptableObject selectedWeapon = weaponSOs[weaponIndex];
        
        Debug.Log($"[WeaponManager] Selected weapon slot {order}, actual weapon: {selectedWeapon.weaponName}");
        
        // 로컬 플레이어의 무기 장착 (즉시 적용)
        EquipWeaponToLocalPlayer(selectedWeapon);
        
        // 네트워크에 투표 등록 (weaponIndex 전달)
        RPC_RegisterWeaponVote(Runner.LocalPlayer, weaponIndex);
        
        // 개인별로 UI 닫기
        CloseWeaponUI();
    }

    private void EquipWeaponToLocalPlayer(WeaponScriptableObject weapon)
    {
        // 로컬 플레이어의 Player 컴포넌트 찾기
        NetworkObject playerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        
        if (playerObj != null && playerObj.TryGetComponent(out WeaponController weaponController))
        {
            // 무기 인덱스 찾기
            int weaponIndex = System.Array.IndexOf(weaponSOs, weapon);
            if (weaponIndex >= 0)
            {
                // RPC를 통해 무기 장착 (State Authority에서만 실행)
                weaponController.RPC_EquipWeapon(weaponIndex);
                Debug.Log($"[WeaponManager] Equipped weapon: {weapon.weaponName}");
            }
            else
            {
                Debug.LogWarning($"[WeaponManager] Weapon not found in weaponSOs array: {weapon.weaponName}");
            }
        }
        else
        {
            Debug.LogWarning($"[WeaponManager] Could not find WeaponController on local player!");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RegisterWeaponVote(PlayerRef player, int weaponIndex)
    {
        // 1. 이미 같은 무기를 투표했으면 투표 취소, 아니면 새로 등록
        if (playerWeaponVotes.TryGet(player, out int currentVote) && currentVote == weaponIndex)
        {
            playerWeaponVotes.Remove(player);
            Debug.Log($"[WeaponManager] Player {player} vote cancelled for weapon {weaponSOs[weaponIndex].weaponName}");
        }
        else
        {
            // 2. 투표 등록 또는 변경
            playerWeaponVotes.Set(player, weaponIndex);
            Debug.Log($"[WeaponManager] Player {player} voted for weapon: {weaponSOs[weaponIndex].weaponName} (index: {weaponIndex})");
        }

        UpdateVoteVisuals();
    }

    private void UpdateVoteVisuals()
    {
        // 투표 현황을 UI에 표시 (필요에 따라 구현)
        // 각 슬롯별로 투표한 플레이어 정보 표시 등
    }
}


