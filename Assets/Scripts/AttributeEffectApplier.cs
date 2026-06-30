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

    // 대상별로 진행 중인 화염 도트 코루틴을 추적해, 새로 불이 붙으면 중첩 대신 갱신한다.
    private readonly System.Collections.Generic.Dictionary<IDamageable, Coroutine> _activeFireDoTs = new();

    // 대상별 둔화 상태 추적: 이미 둔화 중일 때 또 얼음 공격을 맞아도 속도를 또 곱하지 않고
    // (원래 속도 * (1 - iceSlowAmount)) 값을 유지한 채 지속시간만 갱신한다.
    // 예전엔 맞을 때마다 "현재(이미 느려진) 속도"에 다시 (1-iceSlowAmount)를 곱해서 중첩 적용했기 때문에,
    // 연속으로 여러 번 맞으면 속도가 기하급수적으로 줄어 0에 가까워져 캐릭터가 움직이지 못하는 버그가 있었다.
    private readonly System.Collections.Generic.Dictionary<IDamageable, (Coroutine co, float originalSpeed)> _activeIceSlows = new();

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
                // 이미 불타고 있으면 중첩시키지 않고 지속시간만 갱신(리프레시)한다.
                if (_activeFireDoTs.TryGetValue(target, out var existingDoT) && existingDoT != null)
                    StopCoroutine(existingDoT);
                _activeFireDoTs[target] = StartCoroutine(FireDoTDamage(target, owner));
                break;
            case TargetAttribute.Ice:
                if (target is MonoBehaviour mb)
                {
                    // 타겟이 플레이어인 경우
                    if (mb.TryGetComponent<Starter.Platformer.Player>(out var p))
                        ApplyIceSlow(target, p.moveSpeed, () => p != null ? p.moveSpeed : 0f, v => { if (p != null) p.moveSpeed = v; });
                    // 타겟이 적인 경우
                    else if (mb.TryGetComponent<Enemy>(out var e))
                        ApplyIceSlow(target, e.agent.speed, () => e != null && e.agent != null ? e.agent.speed : 0f, v => { if (e != null && e.agent != null) e.agent.speed = v; });
                }
                break;
            case TargetAttribute.Electric:
                ApplyElectricChainLogic(position, baseDamage, owner);
                break;
            case TargetAttribute.Water:
                ApplyWaterKnockbackLogic(target, position);
                break;
            case TargetAttribute.Normal:
                target.TakeHit(baseDamage * normalDamageBonusMultiplier, new RaycastHit(), owner);
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

    private IEnumerator FireDoTDamage(IDamageable target, GameObject owner)
    {
        float elapsed = 0f;
        while (elapsed < fireDoTDuration && target != null)
        {
            target.TakeHit(fireDoTDamagePerTick, new RaycastHit(), owner);
            elapsed += fireDoTInterval;
            yield return new WaitForSeconds(fireDoTInterval);
        }
        if (target != null) _activeFireDoTs.Remove(target);
    }

    // target: Dictionary 키로만 사용 (플레이어/적 공용 식별자), currentSpeed: 첫 적용 시점의 현재 속도(=원래 속도),
    // getSpeed/setSpeed: 플레이어냐 적이냐에 따라 다른 속도 필드를 다루기 위한 콜백.
    private void ApplyIceSlow(IDamageable target, float currentSpeed, System.Func<float> getSpeed, System.Action<float> setSpeed)
    {
        if (_activeIceSlows.TryGetValue(target, out var existing))
        {
            // 이미 둔화 중: 속도를 또 곱하지 않고 지속시간만 갱신한다.
            if (existing.co != null) StopCoroutine(existing.co);
            var refreshed = StartCoroutine(IceSlowRoutine(target, setSpeed, existing.originalSpeed));
            _activeIceSlows[target] = (refreshed, existing.originalSpeed);
        }
        else
        {
            float originalSpeed = currentSpeed;
            setSpeed(originalSpeed * (1f - iceSlowAmount));
            var co = StartCoroutine(IceSlowRoutine(target, setSpeed, originalSpeed));
            _activeIceSlows[target] = (co, originalSpeed);
        }
    }

    private IEnumerator IceSlowRoutine(IDamageable target, System.Action<float> setSpeed, float originalSpeed)
    {
        yield return new WaitForSeconds(iceSlowDuration);
        setSpeed(originalSpeed);
        _activeIceSlows.Remove(target);
    }

    /// <summary>
    /// 치트(F6): 대상에게 걸려있는 화염 도트/얼음 둔화를 강제로 즉시 해제한다.
    /// 둔화는 원래 속도로 복원하고, 도트는 더 이상 틱이 들어가지 않게 코루틴만 정지한다.
    /// </summary>
    public void CheatClearStatusEffects(IDamageable target, System.Action<float> setSpeed)
    {
        if (_activeIceSlows.TryGetValue(target, out var slow))
        {
            if (slow.co != null) StopCoroutine(slow.co);
            setSpeed(slow.originalSpeed);
            _activeIceSlows.Remove(target);
        }

        if (_activeFireDoTs.TryGetValue(target, out var fireCo))
        {
            if (fireCo != null) StopCoroutine(fireCo);
            _activeFireDoTs.Remove(target);
        }
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
                damageable.TakeHit(baseDamage * electricChainDamageMultiplier, new RaycastHit(), owner);
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