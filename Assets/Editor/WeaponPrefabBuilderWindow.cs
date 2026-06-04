using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 모델 → 네트워크 무기 변환 UI / 진입점 모음.
///  - Tools > Weapon Prefab Builder : 창에서 타입별로 직접 생성
///  - 프로젝트 창 우클릭 > Create Network Weapon : 선택한 모델 일괄 생성(Projectile 기본)
///  - Assets/WeaponModels/ 폴더 자동 감시 : 넣기만 하면 자동 생성(Projectile 기본)
/// </summary>
public class WeaponPrefabBuilderWindow : EditorWindow
{
    private enum Mode { 새로만들기, 기존무기에추가 }
    private Mode _mode = Mode.새로만들기;
    private GameObject _existingWeapon;

    private GameObject _model;
    private string _name = "";
    private WeaponType _type = WeaponType.Projectile;
    private RuntimeAnimatorController _controller;
    private Vector3 _firePos = new Vector3(0f, 0.45f, 0.03f);

    // Projectile
    private GameObject _projectileAsset;
    private int _bufferSize = 2;
    private GameObject _projectileVisual;
    private GameObject _projectileHitEffect;
    private float _projectileImpulse = 30f;
    private float _projectileLifeTime = 4f;
    // Laser
    private GameObject _laserVfx;
    // Area
    private GameObject _areaScope;
    private GameObject _areaVfx;
    // Strike
    private GameObject _strikeScope;
    private GameObject _strikeParticle;

    [MenuItem("Tools/Weapon Prefab Builder")]
    public static void Open() => GetWindow<WeaponPrefabBuilderWindow>("Weapon Prefab Builder");

    private void OnGUI()
    {
        _mode = (Mode)EditorGUILayout.EnumPopup("작업", _mode);

        if (_mode == Mode.새로만들기)
        {
            EditorGUILayout.HelpBox(
                "무기 모델을 넣으면 Resources/WeaponPrefabs 에 네트워크 무기 프리팹으로 새로 만들어 줍니다.\n" +
                "수치는 기존대로 CSV(Tools > Sync Weapon Data)에서 채워집니다.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "이미 만들어 둔 무기 프리팹은 그대로 두고, 나중에 만든 투척물/이펙트만 끼워 넣습니다.\n" +
                "(firePos 위치 등 기존 수정값은 보존됩니다)",
                MessageType.Info);
        }

        EditorGUILayout.Space();
        if (_mode == Mode.새로만들기)
        {
            EditorGUI.BeginChangeCheck();
            _model = (GameObject)EditorGUILayout.ObjectField("모델 프리팹", _model, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck() && _model != null && string.IsNullOrEmpty(_name))
                _name = _model.name;
            _name = EditorGUILayout.TextField("무기 이름(접미사 Weapon 자동)", _name);
        }
        else
        {
            _existingWeapon = (GameObject)EditorGUILayout.ObjectField(
                "기존 무기 프리팹", _existingWeapon, typeof(GameObject), false);
        }

        _type = (WeaponType)EditorGUILayout.EnumPopup("무기 타입", _type);
        if (_mode == Mode.새로만들기)
        {
            _controller = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                "Animator Controller(비우면 기본값)", _controller, typeof(RuntimeAnimatorController), false);
            _firePos = EditorGUILayout.Vector3Field("firePos 위치", _firePos);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"[{_type}] 타입 설정", EditorStyles.boldLabel);
        switch (_type)
        {
            case WeaponType.Projectile:
                EditorGUILayout.HelpBox("파티클/모델 프리팹을 넣으면 그걸 투척물(투사체)로 만들어 무기에 연결합니다.", MessageType.Info);
                _projectileVisual = (GameObject)EditorGUILayout.ObjectField(
                    "투척물로 쓸 파티클 프리팹", _projectileVisual, typeof(GameObject), false);
                _projectileImpulse = EditorGUILayout.FloatField("발사 힘(impulse)", _projectileImpulse);
                _projectileLifeTime = EditorGUILayout.FloatField("수명(초)", _projectileLifeTime);
                _bufferSize = EditorGUILayout.IntSlider("버퍼 크기", _bufferSize, 1, 8);

                EditorGUILayout.Space();
                _projectileAsset = (GameObject)EditorGUILayout.ObjectField(
                    "(선택) 이미 만든 투사체가 있으면", _projectileAsset, typeof(GameObject), false);
                break;
            case WeaponType.Laser:
                _laserVfx = (GameObject)EditorGUILayout.ObjectField(
                    "레이저 빔 VFX/LineRenderer", _laserVfx, typeof(GameObject), false);
                break;
            case WeaponType.Area:
                _areaScope = (GameObject)EditorGUILayout.ObjectField("AttackScope(조준 범위)", _areaScope, typeof(GameObject), false);
                _areaVfx = (GameObject)EditorGUILayout.ObjectField("장판 VFX/파티클", _areaVfx, typeof(GameObject), false);
                break;
            case WeaponType.Strike:
                _strikeScope = (GameObject)EditorGUILayout.ObjectField("AttackScope(조준 범위)", _strikeScope, typeof(GameObject), false);
                _strikeParticle = (GameObject)EditorGUILayout.ObjectField("낙하 파티클", _strikeParticle, typeof(GameObject), false);
                break;
        }

