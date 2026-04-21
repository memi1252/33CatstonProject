using UnityEngine;
using System.Collections;
using Fusion;

/// <summary>
/// 무기 속성별 효과 적용 관리 (네트워크 동기화)
/// Fire: DoT 데미지 + 불 VFX
/// Ice: 둔화 + 얼음 VFX
/// Electric: 체인 공격 + 번개 VFX
/// Water: 넉백 + 물 VFX
/// Normal: 추가 데미지 + 기본 VFX
/// </summary>
public class AttributeEffectApplier : NetworkBehaviour
{
    public static AttributeEffectApplier Instance { get; private set; }

    [Header("Fire (불) - DoT 설정")]
    public float fireDoTDuration = 3f;
    public float fireDoTInterval = 0.5f;
    public float fireDoTDamageMultiplier = 0.2f;
    public ParticleSystem fireParticlePrefab;
    public Color fireColor = new Color(1f, 0.5f, 0f, 1f); // 주황색

    [Header("Ice (얼음) - 둔화 설정")]
    public float iceSlowDuration = 2f;
    public float iceSlowAmount = 0.5f;
    public ParticleSystem iceParticlePrefab;
    public Color iceColor = new Color(0f, 0.8f, 1f, 1f); // 밝은 파란색

    [Header("Electric (번개) - 체인 설정")]
    public float electricChainRadius = 10f;
    public int electricMaxChains = 2;
    public float electricChainDamageMultiplier = 0.8f;
    public ParticleSystem electricParticlePrefab;
    public Color electricColor = new Color(1f, 1f, 0f, 1f); // 노란색
    public LineRenderer electricChainLinePrefab;

    [Header("Water (물) - 넉백 설정")]
    public float waterKnockbackForce = 10f;
    public float waterKnockbackDuration = 0.5f;
    public ParticleSystem waterParticlePrefab;
    public Color waterColor = new Color(0f, 0.5f, 1f, 1f); // 파란색

    [Header("Normal (일반) - 추가 데미지")]
    public float normalDamageBonus = 0.1f;
    public ParticleSystem normalParticlePrefab;
    public Color normalColor = new Color(1f, 1f, 0.8f, 1f); // 밝은 노란색

    [Header("공통 VFX 설정")]
    public float vfxLifetime = 2f; // VFX 지속 시간
    public float screenShakeIntensity = 0.2f; // 스크린 셰이크 강도

