using UnityEngine;
using Starter.Platformer;

/// <summary>
/// 텔레포트 포털. 트리거 안에 로컬 플레이어가 들어오면 안내 UI를 표시하고
/// 상호작용 키를 누르면 확인창을 띄워 텔레포트를 수행합니다.
/// 트리거용 Collider(isTrigger = true)와 destination Transform이 필요합니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("이동할 도착 지점. 회전도 함께 적용됩니다.")]
    [SerializeField] private Transform destination;
    [Tooltip("확인창에 표시될 포털 이름")]
    [SerializeField] private string portalName = "이곳";

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [Tooltip("근접 시 표시될 월드 안내 UI(자식 GameObject 추천). 시작 시 자동 비활성화.")]
    [SerializeField] private GameObject worldPromptObject;

    [Header("Timing")]
    [Tooltip("페이드 인/아웃 길이(초). 0이면 페이드를 사용하지 않음.")]
    [SerializeField] private float fadeDuration = 0f;
    [Tooltip("캐릭터가 사라진 뒤 카메라가 도착지로 이동할 시간(초). 이 시간 동안 캐릭터는 보이지 않음.")]
    [SerializeField] private float cameraTravelDuration = 0.6f;
    [Tooltip("캐릭터 사라진 뒤 카메라 이동 전 출발 VFX 보여주는 시간(초)")]
    [SerializeField] private float departHoldDuration = 0.6f;
    [Tooltip("도착 후 도착 VFX 재생까지 대기 시간(초)")]
    [SerializeField] private float arriveHoldDuration = 0.2f;

    private Player _localPlayerInRange;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        if (worldPromptObject != null) worldPromptObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        if (player == null || !player.HasInputAuthority) return;
        _localPlayerInRange = player;
        if (worldPromptObject != null) worldPromptObject.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        if (player == null || player != _localPlayerInRange) return;
        _localPlayerInRange = null;
        if (worldPromptObject != null) worldPromptObject.SetActive(false);
    }

    // 트리거 범위 안에 있는 동안 포털 GameObject가 비활성화되면(스테이지 전환 시 잠금/해제 등)
    // OnTriggerExit가 호출되지 않아 _localPlayerInRange가 그대로 남는다.
    // 이 상태로 포털이 다시 활성화되면 플레이어가 어디 있든 F키만 눌러도 텔레포트가 발동하는 버그가 생긴다.
    private void OnDisable()
    {
        _localPlayerInRange = null;
        if (worldPromptObject != null) worldPromptObject.SetActive(false);
    }

private void Update()
    {
        if (_localPlayerInRange == null) return;
        if (destination == null) return;
        if (TeleportManager.Instance != null && TeleportManager.Instance.IsBusy) return;

        // 안전장치: 실제로 포털 근처에 있는지 거리로 한 번 더 확인 (위 OnDisable로도 막히지만 이중 방어)
        const float rangeCheckDist = 5f;
        if (Vector3.Distance(transform.position, _localPlayerInRange.transform.position) > rangeCheckDist)
        {
            _localPlayerInRange = null;
            if (worldPromptObject != null) worldPromptObject.SetActive(false);
            return;
        }

        if (Input.GetKeyDown(interactKey))
        {
            if (worldPromptObject != null) worldPromptObject.SetActive(false);
            SoundManager.Instance?.PlayPortalTeleport();
            _localPlayerInRange.StartTeleport(this);

            if (StageManager.Instance != null)
                StageManager.Instance.NotifyExitPortalUsed(this, _localPlayerInRange.Runner.LocalPlayer);
        }
    }

    // 도착 지점을 런타임에 외부(StageManager 등)에서 지정할 수 있게 한다.
    public void SetDestination(Transform dest)
    {
        if (dest != null) destination = dest;
    }

    // Player가 텔레포트 시퀀스에서 사용
    public Vector3 DestinationPosition => destination != null ? destination.position : transform.position;
    public Quaternion DestinationRotation => destination != null ? destination.rotation : transform.rotation;
    public string PortalName => portalName;
    public float FadeDuration => Mathf.Max(0f, fadeDuration);
    public float DepartHoldDuration => Mathf.Max(0f, departHoldDuration);
    public float ArriveHoldDuration => Mathf.Max(0f, arriveHoldDuration);
    public float CameraTravelDuration => Mathf.Max(0f, cameraTravelDuration);
}