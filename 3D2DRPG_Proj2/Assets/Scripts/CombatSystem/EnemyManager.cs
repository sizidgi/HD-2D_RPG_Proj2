using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.TextCore.Text;

public class EnemyManager : MonoBehaviour
{
    [SerializeField]
    public List<CharacterData> enemyData;
    [SerializeField, Header("敵の攻撃ランダムフラグ")]
    private bool AttackRandamFlag;
    [SerializeField]
    private TurnManager turnManager;
    [SerializeField]
    private List<Vector3> vector3s;
    [SerializeField, Header("前に出る距離")]
    private float forwardDistance = 2f;
    [SerializeField, Header("前に出る時間")]
    private float forwardDuration = 0.5f;
    [SerializeField, Header("戻る時間")]
    private float returnDuration = 0.5f;
    private List<GameObject> enemygameObjects = new List<GameObject>();
    [SerializeField]
    public Animator enemyAnimator;
    private Animator playerSideAnimator;
    [SerializeField]
    private SkillSelectionUI skillSelectionUI; // スキル選択UIの参照(スキル名表示用)

    // 戦闘カメラ制御
    [SerializeField] private BattleCameraController battleCamera;
    
    // ボスイベントトリガー
    private BossEventTrigger bossEventTrigger;

    public List<GameObject> GetEnemyData() { return enemygameObjects; }

    //public enum StatusEffect
    //{
    //    Poison,
    //    Stun,
    //    Burn,
    //    Freeze,
    //    Sleep,
    //    Silent,
    //    DamageUp,
    //    TurnChange,
    //    DefenceUp,
    //    SpdDown,
    //    SpdUp,
    //    MagicDamageDown,
    //    MagicCounter,
    //    Makituki,
    //    Zouen,
    //} //毒、スタン、やけど、凍結、眠り、魔封,ダメ増,ターンチェンジ,防御UP,スピードUP,スピードDN,マジックダメDN,反射,巻きつき,増援

    private void Awake()
    {
        if (GameManager.Instance != null && GameManager.Instance.EnemyData.Count != 0)
        {
            enemyData.Clear();
            enemyData.AddRange(GameManager.Instance.EnemyData);
        }
        
        // enemyDataが空の場合は敵を生成しない
        if (enemyData != null && enemyData.Count > 0)
        {
            for (int i = 0; i < enemyData.Count; i++)
            {
                enemyData[i].CharacterTransfrom = vector3s[i];
                var obj = Instantiate(enemyData[i].CharacterObj, vector3s[i] * 2, Quaternion.identity);
                obj.AddComponent<Character>().init(enemyData[i]);
                obj.transform.localRotation = Quaternion.Euler(0, -90, 0);

                Debug.Log("★★★★★★★★★" + obj.name);
                if(obj.name == "Tensi(Clone)")
                {
                    obj.transform.localRotation = Quaternion.Euler(0, 134, 0);
                    obj.transform.localPosition = new Vector3(5, 5, 10);
                }
                if (obj.name == "Akuma(Clone)")
                {
                    obj.transform.localRotation = Quaternion.Euler(0, 77, 0);
                }
                
                obj.transform.parent = this.gameObject.transform;
                enemygameObjects.Add(obj);
            }
        }
        else
        {
            Debug.LogWarning("[EnemyManager] enemyDataが空です。敵を生成しません。");
        }
        
        // ボスイベントトリガーを検索して初期化
        bossEventTrigger = FindObjectOfType<BossEventTrigger>();
        if (bossEventTrigger != null && enemygameObjects.Count > 0)
        {
            // 最初の敵をボスとして設定
            var bossChar = enemygameObjects[0].GetComponent<Character>();
            if (bossChar != null)
            {
                bossEventTrigger.Initialize(bossChar);
            }
        }
    }
    public void Test()
    {
        //EnemyChange
        turnManager.FlagChange();
    }
    
    /// <summary>
    /// ボスイベントをチェック
    /// </summary>
    public void CheckBossEvents()
    {
        if (bossEventTrigger != null)
        {
            bossEventTrigger.CheckEvents();
        }
    }

