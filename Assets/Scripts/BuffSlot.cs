using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System.Text.RegularExpressions;

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
        buffNameText.text = buffScripableObject.contractName;
        buffIconImage.sprite = buffScripableObject.contractIcon;
        string description = buffScripableObject.description;
        if(buffScripableObject.description.Length > 0)
        {
            if (buffScripableObject == null) return;

            string originalText = buffScripableObject.description;
    
            // 1. 결과물을 담을 변수 생성
            string resultText = originalText;

            // 2. 반복문을 돌며 모든 Ratio 태그 치환
            for (int i = 0; i < buffScripableObject.contractBuffs.Length; i++)
            {
                string targetTag = "{Ratio:" + i + "}";
                string value = (buffScripableObject.contractBuffs[i].ratio * 100).ToString(); // 0.5일 경우 50으로 변환
        
                resultText = resultText.Replace(targetTag, value + "%");
            }

            // 3. 로그로 치환 결과 최종 확인 (이게 콘솔에 어떻게 찍히는지 보세요!)
            Debug.Log($"[치환 완료]: {resultText}");

            description = resultText;
        }
        
        if (BuffDescriptionText != null)
        {
            BuffDescriptionText.text = description;
        }

        BuffConditionsText.text = "";
        this.contractScriptableObject = buffScripableObject;
    }

    public void Set(BuffScripableObject buffScripableObject)
    {
        buffNameText.text = buffScripableObject.buffName;
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

            description = resultText;
        }
        
        if (BuffDescriptionText != null)
        {
            BuffDescriptionText.text = description;
        }

        string conditionDescription = buffScripableObject.voteDesc;
        if (buffScripableObject.isVotingCondition)
        {
            if (buffScripableObject == null) return;

            string originalText = buffScripableObject.voteDesc;
    
            // 1. 결과물을 담을 변수 생성
            string resultText = originalText;

            // 2. 반복문을 돌며 모든 Ratio 태그 치환
            for (int i = 0; i < buffScripableObject.votingAbility.Length; i++)
            {
                string targetTag = "{Ratio:" + i + "}";
                string value = (buffScripableObject.votingAbility[i].ratio * 100).ToString(); // 0.5일 경우 50으로 변환
        
                resultText = resultText.Replace(targetTag, value + "%");
            }

            // 3. 로그로 치환 결과 최종 확인 (이게 콘솔에 어떻게 찍히는지 보세요!)
            Debug.Log($"[치환 완료]: {resultText}");

            conditionDescription = resultText;
            BuffConditionsText.text = conditionDescription;
        }
        else
        {
            BuffConditionsText.text = "";
        }
        
        this.buffScripableObject = buffScripableObject;
    }

    public void UpdateVotePlayer(string playerName)
    {
        if (votingPlayerText != null)
        {
            votingPlayerText.text = $"{playerName}";
        }
    }
}
