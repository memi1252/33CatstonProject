using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Labels")]
    [SerializeField] private TextMeshProUGUI masterLabel;
    [SerializeField] private TextMeshProUGUI bgmLabel;
    [SerializeField] private TextMeshProUGUI sfxLabel;

    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        if (SoundManager.Instance == null) return;

        masterSlider.value = SoundManager.Instance.GetMasterVolume();
        bgmSlider.value = SoundManager.Instance.GetBGMVolume();
        sfxSlider.value = SoundManager.Instance.GetSFXVolume();

        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);

        UpdateLabels();
    }

    private void OnMasterChanged(float value)
    {
        SoundManager.Instance?.SetMasterVolume(value);
        UpdateLabels();
    }

    private void OnBGMChanged(float value)
    {
        SoundManager.Instance?.SetBGMVolume(value);
        UpdateLabels();
    }

    private void OnSFXChanged(float value)
    {
        SoundManager.Instance?.SetSFXVolume(value);
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (masterLabel) masterLabel.text = $"마스터: {Mathf.RoundToInt(masterSlider.value * 100)}%";
        if (bgmLabel)    bgmLabel.text    = $"BGM: {Mathf.RoundToInt(bgmSlider.value * 100)}%";
        if (sfxLabel)    sfxLabel.text    = $"효과음: {Mathf.RoundToInt(sfxSlider.value * 100)}%";
    }

    public void OpenSettings() => settingsPanel?.SetActive(true);
    public void CloseSettings() => settingsPanel?.SetActive(false);
    public void ToggleSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}
