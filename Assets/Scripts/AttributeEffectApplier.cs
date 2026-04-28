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
    public GameObject fireParticlePrefab;
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
    public LayerMask targetLayerMask; // Inspector에서 Player와 Enemy 레이어를 모두 체크하세요!

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // [변경점] owner를 GameObject로 받아 플레이어/적 모두 대응 가능하게 함
    public void ApplyAttributeEffect(TargetAttribute attribute, IDamageable target, Vector3 targetPosition, float baseDamage, GameObject owner)
    {
        if (target == null || Runner == null) return;

        if (Object.HasStateAuthority)
        {
            ApplyLogicOnServer(attribute, target, baseDamage, owner, targetPosition);
        }

        if (target is NetworkBehaviour targetNB && targetNB.Object != null)
        {
            RPC_PlayAttributeVFX(attribute, targetNB.Object.Id, targetPosition);
        }
    }

    private void ApplyLogicOnServer(TargetAttribute attribute, IDamageable target, float baseDamage, GameObject owner, Vector3 position)
    {
        switch (attribute)
        {
            case TargetAttribute.Fire:
                StartCoroutine(FireDoTDamage(target));
                break;
            case TargetAttribute.Ice:
                if (target is MonoBehaviour mb)
                {
                    // 타겟이 플레이어인 경우
                    if (mb.TryGetComponent<Starter.Platformer.Player>(out var p))
                        StartCoroutine(SlowPlayer(p));
                    // 타겟이 적인 경우
                    else if (mb.TryGetComponent<Enemy>(out var e))
                        StartCoroutine(SlowEnemy(e));
                }
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
        if (prefab == null) return;

        Vector3 spawnPos = (parent != null) ? parent.position : fallbackPos;
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);

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

    private IEnumerator SlowEnemy(Enemy enemy)
    {
        if (enemy == null) yield break;
        float originalSpeed = enemy.agent.speed;
        enemy.agent.speed *= (1f - iceSlowAmount);
        yield return new WaitForSeconds(iceSlowDuration);
        if (enemy != null) enemy.agent.speed = originalSpeed;
    }

    private void ApplyElectricChainLogic(Vector3 origin, float baseDamage, GameObject owner)
    {
        Collider[] hits = Physics.OverlapSphere(origin, electricChainRadius, targetLayerMask);
        int count = 0;
        foreach (var col in hits)
        {
            if (count >= electricMaxChains) break;
            // 공격자 본인 제외
            if (owner != null && col.transform.root.gameObject == owner) continue;

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