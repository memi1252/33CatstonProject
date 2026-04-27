/*
==================== 무기 속성 효과 시스템 가이드 ====================

이 시스템은 무기의 속성(Fire, Ice, Electric, Water, Normal)에 따라
투척물, 레이저, 지역 공격, 스트라이크 공격에 특별한 효과를 적용합니다.

=== 속성별 효과 ===

1. Fire (불) - DoT 데미지
   - 대상에게 지속적인 피해를 입힘
   - fireDoTDuration: DoT 지속 시간
   - fireDoTInterval: DoT 피해 간격
   - fireDoTDamageMultiplier: 각 틱당 기본 데미지의 %

2. Ice (얼음) - 둔화
   - 대상의 이동 속도를 감소시킴
   - iceSlowDuration: 둔화 지속 시간
   - iceSlowAmount: 속도 감소 비율 (0.5 = 50% 감소)

3. Electric (번개) - 체인 공격
   - 근처 대상들에게 공격을 전이시킴
   - electricChainRadius: 체인 감지 범위
   - electricMaxChains: 최대 체인 수
   - electricChainDamageMultiplier: 체인 데미지 비율

4. Water (물) - 넉백
   - 대상을 날려냄
   - waterKnockbackForce: 넉백 강도
   - waterKnockbackDuration: 넉백 지속 시간

5. Normal (일반) - 추가 데미지
   - 기본 데미지에 추가 피해를 입힘
   - normalDamageBonus: 추가 데미지 비율 (0.1 = 10% 추가)

=== 적용 방법 ===

1. Scene에 빈 GameObject를 생성
2. AttributeEffectApplier 스크립트를 추가
3. Inspector에서 각 속성별 파라미터 조정
4. 각 무기에 적절한 targetAttribute 설정

=== 작동 원리 ===

투척물 공격:
  Weapon_NetworkObject.FireSimple/FireWithBuffer()
    → PhysicsProjectile.Fire()에 targetAttribute 전달
    → OnHitObject()에서 피격 대상에 속성 효과 적용

레이저 공격:
  Weapon_NetworkObject.FireLaserLogic()
    → 피격 대상에 직접 데미지 + 속성 효과 적용

지역 공격 (Area):
  Weapon_NetworkObject.AreaAttackLogic()
    → DealAreaDamage()에서 범위 내 모든 적에게 피해
    → 첫 틱에만 속성 효과 적용 (중복 방지)

스트라이크 공격:
  Weapon_NetworkObject.StrikeAttackLogic()
    → DealStrikeDamage()에서 범위 내 모든 적에게 피해 + 속성 효과 적용

=== 주의사항 ===

1. AttributeEffectApplier는 Scene에 하나만 있어야 함
2. Ice 효과는 Player 클래스의 moveSpeed를 직접 수정함
3. Electric 체인은 소유자를 제외하고 적용
4. Water 효과는 대상에 Rigidbody가 필요함
5. DoT는 MonoBehaviour의 StartCoroutine 사용

=== 커스터마이징 예제 ===

// Fire 효과를 더 강하게 하려면
fireDoTDamageMultiplier = 0.3f; // 30% 증가

// Ice 효과를 더 오래 지속시키려면
iceSlowDuration = 4f; // 4초

// Electric 체인을 더 멀리까지 전이하려면
electricChainRadius = 15f;
electricMaxChains = 3;
*/

