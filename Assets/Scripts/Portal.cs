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
    [Tooltip("페이드 인/아웃 길이(초)")]
    [SerializeField] private float fadeDuration = 0.4f;
    [Tooltip("캐릭터 사라진 뒤 페이드아웃까지 대기 시간(초). 출발 VFX 보여주는 시간.")]
    [SerializeField] private float departHoldDuration = 0.6f;
    [Tooltip("도착지 페이드인 후 도착 VFX 재생까지 대기 시간(초)")]
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

    private void Update()
    {
        if (_localPlayerInRange == null) return;
        if (destination == null) return;
        if (TeleportManager.Instance != null && TeleportManager.Instance.IsBusy) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (worldPromptObject != null) worldPromptObject.SetActive(false);
            _localPlayerInRange.StartTeleport(this);
        }
    }

    // Player가 텔레포트 시퀀스에서 사용
    public Vector3 DestinationPosition => destination != null ? destination.position : transform.position;
    public Quaternion DestinationRotation => destination != null ? destination.rotation : transform.rotation;
    public string PortalName => portalName;
    public float FadeDuration => Mathf.Max(0f, fadeDuration);
    public float DepartHoldDuration => Mathf.Max(0f, departHoldDuration);
    public float ArriveHoldDuration => Mathf.Max(0f, arriveHoldDuration);
}