using System.IO;
using Fusion;
using Fusion.Addons.Physics;
using Projectiles;
using Projectiles.NetworkObjectExample;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 무기 "모델" 프리팹을 Resources/WeaponPrefabs 에 있는 무기들처럼
/// 네트워크 무기 프리팹(NetworkObject + Weapon_NetworkObject + firePos ...)으로 자동 변환해 주는 도구.
///
/// 무기 타입별 자동 구성:
///  - Projectile : Ammo 프리팹을 따로 만들어 두면(Resources/ProjectilePrefabs/{이름}Ammo) 이름으로 자동 연결 + 버퍼 구성
///  - Laser      : 빔 VFX 프리팹을 자식으로 넣고 VisualEffect(또는 LineRenderer)에 연결
///  - Area       : AttackScope + 장판 VFX 프리팹을 자식으로 넣고 attackScope / VisualEffect 에 연결
///  - Strike     : AttackScope + 낙하 파티클 프리팹을 자식으로 넣고 attackScope / ParticleEffect 에 연결
///
/// 무기 수치(데미지/공격타입/쿨타임 등)는 기존대로 CSV(Tools > Sync Weapon Data) 에서 채워집니다.
/// </summary>
public static class WeaponPrefabBuilder
{
    public const string OutputFolder = "Assets/Resources/WeaponPrefabs";
    public const string WeaponSuffix = "Weapon";

    public const string ProjectileFolder = "Assets/Resources/ProjectilePrefabs";
    public const string ProjectileSuffix = "Ammo";

    // 기본 애니메이터 컨트롤러 (testWeapon.controller, "Attack" 트리거 보유)
    private const string DefaultAnimatorControllerGuid = "5f45187e68dd2e44291a71fe2a4e15e0";

    private const int WeaponLayer = 6;          // 기존 무기 프리팹 m_Layer
    private const int DefaultRaycastBits = 320; // 기존 무기 raycastLayerMask m_Bits

    /// <summary>마지막 작업 결과 메시지(창에서 팝업으로 보여주기 위함).</summary>
    public static string LastMessage { get; private set; }
    /// <summary>마지막 작업에서 투척물/이펙트가 실제로 연결됐는지(경고 '※' 없음).</summary>
    public static bool LastConnected { get; private set; }

    private static void Report(string fullMessage, string summary)
    {
        LastMessage = summary;
        LastConnected = string.IsNullOrEmpty(summary) || !summary.Contains("※");
        if (LastConnected)
            Debug.Log($"<color=#00FF00><b>[WeaponPrefabBuilder]</b></color> {fullMessage}");
        else
            Debug.LogWarning($"[WeaponPrefabBuilder] {fullMessage}");
    }

    public class Options
    {
        public WeaponType weaponType = WeaponType.Projectile;

        public RuntimeAnimatorController animatorController;
        public int raycastMaskBits = DefaultRaycastBits;
        public Vector3 firePosLocalPosition = new Vector3(0f, 0.45f, 0.03f);
        public bool overwrite = true;

        // Projectile
        public GameObject projectileAsset; // null 이면 {이름}Ammo 로 자동 탐색
        public int bufferSize = 2;
        // 투사체가 없을 때 자동으로 투사체 프리팹까지 만들지
        public bool createProjectileIfMissing = false;
        public GameObject projectileVisual;     // 투사체에 붙일 비주얼 모델(선택)
        public GameObject projectileHitEffect;  // 명중 이펙트(선택)
        public float projectileImpulse = 30f;
        public float projectileLifeTime = 4f;
        public float projectileColliderRadius = 0.2f;

        // Laser
        public GameObject laserVfxAsset;   // VisualEffect / LineRenderer 보유 프리팹

        // Area
        public GameObject attackScopeAsset; // 조준 범위 표시 오브젝트
        public GameObject areaVfxAsset;     // 장판 VFX(VisualEffect) 또는 파티클

        // Strike
        public GameObject strikeScopeAsset;   // 조준 범위 표시 오브젝트
        public GameObject strikeParticleAsset; // 낙하 파티클(ParticleSystem)
    }

