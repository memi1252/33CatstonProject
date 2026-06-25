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

            _player = gameObject.AddComponent<MMF_Player>();
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
            _spring.MoveToScaleMin = targetScale;
            _spring.MoveToScaleMax = targetScale;
            _player.PlayFeedbacks();
        }
    }
}
