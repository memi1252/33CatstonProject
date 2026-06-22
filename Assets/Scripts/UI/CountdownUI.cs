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

    // UIManager에 연결이 없을 때 동적으로 생성
    public static CountdownUI GetOrCreate()
    {
        if (UIManager.Instance != null && UIManager.Instance.countdownUI != null)
            return UIManager.Instance.countdownUI;

        // 동적 생성
        var go = new GameObject("CountdownUI_Dynamic");
        DontDestroyOnLoad(go);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        go.AddComponent<UnityEngine.UI.CanvasScaler>();

        var textGo = new GameObject("CountText");
        textGo.transform.SetParent(go.transform, false);
        var rect = textGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400, 300);
        rect.anchoredPosition = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 200;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        var outline = textGo.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0, 0, 0, 1f);
        outline.effectDistance = new Vector2(4, -4);

        var cd = go.AddComponent<CountdownUI>();
        cd.countText = tmp;

        if (UIManager.Instance != null)
            UIManager.Instance.countdownUI = cd;

        go.SetActive(false);
        return cd;
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
            SetScale(1.6f);
            yield return LerpScale(1.6f, 1f, 0.3f);
            yield return new WaitForSeconds(0.7f);
        }
        if (countText != null) countText.text = "GO!";
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
