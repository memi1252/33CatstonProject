using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 등장 시 화면 하단에 표시되는 대형 HP 바.
/// ShowBoss(enemy, bossName)로 활성화, 보스 사망 시 자동 숨김.
/// </summary>
public class BossHPUI : MonoBehaviour
{
    [Header("레퍼런스")]
    public Image hpFillImage;
    public Image hpDelayFillImage;   // HP 감소 후 천천히 따라오는 지연 바 (선택)
    public TextMeshProUGUI bossNameText;
    public CanvasGroup canvasGroup;

    [Header("연출")]
    [Tooltip("등장/퇴장 페이드 시간(초)")]
    public float fadeDuration = 0.5f;
    [Tooltip("HP 지연 바가 실제 HP를 따라오는 속도")]
    public float delayBarSpeed = 1.5f;

    private Enemy _boss;
    private float _maxHealth;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_boss == null || _maxHealth <= 0f) return;

        // 보스 사망 감지
        if (_boss.isDead)
        {
            HideBoss();
            return;
        }

        float ratio = Mathf.Clamp01(_boss.health / _maxHealth);
        if (hpFillImage != null)
            hpFillImage.fillAmount = ratio;

        if (hpDelayFillImage != null)
            hpDelayFillImage.fillAmount = Mathf.MoveTowards(
                hpDelayFillImage.fillAmount, ratio, delayBarSpeed * Time.deltaTime);
    }

    public void ShowBoss(Enemy boss, string bossName)
    {
        _boss = boss;
        // Runner.Spawn은 StateAuthority 기준 Spawned() 완료 후 반환되므로 health는 이미
        // EnemyGlobalBuffs 보너스까지 적용된 최종값(maxHealth)이다. startingHealth를 쓰면
        // 보너스 적용분만큼 항상 꽉 차 보이는 버그가 생기므로 반드시 health를 사용해야 한다.
        _maxHealth = boss.health > 0f ? boss.health : boss.startingHealth;

        if (bossNameText != null)
            bossNameText.text = bossName;

        if (hpFillImage != null) hpFillImage.fillAmount = 1f;
        if (hpDelayFillImage != null) hpDelayFillImage.fillAmount = 1f;

        gameObject.SetActive(true);
        Fade(1f);
    }

    public void HideBoss()
    {
        _boss = null;
        Fade(0f, () => gameObject.SetActive(false));
    }

    private void Fade(float target, System.Action onComplete = null)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(target, onComplete));
    }

    private IEnumerator FadeRoutine(float target, System.Action onComplete)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = target;
        onComplete?.Invoke();
    }
}
