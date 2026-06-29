using UnityEngine;

/// <summary>
/// 항상 메인 카메라를 바라보게 회전시킨다. World Space UI(포탈 안내 텍스트 등)에 붙여서
/// 어느 방향에서 봐도 글씨가 똑바로 보이게 한다.
/// </summary>
public class Billboard : MonoBehaviour
{
    [Tooltip("켜면 카메라 쪽으로 그냥 고개를 돌리듯(Y축만) 회전한다. 끄면 카메라를 정면으로 바라보게 완전히 회전한다.")]
    public bool yAxisOnly = false;

    private void LateUpdate()
    {
        if (Camera.main == null) return;

        if (yAxisOnly)
        {
            Vector3 dir = transform.position - Camera.main.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
