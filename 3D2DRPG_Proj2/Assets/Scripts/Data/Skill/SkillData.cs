using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//public enum DamageType { Physical, Magical, True }
//public enum ElementType { None, Fire, Ice, Thunder, Wind, Light, Dark }
//public enum TargetType { SingleEnemy, AllEnemies, Self, Ally, AllAllies }
//public enum BuffType { None, AttackUp, DefenseUp, SpeedUp, AttackDown, DefenseDown }
public enum TargetScope { Single, All, Other }
//public enum StatusEffect { Poison, Stun, Burn, Freeze, Sleep }
public enum ZokuseiType
{
    Buturi,
    Mahou
}
public enum SkillEffectType { Attack, Heal, Buff, ExtraAction, Revive }
[CreateAssetMenu(menuName = "SkillData")]
public class SkillData : ScriptableObject
{
    [Header("��{���")]
    public string skillName;
    [TextArea] public string description;
    public Sprite icon;
    public AudioClip soundEffect;
    public GameObject vfxPrefab;
    public string animationName;
    public SkillEffectType effectType;
    //public string targetType;

    [Header("�퓬�p�����[�^")]
    public float power = 10f;
    public int mpCost = 0;
    public float cooldown = 0f;
    public int hitCount = 1;
    [Range(0, 1)] public float criticalRate = 0.1f;
    [Range(0, 1)] public float accuracy = 1f;
    public bool isIntSansyou = false;

    public TargetScope targetScope = TargetScope.Single;
    public StatusEffect statusEffect;
    //public DamageType damageType;
    //public ElementType elementType;
    //public TargetType targetType;

    [Header("�R���{�ݒ�")]
    public bool canCombo = false;
    public bool DamageUp = false;
    public int ComboDamage = 0;
    public SkillData comboNextSkill;
    public float timingWindowStart = 0.3f;
    public float timingWindowEnd = 0.6f;
    public float comboDamageMultiplier = 1.2f;
    public int maxcombo= 3;
    public bool missCancel = true;

    [Header("�A�����ʂ����H")]
    public bool rengeki = false;
    [Header("�A�����ʂ����H")]
    public int rengekiCount = 0;

    [Header("�U����A�����ŉ񕜂��邩�H")]
    public bool atkAftHeal = false;
    public float wariaiHeal = 0f;
    public float wariai = 0f;

    [Header("��Ԉُ�E����")]
    //public StatusEffect inflictStatus;
    public float statusChance = 0f;
    [Header("�o�t�̊Ǘ��X�N���v�g")]
    public List<BuffBase> buffEffect;
    [Header("�o�t�l")]
    public float buffValue = 0f;
    [Header("�o�t�̌p������")]
    public int buffDuration = 0;

    [Header("�_���[�W�{�[�i�X���g�p����")]
    public bool DamageBonusFlg = false;

    [Header("����")]
    public ZokuseiType ZokuseiType = ZokuseiType.Buturi; 

    [Header("�����_�����ʃX�L��")]
    [Tooltip("true�̏ꍇ�͉񕜂��_���[�W��")]
    public bool isRandomEffect = false;

    [Header("��x����H")]
    public bool isOnlyOnece = false;

    [Header("�U����")]
    public int attackCount = 1;

    [Header("�K�E�Z���H")]
    public bool isUltimateSkill = false;

    [Header("���ʂȍs���񐔂������H")]
    public bool hasExtraActions = false;

    [Tooltip("���ʂȍs���񐔂łǂꂭ�炢�s�����邩")]
    public int extraActionCount = 2;
}

