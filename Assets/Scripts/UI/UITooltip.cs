using UnityEngine;
using TMPro;

namespace Starter.UI
{
    /// <summary>
    /// 마우스 근처에 따라다니는 간단한 텍스트 툴팁. 씬에 하나만 존재하는 싱글톤.
    /// UITooltipTrigger가 Show()/Hide()를 호출한다.
    /// </summary>
    public class UITooltip : MonoBehaviour
    {
        public static UITooltip Instance { get; private set; }

        public RectTransform panel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI bodyText;
        public Vector2 cursorOffset = new Vector2(16f, -16f);

        private RectTransform _canvasRect;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            Hide();
        }

        private void Update()
        {
            if (panel == null || !panel.gameObject.activeSelf) return;
            FollowCursor();
        }

        public void Show(string title, string body)
        {
            if (panel == null) return;
            panel.gameObject.SetActive(true);
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
            FollowCursor();
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void FollowCursor()
        {
            if (_canvasRect == null) return;
            Vector2 localPoint;
            var cam = _canvasRect.GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
            UnityEngine.RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Input.mousePosition, cam, out localPoint);
            panel.anchoredPosition = localPoint + cursorOffset;
        }
    }
}
