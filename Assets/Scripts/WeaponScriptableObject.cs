using UnityEngine;

public enum WeaponType
{
    Projectile,
    Laser,
    FloorBoard
}

public enum Grade
{
    Common,
    Unique,
    Epic,
}

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapon")]
public class WeaponScriptableObject : ScriptableObject
{
    public int weaponID;
    public string weaponName;
    public Sprite weaponIcon;
    public string description;
    public GameObject weaponPrefab; // 무기 프리팹
    public GameObject projectilePrefab; // 투사체 프리팹
    public WeaponType weaponType = WeaponType.Projectile;
    public Grade grade = Grade.Common;
    public float damageRatio = 0f; // damage * N% + WeaponDamage
    public float weaponDamage = 0f;
    public float tileSize = 0f; // 투사체-크기, 레이저-두께, 장판형(위에서 떨어뜨리는 스킬 포함)-범위 크기
    public float intersection = 0f; //투사체- 사러기존재, 일정이상날아가면 파괴됨(적중시 폭방이 있으면 파괴될때 폭발함), 레이저 - 사거리 무한, 장판형 - 일정 사거리 존재, 그 사러기 내에서만 장판 생성가능
    public float attackSpeed = 0f; // 무기 사용쿨타임?
    public float playerSpeed = 0f; // 무기 사용시 플레이어 이동속도 증가량
    public float projectileSpeed = 0f; // 투사체 속도
}
