using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// バフの適用範囲
/// </summary>
public enum BuffRange
{
    Self,       // 自分自身
    Ally,       // 味方
    Enemy,      // 敵
    AllAllies,  // 全味方
    AllEnemies  // 全敵
}

public enum StatusEffect
{
    Poison,
    Stun,
    Burn,
    Freeze,
    Sleep,
    Silent,
    DamageUp,
    TurnChange,
    DefenceUp,
    SpdDown,
    SpdUp,
    MagicDamageDown,
    MagicCounter,
    Makituki,
    Zouen,
    MPRecovery,
    LockIn,
    None
} //毒、スタン、やけど、凍結、眠り、魔封,ダメ増,ターンチェンジ,防御UP,スピードUP,スピードDN,マジックダメDN,反射,巻きつき,増援

/// <summary>
/// バフの基底クラス
/// </summary>
public abstract class BuffBase : ScriptableObject
{ 
    [Header("基本情報")]
    [Tooltip("バフの一意の識別子（オプション）")]
    public string buffId;
    
    [Header("バフ名")]
    public string buffName;
    
    [Header("バフ説明")]
    [TextArea(3, 5)]
    public string description;
    
    [Header("バフアイコン")]
    [Tooltip("UI表示用のアイコン")]
    public Sprite icon;
    
    [Header("バフタイプ")]
    [Tooltip("カテゴリ分類（攻撃系/防御系/特殊系など）")]
    public BuffType buffType = BuffType.StatusEnhancement;
    
    [Header("持続時間")]
    [Tooltip("バフの持続ターン数")]
    public int duration;
    
    [Header("バフ範囲")]
    [Tooltip("バフが適用される対象範囲")]
    public BuffRange buffRange;
    
    [Header("バフを付与したキャラクター")]
    [Tooltip("このバフを付与したキャラクター（自動設定）")]
    public Character sourceCharacter;

    [Header("自分自身が対象")]
    [Tooltip("選択フェイズをなくして自分自身のみ対象にする")]
    public bool isSelfTarget = false;

    /// <summary>
    /// 現在処理中のBuffInstance（Apply/Remove時にBuffInstance側で設定。ScriptableObject共有のためNonSerialized）
    /// </summary>
    [System.NonSerialized]
    public BuffInstance currentInstance;

    /// <summary>
    /// CharacterBuffManager.RemoveBuff経由でRemove中か（二重RemoveBuff呼び出し防止用）
    /// </summary>
    [System.NonSerialized]
    public bool isBeingRemoved;

    /// <summary>
    /// バフを適用する
    /// </summary>
    /// <param name="target">適用対象のキャラクター</param>
    public abstract void Apply(Character target);

    /// <summary>
    /// 状態異常
    /// </summary>
    public StatusEffect statusEffect = StatusEffect.None;

    /// <summary>
    /// バフを解除する
    /// </summary>
    public abstract void Remove();
}
