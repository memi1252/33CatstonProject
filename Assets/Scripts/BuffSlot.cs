using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class BuffSlot : MonoBehaviour
{
    public TextMeshProUGUI buffNameText;
    public Image buffIconImage;
    public TextMeshProUGUI BuffDescriptionText;
    public TextMeshProUGUI BuffConditionsText;
    public TextMeshProUGUI votingPlayerText;

    private Buff buff;

    public void Set(BuffScripableObject buffScripableObject)
    {
        buffNameText.text = buffScripableObject.buffName;
        buffIconImage.sprite = buffScripableObject.buffIcon;
        BuffDescriptionText.text = buffScripableObject.buffDescription;
        BuffConditionsText.text = buffScripableObject.buffConditions;
        buff = buffScripableObject.buff;
    }
   
}
