/// <summary>
/// 로컬 클라이언트의 무기 선택을 씬 전환(로비 → 게임) 사이에 보관한다.
/// 각 클라이언트는 자기 자신의 선택만 보관하면 된다(다른 플레이어 선택은 네트워크 인덱스로 동기화됨).
/// ScriptableObject 에셋 참조는 씬이 바뀌어도 유효하므로 정적 필드로 보관해도 안전하다.
/// </summary>
public static class PlayerLoadout
{
    /// <summary>로비에서 마지막으로 선택/확정한 무기. 게임 씬 스폰 시 이 무기를 장착한다.</summary>
    public static WeaponScriptableObject SelectedWeapon;
}
