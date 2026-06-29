using UnityEngine;
using UnityEngine.EventSystems;
using MoreMountains.Feedbacks;

namespace Starter.UI
{
    /// <summary>
    /// 마우스를 올리면 살짝 커지고, 벗어나면 원래 크기로 돌아오는 버튼 호버 효과.
    /// Feel(MMFeedbacks)의 MMF_ScaleSpring(MoveTo 모드)을 사용해 자연스러운 탄성으로 움직인다.
    /// </summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("호버 시 커지는 배율")]
        public float hoverScale = 1.15f;

        [Tooltip("스프링이 멈추는 속도. 낮을수록 오래 튕긴다")]
        [Range(0.05f, 1f)] public float damping = 0.5f;

        [Tooltip("튕기는 빠르기")]
        public float frequency = 8f;

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

            // 슬롯에 이미 다른 용도(등장 애니메이션 등)의 MMF_Player가 붙어있으면 AddComponent가 실패해서
            // _player가 null로 남고 바로 다음 줄에서 NullReferenceException이 났다. 같은 플레이어를 공유하면
            // PlayFeedbacks() 호출 시 무관한 다른 효과까지 같이 재생되니, 전용 자식 오브젝트에 따로 만든다.
            if (GetComponent<MMF_Player>() != null)
            {
                var holder = new GameObject("HoverScaleFeel");
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
            };
            _player.AddFeedback(_spring);
            _player.Initialization();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PlayTo(_baseScale * hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PlayTo(_baseScale);
        }

        private void PlayTo(Vector3 targetScale)
        {
            if (_player == null) return;
            _spring.MoveToScaleMin = targetScale;
            _spring.MoveToScaleMax = targetScale;
            _player.PlayFeedbacks();
        }
    }
}
