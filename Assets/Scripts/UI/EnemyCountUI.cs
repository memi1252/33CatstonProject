using UnityEngine;
using TMPro;

namespace Starter.UI
{
    /// <summary>
    /// 현재 씬에 살아있는 적 수를 표시한다. Enemy NetworkObject는 모든 클라이언트에 복제되므로
    /// 네트워크 동기화 없이 각자 로컬에서 세도 정확하다.
    /// </summary>
    public class EnemyCountUI : MonoBehaviour
    {
        public TextMeshProUGUI countText;

        [Tooltip("매번 FindObjectsByType을 쓰지 않도록 갱신 주기(초)")]
        public float refreshInterval = 0.25f;

        private float _timer;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = refreshInterval;

            int count = 0;
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(UnityEngine.FindObjectsSortMode.None))
            {
                if (enemy != null && !enemy.isDead) count++;
            }

            // 전투 웨이브 중(살아있는 적이 있을 때)에만 표시한다. 로비/무기선택/보상 화면 등 적이 없을 땐 숨김.
            // 주의: 이 스크립트가 countText와 같은 GameObject에 있으므로 GameObject 자체를 SetActive(false)하면
            // Update()가 멈춰서 적이 다시 생겨도 영영 다시 켜지지 않는다. enabled만 꺼서 렌더링만 숨긴다.
            if (countText != null)
            {
                countText.enabled = count > 0;
                countText.text = $"적: {count}";
            }
        }
    }
}
