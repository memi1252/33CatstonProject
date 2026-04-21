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

    // 왼쪽에 표시될 등급, 무기 타입, 속성 이미지
    public Image gradeIconImage;
    public Image weaponTypeIconImage;
    public Image targetAttributeIconImage;

    [Header("등급 아이콘 (Grade)")]
    public Sprite commonGradeIcon;
    public Sprite uniqueGradeIcon;
    public Sprite epicGradeIcon;

    [Header("무기 타입 아이콘 (Weapon Type)")]
    public Sprite projectileIcon;
    public Sprite laserIcon;
    public Sprite areaIcon;
    public Sprite strikeIcon;

    [Header("속성 아이콘 (Target Attribute)")]
    public Sprite fireAttributeIcon;
    public Sprite iceAttributeIcon;
    public Sprite electricAttributeIcon;
    public Sprite waterAttributeIcon;
    public Sprite normalAttributeIcon;

    public int Order { get; set; }
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

        if (weaponNameText != null)
            weaponNameText.text = weaponSo.weaponName;

        if (weaponIconImage != null)
            weaponIconImage.sprite = weaponSo.weaponIcon;

        if (weaponDescriptionText != null)
            weaponDescriptionText.text = weaponSo.description;

        if (weaponStatsText != null)
        {
            string gradeColor = GetGradeColor(weaponSo.grade);
            string typeColor = GetWeaponTypeColor(weaponSo.weaponType);
            string statsInfo = $"<b>Damage:</b> {weaponSo.weaponDamage}\n" +
                               $"<b>Attack Speed:</b> {weaponSo.attackSpeed}\n" +
                               $"<b><color={typeColor}>Type: {weaponSo.weaponType}</color></b>\n" +
                               $"<b><color={gradeColor}>Grade: {weaponSo.grade}</color></b>";
            weaponStatsText.text = statsInfo;
        }

        // 등급 아이콘
        if (gradeIconImage != null)
        {
            Sprite gradeIcon = GetGradeIconSprite(weaponSo.grade);
            if (gradeIcon != null)
            {
                gradeIconImage.sprite = gradeIcon;
                gradeIconImage.color = Color.white;
            }
            else
            {
                gradeIconImage.sprite = null;
                gradeIconImage.color = GetGradeColorValue(weaponSo.grade);
            }
        }

        // 무기 타입 아이콘
        if (weaponTypeIconImage != null)
        {
            Sprite typeIcon = GetWeaponTypeIconSprite(weaponSo.weaponType);
            if (typeIcon != null)
            {
                weaponTypeIconImage.sprite = typeIcon;
                weaponTypeIconImage.color = Color.white;
            }
            else
            {
                weaponTypeIconImage.sprite = null;
                weaponTypeIconImage.color = GetWeaponTypeColorValue(weaponSo.weaponType);
            }
        }

        // 속성 아이콘
        if (targetAttributeIconImage != null)
        {
            Sprite attributeIcon = GetTargetAttributeIconSprite(weaponSo.targetAttribute);
            if (attributeIcon != null)
            {
                targetAttributeIconImage.sprite = attributeIcon;
                targetAttributeIconImage.color = Color.white;
            }
            else
            {
                targetAttributeIconImage.sprite = null;
                targetAttributeIconImage.color = GetTargetAttributeColorValue(weaponSo.targetAttribute);
            }
        }
        this.weaponScriptableObject = weaponSo;
    }

    private Sprite GetGradeIconSprite(Grade grade)
    {
        return grade switch
        {
            Grade.Common => commonGradeIcon,
            Grade.Unique => uniqueGradeIcon,
            Grade.Epic => epicGradeIcon,
            _ => null
        };
    }

    private Sprite GetWeaponTypeIconSprite(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Projectile => projectileIcon,
            WeaponType.Laser => laserIcon,
            WeaponType.Area => areaIcon,
            WeaponType.Strike => strikeIcon,
            _ => null
        };
    }

    private Sprite GetTargetAttributeIconSprite(TargetAttribute attribute)
    {
        return attribute switch
        {
            TargetAttribute.Fire => fireAttributeIcon,
            TargetAttribute.Ice => iceAttributeIcon,
            TargetAttribute.Electric => electricAttributeIcon,
            TargetAttribute.Water => waterAttributeIcon,
            TargetAttribute.Normal => normalAttributeIcon,
            _ => null
        };
    }

    private string GetGradeColor(Grade grade)
    {
        return grade switch
        {
            Grade.Common => "#FFFFFF",
            Grade.Unique => "#0066FF",
            Grade.Epic => "#FF00FF",
            _ => "#FFFFFF"
        };
    }

    private Color GetGradeColorValue(Grade grade)
    {
        return grade switch
        {
            Grade.Common => new Color(1f, 1f, 1f, 1f),
            Grade.Unique => new Color(0f, 0.4f, 1f, 1f),
            Grade.Epic => new Color(1f, 0f, 1f, 1f),
            _ => new Color(1f, 1f, 1f, 1f)
        };
    }

    private string GetWeaponTypeColor(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Projectile => "#FFFF00",
            WeaponType.Laser => "#FF6600",
            WeaponType.Area => "#00FF00",
            WeaponType.Strike => "#FF0000",
            _ => "#FFFFFF"
        };
    }

    private Color GetWeaponTypeColorValue(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Projectile => new Color(1f, 1f, 0f, 1f),
            WeaponType.Laser => new Color(1f, 0.4f, 0f, 1f),
            WeaponType.Area => new Color(0f, 1f, 0f, 1f),
            WeaponType.Strike => new Color(1f, 0f, 0f, 1f),
            _ => new Color(1f, 1f, 1f, 1f)
        };
    }

    private Color GetTargetAttributeColorValue(TargetAttribute attribute)
    {
        return attribute switch
        {
            TargetAttribute.None => new Color(0.8f, 0.8f, 0.8f, 1f),
            TargetAttribute.Fire => new Color(1f, 0.3f, 0f, 1f),
            TargetAttribute.Ice => new Color(0f, 0.8f, 1f, 1f),
            TargetAttribute.Electric => new Color(1f, 1f, 0f, 1f),
            TargetAttribute.Water => new Color(0f, 0.5f, 1f, 1f),
            TargetAttribute.Normal => new Color(1f, 1f, 1f, 1f),
            _ => new Color(0.8f, 0.8f, 0.8f, 1f)
        };
    }
}



