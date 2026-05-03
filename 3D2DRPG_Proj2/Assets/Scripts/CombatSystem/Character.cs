using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Character: MonoBehaviour
{
    private CharacterData characterData;
    public string charactername;
    public Sprite characterIcon;
    public bool enemyCheckFlag;
    public int hp, mp, atk, def, spd, Int;
    public int maxHp, maxMp;
    public int exp, level;
    public GameObject CharacterObj;
    public SkillData[] skills;
    public Vector3 CharacterTransfrom;
    public StatusFlag StatusFlag;
    private GameObject characterDataObj;
    [SerializeField]
    private Animator enemyAnimator;
    private Animator playerAnimator;
    
    // バフ管理
    private CharacterBuffManager buffManager;
    
    // ベースステータス（バフ適用前の元の値）
    private int baseAtk;
    private int baseDef;
    private int baseSpd;
    private int baseMaxHp;
    private int baseMaxMp;

    //バフ関連
    //すべての攻撃を全体攻撃にするフラグ
    public bool AllAttack;
    
    public void init(CharacterData _characterData)
    {
        characterData = _characterData;
        charactername = characterData.charactername;
        characterIcon = characterData.characterIcon;
        enemyCheckFlag = characterData.enemyCheckFlag;
        hp = characterData.hp;
        mp = characterData.mp;
        atk = characterData.atk;
        def = characterData.def;
        spd = characterData.spd;
        Int = characterData.Int;
        maxHp = characterData.maxHp;
        maxMp = characterData.maxMp;
        exp = characterData.exp;
        level = characterData.level;
        CharacterObj = this.gameObject;
        characterDataObj= characterData.CharacterObj;
        skills = characterData.skills;
        CharacterTransfrom= characterData.CharacterTransfrom;
        StatusFlag= characterData.StatusFlag;
        
        //敵のアニメーションを探す
        if (this.transform.Find("BlockWolfAnimation"))
        {
            enemyAnimator = this.transform.Find("BlockWolfAnimation").GetComponent<Animator>();

        }
        else
        {
            //MaterialFileにある味方サイドのキャラ達の配下にあるGameObjectの中のAnimatorコンポネントを探して取ってくる
            playerAnimator = this.transform.Find("GameObject").GetComponent<Animator>();
        }

            // ベースステータスを保存
            baseAtk = atk;
        baseDef = def;
        baseSpd = spd;
        baseMaxHp = maxHp;
        baseMaxMp = maxMp;
        
        // バフマネージャーの初期化
        InitializeBuffManager();
    }
    
    /// <summary>
    /// バフマネージャーの初期化
    /// </summary>
    private void InitializeBuffManager()
    {
        buffManager = GetComponent<CharacterBuffManager>();
        if (buffManager == null)
        {
            buffManager = gameObject.AddComponent<CharacterBuffManager>();
        }
        buffManager.Initialize(this);
    }
    // ダメージ計算
    public int Attack(Character enemy,SkillData skillData)
    {
        // バフ適用後の防御力を使用
        int effectiveDef = enemy.GetEffectiveDefense();
        int damage = Mathf.Max(0, (int)skillData.power - effectiveDef);
        enemy.TakeDamage(damage);
        return damage;
    }

    // ダメージを受ける
    public void TakeDamage(int damage)
    {
        hp = Mathf.Max(0, hp - damage);
    }

    // HP/MP回復
    public void Heal(int amount)
    {
        hp = Mathf.Min(maxHp, hp + amount);
    }
    public void RestoreMp(int amount)
    {
        mp = Mathf.Min(maxMp, mp + amount);
    }

    [Header("レベルアップ設定")]
    public int maxLevel = 20; // 最大レベル
    
    // レベルアップイベント（旧レベルを引数として渡す）
    public System.Action<int> OnLevelUp;
    
    public Animator EnemyAnimator
    {
        get { return enemyAnimator; }
    }

    public Animator PlayerAnimator
    {
        get { return playerAnimator; }
    }

    // 経験値アップ
    public void GainExp(int amount)
    {
        if (level >= maxLevel)
        {
            exp = 0; // 最大レベルに達している場合は経験値を0に
            Debug.Log($"[{charactername}] 最大レベル到達のため経験値獲得なし");
            return;
        }
        
        int startLevel = level;
        int startExp = exp;
        
        exp += amount;
        
        Debug.Log($"[{charactername}] 経験値獲得: +{amount} (現在: {exp}, Lv{level}→Lv{level+1}に必要: {ExpToLevelUp()})");
        
        // CharacterDataも同期
        if (characterData != null)
        {
            characterData.exp = exp;
        }
        
        // 連続レベルアップに対応
        int levelUpCount = 0;
        while (exp >= ExpToLevelUp() && level < maxLevel)
        {
            int expNeeded = ExpToLevelUp();
            exp -= expNeeded;
            
            Debug.Log($"[{charactername}] レベルアップ条件達成: {exp + expNeeded} >= {expNeeded}, 余剰経験値: {exp}");
            
            // CharacterDataの経験値も同期
            if (characterData != null)
            {
                characterData.exp = exp;
            }
            
            LevelUp();
            levelUpCount++;
        }
        
        if (levelUpCount > 0)
        {
            Debug.Log($"★ [{charactername}] Lv{startLevel}→Lv{level} ({levelUpCount}回レベルアップ) 経験値: {startExp}→{exp}/{ExpToLevelUp()}");
        }
        
        // 最大レベルに達した場合
        if (level >= maxLevel)
        {
            exp = 0;
            if (characterData != null)
            {
                characterData.exp = 0;
            }
        }
    }
    
    /// <summary>
    /// 次のレベルアップに必要な経験値を計算
    /// GameManagerのExpTableを使用
    /// </summary>
    private int ExpToLevelUp()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetRequiredExp(level);
        }
        
        // フォールバック（GameManagerがない場合）
        return 100 + (level - 1) * (level - 1) * 50;
    }
    
    /// <summary>
    /// レベルアップ処理（GameManagerのLevelUpTableシステムに委譲）
    /// </summary>
    private void LevelUp()
    {
        int oldLevel = level;
        
        // CharacterDataを通してGameManagerのレベルアップ処理を実行
        if (characterData != null && GameManager.Instance != null)
        {
            GameManager.Instance.LevelUp(characterData);
            
            // GameManagerで更新されたCharacterDataをCharacterに反映
            level = characterData.level;
            maxHp = characterData.maxHp;
            maxMp = characterData.maxMp;
            atk = characterData.atk;
            def = characterData.def;
            spd = characterData.spd;
            Int = characterData.Int;
            
            // ベースステータスを更新
            baseAtk = atk;
            baseDef = def;
            baseSpd = spd;
            baseMaxHp = maxHp;
            baseMaxMp = maxMp;
            
            // バフマネージャーにベースステータスの更新を通知
            if (buffManager != null)
            {
                buffManager.UpdateBaseStats(baseAtk, baseDef, baseSpd, baseMaxHp, baseMaxMp);
            }
            
            // HP/MP全回復
            hp = maxHp;
            mp = maxMp;
            
            // スキル配列を同期
            skills = characterData.skills;
            
            // レベルアップイベント発火
            OnLevelUp?.Invoke(oldLevel);
            
            Debug.Log($"{charactername} がレベル {oldLevel} から {level} にレベルアップしました！");
        }
        else
        {
            // GameManagerがない場合はデフォルトのレベルアップ処理
            level++;
            maxHp += 10;
            maxMp += 5;
            atk += 2;
            def += 2;
            spd += 1;
            
            baseAtk = atk;
            baseDef = def;
            baseSpd = spd;
            baseMaxHp = maxHp;
            baseMaxMp = maxMp;
            
            if (buffManager != null)
            {
                buffManager.UpdateBaseStats(baseAtk, baseDef, baseSpd, baseMaxHp, baseMaxMp);
            }
            
            hp = maxHp;
            mp = maxMp;
            
            OnLevelUp?.Invoke(oldLevel);
            
            Debug.LogWarning($"{charactername} のレベルアップ: GameManagerまたはCharacterDataがないため、デフォルト処理を使用");
        }
    }
    
    /// <summary>
    /// 次のレベルアップに必要な経験値を取得（外部から参照可能）
    /// </summary>
    public int GetExpToNextLevel()
    {
        if (level >= maxLevel) return 0;
        return ExpToLevelUp();
    }
    
    /// <summary>
    /// 現在の経験値の割合を取得（0.0～1.0）
    /// </summary>
    public float GetExpRatio()
    {
        int expNeeded = GetExpToNextLevel();
        if (expNeeded <= 0) return 1f;
        return (float)exp / expNeeded;
    }

    ///<summary>
    ///
    /// CharacterDataを取得（元のScriptableObjectを更新して返す）
    ///
    ///</summary>
    public CharacterData GetCharacterData()
    {
        // 元のcharacterData（ScriptableObject）を更新
        if (characterData != null)
        {
            characterData.hp = hp;
            characterData.mp = mp;
            characterData.atk = atk;
            characterData.def = def;
            characterData.spd = spd;
            characterData.Int = Int;
            characterData.maxHp = maxHp;
            characterData.maxMp = maxMp;
            characterData.exp = exp;
            characterData.level = level;
            characterData.skills = skills;
            characterData.CharacterTransfrom = CharacterTransfrom;
            characterData.StatusFlag = StatusFlag;
            
            return characterData;
        }
        
        Debug.LogWarning($"[Character] {charactername} のcharacterDataがnullです");
        return null;
    }
    
    /// <summary>
    /// バフ適用後の攻撃力を取得
    /// </summary>
    public int GetEffectiveAttack(bool IntSansyou)
    {
        if (buffManager != null)
        {
            return buffManager.GetEffectiveAttack(IntSansyou);
        }
        return atk;
    }
    
    /// <summary>
    /// バフ適用後の防御力を取得
    /// </summary>
    public int GetEffectiveDefense()
    {
        if (buffManager != null)
        {
            return buffManager.GetEffectiveDefense();
        }
        return def;
    }
    
    /// <summary>
    /// バフ適用後の速度を取得
    /// </summary>
    public int GetEffectiveSpeed()
    {
        if (buffManager != null)
        {
            return buffManager.GetEffectiveSpeed();
        }
        return spd;
    }
    
    /// <summary>
    /// バフ適用後の魔法攻撃力を取得
    /// </summary>
    public int GetEffectiveMagicAttack()
    {
        if (buffManager != null)
        {
            return buffManager.GetEffectiveMagicAttack();
        }
        return atk; // 将来的にINTフィールドが追加されたら変更
    }
    
    /// <summary>
    /// バフマネージャーを取得
    /// </summary>
    public CharacterBuffManager GetBuffManager()
    {
        return buffManager;
    }
    
    /// <summary>
    /// バフを適用
    /// </summary>
    public bool ApplyBuff(BuffInstance buffInstance, Character appliedBy)
    {
        if (buffManager != null)
        {
            return buffManager.ApplyBuff(buffInstance, appliedBy);
        }
        return false;
    }
    
    /// <summary>
    /// バフのターン経過処理
    /// </summary>
    public void TickBuffTurn()
    {
        if (buffManager != null)
        {
            buffManager.TickTurn();
        }
    }

}
