using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;

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


    void Awake()
    {
        voteButton.onClick.AddListener(() =>
        {
            BuffManager.Instance.OnVoteButtonClicked(Order);
        }
        );
    }


    public void Set(BuffScripableObject buffScripableObject)
    {
        buffNameText.text = buffScripableObject.buffName;
        buffIconImage.sprite = buffScripableObject.buffIcon;
        BuffDescriptionText.text = $"{buffScripableObject.buffDescription}";
        BuffConditionsText.text = buffScripableObject.buffConditions;
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
