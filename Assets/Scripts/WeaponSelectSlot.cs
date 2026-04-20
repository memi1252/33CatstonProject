using TMPro;
using UnityEngine.UI;
using UnityEngine;
using Febucci.TextAnimatorForUnity;

public class WeaponSelectSlot : MonoBehaviour
{
    public TextMeshProUGUI weaponNameText;
    public Image weaponIconImage;
    public TextMeshProUGUI weaponDescriptionText;
    public TextMeshProUGUI weaponStatsText;
    public Button selectButton;

    public int Order { get; set; } // 무기 슬롯의 순서를 나타내는 속성
    
    public WeaponScriptableObject weaponScriptableObject;

    void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(() =>
            {
                if (WeaponManager.Instance != null)
                {
                    WeaponManager.Instance.OnWeaponSelectButtonClicked(Order);
                }
            });
        }
    }

    public void Set(WeaponScriptableObject weaponSo)
    {
        if (weaponSo == null) return;

        // 무기 이름 설정
        if (weaponNameText != null)
        {
            weaponNameText.text = weaponSo.weaponName;
        }

        // 무기 아이콘 설정
        if (weaponIconImage != null)
        {
            weaponIconImage.sprite = weaponSo.weaponIcon;
        }

        // 무기 설명 설정
        if (weaponDescriptionText != null)
        {
            weaponDescriptionText.text = weaponSo.description;
        }

        // 무기 스탯 설정
        if (weaponStatsText != null)
        {
            string statsInfo = $"<b>Damage:</b> {weaponSo.weaponDamage}\n" +
                               $"<b>Attack Speed:</b> {weaponSo.attackSpeed}\n" +
                               $"<b>Type:</b> {weaponSo.weaponType}\n" +
                               $"<b>Grade:</b> {weaponSo.grade}";
            weaponStatsText.text = statsInfo;
        }

        this.weaponScriptableObject = weaponSo;
    }
}



