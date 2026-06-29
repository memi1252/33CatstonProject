using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System.Text.RegularExpressions;
using Febucci.TextAnimatorForUnity;

public class BuffSlot : MonoBehaviour
{
    public TextMeshProUGUI buffNameText;
    public Image buffIconImage;
    public TextMeshProUGUI BuffDescriptionText;
    public TextMeshProUGUI BuffConditionsText;
    public TextMeshProUGUI votingPlayerText;
    public Button voteButton;

    public int Order { get; set; } // 버프 슬롯의 순서를 나타내는 속성
    
    public BuffScripableObject buffScripableObject;
    public ContractScriptableObject contractScriptableObject;


    // {Ratio:N} / {Voted_Raito:N} 태그 개수가 실제 ratio 배열 길이보다 많으면(데이터 작성 누락)
    // 안 치환된 raw 태그가 그대로 보이는 버그가 있었다. 남은 태그를 안전하게 제거한다.
    private static readonly Regex UnresolvedTagPattern = new Regex(@"\{?(Ratio|Voted_Raito):\d+\}");

    private static string CleanupUnresolvedTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (UnresolvedTagPattern.IsMatch(text))
        {
            Debug.LogWarning($"[BuffSlot] 치환 안 된 태그가 남아있습니다 (ratio 배열 길이 부족 또는 '{{' 오타): \"{text}\"");
            text = UnresolvedTagPattern.Replace(text, "0%");
        }
        return text;
    }

    // 다른 곳(예: ActiveBuffDisplayUI 툴팁)에서도 같은 {Ratio:N} 치환이 필요해서 재사용 가능하게 뺐다.
    public static string FormatContractDescription(ContractScriptableObject buff)
    {
        if (buff == null) return "";
        string resultText = buff.description ?? "";
        if (buff.contractBuffs == null) return resultText;

        bool isCount = buff.valueType == global::ValueType.Count;
        for (int i = 0; i < buff.contractBuffs.Length; i++)
        {
            string targetTag = "{Ratio:" + i + "}";
            float raw = buff.contractBuffs[i].ratio;
            string value = isCount ? raw.ToString() : (raw * 100).ToString();
            resultText = resultText.Replace(targetTag, value + (isCount ? "" : "%"));
        }
        return CleanupUnresolvedTags(resultText);
    }

    public static string FormatBuffDescription(BuffScripableObject buff)
    {
        if (buff == null) return "";
        string resultText = buff.buffDescription ?? "";
        if (buff.buffProperties == null) return resultText;

        for (int i = 0; i < buff.buffProperties.Length; i++)
        {
            string targetTag = "{Ratio:" + i + "}";
            string value = (buff.buffProperties[i].ratio * 100).ToString();
            resultText = resultText.Replace(targetTag, value + "%");
        }
        return CleanupUnresolvedTags(resultText);
    }

    void Awake()
    {
        voteButton.onClick.AddListener(() =>
        {
            BuffManager.Instance.OnVoteButtonClicked(Order);
        }
        );
    }

    public void Set(ContractScriptableObject buffScripableObject)
    {
         buffNameText.GetComponent<TypewriterComponent>().ShowText(buffScripableObject.contractName);
        buffIconImage.sprite = buffScripableObject.contractIcon;
        string description = buffScripableObject.description;
        if(buffScripableObject.description.Length > 0)
        {
            if (buffScripableObject == null) return;

            string originalText = buffScripableObject.description;
    
            // 1. 결과물을 담을 변수 생성
            string resultText = originalText;

            // 2. 반복문을 돌며 모든 Ratio 태그 치환
            // valueType이 Count면 원래 숫자 그대로(예: 25), Percent면 ×100 + "%"(예: 0.5 -> 50%)로 표시한다.
            // (이전엔 항상 ×100 + "%"로 처리해서 Count 타입 계약이 2000% 같은 식으로 잘못 표시됐었음)
            bool isCount = buffScripableObject.valueType == ValueType.Count;
            for (int i = 0; i < buffScripableObject.contractBuffs.Length; i++)
            {
                string targetTag = "{Ratio:" + i + "}";
                float raw = buffScripableObject.contractBuffs[i].ratio;
                string value = isCount ? raw.ToString() : (raw * 100).ToString();

                resultText = resultText.Replace(targetTag, value + (isCount ? "" : "%"));
            }

            // 3. 로그로 치환 결과 최종 확인 (이게 콘솔에 어떻게 찍히는지 보세요!)
            Debug.Log($"[치환 완료]: {resultText}");

            description = CleanupUnresolvedTags(resultText);
        }

        if (BuffDescriptionText != null)
        {
            BuffDescriptionText.GetComponent<TypewriterComponent>().ShowText(description);
            //BuffDescriptionText.text = description;
        }

        BuffConditionsText.text = "";
        this.contractScriptableObject = buffScripableObject;
    }

    public void Set(BuffScripableObject buffScripableObject)
    {
        buffNameText.GetComponent<TypewriterComponent>().ShowText(buffScripableObject.buffName);
        buffIconImage.sprite = buffScripableObject.buffIcon;
        string description = buffScripableObject.buffDescription;
        if(buffScripableObject.buffProperties.Length > 0)
        {
            if (buffScripableObject == null) return;

            string originalText = buffScripableObject.buffDescription;
    
            // 1. 결과물을 담을 변수 생성
            string resultText = originalText;

            // 2. 반복문을 돌며 모든 Ratio 태그 치환
            for (int i = 0; i < buffScripableObject.buffProperties.Length; i++)
            {
                string targetTag = "{Ratio:" + i + "}";
                string value = (buffScripableObject.buffProperties[i].ratio * 100).ToString(); // 0.5일 경우 50으로 변환
        
                resultText = resultText.Replace(targetTag, value + "%");
            }

            // 3. 로그로 치환 결과 최종 확인 (이게 콘솔에 어떻게 찍히는지 보세요!)
            Debug.Log($"[치환 완료]: {resultText}");

            description = CleanupUnresolvedTags(resultText);
        }

        if (BuffDescriptionText != null)
        {
            BuffDescriptionText.GetComponent<TypewriterComponent>().ShowText(description);
        }

        string conditionDescription = buffScripableObject.voteDesc;
        // isVotingCondition은 BuffManager의 득표 조건부 적용 로직에 쓰이는 플래그일 뿐,
        // voteDesc에 {Voted_Raito:N} 태그가 있는지와는 무관하다. 이걸로 치환 여부를 가르면
        // isVotingCondition=false인데 태그가 있는 경우(I_003, I_004, I_005, I_009 등) 치환이 안 돼서
        // 화면에 "{Voted_Raito:0}" 같은 raw 태그가 그대로 보이는 버그가 있었다.
        // votingAbility 배열이 있으면 항상 치환을 시도한다.
        if (buffScripableObject.votingAbility != null && buffScripableObject.votingAbility.Length > 0)
        {
            string originalText = buffScripableObject.voteDesc;
            string resultText = originalText;

            for (int i = 0; i < buffScripableObject.votingAbility.Length; i++)
            {
                string targetTag = "{Voted_Raito:" + i + "}";
                string value = (buffScripableObject.votingAbility[i].ratio * 100).ToString(); // 0.5일 경우 50으로 변환

                resultText = resultText.Replace(targetTag, value + "%");
            }

            Debug.Log($"[치환 완료]: {resultText}");

            conditionDescription = CleanupUnresolvedTags(resultText);
        }

        if (buffScripableObject.isVotingCondition)
        {
            BuffConditionsText.text = conditionDescription;
        }
        else
        {
            BuffConditionsText.GetComponent<TypewriterComponent>().ShowText(conditionDescription);
        }
        
        this.buffScripableObject = buffScripableObject;
    }

    public void UpdateVotePlayer(string playerName)
    {
        if (votingPlayerText != null)
        {
            votingPlayerText.text = $"{playerName}";
            if (votingPlayerText.fontSize > 16f) votingPlayerText.fontSize = 16f;
        }
    }
}
