using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 타겟(보통 로컬 플레이어) 사이를 막는 건물, 또는 카메라가 건물 안에 들어간 경우를 감지해서
/// Custom/DitherDissolve 셰이더로 점점 점선처럼 흐릿하게(디더 패턴 디졸브) 만든다.
/// 일반 알파 블렌딩과 달리 클립 기반이라 투명 정렬 문제 없이 플레이어/적이 잘 보인다.
/// </summary>
public class CameraOcclusionFade : MonoBehaviour
{
    public Transform target;
    public LayerMask occluderMask;
    [Range(0f, 1f)] public float maxDissolve = 0.85f;
    public float fadeSpeed = 6f;
    public float targetHeightOffset = 1.2f;
    public float cameraInsideCheckRadius = 0.5f;

    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private Shader _ditherShader;

    private class FadeEntry
    {
        public Renderer renderer;
        public Material[] dissolveMaterials;
        public Material[] originalMaterials;
        public float currentDissolve;
        public bool wantFaded;
        // 카메라가 건물 안에 직접 들어간 경우엔 점선(디더) 패턴으로 일부가 계속 보이면
        // (창틀 등 디테일이 카메라에 바로 붙어 있어서) 시야를 가린다. 이 경우엔 점선 없이 완전히 지운다.
        public bool wantFullyHidden;
    }

    private readonly Dictionary<Renderer, FadeEntry> _entries = new();
    private readonly HashSet<Renderer> _hitThisFrame = new();
    private readonly HashSet<Renderer> _insideThisFrame = new();
    private readonly List<Renderer> _toRemove = new();

    private void Awake()
    {
        if (occluderMask.value == 0)
            occluderMask = 1 << LayerMask.NameToLayer("Building");

        _ditherShader = Shader.Find("Custom/DitherDissolve");
        if (_ditherShader == null)
            Debug.LogError("[CameraOcclusionFade] Custom/DitherDissolve 셰이더를 찾을 수 없습니다.");
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            var localPlayer = GameManager.Instance != null ? GameManager.Instance.LocalPlayer : null;
            if (localPlayer != null) target = localPlayer.transform;
        }
        if (target == null || _ditherShader == null) return;

        _hitThisFrame.Clear();
        _insideThisFrame.Clear();

        // 1) 카메라와 타겟 사이를 가리는 건물 (부분 디더 디졸브)
        Vector3 targetPos = target.position + Vector3.up * targetHeightOffset;
        Vector3 toTarget = targetPos - transform.position;
        float dist = toTarget.magnitude;

        if (dist > 0.01f)
        {
            var hits = Physics.RaycastAll(transform.position, toTarget.normalized, dist, occluderMask);
            foreach (var hit in hits)
                CollectRenderers(hit.collider, _hitThisFrame);
        }

        // 2) 카메라 자체가 건물 내부에 들어간 경우 (얇은 구체로 겹치는 콜라이더 탐지) - 완전히 클립
        var overlaps = Physics.OverlapSphere(transform.position, cameraInsideCheckRadius, occluderMask);
        foreach (var col in overlaps)
            CollectRenderers(col, _insideThisFrame);

        foreach (var rend in _hitThisFrame)
        {
            if (!_entries.TryGetValue(rend, out var entry))
            {
                entry = CreateEntry(rend);
                if (entry == null) continue;
                _entries[rend] = entry;
            }
            entry.wantFaded = true;
        }
        foreach (var rend in _insideThisFrame)
        {
            if (!_entries.TryGetValue(rend, out var entry))
            {
                entry = CreateEntry(rend);
                if (entry == null) continue;
                _entries[rend] = entry;
            }
            entry.wantFaded = true;
            entry.wantFullyHidden = true;
        }

        foreach (var entry in _entries.Values)
        {
            if (!_hitThisFrame.Contains(entry.renderer) && !_insideThisFrame.Contains(entry.renderer))
                entry.wantFaded = false;
            if (!_insideThisFrame.Contains(entry.renderer))
                entry.wantFullyHidden = false;
        }

        _toRemove.Clear();
        foreach (var kv in _entries)
        {
            var entry = kv.Value;
            if (entry.renderer == null) { _toRemove.Add(kv.Key); continue; }

            float targetDissolve = entry.wantFaded ? (entry.wantFullyHidden ? 1f : maxDissolve) : 0f;
            entry.currentDissolve = Mathf.MoveTowards(entry.currentDissolve, targetDissolve, fadeSpeed * Time.deltaTime);

            if (entry.currentDissolve > 0.001f)
            {
                if (entry.renderer.sharedMaterials != entry.dissolveMaterials)
                    entry.renderer.materials = entry.dissolveMaterials;
                foreach (var mat in entry.dissolveMaterials)
                    if (mat != null) mat.SetFloat(DissolveAmountId, entry.currentDissolve);
            }
            else if (!entry.wantFaded)
            {
                entry.renderer.sharedMaterials = entry.originalMaterials;
                _toRemove.Add(kv.Key);
            }
        }

        foreach (var key in _toRemove) _entries.Remove(key);
    }

    private void CollectRenderers(Collider col, HashSet<Renderer> into)
    {
        foreach (var rend in col.GetComponentsInChildren<Renderer>())
            into.Add(rend);
    }

    private FadeEntry CreateEntry(Renderer rend)
    {
        var originals = rend.sharedMaterials;
        var dissolveMats = new Material[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            if (originals[i] == null) { dissolveMats[i] = null; continue; }
            var mat = new Material(_ditherShader);
            mat.name = originals[i].name + " (Dissolve)";
            if (originals[i].HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", originals[i].GetTexture("_BaseMap"));
            if (originals[i].HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", originals[i].GetColor("_BaseColor"));
            dissolveMats[i] = mat;
        }

        return new FadeEntry { renderer = rend, originalMaterials = originals, dissolveMaterials = dissolveMats, currentDissolve = 0f };
    }
}
