using UnityEngine;

[CreateAssetMenu(fileName = "New Buff", menuName = "Buff")]
public class BuffScripableObject : ScriptableObject
{
    public int buffID;
    public string buffName;
    public Sprite buffIcon;
    public string buffDescription;
    public string buffConditions;
    public BuffProperties buffProperties;
}

[System.Serializable]
public class BuffProperties{
    [Header("Buff Properties")]
    public float maxHp;
    public float maxMp;
    public float damage;
    public float attackSpeed;
    public float moveSpeed;
    public float allDamage;
    public float damageReceived;
    public float criticalChance;
    public float criticalDamage;
    public float enemiesDamage;
    public float enemiesReceived;
    public float enemiesHp;
    public float boosDamage;
    public float boosReceived;
    public float boosHp;
}
