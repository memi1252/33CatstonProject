using UnityEngine;

/// <summary>
/// 모든 무기(WeaponScriptableObject)의 단일 출처(인덱스 표준).
/// 네트워크로는 이 배열의 인덱스만 주고받고, 각 클라이언트가 같은 DB 에셋으로 SO를 복원한다.
/// Resources/WeaponDatabase.asset 로 저장하여 어느 씬에서든 로드 가능해야 한다.
/// </summary>
[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Weapon Database")]
public class WeaponDatabase : ScriptableObject
{
    [Tooltip("장착 가능한 모든 무기. 모든 클라이언트가 동일한 순서/내용을 가져야 한다.")]
    public WeaponScriptableObject[] weapons;

    private static WeaponDatabase _instance;

    public static WeaponDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<WeaponDatabase>("WeaponDatabase");
                if (_instance == null)
                {
                    Debug.LogError("[WeaponDatabase] Resources/WeaponDatabase.asset 를 찾을 수 없습니다. " +
                                   "Create > Weapon Database 로 생성하고 Assets/Resources/ 아래에 두세요.");
                }
            }
            return _instance;
        }
    }

    /// <summary>weapons 배열에서 SO의 인덱스를 반환. 없으면 -1.</summary>
    public int IndexOf(WeaponScriptableObject weapon)
    {
        if (weapon == null || weapons == null) return -1;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == weapon) return i;
        }
        return -1;
    }

    /// <summary>인덱스로 SO를 반환. 범위를 벗어나면 null.</summary>
    public WeaponScriptableObject Get(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length) return null;
        return weapons[index];
    }
}
