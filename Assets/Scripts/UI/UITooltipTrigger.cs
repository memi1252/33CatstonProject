using UnityEngine;
using UnityEngine.EventSystems;

namespace Starter.UI
{
    /// <summary>
    /// 이 컴포넌트가 붙은 UI 위에 마우스를 올리면 UITooltip에 제목/본문을 표시한다.
    /// </summary>
    public class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string title;
        public string body;

        public void OnPointerEnter(PointerEventData eventData)
        {
            UITooltip.Instance?.Show(title, body);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UITooltip.Instance?.Hide();
        }

        private void OnDisable()
        {
            UITooltip.Instance?.Hide();
        }
    }
}
