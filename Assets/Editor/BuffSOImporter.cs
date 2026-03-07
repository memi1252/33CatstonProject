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

    private const string ImprintSAVE_PATH = "Assets/Resources/ImprintBuffs";

    [MenuItem("Tools/Sync Buff Data (Regex CSV)")]
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
            so.buffConditions = data[14];
            // Condition_EX는 19번째 열 (인덱스 18)
            if (data.Length > 18) so.buffConditions = data[18];

            if (so.buffProperties == null) so.buffProperties = new BuffProperties();
            ResetProperties(so.buffProperties);

            // --- 능력치 매핑 핵심 구간 ---
            
            // 1. 증가 처리 (Increase_Abilities: data[4], Increase_Ratio: data[9])
            // isDecrease 파라미터를 false로 전달
            ApplyStatMapping(so.buffProperties, data[3], data[8], false);

            // 2. 감소 처리 (Decrease_Abilities: data[5], Decrease_Ratio: data[10])
            // isDecrease 파라미터를 true로 전달하여 무조건 마이너스 처리
            ApplyStatMapping(so.buffProperties, data[4], data[9], true);

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#00FF00><b>[BuffManager]</b></color> 증감 수치(Ratio) 매핑 완료!");
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
}