        EditorGUILayout.Space();

        WeaponPrefabBuilder.Options BuildOptions() => new WeaponPrefabBuilder.Options
        {
            weaponType = _type,
            animatorController = _controller,
            firePosLocalPosition = _firePos,
            overwrite = true,
            projectileAsset = _projectileAsset,
            bufferSize = _bufferSize,
            createProjectileIfMissing = true,
            projectileVisual = _projectileVisual,
            projectileHitEffect = _projectileHitEffect,
            projectileImpulse = _projectileImpulse,
            projectileLifeTime = _projectileLifeTime,
            laserVfxAsset = _laserVfx,
            attackScopeAsset = _areaScope,
            areaVfxAsset = _areaVfx,
            strikeScopeAsset = _strikeScope,
            strikeParticleAsset = _strikeParticle,
        };

        if (_mode == Mode.새로만들기)
        {
            using (new EditorGUI.DisabledScope(_model == null))
            {
                if (GUILayout.Button("네트워크 무기 생성", GUILayout.Height(32)))
                {
                    GameObject result = WeaponPrefabBuilder.Build(_model, _name, BuildOptions());
                    if (result != null)
                    {
                        EditorGUIUtility.PingObject(result);
                        Selection.activeObject = result;
                    }
                    ShowResultDialog(result != null);
                }
            }
        }
        else
        {
            using (new EditorGUI.DisabledScope(_existingWeapon == null))
            {
                if (GUILayout.Button("기존 무기에 투척물/이펙트 추가", GUILayout.Height(32)))
                {
                    bool ok = WeaponPrefabBuilder.AttachToExisting(_existingWeapon, BuildOptions());
                    if (ok) EditorGUIUtility.PingObject(_existingWeapon);
                    ShowResultDialog(ok);
                }
            }
        }
    }

    private static void ShowResultDialog(bool ranOk)
    {
        string msg = WeaponPrefabBuilder.LastMessage;
        if (string.IsNullOrEmpty(msg))
            msg = ranOk ? "완료되었습니다." : "실패했습니다. Console 로그를 확인하세요.";

        bool connected = ranOk && WeaponPrefabBuilder.LastConnected;
        string title = connected ? "완료 ✅" : "확인 필요 ⚠";
        EditorUtility.DisplayDialog(title, msg, "확인");
    }

    // --- 프로젝트 창 우클릭 메뉴 (Projectile 기본) ---
    [MenuItem("Assets/Create Network Weapon", false, 30)]
    private static void CreateFromSelection()
    {
        foreach (var obj in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            WeaponPrefabBuilder.Build(obj, obj.name);
    }

    [MenuItem("Assets/Create Network Weapon", true)]
    private static bool CreateFromSelectionValidate()
    {
        foreach (var _ in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            return true;
        return false;
    }
}

/// <summary>
/// Assets/WeaponModels/ 폴더에 모델/프리팹을 넣으면 자동으로 네트워크 무기를 생성한다.
/// (Projectile 타입으로 생성하며, 이미 같은 이름의 무기 프리팹이 있으면 건너뛴다.)
/// </summary>
public class WeaponModelAutoImporter : AssetPostprocessor
{
    public const string WatchFolder = "Assets/WeaponModels";

    private static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        var toBuild = new List<string>();

        void Consider(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!path.Replace('\\', '/').StartsWith(WatchFolder + "/")) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".prefab" && ext != ".fbx" && ext != ".obj" && ext != ".blend") return;
            if (!toBuild.Contains(path)) toBuild.Add(path);
        }

        foreach (var p in importedAssets) Consider(p);
        foreach (var p in movedAssets) Consider(p);
        if (toBuild.Count == 0) return;

        // 임포트 파이프라인 도중 프리팹 저장을 피하기 위해 한 프레임 뒤로 미룸
        EditorApplication.delayCall += () =>
        {
            foreach (var path in toBuild)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                string baseName = Path.GetFileNameWithoutExtension(path);
                string outPath = $"{WeaponPrefabBuilder.OutputFolder}/{baseName}{WeaponPrefabBuilder.WeaponSuffix}.prefab";
                if (File.Exists(outPath))
                {
                    Debug.Log($"[WeaponModelAutoImporter] 이미 존재하여 자동 생성 건너뜀: {outPath}");
                    continue;
                }

                WeaponPrefabBuilder.Build(model, baseName,
                    new WeaponPrefabBuilder.Options
                    {
                        weaponType = WeaponType.Projectile,
                        overwrite = false,
                        createProjectileIfMissing = true, // 투사체 없으면 같이 만들어 바로 발사 가능하게
                    });
            }
            AssetDatabase.SaveAssets();
        };
    }
}
