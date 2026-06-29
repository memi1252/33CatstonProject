using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Feedbacks;

namespace Starter.UI
{
    /// <summary>
    /// 슬라이더 값이 바뀔 때마다(드래그 중) 핸들이 살짝 튕기는 효과.
    /// 같은 Slider가 붙은 오브젝트(또는 그 자식)에 붙이면 자동으로 핸들을 찾아 적용한다.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class UISliderBump : MonoBehaviour
    {
        [Tooltip("값이 바뀔 때 핸들이 커지는 배율")]
        public float bumpScale = 1.3f;

        [Range(0.05f, 1f)] public float damping = 0.35f;
        public float frequency = 10f;

        private Slider _slider;
        private RectTransform _handleRect;
        private MMF_Player _player;
        private MMF_ScaleSpring _spring;
        private Vector3 _baseScale;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            _handleRect = _slider.handleRect;
            if (_handleRect == null) return;

            _baseScale = _handleRect.localScale;

            // 에디터에서 플레이 중이 아닐 때 AddComponent<MMF_Player>()가 실패해서 에러가 나던 문제를 막기 위해
            // 실제 플레이(런타임) 중에만 Feel 셋업을 한다.
            if (!Application.isPlaying) return;

            _player = _handleRect.gameObject.AddComponent<MMF_Player>();
            _spring = new MMF_ScaleSpring
            {
                AnimateScaleTarget = _handleRect,
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
            if (_slider != null) _slider.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnDisable()
        {
            if (_slider != null) _slider.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(float _)
        {
            if (_handleRect == null || _player == null) return;
            _spring.MoveToScaleMin = _baseScale * bumpScale;
            _spring.MoveToScaleMax = _baseScale * bumpScale;
            _player.PlayFeedbacks();
            // 살짝 커진 뒤 바로 다시 원래 크기로 스프링 이동시켜서 "톡" 하는 느낌을 만든다.
            Invoke(nameof(SettleBack), 0.06f);
        }

        private void SettleBack()
        {
            if (_handleRect == null || _player == null) return;
            _spring.MoveToScaleMin = _baseScale;
            _spring.MoveToScaleMax = _baseScale;
            _player.PlayFeedbacks();
        }
    }
}
