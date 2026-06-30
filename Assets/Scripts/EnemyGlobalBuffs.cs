using UnityEngine;

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

    // 스테이지를 진행할수록 적이 자연스럽게 강해지도록 하는 자동 난이도 상승분.
    // 각인/계약 버프(누적 delta)와는 별개로, 현재 스테이지 인덱스로부터 매번 직접 계산한다(중복 누적 방지).
    public static float stageDamageBonus = 0f;
    public static float stageHealthBonus = 0f;
    public static float stageSpeedBonus  = 0f;

    /// <summary>StageManager.OnStageChanged에서 모든 클라이언트가 동일하게 호출한다.</summary>
    public static void SetStageScaling(int stageIndex)
    {
        int n = stageIndex < 0 ? 0 : stageIndex;
        // 데미지는 스테이지마다 +12%씩 상한 없이 계속 누적돼서, 후반 스테이지(20+)에선
        // 기본 데미지의 3배가 넘어가는데 플레이어 체력은 증강으로 골라야만 느는 구조라
        // 갈수록 일방적으로 세진다는 문제가 있었다. 증가율을 더 낮추고 상한(+100%)을 둔다.
        stageDamageBonus = Mathf.Min(n * 0.05f, 1.0f);
        stageHealthBonus = n * 0.15f; // 스테이지마다 체력 +15%
        stageSpeedBonus = n * 0.03f;  // 스테이지마다 이동속도 +3% (너무 빨라지지 않게 적게)
    }

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
        stageDamageBonus = stageHealthBonus = stageSpeedBonus = 0f;
    }

    // 적용 헬퍼: base * (1 + bonus) — bonus가 0이면 base 그대로
    public static float ScaledDamage(float baseDmg, bool isBoss)
        => baseDmg * (1f + (isBoss ? bossDamageBonus : damageBonus) + stageDamageBonus);

    public static float ScaledSpeed(float baseSpd, bool isBoss)
        => baseSpd * (1f + speedBonus + stageSpeedBonus); // 보스도 일반 speedBonus 사용

    public static float ScaledHealth(float baseHp, bool isBoss)
        => baseHp * (1f + (isBoss ? bossHealthBonus : healthBonus) + stageHealthBonus);

    public static float ScaledReceived(float dmg, bool isBoss)
        => dmg * (1f + (isBoss ? bossReceivedBonus : receivedBonus));
}