    /// <summary>
    /// 敵全滅時のイベントをチェック（HP 0%イベント）
    /// </summary>
    /// <param name="onEventEnd">イベント終了後のコールバック</param>
    /// <returns>0%イベントが発動したかどうか</returns>
    public bool CheckEnemyDefeatedEvent(System.Action onEventEnd = null)
    {
        if (bossEventTrigger != null)
        {
            return bossEventTrigger.CheckEnemyDefeatedEvent(onEventEnd);
        }
        return false;
    }

    /// <summary>
    /// Enemy の行動処理（簡単AI: スキルがあれば使用、なければプレイヤーを攻撃）
    /// TurnManager から呼び出される。プレイヤーの Character を攻撃する
    /// </summary>
    public void Test(Character actingEnemy)
    {
        // Nullチェック
        if (actingEnemy == null)
        {
            turnManager.FlagChange();
            return;
        }

        // エネミーのターン処理を開始（コルーチンで実行）
        StartCoroutine(EnemyTurnSequence(actingEnemy));
    }

    /// <summary>
    /// エネミーのターンシーケンス（前に出る→考える→攻撃→戻る）
    /// </summary>
    private IEnumerator EnemyTurnSequence(Character actingEnemy)
    {
        if (actingEnemy == null || actingEnemy.gameObject == null)
        {
            turnManager.FlagChange();
            yield break;
        }

        // 元の位置を保存
        Vector3 originalPosition = actingEnemy.transform.position;
        Vector3 forwardPosition = originalPosition + Vector3.forward * forwardDistance;

        // プレイヤーの取得
        List<Character> playerCandidates = new List<Character>();
        foreach (var playerObj in turnManager.players)
        {
            if (playerObj == null) continue;
            var ch = playerObj.GetComponent<Character>();
            if (ch != null) playerCandidates.Add(ch);
        }

        if (playerCandidates.Count == 0)
        {
            // プレイヤーがいない -> ターン終了
            turnManager.FlagChange();
            yield break;
        }

        // スキル選択（使用可能なスキルがあれば選択、なければnullでスキルなし）
        SkillData chosenSkill = null;
        if (actingEnemy.skills != null && actingEnemy.skills.Length > 0)
        {
            List<SkillData> avail = new List<SkillData>();
            foreach (var s in actingEnemy.skills)
            {
                if (s != null) avail.Add(s);
            }
            if (avail.Count > 0)
            {
                chosenSkill = avail[UnityEngine.Random.Range(0, avail.Count)];
            }
        }
        if (chosenSkill)
        {
            skillSelectionUI.SetSkillWindowText(chosenSkill.skillName);
            skillSelectionUI.ShowSkillWindow(true);

            battleCamera.PlaySkillFocus(actingEnemy.CharacterTransfrom);
        }

        // ターゲット選択（優先: HPが低いプレイヤーを優先。ランダムフラグがtrueなら Random.Range を使用）
        Character target = null;
        int minHp = int.MaxValue;
        if (!AttackRandamFlag)
        {
            foreach (var p in playerCandidates)
            {
                if (p == null) continue;
                if (p.hp < minHp)
                {
                    minHp = p.hp;
                    target = p;
                }
            }
        }

        //ターゲットをロックインしているかチェック、ロックインしてなければnullを返す。
        Character lockedTarget = CheckLockIn(actingEnemy);
        if (lockedTarget != null)
        {
            target = lockedTarget;
        }

        // それでもnullならランダム選択
        if (target == null && chosenSkill != null && chosenSkill.targetScope == TargetScope.Single)
        {
            target = playerCandidates[UnityEngine.Random.Range(0, playerCandidates.Count)];
        }


        // 1. 前に出る、トゥイーンアニメーション
        Tween forwardTween = actingEnemy.transform.DOMove(forwardPosition, forwardDuration)
            .SetEase(Ease.OutQuad);
        yield return forwardTween.WaitForCompletion();

        // 攻撃回数を取得
        int attackCount = (chosenSkill != null && chosenSkill.attackCount > 0)
            ? chosenSkill.attackCount
            : 1;

        // 攻撃回数分ループ
        for (int i = 0; i < attackCount; i++)
        {

            // 攻撃アニメーション再生
            enemyAnimator = actingEnemy.EnemyAnimator;

            if (enemyAnimator != null)
                enemyAnimator.SetTrigger("Attack");

            yield return new WaitForSeconds(0.3f);

            // 3. 攻撃処理を実行
            if (chosenSkill != null && chosenSkill.effectType == SkillEffectType.ExtraAction)
            {
                //なにもしない。下の追加処理で動くので
            }
            else if (chosenSkill.effectType == SkillEffectType.Buff)
            {
                // バフスキルの処理
                ApplyBuff(chosenSkill, actingEnemy);
            }
            else if (chosenSkill.targetScope == TargetScope.Single)
            {
                ApplyAttack(target, chosenSkill, actingEnemy);
            }
            else if (chosenSkill.targetScope == TargetScope.All)
            {
                if (chosenSkill.effectType == SkillEffectType.Heal)
                {
                    if (chosenSkill.isOnlyOnece == false) //とりあえず殴っておく。
                    {
                        ApplyAttack(playerCandidates, chosenSkill, actingEnemy);
                    }
                    else
                    {
                        List<Character> enemies = GetAllies();
                        ApplyHeal(enemies, chosenSkill, actingEnemy);
                    }

                }
                else
                {
                    ApplyAttack(playerCandidates, chosenSkill, actingEnemy);
                }
            }

            // 2回目以降の攻撃がある場合、一旦戻って再度前に出る
            if (i < attackCount - 1)
            {
                // 4a. 元の位置に戻る
                Tween returnTweenA = actingEnemy.transform.DOMove(originalPosition, returnDuration)
                    .SetEase(Ease.InQuad);
                yield return returnTweenA.WaitForCompletion();

                yield return new WaitForSeconds(0.3f); // 少し間を置く

                // 4b. 再度前に出る
                Tween forwardAgain = actingEnemy.transform.DOMove(forwardPosition, forwardDuration)
                    .SetEase(Ease.OutQuad);
                yield return forwardAgain.WaitForCompletion();
            }
        }

        // 追加行動スキルの場合、さらに2回行動
        if (chosenSkill != null && chosenSkill.hasExtraActions)
        {
            // 使用可能なスキルをフィルタリング
            List<SkillData> availableSkills = new List<SkillData>();
            foreach (var s in actingEnemy.skills)
            {
                if (s == null) continue;

                // 今使ったスキルを除外
                if (s == chosenSkill) continue;

                // 必殺技を除外
                if (s.isUltimateSkill) continue;

                availableSkills.Add(s);
            }

            // 2回ランダムに行動
            for (int k = 0; k < 2; k++)
            {
                if (availableSkills.Count == 0) break;

                // ランダムにスキルを選択
                SkillData extraSkill = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];

                // 戻る → 前に出る
                Tween returnTweenA = actingEnemy.transform.DOMove(originalPosition, returnDuration)
                    .SetEase(Ease.InQuad);
                yield return returnTweenA.WaitForCompletion();

                yield return new WaitForSeconds(0.3f);

                Tween forwardTweenA = actingEnemy.transform.DOMove(forwardPosition, forwardDuration)
                    .SetEase(Ease.OutQuad);
                yield return forwardTweenA.WaitForCompletion();

                if (enemyAnimator != null)
                    enemyAnimator.SetTrigger("Attack");

                yield return new WaitForSeconds(0.5f);

                // 攻撃実行
                if (extraSkill.targetScope == TargetScope.Single)
                {
                    // 新しいターゲットを選択（または同じターゲット）
                    var newTarget = playerCandidates[UnityEngine.Random.Range(0, playerCandidates.Count)];
                    ApplyAttack(newTarget, extraSkill, actingEnemy);
                }
                else
                {
                    ApplyAttack(playerCandidates, extraSkill, actingEnemy);
                }
            }
        }

