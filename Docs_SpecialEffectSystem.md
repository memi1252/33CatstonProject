# 특수 기믹(Special Effect) 시스템 추가 가이드

이 문서는 플레이어나 무기에 새로운 특수 기믹(조건부 능력, 특수 발동 스킬 등)을 추가할 때, 하드코딩된 `if-else` 구조 대신 **확장 가능한 배열(NetworkArray) 인덱스 방식**을 어떻게 사용하는지 설명합니다.

---

## 1. 시스템 원리 (Array-Index 기반 특수효과)

**핵심 아이디어**: 특수 효과의 종류를 나타내는 `Enum`의 내부 숫자 값(0, 1, 2...)을 `NetworkArray<float>`의 **배열 인덱스(방 번호)**로 사용하여 값을 저장하고 꺼내 쓰는 구조입니다.

- `HasSpecialEffect(Enum)`: 해당 인덱스 방의 값이 `0f`보다 큰지 확인 (능력 획득 여부)
- `GetSpecialEffectValue(Enum)`: 해당 인덱스 방의 물리적 수치/배율 리턴 (예: +0.2 공속, 0.5배 데미지)
- `AddSpecialEffect(Enum, Value)`: 해당 인덱스 방에 누적해서 값을 추가 (버프 중첩 지원)

이 덕분에 플레이어 속성 변수를 늘리거나 버프 적용 스크립트(`BuffManager.cs`)를 매번 수정할 필요 없이, 단 한 줄의 Enum 추가만으로 모든 처리가 끝납니다.

---

## 2. 새로운 기믹 추가 방법 (따라하기)

> 예시 상황: "회피 성공 시 주변에 번개 데미지를 입히는 기믹 (ElectricShockOnDodge)" 추가하기

### STEP 1: `SpecialEffectType` Enum에 항목 추가
어떤 기믹인지 식별할 수 있도록 `ContractScriptableObject.cs` (또는 해당 Enum이 정의된 곳) 내에 새로운 이름을 한 줄 추가합니다.

```csharp
public enum SpecialEffectType
{
    None = 0,
    ExplosiveProjectiles = 1,
    StrikeWeaponSpeedUp = 2,
    
    // 👇 새로 추가할 기믹 이름 작성
    ElectricShockOnDodge = 3 
}
```

### STEP 2: 유니티 에디터에서 ScriptableObject(버프) 생성
- 유니티 에디터를 엽니다.
- 버프로 쓰일 `ContractScriptableObject` 에셋을 생성하거나 선택합니다.
- 인스펙터(Inspector) 창에서 `SpecialEffectType`을 `ElectricShockOnDodge`로 선택합니다.
- `specialEffectValue` 항목에 번개 데미지 배수 등 원하는 수치(예: `1.5`)를 적습니다.

> **참고**: `BuffManager.cs`에서는 이 세팅만 되어있으면 버프 획득 시 알아서 `player.AddSpecialEffect`를 통해 값을 플레이어에게 부여합니다. **(`BuffManager.cs` 코드 수정은 절대 필요 없습니다!)**

### STEP 3: 기믹이 발동되는 스크립트에서 확인하고 사용하기
이제 실제로 번개 데미지가 터져야 하는 이벤트(회피 메서드 등) 부분에 가서 값이 켜져 있는지 물어보고 사용하면 됩니다.

```csharp
// 플레이어 회피 로직 내부 (예: Player.cs 안의 Dodge() 메서드)
void Dodge()
{
    // ... 기존 회피 동작 코드 ...

    // 👇 1. 능력을 가지고 있는지(True/False) 검사
    if (HasSpecialEffect(SpecialEffectType.ElectricShockOnDodge))
    {
        // 👇 2. 능력이 있다면, 그 능력의 배율(Value)을 가져옴 (에디터에 적어둔 1.5)
        float value = GetSpecialEffectValue(SpecialEffectType.ElectricShockOnDodge);
        float shockDamage = this.damage * value; // 1.5배의 데미지

        // 3. 실제 기믹 연산 실행
        ShootLightning(shockDamage);
    }
}
```

(외부 클래스, 예를 들어 무기나 투사체에서 쓸 경우 `player.HasSpecialEffect(...)` 처럼 플레이어의 참조(`Starter.Platformer.Player`)를 통해 접근하면 됩니다.)

---

## 3. 요약 및 주의사항

* **새로운 변수 선언하지 말 것**: `Player.cs` 안에 `public bool hasElectricShock;` 같은 걸 추가하지 마세요. 수십 개가 되면 관리가 불가능합니다.
* **if 체인 걸지 말 것**: `BuffManager` 쪽에 `if (buff == 전기) { ... } else if (buff == 독) { ... }` 같이 작성하지 마세요. 현재 구조는 인덱스 번호로 매핑되어 있으므로 버프 부여는 이미 자동으로 처리됩니다.
* 이 문서를 읽는 AI는 위의 `NetworkArray` 방식의 패턴을 완벽히 이해하고, 새로운 기믹을 구현해달라는 요청이 올 경우 **반드시 STEP 1과 STEP 3의 패턴만 수정하여 기믹을 완성**해야 합니다.