    private LayerMask raycastLayerMask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        raycastLayerMask = LayerMask.GetMask("Enemy");
    }

    /// <summary>
    /// 속성별 효과 적용
    /// </summary>
    public void ApplyAttributeEffect(TargetAttribute attribute, IDamageable target, Vector3 targetPosition, float baseDamage, Starter.Platformer.Player owner)
    {
        if (target == null) return;

        Transform targetTransform = (target is MonoBehaviour mb) ? mb.transform : null;

        switch (attribute)
        {
            case TargetAttribute.Fire:
                ApplyFireEffect(target, baseDamage, targetTransform);
                break;
            case TargetAttribute.Ice:
                ApplyIceEffect(target, targetTransform);
                break;
            case TargetAttribute.Electric:
                ApplyElectricEffect(targetPosition, baseDamage, owner, targetTransform);
                break;
            case TargetAttribute.Water:
                ApplyWaterEffect(target, targetTransform);
                break;
            case TargetAttribute.Normal:
                ApplyNormalEffect(target, baseDamage, targetTransform);
                break;
        }
    }

    /// <summary>
    /// 불 속성: DoT 데미지 + 불 이펙트
    /// </summary>
    private void ApplyFireEffect(IDamageable target, float baseDamage, Transform targetTransform)
    {
        if (target is MonoBehaviour targetMB)
        {
            targetMB.StartCoroutine(FireDoTDamage(target, baseDamage));
        }
        
        // VFX: 불 파티클 생성 (적의 자식으로)
        PlayAttributeParticle(fireParticlePrefab, targetTransform, fireColor);
        ScreenShake(screenShakeIntensity * 0.5f);
    }

    private IEnumerator FireDoTDamage(IDamageable target, float baseDamage)
    {
        float elapsed = 0f;
        float dotDamagePerTick = 1f;

        while (elapsed < fireDoTDuration)
        {
            // 적이 죽었거나 없어졌으면 코루틴 종료
            if (target == null)
            {
                yield break;
            }

            target.TakeHit(dotDamagePerTick, new RaycastHit());
            elapsed += fireDoTInterval;
            yield return new WaitForSeconds(fireDoTInterval);
        }
    }

    /// <summary>
    /// 얼음 속성: 둔화 + 얼음 이펙트
    /// </summary>
    private void ApplyIceEffect(IDamageable target, Transform targetTransform)
    {
        if (target is MonoBehaviour targetMB && targetMB.TryGetComponent<Starter.Platformer.Player>(out var player))
        {
            targetMB.StartCoroutine(SlowPlayer(player));
        }
        
        // VFX: 얼음 파티클 생성 (적의 자식으로)
        PlayAttributeParticle(iceParticlePrefab, targetTransform, iceColor);
    }

    private IEnumerator SlowPlayer(Starter.Platformer.Player player)
    {
        float originalSpeed = player.moveSpeed;
        player.moveSpeed *= (1f - iceSlowAmount);

        yield return new WaitForSeconds(iceSlowDuration);

        player.moveSpeed = originalSpeed;
    }

    /// <summary>
    /// 번개 속성: 체인 공격 + 번개 이펙트
    /// </summary>
    private void ApplyElectricEffect(Vector3 position, float baseDamage, Starter.Platformer.Player owner, Transform targetTransform)
    {
        if (owner == null) return;

        Collider[] nearbyEnemies = Physics.OverlapSphere(position, electricChainRadius, raycastLayerMask);

        int chainCount = 0;
        foreach (var enemyCollider in nearbyEnemies)
        {
            if (chainCount >= electricMaxChains) break;

            IDamageable damageable = enemyCollider.GetComponentInParent<IDamageable>();
            if (damageable != null && enemyCollider.transform.root.gameObject != owner.gameObject)
            {
                float chainDamage = baseDamage * electricChainDamageMultiplier;
                damageable.TakeHit(chainDamage, new RaycastHit());
                chainCount++;
                
                // VFX: 체인 라인 그리기
                DrawElectricChain(position, enemyCollider.transform.position);
            }
        }
        
        // VFX: 번개 파티클 생성 (적의 자식으로)
        PlayAttributeParticle(electricParticlePrefab, targetTransform, electricColor);
        ScreenShake(screenShakeIntensity * 0.7f);
    }

    private void DrawElectricChain(Vector3 start, Vector3 end)
    {
        if (electricChainLinePrefab == null) return;
        
        LineRenderer line = Instantiate(electricChainLinePrefab);
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        
        Destroy(line.gameObject, vfxLifetime);
    }

    /// <summary>
    /// 물 속성: 넉백 + 물 이펙트
    /// </summary>
    private void ApplyWaterEffect(IDamageable target, Transform targetTransform)
    {
        if (target is MonoBehaviour targetMB && targetMB.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 knockbackDirection = (targetMB.transform.position - targetMB.transform.position).normalized;
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(knockbackDirection * waterKnockbackForce, ForceMode.Impulse);
            
            // VFX: 물 파티클을 넉백 방향으로 생성 (적의 자식으로)
            PlayDirectionalParticle(waterParticlePrefab, targetTransform, knockbackDirection, waterColor);
        }
    }

    /// <summary>
    /// 일반 속성: 추가 데미지 + 기본 이펙트
    /// </summary>
    private void ApplyNormalEffect(IDamageable target, float baseDamage, Transform targetTransform)
    {
        if (target is MonoBehaviour targetMB && targetMB.TryGetComponent<IDamageable>(out var damageable))
        {
            float bonusDamage = baseDamage * normalDamageBonus;
            damageable.TakeHit(bonusDamage, new RaycastHit());
        }
        
        // VFX: 일반 파티클 생성 (적의 자식으로)
        PlayAttributeParticle(normalParticlePrefab, targetTransform, normalColor);
    }

    /// <summary>
    /// 속성 파티클 생성 및 색상 적용 (적의 자식으로 설정)
    /// </summary>
    private void PlayAttributeParticle(ParticleSystem prefab, Transform targetTransform, Color color)
    {
        if (prefab == null) return;
        
        ParticleSystem particle = Instantiate(prefab, targetTransform != null ? targetTransform.position : Vector3.zero, Quaternion.identity);
        
        // 적의 자식으로 설정
        if (targetTransform != null)
        {
            particle.transform.SetParent(targetTransform);
            particle.transform.localPosition = Vector3.zero; // 적의 중심에 위치
        }
        
        // 파티클 색상 변경
        var main = particle.main;
        main.startColor = color;
        
        particle.Play();
        Destroy(particle.gameObject, vfxLifetime);
    }

    /// <summary>
    /// 방향이 있는 파티클 생성 (적의 자식으로 설정)
    /// </summary>
    private void PlayDirectionalParticle(ParticleSystem prefab, Transform targetTransform, Vector3 direction, Color color)
    {
        if (prefab == null) return;
        
        ParticleSystem particle = Instantiate(prefab, targetTransform != null ? targetTransform.position : Vector3.zero, Quaternion.FromToRotation(Vector3.forward, direction));
        
        // 적의 자식으로 설정
        if (targetTransform != null)
        {
            particle.transform.SetParent(targetTransform);
            particle.transform.localPosition = Vector3.zero; // 적의 중심에 위치
        }
        
        var main = particle.main;
        main.startColor = color;
        
        particle.Play();
        Destroy(particle.gameObject, vfxLifetime);
    }

    /// <summary>
    /// 스크린 셰이크 효과
    /// </summary>
    private void ScreenShake(float intensity)
    {
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            // GameManager에 카메라 셰이크 기능이 있다면 호출
            // gameManager.ShakeCamera(intensity);
        }
    }
}


