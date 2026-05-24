using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ステータスフラグ
/// </summary>
public enum StatusFlag
{
    None = 0,
    Move,
    Select,
    Attack,
    Heal,
    Buff,
    Revive,
    End
}

/// <summary>
/// キャラクターデータ
/// </summary>
[CreateAssetMenu(menuName = "CharacterData")]
public class CharacterData : ScriptableObject
{
    public string charactername;
    public Sprite characterIcon;
    public Sprite standingImage; // 立ち絵
    public bool enemyCheckFlag;
    public int hp, mp, atk,Int, def, spd;
    public int maxHp, maxMp;
    public int exp, level;
    public GameObject CharacterObj;
    public SkillData[] skills;
    public Vector3 CharacterTransfrom;
    public StatusFlag StatusFlag;
    
    [Header("レベルアップテーブル")]
    [Tooltip("キャラクター専用のレベルアップテーブル")]
    public LevelUpTable levelUpTable;
}
