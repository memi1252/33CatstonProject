using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    [Tooltip("비워두면 기본 UIClick 소리 사용")]
    [SerializeField] private AudioClip overrideClip;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (overrideClip != null)
                SoundManager.Instance?.PlaySFX(overrideClip);
            else
                SoundManager.Instance?.PlayUIClick();
        });
    }
}
