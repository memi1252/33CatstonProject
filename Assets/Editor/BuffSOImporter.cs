using UnityEngine;
using UnityEditor;
using System.Net;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class BuffSOImporter : EditorWindow
{
    private const string ImprintCSV_URL =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vStTr_3obhFkhhsVXuKaaj6FFZ_B68LeWYFilpvg0AI_oWd9YeZdvypOP1lhV3yjGGYM_P1j5vppA28/pub?gid=847422572&single=true&output=csv";
    private const string WeaponCSV_URL =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vStTr_3obhFkhhsVXuKaaj6FFZ_B68LeWYFilpvg0AI_oWd9YeZdvypOP1lhV3yjGGYM_P1j5vppA28/pub?gid=0&single=true&output=csv";
    private const string EnemyCSV_URL =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vStTr_3obhFkhhsVXuKaaj6FFZ_B68LeWYFilpvg0AI_oWd9YeZdvypOP1lhV3yjGGYM_P1j5vppA28/pub?gid=610968711&single=true&output=csv";
    private const string SkillCSV_URL =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vStTr_3obhFkhhsVXuKaaj6FFZ_B68LeWYFilpvg0AI_oWd9YeZdvypOP1lhV3yjGGYM_P1j5vppA28/pub?gid=1774451517&single=true&output=csv";
    private const string ContractCSV_URL =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vStTr_3obhFkhhsVXuKaaj6FFZ_B68LeWYFilpvg0AI_oWd9YeZdvypOP1lhV3yjGGYM_P1j5vppA28/pub?gid=1295339202&single=true&output=csv";
    
    private const string ImprintSAVE_PATH = "Assets/ScriptableObject/Imprints";
    private const string WeaponSAVE_PATH = "Assets/ScriptableObject/Weapons";
    private const string EnemySAVE_PATH = "Assets/ScriptableObject/Enemies";
    private const string SkillSAVE_PATH = "Assets/ScriptableObject/Skills";
    private const string ContractSAVE_PATH = "Assets/ScriptableObject/Contracts";

    [MenuItem("Tools/Sync Imprint Data (CSV)")]
    public static void DownloadSheet()
    {
        using (WebClient client = new WebClient())
        {
            client.Encoding = System.Text.Encoding.UTF8;
            try
            {
                string csvContent = client.DownloadString(ImprintCSV_URL);
                if (string.IsNullOrEmpty(csvContent)) return;
                ProcessCSV(csvContent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BuffImporter] 오류 발생: {e.Message}");
            }
        }
    }

    [MenuItem("Tools/Sync Weapon Data (CSV)")]
    public static void DownloadWeaponSheet()
    {
        using (WebClient client = new WebClient())
        {
            client.Encoding = System.Text.Encoding.UTF8;
            try
            {
                string csvContent = client.DownloadString(WeaponCSV_URL);
                if (string.IsNullOrEmpty(csvContent)) return;
                ProcessWeaponCSV(csvContent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WeaponImporter] 오류 발생: {e.Message}");
            }
        }
    }

    [MenuItem("Tools/Sync Enemy Data (CSV)")]
    public static void DownloadEnemySheet()
    {
        using (WebClient client = new WebClient())
        {
            client.Encoding = System.Text.Encoding.UTF8;
            try
            {
                string csvContent = client.DownloadString(EnemyCSV_URL);
                if (string.IsNullOrEmpty(csvContent)) return;
                ProcessEnemyCSV(csvContent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EnemyImporter] 오류 발생: {e.Message}");
            }
        }
    }

    [MenuItem("Tools/Sync Skill Data (CSV)")]
    public static void DownloadSkillSheet()
    {
        using (WebClient client = new WebClient())
        {
            client.Encoding = System.Text.Encoding.UTF8;
            try
            {
                string csvContent = client.DownloadString(SkillCSV_URL);
                if (string.IsNullOrEmpty(csvContent)) return;
                ProcessSkillCSV(csvContent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SkillImporter] 오류 발생: {e.Message}");
            }
        }
    }

    [MenuItem("Tools/Sync Contract Data (CSV)")]
    public static void DownloadContractSheet()
    {
        using (WebClient client = new WebClient())
        {
            client.Encoding = System.Text.Encoding.UTF8;
            try
            {
                string csvContent = client.DownloadString(ContractCSV_URL);
                if (string.IsNullOrEmpty(csvContent)) return;
                ProcessContractCSV(csvContent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ContractImporter] 오류 발생: {e.Message}");
            }
        }
    }

    [MenuItem("Tools/Sync All Data (CSV)")]
    public static void DownloadAllSheets()
    {
        Debug.Log("<color=#FFFF00><b>[DataSync]</b></color> 모든 데이터 동기화를 시작합니다...");
        
        try
        {
            // Imprint 데이터 동기화
            Debug.Log("<color=#FFFF00><b>[DataSync]</b></color> Imprint 데이터 동기화 중...");
            DownloadSheet();
            
            // Weapon 데이터 동기화
            Debug.Log("<color=#FFFF00><b>[DataSync]</b></color> Weapon 데이터 동기화 중...");
            DownloadWeaponSheet();
            
            // Enemy 데이터 동기화
            Debug.Log("<color=#FFFF00><b>[DataSync]</b></color> Enemy 데이터 동기화 중...");
            DownloadEnemySheet();
            
            // Skill 데이터 동기화
            Debug.Log("<color=#FFFF00><b>[DataSync]</b></color> Skill 데이터 동기화 중...");
            DownloadSkillSheet();
            
            // Contract 데이터 동기화
            Debug.Log("<color=#FFFF00><b>[DataSync]</b></color> Contract 데이터 동기화 중...");
            DownloadContractSheet();
            
            Debug.Log("<color=#00FF00><b>[DataSync]</b></color> 🎉 모든 데이터 동기화가 완료되었습니다!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataSync] 전체 동기화 중 오류 발생: {e.Message}");
        }
    }

    private static void ProcessWeaponCSV(string csvContent)
    {
        string[] lines = csvContent.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (!Directory.Exists(WeaponSAVE_PATH)) Directory.CreateDirectory(WeaponSAVE_PATH);

        string csvRegexPattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";

        for (int i = 1; i < lines.Length; i++) // 첫 번째 줄은 헤더이므로 건너뛰기
        {
            string[] data = Regex.Split(lines[i], csvRegexPattern);
            if (data.Length < 13) continue; // 최소 13개 열이 필요

            // 따옴표와 공백 제거
            for (int j = 0; j < data.Length; j++)
                data[j] = data[j].Trim(' ', '"');

            string idStr = data[0]; // ID
            if (string.IsNullOrEmpty(idStr)) continue;

            string prefabName = data[1]; // PrefabName
            string name = data[3]; // Name
            string assetPath = $"{WeaponSAVE_PATH}/{idStr}_{name}.asset";

            WeaponScriptableObject so = AssetDatabase.LoadAssetAtPath<WeaponScriptableObject>(assetPath);

            if (so == null)
            {
                so = ScriptableObject.CreateInstance<WeaponScriptableObject>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            // 데이터 매핑
            if (int.TryParse(idStr, out int weaponID))
                so.weaponID = weaponID;
                
            so.weaponName = name;
            so.weaponIcon = Resources.Load<Sprite>($"WeaponIcons/{prefabName}");
            so.description = data[6]; // Explanation
            
            // WeaponPrefab과 ProjectilePrefab은 Resources 폴더에서 로드
            so.weaponPrefab = Resources.Load<GameObject>($"WeaponPrefabs/{prefabName}Weapon");
            so.projectilePrefab = Resources.Load<GameObject>($"ProjectilePrefabs/{prefabName}Ammo");
            

            // Grade 매핑 (Common, Unique, Epic)
            switch (data[2].ToLower())
            {
                case "common": so.grade = Grade.Common; break;
                case "unique": so.grade = Grade.Unique; break;
                case "epic": so.grade = Grade.Epic; break;
                default: so.grade = Grade.Common; break;
            }

            // WeaponType 매핑 (Projectile, Laser, FloorBoard)
            switch (data[4].ToLower())
            {
                case "projectile": so.weaponType = WeaponType.Projectile; break;
                case "laser": so.weaponType = WeaponType.Laser; break;
                case "floorboard": so.weaponType = WeaponType.FloorBoard; break;
                default: so.weaponType = WeaponType.Projectile; break;
            }

            switch (data[5].ToLower())
            {
                case "fire" : so.targetAttribute = TargetAttribute.Fire; break;
                case "ice" : so.targetAttribute = TargetAttribute.Ice; break;
                case "electric" : so.targetAttribute = TargetAttribute.Electric; break;
                case "water" : so.targetAttribute = TargetAttribute.Water; break;
                case "normal" : so.targetAttribute = TargetAttribute.Normal; break;
            }

            // 수치 데이터 파싱
            if (float.TryParse(data[7], out float projectileSize))
                so.tileSize = projectileSize;
                
            if (float.TryParse(data[8], out float dmgRatio))
                so.damageRatio = dmgRatio;
                
            if (float.TryParse(data[9], out float baseWeaponDmg))
                so.weaponDamage = baseWeaponDmg;
                
            if (float.TryParse(data[10], out float attackSpeed))
                so.attackSpeed = attackSpeed;
                
            if (float.TryParse(data[11], out float playerSpeed))
                so.playerSpeed = playerSpeed;
                
            if (float.TryParse(data[12], out float projectileSpeed))
                so.projectileSpeed = projectileSpeed;
                
            if (data.Length > 13 && float.TryParse(data[13], out float projectileDis))
                so.projectileDis = projectileDis;

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#00FF00><b>[WeaponManager]</b></color> 무기 데이터 매핑 완료!");
    }

    private static void ProcessCSV(string csvContent)
    {
        string[] lines = csvContent.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (!Directory.Exists(ImprintSAVE_PATH)) Directory.CreateDirectory(ImprintSAVE_PATH);

        string csvRegexPattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";

        for (int i = 1; i < lines.Length; i++)
        {
            string[] data = Regex.Split(lines[i], csvRegexPattern);
            if (data.Length < 11) continue; 

            for (int j = 0; j < data.Length; j++)
                data[j] = data[j].Trim(' ', '"');

            string idStr = data[0];
            if (string.IsNullOrEmpty(idStr)) continue;

            string name = data[2];
            string assetPath = $"{ImprintSAVE_PATH}/{idStr}_{name}.asset";
            BuffScripableObject so = AssetDatabase.LoadAssetAtPath<BuffScripableObject>(assetPath);

            if (so == null)
            {
                so = ScriptableObject.CreateInstance<BuffScripableObject>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            int.TryParse(new string(idStr.Where(char.IsDigit).ToArray()), out so.buffID);
            so.buffName = name;
            so.buffIcon = Resources.Load<Sprite>($"Icons/{data[1]}");
            so.buffDescription = data[5];
            if (data[10] == "true")
            {
                so.isVotingCondition = true;
            }
            else
            {
                so.isVotingCondition = false;
            }
            ResetProperties(so.votingAbility);
            if (int.Parse(data[16]) <= 0)
            {
                ApplyStatMapping(so.votingAbility, data[11], data[16], true);
            }
            else
            {
                ApplyStatMapping(so.votingAbility, data[11], data[16], false);
            }
            switch (data[12])
            {
                case "Count": case "count": so.votingCondition = VotingCondition.Count; break;
                case "Percent": case "percent": so.votingCondition = VotingCondition.Percent; break;
                case "MAX": case "max": so.votingCondition = VotingCondition.MAX; break;
            }
            so.votingValue = int.Parse(data[13]);
            so.buffConditions = data[14];
            switch (data[15])
            {
                case "Count": case "count": so.VotingVType  = VotingCondition.Count; break;
                case "Percent": case "percent": so.VotingVType  = VotingCondition.Percent; break;
                case "MAX": case "max": so.VotingVType  = VotingCondition.MAX; break;
            }
            so.VotingRatio = int.Parse(data[16]);
            so.minPlayer = int.Parse(data[17]);
            if (so.buffProperties == null) so.buffProperties = new BuffProperties();
            ResetProperties(so.buffProperties);

            // --- 능력치 매핑 핵심 구간 ---
            
            // isDecrease 파라미터를 false로 전달
            ApplyStatMapping(so.buffProperties, data[3], data[8], false);

            // isDecrease 파라미터를 true로 전달하여 무조건 마이너스 처리
            ApplyStatMapping(so.buffProperties, data[4], data[9], true);

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#00FF00><b>[BuffManager]</b></color> 증감 수치(Ratio) 매핑 완료!");
    }

    private static void ProcessEnemyCSV(string csvContent)
    {
        string[] lines = csvContent.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (!Directory.Exists(EnemySAVE_PATH)) Directory.CreateDirectory(EnemySAVE_PATH);

        string csvRegexPattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";

        for (int i = 1; i < lines.Length; i++) // 첫 번째 줄은 헤더이므로 건너뛰기
        {
            string[] data = Regex.Split(lines[i], csvRegexPattern);
            if (data.Length < 14) continue; // 최소 14개 열이 필요

            // 따옴표와 공백 제거
            for (int j = 0; j < data.Length; j++)
                data[j] = data[j].Trim(' ', '"');

            string idStr = data[0]; // ID
            if (string.IsNullOrEmpty(idStr)) continue;

            string prefabName = data[2]; // PrefabName
            string name = data[3]; // Name
            string assetPath = $"{EnemySAVE_PATH}/{idStr}_{name}.asset";

            EnemyScriptableObject so = AssetDatabase.LoadAssetAtPath<EnemyScriptableObject>(assetPath);

            if (so == null)
            {
                so = ScriptableObject.CreateInstance<EnemyScriptableObject>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            // 데이터 매핑
            if (int.TryParse(idStr, out int enemyID))
                so.enemyID = enemyID;
                
            so.enemyName = name;
            so.enemyIcon = Resources.Load<Sprite>($"EnemyIcons/{prefabName}");
            so.enemyPrefab = Resources.Load<GameObject>($"EnemyPrefabs/{prefabName}");

            // Appeared 매핑 (true/false)
            if (bool.TryParse(data[1], out bool appeared))
                so.appeared = appeared;
            else
                so.appeared = data[1].ToLower() == "true" || data[1] == "1";

            // EnemyType 매핑 (Normal, Elite, Boss, Special)
            switch (data[4].ToLower())
            {
                case "melee": so.enemyType = EnemyType.Melee; break;
                case "ranged": so.enemyType = EnemyType.Ranged; break;
                case "destruct": so.enemyType = EnemyType.destruct; break;
            }

            // ProjectileType 매핑 (None, Bullet, Laser, Magic, Physical)
            switch (data[5].ToLower())
            {
                case "none": so.projectileType = ProjectileType.None; break;
                case "laser": so.projectileType = ProjectileType.Laser; break;
                case "area": so.projectileType = ProjectileType.Area; break;
                default: so.projectileType = ProjectileType.None; break;
            }

            // 수치 데이터 파싱
            if (float.TryParse(data[6], out float range))
                so.range = range;
                
            if (float.TryParse(data[7], out float size))
                so.size = size;
                
            if (float.TryParse(data[8], out float hp))
                so.hp = hp;
                
            if (float.TryParse(data[9], out float damage))
                so.damage = damage;
                
            if (float.TryParse(data[10], out float attackSpeed))
                so.attackSpeed = attackSpeed;
                
            if (float.TryParse(data[11], out float speed))
                so.speed = speed;
                
            if (float.TryParse(data[12], out float allDMG))
                so.allDMG = allDMG;
                
            if (float.TryParse(data[13], out float dmgReceived))
                so.dmgReceived = dmgReceived;

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#00FF00><b>[EnemyManager]</b></color> 적 데이터 매핑 완료!");
    }

    private static void ProcessSkillCSV(string csvContent)
    {
        string[] lines = csvContent.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (!Directory.Exists(SkillSAVE_PATH)) Directory.CreateDirectory(SkillSAVE_PATH);

        string csvRegexPattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";

        for (int i = 1; i < lines.Length; i++) // 첫 번째 줄은 헤더이므로 건너뛰기
        {
            string[] data = Regex.Split(lines[i], csvRegexPattern);
            if (data.Length < 8) continue; // 최소 8개 열이 필요

            // 따옴표와 공백 제거
            for (int j = 0; j < data.Length; j++)
                data[j] = data[j].Trim(' ', '"');

            string idStr = data[0]; // ID
            if (string.IsNullOrEmpty(idStr)) continue;

            string iconName = data[1]; // IconName
            string name = data[2]; // Name
            string assetPath = $"{SkillSAVE_PATH}/{idStr}_{name}.asset";

            SkillScriptableObject so = AssetDatabase.LoadAssetAtPath<SkillScriptableObject>(assetPath);

            if (so == null)
            {
                so = ScriptableObject.CreateInstance<SkillScriptableObject>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            // 데이터 매핑
            if (int.TryParse(idStr, out int skillID))
                so.skillID = skillID;
                
            so.skillName = name;
            so.skillIcon = Resources.Load<Sprite>($"SkillIcons/{iconName}");
            so.description = data[3]; // Explanation

            // 수치 데이터 파싱
            if (float.TryParse(data[5], out float skillDuration))
                so.skillDuration = skillDuration;

            // ValueType 매핑 (Percent, Fixed, Count)
            switch (data[6].ToLower())
            {
                case "percent": so.valueType = ValueType.Percent; break;
                case "fixed": so.valueType = ValueType.Fixed; break;
                case "count": so.valueType = ValueType.Count; break;
                default: so.valueType = ValueType.Percent; break;
            }

            // 여러 버프와 비율 처리
            string buffString = data[4]; // Buff
            string ratioString = data[7]; // Buff_Ratio

            string[] buffTypes = buffString.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            string[] buffRatios = ratioString.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            // 버프 개수만큼 배열 생성
            int buffCount = Mathf.Max(buffTypes.Length, buffRatios.Length);
            so.skillBuffs = new SkillBuffData[buffCount];

            for (int k = 0; k < buffCount; k++)
            {
                so.skillBuffs[k] = new SkillBuffData();
                so.skillBuffs[k].buffType = new BuffProperties();
                
                // 각 버프에 해당하는 비율 값 파싱
                float ratio = 0f;
                if (k < buffRatios.Length && float.TryParse(buffRatios[k].Trim(), out ratio))
                    so.skillBuffs[k].buffRatio = ratio;

                // 버프 타입에 따라 BuffProperties의 적절한 속성에 해당 비율 값 설정
                if (k < buffTypes.Length)
                {
                    string buffType = buffTypes[k].Trim();
                    // 각 버프의 고유한 ratio 값을 사용
                    ApplySkillBuffMapping(so.skillBuffs[k].buffType, buffType, so.skillBuffs[k].buffRatio);
                }
            }

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#00FF00><b>[SkillManager]</b></color> 스킬 데이터 매핑 완료!");
    }

    private static void ProcessContractCSV(string csvContent)
    {
        string[] lines = csvContent.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (!Directory.Exists(ContractSAVE_PATH)) Directory.CreateDirectory(ContractSAVE_PATH);

        string csvRegexPattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";

        for (int i = 1; i < lines.Length; i++) // 첫 번째 줄은 헤더이므로 건너뛰기
        {
            string[] data = Regex.Split(lines[i], csvRegexPattern);
            if (data.Length < 9) continue; // 최소 9개 열이 필요

            // 따옴표와 공백 제거
            for (int j = 0; j < data.Length; j++)
                data[j] = data[j].Trim(' ', '"');

            string idStr = data[0]; // ID
            if (string.IsNullOrEmpty(idStr)) continue;

            string imageName = data[1]; // ImageName  
            string name = data[2]; // Name
            string assetPath = $"{ContractSAVE_PATH}/{idStr}_{name}.asset";

            ContractScriptableObject so = AssetDatabase.LoadAssetAtPath<ContractScriptableObject>(assetPath);

            if (so == null)
            {
                so = ScriptableObject.CreateInstance<ContractScriptableObject>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            // 데이터 매핑
            if (int.TryParse(idStr, out int contractID))
                so.contractID = contractID;
                
            so.contractName = name;
            so.contractIcon = Resources.Load<Sprite>($"ContractIcons/{imageName}");
            so.description = data[7]; // Explanation

            // TargetType 매핑 (None, Player, Enemy, Both)
            switch (data[3].ToLower())
            {
                case "none": so.targetType = TargetType.None; break;
                case "projectile": so.targetType = TargetType.Projectile; break;
                case "laser": so.targetType = TargetType.Laser; break;
                case "area": so.targetType = TargetType.Area; break;
                default: so.targetType = TargetType.None; break;
            }

            // TargetAttribute 매핑 (None, HP, MP, Damage, Speed, All)
            switch (data[4].ToLower())
            {
                case "none": so.targetAttribute = TargetAttribute.None; break;
                case "fire": so.targetAttribute = TargetAttribute.Fire; break;
                case "ice": so.targetAttribute = TargetAttribute.Ice; break;
                case "electric": so.targetAttribute = TargetAttribute.Electric; break;
                case "water": so.targetAttribute = TargetAttribute.Water; break;
                case "normal": so.targetAttribute = TargetAttribute.Normal; break;
                default: so.targetAttribute = TargetAttribute.None; break;
            }

            // ValueType 매핑 (Percent, Fixed, Count)
            switch (data[6].ToLower())
            {
                case "percent": so.valueType = ValueType.Percent; break;
                case "fixed": so.valueType = ValueType.Fixed; break;
                case "count": so.valueType = ValueType.Count; break;
                default: so.valueType = ValueType.Percent; break;
            }

            // 여러 버프와 비율 처리
            string abilitiesString = data[5]; // TargetAbilities
            string ratioString = data[8]; // Ratio

            string[] abilityTypes = abilitiesString.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            string[] ratios = ratioString.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            // 버프 개수만큼 배열 생성
            int buffCount = Mathf.Max(abilityTypes.Length, ratios.Length);
            so.contractBuffs = new ContractBuffData[buffCount];

            for (int k = 0; k < buffCount; k++)
            {
                so.contractBuffs[k] = new ContractBuffData();
                so.contractBuffs[k].targetAbilities = new BuffProperties();
                
                // 각 버프에 해당하는 비율 값 파싱
                float ratio = 0f;
                if (k < ratios.Length && float.TryParse(ratios[k].Trim(), out ratio))
                    so.contractBuffs[k].ratio = ratio;

                // 버프 타입에 따라 BuffProperties의 적절한 속성에 해당 비율 값 설정
                if (k < abilityTypes.Length)
                {
                    string abilityType = abilityTypes[k].Trim();
                    ApplyContractBuffMapping(so.contractBuffs[k].targetAbilities, abilityType, so.contractBuffs[k].ratio);
                }
            }

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#00FF00><b>[ContractManager]</b></color> 계약 데이터 매핑 완료!");
    }

    private static void ResetProperties(BuffProperties props)
    {
        props.maxHp = 0; props.maxMp = 0; props.damage = 0;
        props.attackSpeed = 0; props.moveSpeed = 0; props.allDamage = 0;
        props.damageReceived = 0; props.criticalChance = 0; props.criticalDamage = 0;
        props.enemiesDamage = 0; props.enemiesReceived = 0; props.enemiesHp = 0;
        props.boosDamage = 0; props.boosReceived = 0; props.boosHp = 0;
    }

    private static void ApplyStatMapping(BuffProperties props, string type, string valueStr, bool isDecrease)
    {
        if (string.IsNullOrEmpty(type) || !float.TryParse(valueStr, out float value)) return;

        // [핵심] 감소 능력치라면 무조건 음수로 변환
        if (isDecrease) 
        {
            value = -Mathf.Abs(value); // 양수가 들어와도 음수로 강제 변환
        }

        string cleanType = type.Replace("_", "").ToLower();

        switch (cleanType)
        {
            case "hp": case "maxhp": props.maxHp += value; break;
            case "mp": case "maxmp": props.maxMp += value; break;
            case "damage": case "dmg": props.damage += value; break;
            case "attackspeed": case "atkspeed": props.attackSpeed += value; break;
            case "movespeed": case "speed": props.moveSpeed += value; break;
            case "alldamage": case "alldmg": props.allDamage += value; break;
            case "damagereceived": case "dmgreceived": props.damageReceived += value; break;
            case "criticalchance": case "critchance": props.criticalChance += value; break;
            case "criticaldamage": case "critdmg": props.criticalDamage += value; break;
            case "enemiesdamage": case "enemiesdmg": props.enemiesDamage += value; break;
            case "enemiesreceived": props.enemiesReceived += value; break;
            case "enemieshp": props.enemiesHp += value; break;
            case "bossdamage": case "bossdmg": case "boosdamage": case "boosdmg": props.boosDamage += value; break;
            case "bossreceived": case "boosreceived": props.boosReceived += value; break;
            case "bosshp": case "booshp": props.boosHp += value; break;
        }
    }

    private static void ApplySkillBuffMapping(BuffProperties props, string buffType, float value)
    {
        if (string.IsNullOrEmpty(buffType)) return;

        string cleanType = buffType.Replace("_", "").ToLower();

        switch (cleanType)
        {
            case "hp": case "maxhp": props.maxHp = value; break;
            case "mp": case "maxmp": props.maxMp = value; break;
            case "damage": case "dmg": props.damage = value; break;
            case "attackspeed": case "atkspeed": props.attackSpeed = value; break;
            case "movespeed": case "speed": props.moveSpeed = value; break;
            case "alldamage": case "alldmg": props.allDamage = value; break;
            case "damagereceived": case "dmgreceived": props.damageReceived = value; break;
            case "criticalchance": case "critchance": props.criticalChance = value; break;
            case "criticaldamage": case "critdmg": props.criticalDamage = value; break;
            case "enemiesdamage": case "enemiesdmg": props.enemiesDamage = value; break;
            case "enemiesreceived": props.enemiesReceived = value; break;
            case "enemieshp": props.enemiesHp = value; break;
            case "bossdamage": case "bossdmg": case "boosdamage": case "boosdmg": props.boosDamage = value; break;
            case "bossreceived": case "boosreceived": props.boosReceived = value; break;
            case "bosshp": case "booshp": props.boosHp = value; break;
        }
    }

    private static void ApplyContractBuffMapping(BuffProperties props, string abilityType, float value)
    {
        if (string.IsNullOrEmpty(abilityType)) return;

        string cleanType = abilityType.Replace("_", "").ToLower();

        switch (cleanType)
        {
            case "hp": case "maxhp": props.maxHp = value; break;
            case "mp": case "maxmp": props.maxMp = value; break;
            case "damage": case "dmg": props.damage = value; break;
            case "attackspeed": case "atkspeed": props.attackSpeed = value; break;
            case "movespeed": case "speed": props.moveSpeed = value; break;
            case "alldamage": case "alldmg": props.allDamage = value; break;
            case "damagereceived": case "dmgreceived": props.damageReceived = value; break;
            case "criticalchance": case "critchance": props.criticalChance = value; break;
            case "criticaldamage": case "critdmg": props.criticalDamage = value; break;
            case "enemiesdamage": case "enemiesdmg": props.enemiesDamage = value; break;
            case "enemiesreceived": props.enemiesReceived = value; break;
            case "enemieshp": props.enemiesHp = value; break;
            case "bossdamage": case "bossdmg": case "boosdamage": case "boosdmg": props.boosDamage = value; break;
            case "bossreceived": case "boosreceived": props.boosReceived = value; break;
            case "bosshp": case "booshp": props.boosHp = value; break;
        }
    }
}