using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class CountdownUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private TMP_FontAsset galmuriFont;

    private void Awake()
    {
        if (galmuriFont != null && countText != null)
            countText.font = galmuriFont;
        gameObject.SetActive(false);
    }

    public static CountdownUI GetOrCreate()
    {
        if (UIManager.Instance != null && UIManager.Instance.countdownUI != null)
            return UIManager.Instance.countdownUI;

        Debug.LogWarning("[CountdownUI] UIManager.countdownUI가 연결되지 않았습니다. LobbyScene의 UIManager를 확인하세요.");
        return null;
    }

    public void StartCountdown(int seconds, Action onComplete)
    {
        gameObject.SetActive(true);
        StartCoroutine(CountdownCoroutine(seconds, onComplete));
    }

    private IEnumerator CountdownCoroutine(int seconds, Action onComplete)
    {
        for (int i = seconds; i > 0; i--)
        {
            if (countText != null) countText.text = i.ToString();
            SoundManager.Instance?.PlayCountdownTick();
            SetScale(1.6f);
            yield return LerpScale(1.6f, 1f, 0.3f);
            yield return new WaitForSeconds(0.7f);
        }
        if (countText != null) countText.text = "GO!";
        SoundManager.Instance?.PlayCountdownGo();
        SetScale(1.8f);
        yield return LerpScale(1.8f, 1f, 0.3f);
        yield return new WaitForSeconds(0.5f);
        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    private void SetScale(float s) => transform.localScale = Vector3.one * s;

    private IEnumerator LerpScale(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        transform.localScale = Vector3.one * to;
    }
}
