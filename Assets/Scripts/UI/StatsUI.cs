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
    public Image ringImage;
    public Image weaponTypemage;
    public Image weaponGradeImage;
    public Image weaponAttributeImage;
    
    public List<Stats> weaponAttribute = new List<Stats>();
    public List<Stats> weaponGrade = new List<Stats>();
    public List<Stats> weaponType = new List<Stats>();

    public void Set(WeaponType wt, Grade wg, TargetAttribute wa)
    {
        foreach (var stats in weaponType)
        {
            if (stats.Name == wt.ToString())
            {
                weaponTypemage.sprite = stats.Sprite;
                break;
            }
        }
        
        foreach (var stats in weaponGrade)
        {
            if (stats.Name == wg.ToString())
            {
                weaponGradeImage.sprite = stats.Sprite;
                break;
            }
        }
        
        foreach (var stats in weaponAttribute)
        {
            if (stats.Name == wa.ToString())
            {
                weaponAttributeImage.sprite = stats.Sprite;
                ringImage.sprite = stats.ring;
                break;
            }
        }
    }

    public void AttackCoolTimeView(float value)
    {
        ringImage.fillAmount = value;
    }
}
