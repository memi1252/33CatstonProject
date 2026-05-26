using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 맨홀 뚜껑 위치 마커. 씬에 여러 개 배치해 두면
/// <see cref="ManholeBossEnemy"/> 가 이 위치들 중 하나에서 떠오른다.
/// 시각 효과(뚜껑 열림/닫힘, 증기 분출 등)는 UnityEvent 로 연결한다.
/// </summary>
public class ManholeCover : MonoBehaviour
{
    private static readonly List<ManholeCover> _all = new List<ManholeCover>();
    public static IReadOnlyList<ManholeCover> All => _all;

    [Tooltip("보스가 떠오를 때 호출. 뚜껑 열림 애니, 증기 VFX, 사운드 등을 연결한다.")]
    public UnityEvent OnBossEmerge;

    [Tooltip("보스가 사라질 때 호출. 뚜껑 닫힘 애니 등을 연결한다.")]
    public UnityEvent OnBossSubmerge;

    private void OnEnable()  { _all.Add(this); }
    private void OnDisable() { _all.Remove(this); }

    public void NotifyBossEmerge()   => OnBossEmerge?.Invoke();
    public void NotifyBossSubmerge() => OnBossSubmerge?.Invoke();

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.05f, new Vector3(1.2f, 0.05f, 1.2f));
    }
}
