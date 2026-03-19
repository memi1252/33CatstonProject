using UnityEngine;

[System.Serializable]
public enum ValueType
{
    Percent,
    Fixed,
    Count
}

[System.Serializable]
public class SkillBuffData
{
    public BuffProperties buffType;
    public float buffRatio;
}

[System.Serializable]
public enum ActivationType
{
    Duration,
    AttackCount,
}

[CreateAssetMenu(fileName = "New Skill", menuName = "ScriptableObjects/Skill")]
public class SkillScriptableObject : ScriptableObject
{
    [Header("기본 정보")]
    public string skillID;
    public string skillName;
    public Sprite skillIcon;
    public string description;
    
    [Header("스킬 효과")]
    public SkillBuffData[] skillBuffs;  // 여러 버프를 배열로 처리
    public ActivationType ActivationType;
    public float ActivationValue = 0f;
    public ValueType valueType = ValueType.Percent;
}