    /// <summary>
    /// 모델 프리팹/에셋을 네트워크 무기 프리팹으로 변환. 생성된 프리팹 에셋을 반환.
    /// </summary>
    public static GameObject Build(GameObject modelAsset, string baseName, Options options = null)
    {
        if (modelAsset == null)
        {
            Debug.LogError("[WeaponPrefabBuilder] 모델이 비어 있습니다.");
            return null;
        }

        options ??= new Options();

        if (string.IsNullOrEmpty(baseName))
            baseName = modelAsset.name;

        string weaponName = baseName.EndsWith(WeaponSuffix) ? baseName : baseName + WeaponSuffix;

        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        string outPath = $"{OutputFolder}/{weaponName}.prefab";

        if (File.Exists(outPath) && !options.overwrite)
        {
            Debug.Log($"[WeaponPrefabBuilder] 이미 존재하여 건너뜀: {outPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(outPath);
        }

        GameObject root = new GameObject(weaponName);
        try
        {
            root.layer = WeaponLayer;

            // 모델을 자식으로 (프리팹/모델 링크 유지)
            GameObject modelInstance = InstantiateAsChild(modelAsset, root.transform);
            SetLayerRecursively(modelInstance, WeaponLayer);

            // 발사 위치
            GameObject firePos = new GameObject("firePos") { layer = WeaponLayer };
            firePos.transform.SetParent(root.transform, false);
            firePos.transform.localPosition = options.firePosLocalPosition;

            // Animator
            Animator animator = root.AddComponent<Animator>();
            RuntimeAnimatorController controller = options.animatorController != null
                ? options.animatorController
                : LoadDefaultController();
            if (controller != null)
                animator.runtimeAnimatorController = controller;

            // NetworkObject (Fusion 이 저장 시 자동 베이크)
            root.AddComponent<NetworkObject>();

            // Weapon_NetworkObject
            Weapon_NetworkObject weapon = root.AddComponent<Weapon_NetworkObject>();

            var weaponSo = new SerializedObject(weapon);
            SetObjectRef(weaponSo, "_fireTransform", firePos.transform);
            SetObjectRef(weaponSo, "Animator", animator);
            SetLayerMask(weaponSo, "raycastLayerMask", options.raycastMaskBits);

            string summary;
            switch (options.weaponType)
            {
                case WeaponType.Projectile:
                    summary = WireProjectile(root, weaponSo, baseName, options);
                    break;
                case WeaponType.Laser:
                    summary = WireLaser(root, weaponSo, options);
                    break;
                case WeaponType.Area:
                    summary = WireArea(root, weaponSo, options);
                    break;
                case WeaponType.Strike:
                    summary = WireStrike(root, weaponSo, options);
                    break;
                default:
                    summary = "(알 수 없는 타입)";
                    break;
            }

            weaponSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, outPath, out bool success);
            if (!success)
            {
                Debug.LogError($"[WeaponPrefabBuilder] 프리팹 저장 실패: {outPath}");
                return null;
            }

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate); // Fusion 베이크 트리거

            Report($"[{options.weaponType}] 네트워크 무기 생성: {outPath}\n{summary}", summary);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 이미 만들어 둔 네트워크 무기 프리팹은 그대로 두고, 투척물/이펙트만 끼워 넣는다.
    /// (firePos 위치 등 기존 수정값은 보존됨)
    /// </summary>
    public static bool AttachToExisting(GameObject weaponPrefabAsset, Options options)
    {
        if (weaponPrefabAsset == null)
        {
            Debug.LogError("[WeaponPrefabBuilder] 대상 무기 프리팹이 비어 있습니다.");
            return false;
        }

        string path = AssetDatabase.GetAssetPath(weaponPrefabAsset);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
        {
            Debug.LogError("[WeaponPrefabBuilder] 프리팹 에셋이 아닙니다: " + path);
            return false;
        }

        options ??= new Options();

        // 프리팹 내용을 안전하게 열어 수정
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var weapon = root.GetComponent<Weapon_NetworkObject>();
            if (weapon == null)
            {
                Debug.LogError("[WeaponPrefabBuilder] Weapon_NetworkObject 가 없는 프리팹입니다: " + path);
                return false;
            }

            // 프리팹 이름에서 baseName 추출 (FireWandWeapon -> FireWand)
            string baseName = root.name.EndsWith(WeaponSuffix)
                ? root.name.Substring(0, root.name.Length - WeaponSuffix.Length)
                : root.name;

            var weaponSo = new SerializedObject(weapon);
            string summary;
            switch (options.weaponType)
            {
                case WeaponType.Projectile: summary = WireProjectile(root, weaponSo, baseName, options); break;
                case WeaponType.Laser:      summary = WireLaser(root, weaponSo, options); break;
                case WeaponType.Area:       summary = WireArea(root, weaponSo, options); break;
                case WeaponType.Strike:     summary = WireStrike(root, weaponSo, options); break;
                default:                    summary = "(알 수 없는 타입)"; break;
            }
            weaponSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Report($"[{options.weaponType}] 기존 무기에 추가: {path}\n{summary}", summary);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ----- 타입별 와이어링 -----

    private static string WireProjectile(GameObject root, SerializedObject weaponSo, string baseName, Options o)
    {
        PhysicsProjectile projectile;
        NetworkObject projectileNo;
        string source;

        if (o.projectileAsset != null)
        {
            projectile = o.projectileAsset.GetComponent<PhysicsProjectile>();
            projectileNo = o.projectileAsset.GetComponent<NetworkObject>();
            source = $"슬롯에 넣은 '{o.projectileAsset.name}'";
        }
        else
        {
            FindProjectileByName(baseName, out projectile, out projectileNo);
            source = $"Resources/ProjectilePrefabs/{baseName}{ProjectileSuffix}.prefab";
        }

        // 투사체가 아예 없으면, 옵션에 따라 자동으로 만들어 준다
        if (projectile == null && projectileNo == null && o.projectileAsset == null && o.createProjectileIfMissing)
        {
            GameObject ammo = BuildProjectile(baseName, o);
            if (ammo != null)
            {
                projectile = ammo.GetComponent<PhysicsProjectile>();
                projectileNo = ammo.GetComponent<NetworkObject>();
                source = AssetDatabase.GetAssetPath(ammo);
            }
        }

        if (projectile == null && projectileNo == null && o.projectileAsset == null)
        {
            SetBool(weaponSo, "_useBuffer", false);
            return $"※ 투사체 미연결 — {source} 파일이 없습니다. '투사체 자동 생성'을 켜거나, 투사체 프리팹을 슬롯에 직접 넣으세요.";
        }
        if (projectile == null || projectileNo == null)
        {
            SetBool(weaponSo, "_useBuffer", false);
            string missing = projectile == null ? "PhysicsProjectile" : "NetworkObject";
            return $"※ 투사체 미연결 — {source} 에 '{missing}' 컴포넌트가 없습니다. (투사체 프리팹은 NetworkObject + PhysicsProjectile 둘 다 있어야 합니다)";
        }

        var buffer = root.GetComponent<NetworkObjectBuffer>();
        if (buffer == null) buffer = root.AddComponent<NetworkObjectBuffer>();
        var bufferSo = new SerializedObject(buffer);
        SetObjectRef(bufferSo, "_prefab", projectileNo);
        SetInt(bufferSo, "_bufferSize", Mathf.Clamp(o.bufferSize, 1, NetworkObjectBuffer.CAPACITY));
        bufferSo.ApplyModifiedPropertiesWithoutUndo();

        SetObjectRef(weaponSo, "_projectilePrefab", projectile);
        SetBool(weaponSo, "_useBuffer", true);
        SetObjectRef(weaponSo, "_projectileBuffer", buffer);
        return $"(투사체 연결: {projectile.name}, 버퍼 {o.bufferSize})";
    }

    private static string WireLaser(GameObject root, SerializedObject weaponSo, Options o)
    {
        if (o.laserVfxAsset == null)
            return "※ 레이저 VFX 미연결 (Laser VFX 슬롯에 빔 프리팹을 넣어주세요)";

        GameObject vfx = InstantiateAsChild(o.laserVfxAsset, root.transform);
        SetLayerRecursively(vfx, WeaponLayer);

        var visualEffect = vfx.GetComponentInChildren<VisualEffect>(true);
        var lineRenderer = vfx.GetComponentInChildren<LineRenderer>(true);
        var particle = vfx.GetComponentInChildren<ParticleSystem>(true);

        if (visualEffect != null) SetObjectRef(weaponSo, "VisualEffect", visualEffect);
        if (lineRenderer != null) SetObjectRef(weaponSo, "LineRenderer", lineRenderer);
        if (particle != null) SetObjectRef(weaponSo, "ParticleEffect", particle);

        if (visualEffect == null && lineRenderer == null)
            return "※ 넣은 프리팹에 VisualEffect/LineRenderer 가 없습니다";
        return "(레이저 VFX 연결됨)";
    }

    private static string WireArea(GameObject root, SerializedObject weaponSo, Options o)
    {
        string msg = "(장판 ";
        if (o.attackScopeAsset != null)
        {
            GameObject scope = InstantiateAsChild(o.attackScopeAsset, root.transform);
            SetLayerRecursively(scope, WeaponLayer);
            SetObjectRef(weaponSo, "attackScope", scope);
            msg += "Scope ";
        }
        if (o.areaVfxAsset != null)
        {
            GameObject vfx = InstantiateAsChild(o.areaVfxAsset, root.transform);
            SetLayerRecursively(vfx, WeaponLayer);
            var visualEffect = vfx.GetComponentInChildren<VisualEffect>(true);
            var particle = vfx.GetComponentInChildren<ParticleSystem>(true);
            if (visualEffect != null) { SetObjectRef(weaponSo, "VisualEffect", visualEffect); msg += "VFX "; }
            if (particle != null) { SetObjectRef(weaponSo, "ParticleEffect", particle); msg += "Particle "; }
        }
        if (o.attackScopeAsset == null && o.areaVfxAsset == null)
            return "※ 장판 Scope/VFX 미연결";
        return msg + "연결됨)";
    }

    private static string WireStrike(GameObject root, SerializedObject weaponSo, Options o)
    {
        string msg = "(낙하 ";
        if (o.strikeScopeAsset != null)
        {
            GameObject scope = InstantiateAsChild(o.strikeScopeAsset, root.transform);
            SetLayerRecursively(scope, WeaponLayer);
            SetObjectRef(weaponSo, "attackScope", scope);
            msg += "Scope ";
        }
        if (o.strikeParticleAsset != null)
        {
            GameObject p = InstantiateAsChild(o.strikeParticleAsset, root.transform);
            SetLayerRecursively(p, WeaponLayer);
            var particle = p.GetComponentInChildren<ParticleSystem>(true);
            if (particle != null) { SetObjectRef(weaponSo, "ParticleEffect", particle); msg += "Particle "; }
        }
        if (o.strikeScopeAsset == null && o.strikeParticleAsset == null)
            return "※ 낙하 Scope/Particle 미연결";
        return msg + "연결됨)";
    }

    /// <summary>
    /// 기존 Ammo 프리팹과 같은 구조로 투사체 프리팹을 새로 만들어 Resources/ProjectilePrefabs 에 저장한다.
    /// (NetworkObject + NetworkRigidbody3D + Rigidbody + SphereCollider + PhysicsProjectile)
    /// </summary>
    public static GameObject BuildProjectile(string baseName, Options o)
    {
        o ??= new Options();
        if (!Directory.Exists(ProjectileFolder))
            Directory.CreateDirectory(ProjectileFolder);

        string outPath = $"{ProjectileFolder}/{baseName}{ProjectileSuffix}.prefab";

        GameObject root = new GameObject($"{baseName}{ProjectileSuffix}");
        try
        {
            var col = root.AddComponent<SphereCollider>();
            col.radius = o.projectileColliderRadius;

            root.AddComponent<NetworkObject>();

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            root.AddComponent<NetworkRigidbody3D>();
            var proj = root.AddComponent<PhysicsProjectile>();

            if (o.projectileVisual != null)
                InstantiateAsChild(o.projectileVisual, root.transform);

            var so = new SerializedObject(proj);
            SetFloat(so, "_initialImpulse", o.projectileImpulse);
            SetFloat(so, "_lifeTime", o.projectileLifeTime);
            if (o.projectileHitEffect != null)
                SetObjectRef(so, "_hitEffect", o.projectileHitEffect);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, outPath, out bool success);
            if (!success)
            {
                Debug.LogError($"[WeaponPrefabBuilder] 투사체 저장 실패: {outPath}");
                return null;
            }
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate); // Fusion 베이크
            Debug.Log($"<color=#00FF00><b>[WeaponPrefabBuilder]</b></color> 투사체 생성: {outPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ----- helpers -----

    private static void FindProjectileByName(string baseName, out PhysicsProjectile projectile, out NetworkObject networkObject)
    {
        projectile = null;
        networkObject = null;
        string ammoPath = $"{ProjectileFolder}/{baseName}{ProjectileSuffix}.prefab";
        GameObject ammo = AssetDatabase.LoadAssetAtPath<GameObject>(ammoPath);
        if (ammo == null) return;
        projectile = ammo.GetComponent<PhysicsProjectile>();
        networkObject = ammo.GetComponent<NetworkObject>();
    }

    private static GameObject InstantiateAsChild(GameObject asset, Transform parent)
    {
        GameObject inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (inst == null) inst = Object.Instantiate(asset);
        inst.name = asset.name;
        inst.transform.SetParent(parent, false);
        return inst;
    }

    private static RuntimeAnimatorController LoadDefaultController()
    {
        string path = AssetDatabase.GUIDToAssetPath(DefaultAnimatorControllerGuid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void SetObjectRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[WeaponPrefabBuilder] 필드를 찾을 수 없음: {prop}");
    }

    private static void SetBool(SerializedObject so, string prop, bool value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string prop, int value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.intValue = value;
    }

    private static void SetFloat(SerializedObject so, string prop, float value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.floatValue = value;
    }

    private static void SetLayerMask(SerializedObject so, string prop, int bits)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.intValue = bits;
    }
}