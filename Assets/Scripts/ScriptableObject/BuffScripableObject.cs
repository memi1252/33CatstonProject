using System;using UnityEngine;

[CreateAssetMenu(fileName = "New Buff", menuName = "Buff")]
public class BuffScripableObject : ScriptableObject
{
    public int buffID;
    public string buffName;
    public Sprite buffIcon;
    public string buffDescription;
    public bool isSpecial;
    public ContractBuffData[] buffProperties;
    public bool isVotingCondition;
    public VotingCondition Condition = VotingCondition.Count;
    public VotingCondition VotingCondition = VotingCondition.Count;
    public int votingValue;
    public ApplyType applyType;
    public ContractBuffData[] votingAbility;
    public VotingCondition VotingVType = VotingCondition.Count;
    public string voteDesc;
    public int minPlayer;


}

[System.Serializable]
public enum ApplyType
{
    Self,
    VotedPlayer,
    RandomOne,
    ALLTeam,
    None
}


[System.Serializable]
public enum VotingCondition
{
    Min,
    Fixed,
    Count,
    Percent,
    MAX
}

[System.Serializable]
public class BuffProperties{
    [Header("Buff Properties")]
    public float maxHp;
    public float maxMp;
    public float damage;
    public float attackSpeed;
    public float moveSpeed;
    public float allDamage;
    public float damageReceived;
    public float criticalChance;
    public float criticalDamage;
    public float enemiesDamage;
    public float enemiesSpeed;
    public float enemiesReceived;
    public float enemiesHp;
    public float boosDamage;
    public float boosReceived;
    public float boosHp;
}
