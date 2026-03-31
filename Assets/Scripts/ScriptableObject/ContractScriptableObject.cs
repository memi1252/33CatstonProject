using UnityEngine;

[System.Serializable]
public enum TargetType
{
    Projectile,
    Laser,
    Area,
    Strike,
    None
}

[System.Serializable]
public enum TargetAttribute
{
    None,
    Fire,
    Ice,
    Electric,
    Water,
    Normal
}

[System.Serializable]
public enum SpecialEffectType
{
    None,
    ExplosiveProjectiles,
    StrikeWeaponSpeedUp
}

[System.Serializable]
public class ContractBuffData
{
    public BuffProperties targetAbilities;
    public float ratio;
}

[CreateAssetMenu(fileName = "New Contract", menuName = "ScriptableObjects/Contract")]
public class ContractScriptableObject : ScriptableObject
{
    [Header("기본 정보")]
    public string contractID;
    public string contractName;
    public Sprite contractIcon;
    public string description;
    
    [Header("타겟 정보")]
    public TargetType targetType = TargetType.None;
    public TargetAttribute targetAttribute = TargetAttribute.None;
    
    [Header("특수 기믹")]
    public SpecialEffectType specialEffect = SpecialEffectType.None;
    public float specialEffectValue = 1f; // 기믹 관련된 수치 (폭발 데미지 배율, 증폭 수치 등)
    
    [Header("계약 효과")]
    public ContractBuffData[] contractBuffs;  // 여러 버프를 배열로 처리
    public ValueType valueType = ValueType.Percent;
}
