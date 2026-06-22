/// <summary>
/// 각인/계약으로 적용된 적 전역 버프 누적값.
/// 모든 클라이언트에서 RPC_ApplyImprintBuffs 이후 동일하게 갱신되므로 static으로 관리.
/// 값은 추가분(delta): 0 = 변화 없음, 0.5 = +50%.
/// </summary>
public static class EnemyGlobalBuffs
{
    // 일반 적
    public static float damageBonus      = 0f; // 적 공격 데미지 배율 증가분
    public static float speedBonus       = 0f; // 적 이동 속도 배율 증가분
    public static float healthBonus      = 0f; // 적 최대 체력 배율 증가분
    public static float receivedBonus    = 0f; // 적이 받는 피해 배율 변화분 (양수 = 더 많이 받음)

    // 보스
    public static float bossDamageBonus  = 0f;
    public static float bossHealthBonus  = 0f;
    public static float bossReceivedBonus= 0f;

    /// <summary>BuffProperties의 적/보스 관련 값을 누적 적용.</summary>
    public static void Apply(BuffProperties props)
    {
        damageBonus      += props.enemiesDamage;
        speedBonus       += props.enemiesSpeed;
        healthBonus      += props.enemiesHp;
        receivedBonus    += props.enemiesReceived;

        bossDamageBonus  += props.boosDamage;
        bossHealthBonus  += props.boosHp;
        bossReceivedBonus+= props.boosReceived;
    }

    /// <summary>씬 전환/게임 재시작 시 초기화.</summary>
    public static void Reset()
    {
        damageBonus = speedBonus = healthBonus = receivedBonus = 0f;
        bossDamageBonus = bossHealthBonus = bossReceivedBonus = 0f;
    }

    // 적용 헬퍼: base * (1 + bonus) — bonus가 0이면 base 그대로
    public static float ScaledDamage(float baseDmg, bool isBoss)
        => baseDmg * (1f + (isBoss ? bossDamageBonus : damageBonus));

    public static float ScaledSpeed(float baseSpd, bool isBoss)
        => baseSpd * (1f + speedBonus); // 보스도 일반 speedBonus 사용

    public static float ScaledHealth(float baseHp, bool isBoss)
        => baseHp * (1f + (isBoss ? bossHealthBonus : healthBonus));

    public static float ScaledReceived(float dmg, bool isBoss)
        => dmg * (1f + (isBoss ? bossReceivedBonus : receivedBonus));
}
