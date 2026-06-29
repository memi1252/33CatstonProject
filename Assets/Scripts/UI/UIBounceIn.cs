using UnityEngine;
using MoreMountains.Feedbacks;

namespace Starter.UI
{
    /// <summary>
    /// 패널이 활성화될 때 위에서 내려오면서 통통 튀는(스프링) 애니메이션을 재생한다.
    /// Feel(MMFeedbacks)의 MMF_PositionSpring을 사용한다 — 별도 커브 설정 없이 Damping/Frequency만으로 자연스러운 바운스가 나온다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIBounceIn : MonoBehaviour
    {
        [Tooltip("내려오기 시작하는 위치의 Y 오프셋(원래 위치보다 위쪽)")]
        public float dropFromOffsetY = 250f;

        [Tooltip("스프링이 멈추는 속도. 낮을수록 오래 튕긴다")]
        [Range(0.05f, 1f)] public float damping = 0.45f;

        [Tooltip("튕기는 빠르기")]
        public float frequency = 5f;

        private RectTransform _rect;
        private MMF_Player _player;
        private MMF_PositionSpring _spring;
        private Vector2 _restPosition;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            BuildPlayer();
        }

        private void BuildPlayer()
        {
            // 에디터에서 플레이 중이 아닐 때 AddComponent<MMF_Player>()가 실패해서 에러가 나던 문제를 막기 위해
            // 실제 플레이(런타임) 중에만 Feel 셋업을 한다.
            if (!Application.isPlaying) return;

            _player = gameObject.AddComponent<MMF_Player>();
            _spring = new MMF_PositionSpring
            {
                AnimatePositionTarget = _rect,
                Space = MMF_PositionSpring.Spaces.RectTransform,
                Mode = MMF_PositionSpring.Modes.MoveTo,
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

        private void OnEnable()
        {
            if (_player == null) return;
            _restPosition = _rect.anchoredPosition;

            // 시작 위치를 위쪽으로 옮겨두고, 목표 위치(원래 자리)로 스프링 이동시킨다.
            _rect.anchoredPosition = _restPosition + new Vector2(0f, dropFromOffsetY);
            _spring.MoveToPositionMin = _restPosition;
            _spring.MoveToPositionMax = _restPosition;

            // MMF_PositionSpring은 시작 위치를 Initialization 시점에 캐싱하므로,
            // 방금 위로 옮긴 위치를 새 시작점으로 다시 캐싱해줘야 거기서부터 튕겨 내려온다.
            _player.Initialization();
            _player.PlayFeedbacks();
        }
    }
}
