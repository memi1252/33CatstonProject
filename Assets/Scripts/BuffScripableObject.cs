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
    public float maxHp =0f;
    public float speed = 0f;
    public float damage = 0f;
    public float criticalDamage = 0f;
    public float criticalChance = 0f;
    public float attackRange = 0f;
    public float meleeDefense = 0f;
    public float magicDefense = 0f;    

}
