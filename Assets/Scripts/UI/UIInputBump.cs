using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Starter.UI
{
    /// <summary>
    /// 인풋필드에 한 글자씩 입력할 때마다 살짝 통통 튀는 펀치 효과.
    /// (입력 중인 텍스트 자체에 Febucci 글자별 애니메이션을 적용하면 입력 중 리치텍스트 태그가
    /// 그대로 노출되거나 캐럿/렌더링과 충돌해서, 대신 인풋필드 전체를 스케일 펀치하는 방식으로 같은 느낌을 낸다.)
    /// 레거시 UI.InputField와 TMP_InputField 둘 다 지원.
    ///
    /// 원래는 Feel의 MMF_ScaleSpring(명시적 오일러 적분)을 썼는데, 빠르게 타이핑하면 매 글자마다
    /// 스프링이 멈추기 전에 재트리거되어 적분이 NaN/Infinity로 발산하고, 한 번 발산하면
    /// while(!LowVelocity) 루프가 NaN 비교 때문에 절대 끝나지 않아 매 프레임 "transform.localScale
    /// assign attempt ... not valid" 경고를 영원히 쏟아내는 버그가 있었다. 그래서 물리 스프링 대신
    /// 시간 기반(t∈[0,1]) 보간으로 바꿔서 어떤 입력 패턴에서도 절대 발산할 수 없게 했다.
    /// </summary>
    public class UIInputBump : MonoBehaviour
    {
        [Tooltip("타이핑할 때 커지는 배율")]
        public float bumpScale = 1.06f;

        [Tooltip("커지는/줄어드는 데 걸리는 시간(초)")]
        public float punchDuration = 0.08f;

        private InputField _legacyInput;
        private TMP_InputField _tmpInput;
        private RectTransform _rect;
        private Vector3 _baseScale;
        private bool _isBumped;
        private Coroutine _punchCoroutine;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _legacyInput = GetComponent<InputField>();
            _tmpInput = GetComponent<TMP_InputField>();
            if (_legacyInput == null && _tmpInput == null) return;

            _baseScale = _rect.localScale;
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

            if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
            _isBumped = false;
            if (_rect != null) _rect.localScale = _baseScale;
        }

        private void OnValueChanged(string _)
        {
            // 이미 부풀어 있으면 재트리거하지 않고, 입력이 멈췄을 때만 줄어들게 한다.
            if (!_isBumped)
            {
                _isBumped = true;
                StartPunch(_baseScale * bumpScale);
            }

            CancelInvoke(nameof(SettleBack));
            Invoke(nameof(SettleBack), 0.05f);
        }

        private void SettleBack()
        {
            _isBumped = false;
            StartPunch(_baseScale);
        }

        private void StartPunch(Vector3 target)
        {
            if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
            _punchCoroutine = StartCoroutine(PunchTo(target));
        }

        // t를 0~1로 정규화해서 보간하므로 입력 빈도나 프레임 스파이크와 무관하게 항상 유한한 값으로 수렴한다.
        private System.Collections.IEnumerator PunchTo(Vector3 target)
        {
            Vector3 start = _rect.localScale;
            float duration = Mathf.Max(punchDuration, 0.001f);
            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                _rect.localScale = Vector3.LerpUnclamped(start, target, Mathf.Clamp01(t));
                yield return null;
            }

            _rect.localScale = target;
            _punchCoroutine = null;
        }
    }
}
