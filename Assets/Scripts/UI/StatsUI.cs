using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct Stats
{
    public string Name;
    public Sprite Sprite;
    public Sprite ring;
}

public class StatsUI : MonoBehaviour
{
    public GameObject HpUI;
    public Image ringImage;
    public Image weaponTypemage;
    public Image weaponGradeImage;
    public Image weaponAttributeImage;
    public Image hpImage;
    public Image mpImage;
    
    public List<Stats> weaponAttribute = new List<Stats>();
    public List<Stats> weaponGrade = new List<Stats>();
    public List<Stats> weaponType = new List<Stats>();

    public void Set(WeaponType wt, Grade wg, TargetAttribute wa)
    {
        bool foundType = false;
        foreach (var stats in weaponType)
        {
            if (stats.Name == wt.ToString())
            {
                weaponTypemage.sprite = stats.Sprite;
                foundType = true;
                break;
            }
        }
        // sprite를 null로만 비우면 Image가 흰 사각형으로 그려지므로, 매칭 실패 시 아예 렌더링을 끈다.
        weaponTypemage.enabled = foundType;

        bool foundGrade = false;
        foreach (var stats in weaponGrade)
        {
            if (stats.Name == wg.ToString())
            {
                weaponGradeImage.sprite = stats.Sprite;
                foundGrade = true;
                break;
            }
        }
        weaponGradeImage.enabled = foundGrade;

        // 일치하는 속성이 없으면(예: 무속성=None 항목이 리스트에 없는 경우) 이전 무기의
        // 아이콘이 그대로 남아있던 버그 → 매칭 실패 시 명시적으로 비워준다.
        bool foundAttr = false;
        foreach (var stats in weaponAttribute)
        {
            if (stats.Name == wa.ToString())
            {
                weaponAttributeImage.sprite = stats.Sprite;
                ringImage.sprite = stats.ring;
                foundAttr = true;
                break;
            }
        }
        weaponAttributeImage.enabled = foundAttr;
        if (!foundAttr)
        {
            Debug.LogWarning($"[StatsUI] weaponAttribute 리스트에 '{wa}' 항목이 없습니다. 인스펙터에 추가해주세요.");
        }
    }

    public void AttackCoolTimeView(float value)
    {
        ringImage.fillAmount = value;
    }
    
    public void hpImageView(float value)
    {
        hpImage.fillAmount = value;
    }

    public void mpImageView(float value)
    {
        mpImage.fillAmount = value;
    }
}
