using UnityEngine;
using System.Collections;
using Fusion;

public class AttributeEffectApplier : NetworkBehaviour
{
    public static AttributeEffectApplier Instance { get; private set; }

    [Header("Fire (불)")]
    public float fireDoTDuration = 3f;
    public float fireDoTInterval = 0.5f;
    public float fireDoTDamagePerTick = 2f;
    public GameObject fireParticlePrefab; // 반드시 Inspector에서 확인!
    public Color fireColor = new Color(1f, 0.5f, 0f, 1f);

    [Header("Ice (얼음)")]
    public float iceSlowDuration = 2f;
    public float iceSlowAmount = 0.5f;
    public GameObject iceParticlePrefab;
    public Color iceColor = new Color(0f, 0.8f, 1f, 1f);

    [Header("Electric (번개)")]
    public float electricChainRadius = 10f;
    public int electricMaxChains = 2;
    public float electricChainDamageMultiplier = 0.8f;
    public GameObject electricParticlePrefab;
    public Color electricColor = new Color(1f, 1f, 0f, 1f);

    [Header("Water (물)")]
    public float waterKnockbackForce = 10f;
    public GameObject waterParticlePrefab;
    public Color waterColor = new Color(0f, 0.5f, 1f, 1f);

    [Header("Normal (일반)")]
    public float normalDamageBonusMultiplier = 0.1f;
    public GameObject normalParticlePrefab;
    public Color normalColor = new Color(1f, 1f, 0.8f, 1f);

    [Header("공통 설정")]
    public float vfxLifetime = 2f;
    public float screenShakeIntensity = 0.2f;

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

    public override void Spawned()
    {
        raycastLayerMask = LayerMask.GetMask("Enemy");
    }

    public void ApplyAttributeEffect(TargetAttribute attribute, IDamageable target, Vector3 targetPosition, float baseDamage, Starter.Platformer.Player owner)
    {
        // 1. 기본 널 체크
        if (target == null || Runner == null) return;

        // 2. 서버 로직 처리
        if (Object.HasStateAuthority)
        {
            ApplyLogicOnServer(attribute, target, baseDamage, owner, targetPosition);
        }

        // 3. RPC 호출 (target이 NetworkBehaviour인지 확실히 체크)
        if (target is NetworkBehaviour targetNB && targetNB.Object != null)
        {
            RPC_PlayAttributeVFX(attribute, targetNB.Object.Id, targetPosition);
        }
    }

    private void ApplyLogicOnServer(TargetAttribute attribute, IDamageable target, float baseDamage, Starter.Platformer.Player owner, Vector3 position)
    {
        switch (attribute)
        {
            case TargetAttribute.Fire:
                StartCoroutine(FireDoTDamage(target));
                break;
            case TargetAttribute.Ice:
                if (target is MonoBehaviour mb && mb.TryGetComponent<Starter.Platformer.Player>(out var p))
                    StartCoroutine(SlowPlayer(p));
                break;
            case TargetAttribute.Electric:
                ApplyElectricChainLogic(position, baseDamage, owner);
                break;
            case TargetAttribute.Water:
                ApplyWaterKnockbackLogic(target, position);
                break;
            case TargetAttribute.Normal:
                target.TakeHit(baseDamage * normalDamageBonusMultiplier, new RaycastHit());
                break;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_PlayAttributeVFX(TargetAttribute attribute, NetworkId targetId, Vector3 hitPos)
    {
        // Runner가 유효하지 않으면 중단
        if (Runner == null) return;

        NetworkObject targetObj = Runner.FindObject(targetId);
        Transform targetT = targetObj != null ? targetObj.transform : null;

        switch (attribute)
        {
            case TargetAttribute.Fire: PlayParticle(fireParticlePrefab, targetT, fireColor, hitPos); break;
            case TargetAttribute.Ice: PlayParticle(iceParticlePrefab, targetT, iceColor, hitPos); break;
            case TargetAttribute.Electric: PlayParticle(electricParticlePrefab, targetT, electricColor, hitPos); break;
            case TargetAttribute.Water: PlayParticle(waterParticlePrefab, targetT, waterColor, hitPos); break;
            case TargetAttribute.Normal: PlayParticle(normalParticlePrefab, targetT, normalColor, hitPos); break;
        }
    }

    private void PlayParticle(GameObject prefab, Transform parent, Color color, Vector3 fallbackPos)
    {
        // 핵심 해결책: 프리팹이 진짜 있는지, 그리고 GameObject가 맞는지 체크
        if (prefab == null) 
        {
            Debug.LogWarning($"[AttributeEffect] 프리팹이 할당되지 않았습니다!");
            return;
        }

        Vector3 spawnPos = (parent != null) ? parent.position : fallbackPos;
        
        // 캐스팅 오류 방지를 위해 명시적으로 GameObject로 생성
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity) as GameObject;
        
        if (go == null) return;

        if (parent != null) go.transform.SetParent(parent);

        if (go.TryGetComponent<ParticleSystem>(out var ps))
        {
            var main = ps.main;
            main.startColor = color;
            ps.Play();
        }

        Destroy(go, vfxLifetime);
    }

    // --- 나머지 서버 로직 함수들 ---
    private IEnumerator FireDoTDamage(IDamageable target)
    {
        float elapsed = 0f;
        while (elapsed < fireDoTDuration && target != null)
        {
            target.TakeHit(fireDoTDamagePerTick, new RaycastHit());
            elapsed += fireDoTInterval;
            yield return new WaitForSeconds(fireDoTInterval);
        }
    }

    private IEnumerator SlowPlayer(Starter.Platformer.Player player)
    {
        if (player == null) yield break;
        float originalSpeed = player.moveSpeed;
        player.moveSpeed *= (1f - iceSlowAmount);
        yield return new WaitForSeconds(iceSlowDuration);
        if (player != null) player.moveSpeed = originalSpeed;
    }

    private void ApplyElectricChainLogic(Vector3 origin, float baseDamage, Starter.Platformer.Player owner)
    {
        Collider[] enemies = Physics.OverlapSphere(origin, electricChainRadius, raycastLayerMask);
        int count = 0;
        foreach (var col in enemies)
        {
            if (count >= electricMaxChains) break;
            if (col.transform.root.gameObject == owner.gameObject) continue;
            if (col.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeHit(baseDamage * electricChainDamageMultiplier, new RaycastHit());
                count++;
            }
        }
    }

    private void ApplyWaterKnockbackLogic(IDamageable target, Vector3 hitPos)
    {
        if (target is MonoBehaviour mb && mb.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 dir = (mb.transform.position - hitPos).normalized;
            dir.y = 0.3f;
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(dir * waterKnockbackForce, ForceMode.Impulse);
        }
    }
}