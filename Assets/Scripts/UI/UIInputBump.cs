using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Feedbacks;

namespace Starter.UI
{
    /// <summary>
    /// 인풋필드에 한 글자씩 입력할 때마다 살짝 통통 튀는 펀치 효과.
    /// (입력 중인 텍스트 자체에 Febucci 글자별 애니메이션을 적용하면 입력 중 리치텍스트 태그가
    /// 그대로 노출되거나 캐럿/렌더링과 충돌해서, 대신 인풋필드 전체를 스케일 펀치하는 방식으로 같은 느낌을 낸다.)
    /// 레거시 UI.InputField와 TMP_InputField 둘 다 지원.
    /// </summary>
    public class UIInputBump : MonoBehaviour
    {
        [Tooltip("타이핑할 때 커지는 배율")]
        public float bumpScale = 1.06f;

        [Range(0.05f, 1f)] public float damping = 0.4f;
        public float frequency = 10f;

        private InputField _legacyInput;
        private TMP_InputField _tmpInput;
        private RectTransform _rect;
        private MMF_Player _player;
        private MMF_ScaleSpring _spring;
        private Vector3 _baseScale;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _legacyInput = GetComponent<InputField>();
            _tmpInput = GetComponent<TMP_InputField>();
            if (_legacyInput == null && _tmpInput == null) return;

            _baseScale = _rect.localScale;

            // 에디터에서 플레이 중이 아닐 때 AddComponent<MMF_Player>()가 실패해서 에러가 나던 문제를 막기 위해
            // 실제 플레이(런타임) 중에만 Feel 셋업을 한다.
            if (!Application.isPlaying) return;

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
                MoveToScaleMin = _baseScale,
                MoveToScaleMax = _baseScale,
            };
            _player.AddFeedback(_spring);
            _player.Initialization();
        }

        private void OnEnable()
        {
            _legacyInput?.onValueChanged.AddListener(OnValueChanged);
            _tmpInput?.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnDisable()
        {
            _legacyInput?.onValueChanged.RemoveListener(OnValueChanged);
            _tmpInput?.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(string _)
        {
            if (_player == null) return;
            _spring.MoveToScaleMin = _baseScale * bumpScale;
            _spring.MoveToScaleMax = _baseScale * bumpScale;
            _player.PlayFeedbacks();
            CancelInvoke(nameof(SettleBack));
            Invoke(nameof(SettleBack), 0.05f);
        }

        private void SettleBack()
        {
            if (_player == null) return;
            _spring.MoveToScaleMin = _baseScale;
            _spring.MoveToScaleMax = _baseScale;
            _player.PlayFeedbacks();
        }
    }
}