        //元の位置に戻る前に、ちゃんと処理仕切れるを待つために0.6秒くらい待つ
        yield return new WaitForSeconds(0.6f);
        // 4. 元の位置に戻る、トゥイーンアニメーション
        Tween returnTween = actingEnemy.transform.DOMove(originalPosition, returnDuration)
            .SetEase(Ease.InQuad);
        yield return returnTween.WaitForCompletion();

        // 5. ステータスフラグを終了に
        actingEnemy.StatusFlag = StatusFlag.End;
        // 6. ターンを TurnManager に通知
        turnManager.FlagChange();
    }

    /// <summary>
    /// 個別攻撃処理（ダメージ計算と撃破処理）
    /// </summary>
    private void ApplyAttack(Character target, SkillData skill, Character attacker)
    {
        if (target == null) return;

        float power = 0;
        bool isHeal = false; // 回復フラグ
        float AddDamageBonusPower = 0f;

        // クリティカル判定（ロックオン時は+30%）
        float critRate = skill != null ? skill.criticalRate : 0.1f;

        // ロックオンバフがあり、対象が一致すればクリティカル率上昇 仕様上は基礎値にクリティカルを乗算してからDBを加算
        var buffManager = attacker.GetBuffManager();
        if (buffManager != null)
        {
            var buffs = buffManager.GetActiveBuffs();
            foreach (var buff in buffs)
            {
                if (buff.baseData is LockIn lockInBuff)
                {
                    if (buff.lockedTarget == target)
                    {
                        critRate += lockInBuff.criticalRateBonus; // バフから値を取得
                        Debug.Log($"ロックオン効果：クリティカル率+{lockInBuff.criticalRateBonus * 100}%");
                    }
                }
            }
        }

        // クリティカル判定
        bool isCritical = UnityEngine.Random.value < critRate;
        if (isCritical)
        {
            power *= UnityEngine.Random.Range(1.5f,3.0f); // クリティカルヒット 範囲は1.5から3.0の間らしい。よくわからん
            Debug.Log("クリティカルヒット！");
        }


        if (skill != null)
        {
            // ランダム効果スキルかチェック
            if (skill.isRandomEffect)
            {
                // 50%でダメージか回復か決定
                isHeal = UnityEngine.Random.value < 0.5f;

                // 最大HPの割合 skill.powerはスキル側に0.2などの割合ダメを設定しておき、MAXHPと計算させる。
                power = attacker.maxHp * skill.power;

                if (isHeal)
                {
                    // 回復処理（使用者自身を回復）
                    int beforeHp = attacker.hp;
                    attacker.hp += (int)power;
                    target.hp += (int)power;
                    if (attacker.hp > attacker.maxHp) attacker.hp = attacker.maxHp;
                    
                    // 実際の回復量
                    int actualHealAmount = attacker.hp - beforeHp;
                    
                    Debug.Log($"{attacker.charactername}は{actualHealAmount}回復した！");
                    
                    // 回復エフェクトを再生
                    if (VFXManager.Instance != null && attacker.CharacterObj != null)
                    {
                        VFXManager.Instance.PlayHealEffect(attacker.CharacterObj);
                    }
                    
                    // 回復テキストを表示
                    if (DamageEffectUI.Instance != null && attacker.CharacterObj != null && actualHealAmount > 0)
                    {
                        DamageEffectUI.Instance.ShowHealEffectOnCharacter(attacker.CharacterObj, actualHealAmount);
                    }
                    
                    return; // ここで処理終了
                }
                else
                {
                    // ダメージ処理（プレイヤーにダメージ）
                    //この後のダメージ処理はtargetにダメージを与える処理しかないので、先にPlayerhpを削っておく。
                    var Playerhp = target.hp - power;
                    target.hp = (int)math.floor(Playerhp);

                    // 被弾アニメーション再生
                    playerDamageAnimation(target);

                    //もしこの攻撃で死んだら
                    if (target.hp <= 0)
                    {
                        turnManager.NotifyPlayerDefeated(target);
                    }

                    //削ったら
                    target = attacker; // 自分自身をターゲットに
                }
            }
            else
            {
                if (skill.DamageBonusFlg == true)
                {
                    //ダメージボーナスの処理
                    float rndDB = UnityEngine.Random.Range(1f, attacker.atk + 1);
                    AddDamageBonusPower = skill.power + rndDB;

                    power = AddDamageBonusPower;
                }
                else
                {
                    power = skill.power;
                }
            }

        }
        else
        {
            // スキルがない場合は通常攻撃を使用（CharacterData の atk を参照するが、必要に応じて）
            power = attacker != null ? attacker.atk : 1;
        }

        var targethp = target.hp - power;
        target.hp = (int)math.floor(targethp);

        // 被弾アニメーション再生
        playerDamageAnimation(target);

        //スキルにバフがあるなら適用させる。
        if (skill != null && skill.buffEffect != null && skill.buffEffect.Count > 0)
        {
            foreach (var buffEffect in skill.buffEffect)
            {
                if (buffEffect == null) continue;

                BuffInstance buff = new BuffInstance(buffEffect);
                buff.remainingTurns = skill.buffDuration;

                switch (buffEffect.statusEffect)
                {
                    case StatusEffect.Poison:
                        Debug.Log("毒付与");
                        break;
                    case StatusEffect.Stun:
                        break;
                    case StatusEffect.Burn:
                        break;
                    case StatusEffect.Freeze:
                        break;
                    case StatusEffect.Sleep:
                        break;
                    //case StatusEffect.Silent:
                    //    // ランダムに1つスキルを選んで封じる
                    //    if (target.skills != null && target.skills.Length > 0)
                    //    {
                    //        List<int> availableSkills = new List<int>();
                    //        for (int i = 0; i < target.skills.Length; i++)
                    //        {
                    //            if (target.skills[i] != null) availableSkills.Add(i);
                    //        }

                    //        if (availableSkills.Count > 0)
                    //        {
                    //            buff.LockSkillIndex = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];
                    //            Debug.Log($"{target.charactername}のスキル{buff.LockSkillIndex}番が封じられた");
                    //        }
                    //    }
                    //    break;
                    case StatusEffect.DamageUp:
                        break;
                    case StatusEffect.TurnChange:
                        break;
                    case StatusEffect.DefenceUp:
                        break;
                    case StatusEffect.SpdDown:
                        break;
                    case StatusEffect.SpdUp:
                        break;
                    case StatusEffect.MagicDamageDown:
                        break;
                    case StatusEffect.MagicCounter:
                        break;
                    case StatusEffect.Makituki:
                        break;
                    case StatusEffect.Zouen:
                        break;
                    case StatusEffect.MPRecovery: //天使でしか使わないバフ
                        attacker.mp += attacker.maxMp / 5;
                        break;
                    default:
                        break;
                }
                target.ApplyBuff(buff, attacker);
                // バフスキルの場合のみエフェクトを表示
                if (skill.effectType == SkillEffectType.Buff)
                {
                    PlayBuffVFX(buff, target);
                }
            }
        }
        Debug.Log($"{attacker.name} が {target.name} に {power} ダメージ。残りHP: {target.hp}");

        // ダメージエフェクトを表示（攻撃を受けたターゲットの位置の前に表示）
        // 注: 敵がプレイヤーを攻撃する場合、プレイヤーの位置にエフェクトを表示します
        if (DamageEffectUI.Instance != null && target.CharacterObj != null)
        {
            DamageEffectUI.Instance.ShowDamageEffectOnEnemy(target.CharacterObj, power);
            
            // スキルのVFXを発火
            if (skill != null)
            {
                DamageEffectUI.Instance.PlaySkillVFX(skill, target.CharacterObj);
            }
        }

        if (target.hp <= 0)
        {
            turnManager.NotifyPlayerDefeated(target);
        }
    }

    /// <summary>
    /// 全体攻撃処理（ダメージ計算と撃破処理）
    /// </summary>
    private void ApplyAttack(List<Character> target, SkillData skill, Character attacker)
    {
        if (target == null) return;

        float power = 0;
        float AddDamageBonusPower = 0f;


        if (skill != null)
        {
            //バフがかかった状態のatkを参照しなければならない。
            float attackerAtk = attacker != null ? attacker.GetEffectiveAttack(skill.isIntSansyou) : 1;

            if (skill.DamageBonusFlg == true)
            {

                //ダメージボーナスの処理
                float rndDB = UnityEngine.Random.Range(1f, attackerAtk + 1);
                power = skill.power + rndDB;

                power = AddDamageBonusPower;
            }
            else
            {
                power = skill.power;
            }

        }

        //各キャラに全体攻撃、耐性を含んだ計算は未実装。
        foreach (Character chara in target)
        {
            var targethp = chara.hp - power;
            chara.hp = (int)math.floor(targethp);
            Debug.Log($"{attacker.name} が {chara.name} に {power} ダメージ。残りHP: {chara.hp}");

            // 被弾アニメーション再生
            playerDamageAnimation(chara);

            //スキルにバフがあるなら適用させる。
            if (skill != null && skill.buffEffect != null && skill.buffEffect.Count > 0)
            {
                foreach (var buffEffect in skill.buffEffect)
                {
                    if (buffEffect == null) continue;

                    BuffInstance buff = new BuffInstance(buffEffect);
                    buff.remainingTurns = skill.buffDuration;

                    switch (buffEffect.statusEffect)
                    {
                        case StatusEffect.Poison:
                            break;
                        case StatusEffect.Stun:
                            break;
                        case StatusEffect.Burn:
                            // 状態異常の成功率を計算
                            float successRate = (attacker.atk - chara.atk + 1) * 5f / 100f;

                            // 成功率を0～1の範囲にクランプ（100%を超えないように）
                            successRate = Mathf.Clamp01(successRate);

                            // 判定
                            if (UnityEngine.Random.value < successRate)
                            {
                                chara.ApplyBuff(buff, attacker);
                                // バフスキルの場合のみエフェクトを表示
                                if (skill.effectType == SkillEffectType.Buff)
                                {
                                    PlayBuffVFX(buff, chara);
                                }
                            }
   
                            break;

                    }

                }
            }

            // ダメージエフェクトを表示（攻撃を受けたターゲットの位置の前に表示）
            // 注: 敵がプレイヤーを攻撃する場合、プレイヤーの位置にエフェクトを表示します
            if (DamageEffectUI.Instance != null && chara.CharacterObj != null)
            {
                DamageEffectUI.Instance.ShowDamageEffectOnEnemy(chara.CharacterObj, power);
                
                // スキルのVFXを発火
                if (skill != null)
                {
                    DamageEffectUI.Instance.PlaySkillVFX(skill, chara.CharacterObj);
                }
            }

            if (chara.hp <= 0)
            {
                turnManager.NotifyPlayerDefeated(chara);
            }
        }

    }

    private void ApplyBuff(SkillData skill, Character target)
    {
        Debug.Log("EnemyManager-ApplyBuffに入る。");
        if (skill == null || skill.buffEffect == null || skill.buffEffect.Count == 0)
            return;

        // バフスキルかどうかをチェック（effectTypeがBuffの場合のみエフェクト表示）
        bool isBuffSkill = skill.effectType == SkillEffectType.Buff;

        foreach (var buffBase in skill.buffEffect)
        {
            BuffInstance buff = new BuffInstance(buffBase);
            buff.remainingTurns = skill.buffDuration;
            
            var Player = GetPlayers(); // プレイヤーリスト取得

            // statusEffectで大きく分岐（状態異常の種類ごと）
            switch (buffBase.statusEffect)
            {   //魔法ダメ半減
                case StatusEffect.MagicDamageDown:

                    switch (buffBase.buffRange)
                    {
                        case BuffRange.Self:
                            // 自分自身にバフ
                            target.ApplyBuff(buff, target);
                            if (isBuffSkill) PlayBuffVFX(buff, target);
                            break;

                        case BuffRange.Ally:
                            // 味方1人にバフ（ランダムまたは条件選択）
                            var allies = GetAllies(); // 味方リスト取得
                            if (allies.Count > 0)
                            {
                                var ally = allies[UnityEngine.Random.Range(0, allies.Count)];
                                ally.ApplyBuff(buff, target);
                                if (isBuffSkill) PlayBuffVFX(buff, ally);
                            }
                            break;

                        case BuffRange.AllAllies:
                            // 全味方にバフ
                            var allAllies = GetAllies();
                            foreach (var ally in allAllies)
                            {
                                BuffInstance allyBuff = new BuffInstance(buffBase);
                                allyBuff.remainingTurns = skill.buffDuration;
                                ally.ApplyBuff(allyBuff, target);
                                if (isBuffSkill) PlayBuffVFX(allyBuff, ally);
                            }
                            break;
                    }
                    break;

                case StatusEffect.Makituki:
                    // 敵1人にデバフ

                    if (Player.Count > 0)
                    {
                        var selectedPlayer = Player[UnityEngine.Random.Range(0, Player.Count)];

                        selectedPlayer.ApplyBuff(buff, target);
                        if (isBuffSkill) PlayBuffVFX(buff, selectedPlayer);
                        playerDamageAnimation(selectedPlayer);
                    }
                    
                    break;

                case StatusEffect.Poison:
                    // 敵1人にデバフ
                   
                    if (Player.Count > 0)
                    {
                        var selectedPlayer = Player[UnityEngine.Random.Range(0, Player.Count)];

                        selectedPlayer.ApplyBuff(buff, target);
                        if (isBuffSkill) PlayBuffVFX(buff, selectedPlayer);
                        playerDamageAnimation(selectedPlayer);
                    }
                    
                    break;

                case StatusEffect.Silent:
                    // 敵1人にデバフ
                    var enemies = GetPlayers(); // プレイヤーリスト取得
                    if (enemies.Count > 0)
                    {
                        var enemy = enemies[UnityEngine.Random.Range(0, enemies.Count)];

                        // スキル封じ処理
                        if (enemy.skills != null && enemy.skills.Length > 0)
                        {
                            var availableSkills = new List<int>();
                            for (int i = 0; i < enemy.skills.Length; i++)
                            {
                                if (enemy.skills[i] != null) availableSkills.Add(i);
                            }
                            if (availableSkills.Count > 0)
                            {
                                buff.LockSkillIndex = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];
                            }
                        }

                        enemy.ApplyBuff(buff, target);
                        if (isBuffSkill) PlayBuffVFX(buff, enemy);
                        playerDamageAnimation(enemy);
                    }
                    
                    break;

                case StatusEffect.MagicCounter:
                    // 魔法反射バフ（自分自身）
                    switch (buffBase.buffRange)
                    {
                        case BuffRange.Self:
                            target.ApplyBuff(buff, target);
                            if (isBuffSkill) PlayBuffVFX(buff, target);
                            Debug.Log($"{target.charactername}にマジックカウンター付与");
                            break;
                    }
                    break;

                case StatusEffect.DamageUp:
                    //攻撃力を上げる。(物理魔法兼用)
                    switch (buffBase.buffRange)
                    {
                        case BuffRange.Self:
                            target.ApplyBuff(buff, target);
                            if (isBuffSkill) PlayBuffVFX(buff, target);
                            break;
                        case BuffRange.AllEnemies:

                            var list = GetAllies(); // 敵の味方取得
                            foreach (var enemy in list)
                            {
                                enemy.ApplyBuff(buff, target);
                                if (isBuffSkill) PlayBuffVFX(buff, enemy);
                            }
                            break;
                    }

                    break;

                case StatusEffect.LockIn:
                    // ロックオン：敵1体を対象として選択
                    switch (buffBase.buffRange)
                    {
                        case BuffRange.Self:
                            var enemy = GetPlayers();
                            if (enemy.Count > 0)
                            {
                                // ランダムに敵1体を選択してロックオン
                                var lockedEnemy = enemy[UnityEngine.Random.Range(0, enemy.Count)];
                                buff.lockedTarget = lockedEnemy;

                                // 自分自身にバフを適用
                                target.ApplyBuff(buff, target);
                                if (isBuffSkill) PlayBuffVFX(buff, target);

                                Debug.Log($"{target.charactername}が{lockedEnemy.charactername}をロックオン");
                            }
                            break;
                    }
                    break;

                case StatusEffect.DefenceUp:
                    switch (buffBase.buffRange)
                    {
                        //無の創造用
                        case BuffRange.Self:
                            buff.remainingTurns = 1;
                            // 自分自身にバフを適用
                            target.ApplyBuff(buff, target);
                            if (isBuffSkill) PlayBuffVFX(buff, target);
                            break;

                    }
                    break;
            }
        }
    }
    /// <summary>
    /// 回復処理 今は全体回復用
    /// </summary>
    private void ApplyHeal(List<Character> targets, SkillData skill, Character actingEnemy)
    {
        if (targets == null || skill == null) return;

        foreach (var target in targets)
        {
            if (target == null) continue;

            int beforeHp = target.hp;
            float healAmount = 0;
            healAmount = target.maxHp * skill.power; //割合で回復
            target.hp += (int)healAmount;
            //MaxHPを超えないようにする
            if (target.hp > target.maxHp)
            {
                target.hp = target.maxHp;
            }
            
            // 実際の回復量を計算
            int actualHealAmount = target.hp - beforeHp;
            
            // 回復エフェクトを再生
            if (VFXManager.Instance != null && target.CharacterObj != null)
            {
                VFXManager.Instance.PlayHealEffect(target.CharacterObj);
            }
            
            // 回復テキストを表示
            if (DamageEffectUI.Instance != null && target.CharacterObj != null && actualHealAmount > 0)
            {
                DamageEffectUI.Instance.ShowHealEffectOnCharacter(target.CharacterObj, actualHealAmount);
            }
        }

        //一回限りならばもう使えないようにする
        if (skill.isOnlyOnece)
            skill.isOnlyOnece = false;
    }

    //敵の味方リストを取得する。
    private List<Character> GetAllies()
    {
        List<Character> allies = new List<Character>();
        foreach (var enemyObj in turnManager.enemys)
        {
            if (enemyObj == null) continue;
            var ch = enemyObj.GetComponent<Character>();
            if (ch != null) allies.Add(ch);
        }
        return allies;
    }

    //味方サイドの味方リストを取得する。
    private List<Character> GetPlayers()
    {
        List<Character> players = new List<Character>();
        foreach (var playerObj in turnManager.players)
        {
            if (playerObj == null) continue;
            var ch = playerObj.GetComponent<Character>();
            if (ch != null) players.Add(ch);
        }
        return players;
    }

    //ロックインチェック
    private Character CheckLockIn(Character actingEnemy)
    {
        // ロックオンバフがあれば、そのターゲットに上書き
        var buffManager = actingEnemy.GetBuffManager();
        Character Ltarget = null;
        if (buffManager != null)
        {
            var buffs = buffManager.GetActiveBuffs();
            foreach (var buff in buffs)
            {
                if (buff.baseData.statusEffect == StatusEffect.LockIn)
                {
                    if (buff.lockedTarget != null)
                    {
                        Ltarget = buff.lockedTarget;

                        Debug.Log($"{actingEnemy.charactername}はロックオン対象{Ltarget.charactername}を攻撃");
                        break;
                    }
                }
            }

        }
        return Ltarget;
    }
    
    /// <summary>
    /// バフの種類に応じたVFXを再生
    /// </summary>
    private void PlayBuffVFX(BuffInstance buff, Character target)
    {
        if (VFXManager.Instance == null || target == null || target.CharacterObj == null) return;
        
        // StatusEffectの種類に応じてエフェクトを選択
        switch (buff.baseData.statusEffect)
        {
            case StatusEffect.DamageUp:
            case StatusEffect.DefenceUp:
                VFXManager.Instance.PlayAttackUpEffect(target.CharacterObj);
                break;
                
            case StatusEffect.SpdUp:
            case StatusEffect.MagicCounter:
                VFXManager.Instance.PlayBuffEffect(target.CharacterObj);
                break;
                
            case StatusEffect.Poison:
                VFXManager.Instance.PlayPoisonEffect(target.CharacterObj);
                break;
                
            case StatusEffect.SpdDown:
            case StatusEffect.MagicDamageDown:
            case StatusEffect.Silent:
            case StatusEffect.Makituki:
                VFXManager.Instance.PlayDebuffEffect(target.CharacterObj);
                break;
                
            case StatusEffect.MPRecovery:
                VFXManager.Instance.PlayHealEffect(target.CharacterObj);
                break;
                
            case StatusEffect.LockIn:
                VFXManager.Instance.PlayBuffEffect(target.CharacterObj);
                break;
                
            default:
                // デフォルトはバフエフェクト
                VFXManager.Instance.PlayBuffEffect(target.CharacterObj);
                break;
        }
    }

    /// <summary>
    /// 汎用アニメ再生
    /// </summary>
    /// <param name="target"></param>
    private void playerDamageAnimation(Character target)
    {
        Debug.Log("playerDamageAnimationに入った。");
        // 攻撃アニメーション再生
        playerSideAnimator = target.PlayerAnimator;
        if (playerSideAnimator != null)
        {
            playerSideAnimator.SetTrigger("Damage");
            Debug.Log(target.charactername + "★PlayerにDamageをSet");
        }
        else
        {
            Debug.Log("playerDamagerAnimation:" + target.charactername + "はnull");
        }
    }
}
