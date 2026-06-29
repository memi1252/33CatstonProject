using UnityEngine;
using MoreMountains.Feedbacks;

namespace Starter.UI
{
    /// <summary>
    /// 활성화될 때 스케일 0에서 통통 튀며 커지는 등장 효과.
    /// UIBounceIn(위치 기반)과 달리 anchoredPosition을 건드리지 않아서,
    /// HorizontalLayoutGroup/VerticalLayoutGroup 등 레이아웃이 위치를 관리하는 자식(슬롯 카드 등)에도 안전하게 쓸 수 있다.
    /// </summary>
    public class UIScalePopIn : MonoBehaviour
    {
        [Tooltip("스프링이 멈추는 속도. 낮을수록 오래 튕긴다")]
        [Range(0.05f, 1f)] public float damping = 0.45f;

        [Tooltip("튕기는 빠르기")]
        public float frequency = 6f;

        private RectTransform _rect;
        private MMF_Player _player;
        private MMF_ScaleSpring _spring;
        private Vector3 _baseScale;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _baseScale = _rect.localScale;

            // 에디터에서 플레이 중이 아닐 때 AddComponent<MMF_Player>()가 실패해서 에러가 나던 문제를 막기 위해
            // 실제 플레이(런타임) 중에만 Feel 셋업을 한다.
            if (!Application.isPlaying) return;

            // 슬롯에 이미 다른 용도의 MMF_Player가 붙어있으면 AddComponent가 실패해서 _player가 null로
            // 남고 곧바로 NullReferenceException이 났다(BuffSlot 등에서 반복 발생). 전용 자식에 따로 만든다.
            if (GetComponent<MMF_Player>() != null)
            {
                var holder = new GameObject("ScalePopInFeel");
                holder.transform.SetParent(transform, false);
                _player = holder.AddComponent<MMF_Player>();
            }
            else
            {
                _player = gameObject.AddComponent<MMF_Player>();
            }
            _spring = new MMF_ScaleSpring
            {
                AnimateScaleTarget = _rect,
                Mode = MMF_ScaleSpring.Modes.MoveTo,
                DampingX = damping,
                DampingY = damping,
                DampingZ = damping,
                FrequencyX = frequency,
                FrequencyY = frequency,
                FrequencyZ = frequency,
                MoveToScaleMin = _baseScale,
                MoveToScaleMax = _baseScale,
            };
            _player.AddFeedback(_spring);
            _player.Initialization();
        }

        private void OnEnable()
        {
            if (_player == null) return;
            _rect.localScale = Vector3.zero;
            _player.Initialization(); // 0으로 만든 현재 스케일을 새 시작점으로 다시 캐싱
            _player.PlayFeedbacks();
        }
    }
}
