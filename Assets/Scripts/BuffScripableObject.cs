using UnityEngine;

[CreateAssetMenu(fileName = "New Buff", menuName = "Buff")]
public class BuffScripableObject : ScriptableObject
{
    public string buffName;
    public Sprite buffIcon;
    public string buffDescription;
    public string buffConditions;
    public Buff buff;
}
