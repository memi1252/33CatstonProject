using UnityEngine;

[System.Serializable]
public enum EnemyType
{
    Melee,
    Ranged,
    destruct
}

[System.Serializable]
public enum ProjectileType
{
    None,
    Projectile,
    Laser,
    Area
}

[CreateAssetMenu(fileName = "New Enemy", menuName = "ScriptableObjects/Enemy")]
public class EnemyScriptableObject : ScriptableObject
{
    [Header("기본 정보")]
    public int enemyID;
    public string enemyName;
    public int appeared;
    public Sprite enemyIcon;
    public GameObject enemyPrefab;
    public string description;
    
    [Header("적 타입")]
    public EnemyType enemyType = EnemyType.Melee;
    public ProjectileType projectileType = ProjectileType.None;
    
    [Header("스탯")]
    public float range = 5f;
    public float size = 1f;
    public float hp = 100f;
    public float damage = 10f;
    public float attackSpeed = 1f;
    public float speed = 3f;
    
    [Header("추가 능력치")]
    public float allDMG = 0f;        // 모든 데미지 증가/감소
    public float dmgReceived = 0f;   // 받는 데미지 증가/감소
}
