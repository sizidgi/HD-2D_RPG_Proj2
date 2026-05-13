using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TextCore.Text;

//バフ効果
public enum buffEffect
{
    damegeDown,
    SkillnotUse,
}
//バフの効果を管理するクラス
[Serializable]
public class CharacterBuff
{
    public buffEffect effect;  // バフの効果の種類
    public BuffBase buffBase;  // バフの基本データ(使うか不明）
    public List<Character> target;　// バフの対象キャラクター
    public int remainingTurns;  // バフの残りターン数
}

/// <summary>
/// プレイヤーの戦闘行動を管理するクラス
/// </summary>
public class PlayerManager : MonoBehaviour
{
    #region 関数宣言
    [SerializeField, Header("UIテスト用")]
    private UITest uiTest;  // UIテスト用の参照
    [SerializeField, Header("ComboUI")]
    private ComboAttack comboUI;   // コンボ攻撃UIの参照
    [SerializeField, Header("選択用UI")]
    private SkillSelectionUI skillSelectionUI; // スキル選択UIの参照
    [SerializeField, Header("ターン管理")]
    private TurnManager turnManager; // ターン管理の参照
    [SerializeField, Header("エネミー管理")]
    private EnemyManager enemyManager; // エネミー管理の参照
    [SerializeField, Header("プレイヤーキャラクターリスト")]
    private List<CharacterData> playerCharacters; // プレイヤーキャラクターデータのリスト
    [SerializeField, Header("キャラクターの生成配置座標")]
    private List<Vector3> spawnPositions; // キャラクターの生成配置座標のリスト
    [SerializeField, Header("プレイヤーステータスパネル")]
    private List<PlayerStatusPanel> playerStatusPanel; // プレイヤーステータスパネルのリスト

    [SerializeField, Header("キャラクター戦闘開始位置")]
    private Vector3 ActionPosition;
    [SerializeField, Header("キャラクター開始位置")]
    private Vector3 StartPosition;
    // キャラクターのGameObject保存用
    private List<GameObject> characterObjects = new List<GameObject>();
    // キャラクターとパネルの紐付け（キャラクターGameObject -> パネルIndex）
    private Dictionary<GameObject, int> characterToPanelIndex = new Dictionary<GameObject, int>();
    // 現在選択中のキャラクター
    private Character selectedCharacter;
    // 現在選択中のスキル
    private SkillData selectedSkill;
    // 行動待機のフラグ
    private bool isActionPending = false;
    //選択している敵
    private Character selectedEnemy;
    //コンボ数のcount
    private int comboCount = 0;

    //アニメーション用
    [SerializeField]
    public Animator enemyAnimator;
    private Animator playerSideAnimator;

    public AudioSource seSource;

    //MPが足りなくてキャンセルするとき用のサウンド
    public AudioClip cancelSoundEffect;

    //Buffの管理用リスト
    public List<CharacterBuff>　characterBuffs = new List<CharacterBuff>();

    /// <summary>
    /// キャラクターデータ取得用
    /// </summary>
    public List<GameObject> GetPlayerCharacters() => characterObjects;

    // 戦闘カメラ制御
    [SerializeField] private BattleCameraController battleCamera;

    #endregion

    #region 初期化と更新処理
    /// <summary>
    /// 初期化処理（キャラクターの配置）
    /// </summary>
    private void Awake()
    {
        if (GameManager.Instance != null && GameManager.Instance.PlayerData != null && GameManager.Instance.PlayerData.Count > 0)
        {
            playerCharacters.Clear();
            playerCharacters.AddRange(GameManager.Instance.PlayerData);
        }
        
        // パーティーメンバーが0人の場合は警告を表示して処理をスキップ
        if (playerCharacters == null || playerCharacters.Count == 0)
        {
            Debug.LogWarning("[PlayerManager] パーティーメンバーが0人です。戦闘を開始できません。");
            return;
        }
        
        isActionPending = false;
        
        // 全てのステータスパネルを一旦非表示にする
        foreach (var panel in playerStatusPanel)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }
        
        for (int i = 0; i < playerCharacters.Count; i++)
        {
            // スポーン位置が足りない場合は警告
            if (i >= spawnPositions.Count)
            {
                Debug.LogError($"[PlayerManager] スポーン位置が不足しています。{i + 1}人目のキャラクターを配置できません。");
                break;
            }
            
            // ステータスパネルが足りない場合は警告
            if (i >= playerStatusPanel.Count || playerStatusPanel[i] == null)
            {
                Debug.LogWarning($"[PlayerManager] ステータスパネルが不足しています。{i + 1}人目のキャラクターのUIを表示できません。");
            }
            
            // キャラクターの座標をセット
            playerCharacters[i].CharacterTransfrom = spawnPositions[i];
            // キャラクターのGameObjectを作成
            var obj = Instantiate(playerCharacters[i].CharacterObj, spawnPositions[i], Quaternion.identity);
            obj.AddComponent<Character>().init(playerCharacters[i]);
            obj.transform.parent = transform;
            characterObjects.Add(obj);
            
            // キャラクターとパネルの紐付けを記録
            characterToPanelIndex[obj] = i;
            
            // ステータスパネルの設定
            if (i < playerStatusPanel.Count && playerStatusPanel[i] != null)
            {
                playerStatusPanel[i].gameObject.SetActive(true);
                PlayerData playerData = new PlayerData(characterObjects[i].GetComponent<Character>());
                playerStatusPanel[i].UpdatePlayerStatus(playerData);
                
                // CharacterBuffManagerとPlayerStatusPanelを接続
                CharacterBuffManager buffManager = obj.GetComponent<CharacterBuffManager>();
                if (buffManager != null)
                {
                    buffManager.OnBuffsChanged.AddListener(playerStatusPanel[i].UpdateBuffIcons);
                    Debug.Log($"[PlayerManager] バフアイコン接続完了: {playerCharacters[i].charactername} → PlayerStatusPanel[{i}]");
                }
                else
                {
                    Debug.LogWarning($"[PlayerManager] バフアイコン接続失敗: buffManager={buffManager}");
                }
            }
        }
        
        Debug.Log($"[PlayerManager] {playerCharacters.Count}人のキャラクターを戦闘に配置しました");
    }

    
    /// <summary>
    /// プレイヤーの行動開始（外部から呼び出される）
    /// </summary>
    public void StartPlayerAction(Character character)
    {
        // 戦闘が一時停止中の場合は行動開始しない
        if (turnManager != null && turnManager.IsBattlePaused())
        {
            Debug.Log("[PlayerManager] 戦闘が一時停止中のため、プレイヤーの行動を開始できません");
            return;
        }

        selectedCharacter = character;
        selectedCharacter.StatusFlag = StatusFlag.Move;
        isActionPending = true;
    }
    /// <summary>
    /// ステータスマシンの状態管理・行動処理
    /// </summary>
    private void Update()
    {
        // UIのプレイヤーステータスパネル更新
        PlayerUIUpdate();
        
        // 戦闘が一時停止中の場合は行動処理をスキップ
        if (turnManager != null && turnManager.IsBattlePaused())
        {
            return;
        }
        
        // 対象選択中（Attack / Heal / Buff）にESC/Backspaceでスキル選択に戻る
        if (selectedCharacter != null &&
            (selectedCharacter.StatusFlag == StatusFlag.Attack ||
             selectedCharacter.StatusFlag == StatusFlag.Heal ||
             selectedCharacter.StatusFlag == StatusFlag.Buff))
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                Debug.Log("[PlayerManager] 対象選択をキャンセル - スキル選択に戻ります");
                
                // キャンセルSEを再生
                if (cancelSoundEffect != null && seSource != null)
                {
                    seSource.PlayOneShot(cancelSoundEffect);
                }
                
                // 対象選択UIを閉じる
                if (uiTest != null)
                {
                    uiTest.ClosePanel();
                }
                
                // スキル選択に戻る
                selectedCharacter.StatusFlag = StatusFlag.Select;
                isActionPending = true;
                return;
            }
        }
        
        if (!isActionPending) return;

        // キャラクターの状態に応じて処理を分岐
        PlayerUpdate();
        // 行動処理のフラグをリセット
        isActionPending = false;
    }

    /// <summary>
    /// UIのプレイヤーステータスパネル更新
    /// </summary>
    private void PlayerUIUpdate()
    {
        // 全てのキャラクターをチェック（倒されたキャラクターも含む）
        foreach (var kvp in characterToPanelIndex)
        {
            GameObject characterObj = kvp.Key;
            int panelIndex = kvp.Value;
            
            // パネルインデックスが範囲外の場合はスキップ
            if (panelIndex < 0 || panelIndex >= playerStatusPanel.Count)
            {
                continue;
            }
            
            // キャラクターオブジェクトがnull（撃破されて削除された）場合はパネルを非表示
            if (characterObj == null)
            {
                if (playerStatusPanel[panelIndex] != null)
                {
                    playerStatusPanel[panelIndex].gameObject.SetActive(false);
                }
                continue;
            }
            
            var character = characterObj.GetComponent<Character>();
            if (character == null)
            {
                // キャラクターコンポーネントがない場合もパネルを非表示
                if (playerStatusPanel[panelIndex] != null)
                {
                    playerStatusPanel[panelIndex].gameObject.SetActive(false);
                }
                continue;
            }
            
            // キャラクターが倒されている場合はパネルを非表示
            if (character.hp <= 0)
            {
                if (playerStatusPanel[panelIndex] != null)
                {
                    playerStatusPanel[panelIndex].gameObject.SetActive(false);
                }
            }
            else
            {
                // キャラクターが生きている場合はパネルを更新
                if (playerStatusPanel[panelIndex] != null)
                {
                    playerStatusPanel[panelIndex].gameObject.SetActive(true);
                    PlayerData playerData = new PlayerData(character);
                    playerStatusPanel[panelIndex].UpdatePlayerStatus(playerData);
                }
            }
        }
    }

    /// <summary>
    /// キャラクターの状態に応じて処理を分岐
    /// </summary>
    private void PlayerUpdate()
    {
        switch (selectedCharacter.StatusFlag)
        {
            case StatusFlag.Move:
                PlayerMove();
                break;
            case StatusFlag.Select:
                PlayerSelect();
                break;
            case StatusFlag.Attack:
                PlayerAttackSelect();
                break;
            case StatusFlag.Heal:
                PlayerHealSelect();
                break;
            case StatusFlag.Buff:
                PlayerBuff();
                break;
            case StatusFlag.End:
                PlayerEnd();
                break;
        }
    }
    /// <summary>
    /// 移動処理
    /// </summary>
    private void PlayerMove()
    {


        //開始位置を保存
        StartPosition = selectedCharacter.CharacterObj.transform.position;
        // キャラクターを行動位置に移動
        selectedCharacter.CharacterObj.transform.DOMove(ActionPosition, 1f).OnComplete(() =>
        {
            selectedCharacter.StatusFlag = StatusFlag.Select;
            isActionPending = true;
        }); ;

        
    }
    /// <summary>
    /// スキル選択処理
    /// </summary>
    private void PlayerSelect()
    {
        // スキル選択パネル
        List<SkillData> skills = new List<SkillData>();
        skills.AddRange(selectedCharacter.skills);
        // UnityEventを作成してコールバックを設定
        UnityEvent<int> callback = new UnityEvent<int>();
        //カリバフ効果適用
        callback.AddListener(OnSkillSelected);
        // スキル選択UIを表示
        skillSelectionUI.ShowSkillSelection(skills, callback);
    }
    /// <summary>
    /// 攻撃相手選択フェイズ
    /// </summary>
    private void PlayerAttackSelect()
    {
        Debug.Log($"=== PlayerAttackSelect() 呼ばれました ===");
        
        // 攻撃対象選択パネル
        List<Character> enemies = getEnemy();
        Debug.Log($"敵の数: {enemies.Count}");
        
        var attackEvent = new UnityEvent<int>();
        attackEvent.AddListener((index) => OnAttackSelected(enemies, index));
        uiTest.Inputs(attackEvent, enemies.Count - 1, enemies);
    }
    /// <summary>
    /// 回復味方を選択するファイズ
    /// </summary>
    private void PlayerHealSelect()
    {
        // Heal対象選択パネル対象選択パネル
        List<Character> characters = getPlayer();
        var healEvent = new UnityEvent<int>();
        healEvent.AddListener((index) => OnHealSelected(characters, index));
        uiTest.Inputs(healEvent, characters.Count - 1, characters);
    }
    /// <summary>
    /// バフの処理
    /// </summary>
    private void PlayerBuff()
    {
        if (selectedSkill.buffEffect.Count > 0)
        {
            if (selectedCharacter.mp < selectedSkill.mpCost)
            {
                selectedCharacter.StatusFlag = StatusFlag.Select;
                isActionPending = true;
                return;
            }
            selectedCharacter.mp -= selectedSkill.mpCost;
            SetBuff();
        }
    }
    /// <summary>
    /// 最終処理
    /// </summary>
    private void PlayerEnd()
    {
        StartCoroutine(AttackEndTick());
    }
    private IEnumerator AttackEndTick()
    {
        yield return new WaitForSeconds(1f); // 攻撃エフェクトの表示時間に合わせて待機
        //攻撃後バフの設定
        //バフ効果の管理
        buffTurnManage();
        // キャラクターを開始位置に戻る
        selectedCharacter.CharacterObj.transform.DOMove(StartPosition, 1f).OnComplete(() =>
        {
            selectedCharacter.StatusFlag = StatusFlag.None;
            // ターン処理を終了
            turnManager.FlagChange();
        });
    }

    #endregion

    #region 行動処理のコールバック
    /// <summary>
    /// スキル選択時のコールバック
    /// </summary>
    private void OnSkillSelected(int index)
    {
        Debug.Log($"=== OnSkillSelected() 呼ばれました。Index: {index} ===");
        
        //callBackでかえってきた変数がスキルのリスト内の物選択しているか
        if (index < 0 || index >= selectedCharacter.skills.Length)
        {
            Debug.LogWarning($"無効なスキルインデックス: {index}");
            selectedCharacter.StatusFlag = StatusFlag.Select;
            isActionPending = true;
            return;
        }
        //スキルリストにセットされている確認
        if (selectedCharacter.skills[index] == null)// nullチェック追加
        {
            Debug.LogWarning($"スキル[{index}]が null です");
            selectedCharacter.StatusFlag = StatusFlag.Select;
            isActionPending = true;
            return;
        }
        selectedSkill = selectedCharacter.skills[index];
        
        Debug.Log($"スキル選択: {selectedSkill.skillName}, canCombo: {selectedSkill.canCombo}, effectType: {selectedSkill.effectType}");

        //選択したスキル名を表示させたい
        //
        //スキル呼び出し
        
        Debug.Log($"effectTypeで分岐: {selectedSkill.effectType}");

        //MPが足りるかを確認
        if (selectedCharacter.mp < selectedSkill.mpCost)
        {
            //SEならす
            if (cancelSoundEffect != null)
            {
                seSource.PlayOneShot(cancelSoundEffect);
            }

            Debug.LogWarning($"MP不足: 必要 {selectedSkill.mpCost}, 現在 {selectedCharacter.mp}");
            selectedCharacter.StatusFlag = StatusFlag.Select;
            isActionPending = true;
            return;
        }


        switch (selectedSkill.effectType)
        {
            case SkillEffectType.Attack:
                Debug.Log("StatusFlag.Attackに移行");
                selectedCharacter.StatusFlag = StatusFlag.Attack;
                if (selectedSkill.targetScope == TargetScope.All||selectedCharacter.AllAttack)
                    OnAttackSelected(getEnemy(), 0);
                break;
            case SkillEffectType.Heal:
                Debug.Log("StatusFlag.Healに移行");
                selectedCharacter.StatusFlag = StatusFlag.Heal;
                if (selectedSkill.targetScope == TargetScope.All)
                    OnHealSelected(getPlayer(), 0); 
                break;
            case SkillEffectType.Buff:
                Debug.Log("StatusFlag.Buffに移行");
                selectedCharacter.StatusFlag = StatusFlag.Buff;
                if (selectedSkill.targetScope == TargetScope.All)
                    isActionPending = true;
                //    //仮バフ効果適用
                OnBuffSelected(getPlayer(), 0, selectedSkill.buffEffect[0]);
                break;
        }
        if(selectedSkill.targetScope != TargetScope.All)
            isActionPending = true;
    }

    /// <summary>
    /// 攻撃対象選択時のコールバック
    /// </summary>
    private void OnAttackSelected(List<Character> enemies, int index)
    {
        Debug.Log($"=== OnAttackSelected() 呼ばれました。Index: {index}, TargetScope: {selectedSkill.targetScope}, canCombo: {selectedSkill.canCombo} ===");
        
        // スキルウィンドウ表示
        skillSelectionUI.ShowSkillWindow(false);

        battleCamera.PlaySkillFocus(selectedCharacter.CharacterTransfrom);

        // 攻撃の範囲内かを確認
        if (index < 0 || index >= enemies.Count)
        {
            Debug.LogWarning($"無効な敵インデックス: {index}");
            selectedCharacter.StatusFlag = StatusFlag.Attack;
            isActionPending = true;
            return;
        }
        

        // 全の攻撃スキルの場合、すべての敵に攻撃を適用
        if (selectedSkill.targetScope == TargetScope.All||selectedCharacter.AllAttack)
        {
            Debug.Log("全体攻撃として処理します");
            
            //相手に対するバフが無ければmp消費(バフありだとバフの処理で更にmpが同じく引かれてしまうため)
            if(selectedSkill.buffEffect.Count == 0)
            {
                selectedCharacter.mp -= selectedSkill.mpCost;
                Debug.Log("バフがないのでMPを引きます");
            }
                
            foreach (var enemy in enemies)
            {
                bool EnemyDefeat = ApplyAttack(enemy, selectedSkill);
                if (!EnemyDefeat)
                {
                    Debug.Log($"{enemy}撃破！");
                }
            }
        } else
        {
            if (selectedSkill.canCombo)
            {
                Debug.Log($"=== コンボスキル開始: {selectedSkill.skillName}, MaxCombo: {selectedSkill.maxcombo} ===");
                
                if (comboUI == null)
                {
                    Debug.LogError("PlayerManager: comboUI が null です！Inspectorで設定してください。");
                    return;
                }
                
                selectedCharacter.mp -= selectedSkill.mpCost;
                comboCount = 0;
                //コンボスキルの処理（成功時）
                Func<int, bool> attackEvent;
                // 修正: OnComboApplyAttackメソッドをラムダ式でラップし、Func<int, bool>型にする
                attackEvent = (comboStep) => OnComboApplyAttack();
                var attackEnd = new UnityEvent<int>();
                attackEnd.AddListener((index) => OnComboEnd());
                selectedEnemy = enemies[index];
                comboUI.AttackTiming(selectedSkill.timingWindowStart, selectedSkill.timingWindowEnd);
                comboUI.Inputs(attackEvent, attackEnd, selectedSkill.maxcombo, selectedEnemy);
            }
            else
            {
                //通常スキルの処理  
                var enemy = enemies[index];
                selectedCharacter.mp -= selectedSkill.mpCost;
                bool EnemyDefeat = ApplyAttack(enemy, selectedSkill);
                if (!EnemyDefeat)
                {
                    Debug.Log($"{enemy}撃破！");
                }
            }
        }

        //コンボ以外
        if(!selectedSkill.canCombo)
        {
            //攻撃後バフの設定
            if (selectedCharacter.skills.Length > 0)
            {
                SetBuff();
            }
            selectedCharacter.StatusFlag = StatusFlag.End;
            isActionPending = true;
        }
    }

    /// <summary>
    /// コンボ時の攻撃処理
    /// </summary>
    public bool OnComboApplyAttack()
    {
        comboCount++;
        int attackdamege = 0;
        if (comboCount != 0)
            attackdamege += comboCount*selectedSkill.ComboDamage;
        var enemy = selectedEnemy;
        var enemysurvival = ApplyAttack(enemy, selectedSkill, attackdamege);
        return enemysurvival;
        //selectedCharacter.mp -= selectedSkill.mpCost;
    }

    /// <summary>
    /// コンボ攻撃後の処理
    /// </summary>
    private void OnComboEnd()
    {
        //攻撃後バフの設定
        if (selectedCharacter.skills.Length > 0)
        {
            SetBuff();
        }
        else
        {
            selectedCharacter.StatusFlag = StatusFlag.End;
            isActionPending = true;
        }
    }

    /// <summary>
    /// 攻撃対象選択時のコールバック
    /// </summary>
    private void OnHealSelected(List<Character> characters, int index)
    {
        Debug.Log(selectedCharacter == null ? "selectedCharacter NULL" : "selectedCharacter OK");
        Debug.Log(selectedSkill == null ? "selectedSkill NULL" : "selectedSkill OK");
        Debug.Log(characters == null ? "characters NULL" : "characters OK");
        Debug.Log(seSource == null ? "seSource NULL" : "seSource OK");

        // 対象の範囲内かを確認
        if (index < 0 || index >= characters.Count)
        {
            selectedCharacter.StatusFlag = StatusFlag.Heal;
            isActionPending = true;
            return;
        }
        // MPが存在するかを確認
        if (selectedCharacter.mp < selectedSkill.mpCost)
        {
            selectedCharacter.StatusFlag = StatusFlag.Select;
            isActionPending = true;
            return;
        }
        //SEならす
        if (selectedSkill.soundEffect != null)
        {
            seSource.PlayOneShot(selectedSkill.soundEffect);
        }

        //すべての味方を対象
        if (selectedSkill.targetScope == TargetScope.All)
        {
            //全の回復スキルの処理
            foreach (var getCharacter in characters)
            {
                ApplyHeal(getCharacter, selectedSkill);
            }
            selectedCharacter.mp -= selectedSkill.mpCost;
            selectedCharacter.StatusFlag = StatusFlag.End;
            isActionPending = true;
            return;
        }
        //通常スキルの処理  
        var character = characters[index];
        ApplyHeal(character, selectedSkill);
        selectedCharacter.mp -= selectedSkill.mpCost;
        selectedCharacter.StatusFlag = StatusFlag.End;
        isActionPending = true;
        //ヒール後にバフが必要かを確認
        //攻撃後バフの設定
        //if (selectedCharacter.skills.Length > 0)
        //{
        //    SetBuff();
        //}else
        //{
        //    selectedCharacter.StatusFlag = StatusFlag.End;
        //    isActionPending = true;
        //}

    }

    /// <summary>
    /// バフセレクト後の処理
    /// </summary>
    public void OnBuffSelected(List<Character> characters, int index,BuffBase buffBase)
    {
        if (characters == null)
        {
            if(buffBase.buffRange != BuffRange.AllAllies && buffBase.buffRange != BuffRange.AllEnemies)
            {
                Debug.LogWarning("バフの対象キャラクターが指定されていません。BuffRangeがAllAlliesまたはAllEnemiesの場合、charactersはnullであるべきです。");
                selectedCharacter.StatusFlag = StatusFlag.End;
                isActionPending = true;
                return;
            }
        } else if (index < 0 || index >= characters.Count)
        {
            selectedCharacter.StatusFlag = StatusFlag.Buff;
            isActionPending = true;
            return;
        }
        
        // MP消費処理
        if (selectedSkill != null)
        {
            if (selectedCharacter.mp >= selectedSkill.mpCost)
            {
                selectedCharacter.mp -= selectedSkill.mpCost;
                Debug.Log($"[PlayerManager] MP消費: {selectedCharacter.charactername} -{selectedSkill.mpCost}MP (残り: {selectedCharacter.mp})");
            }
            else
            {
                Debug.LogWarning($"[PlayerManager] MP不足: {selectedCharacter.charactername}");
                selectedCharacter.StatusFlag = StatusFlag.End;
                isActionPending = true;
                return;
            }
        }
        //SEならす
        if (selectedSkill.soundEffect != null)
        {
            seSource.PlayOneShot(selectedSkill.soundEffect);
        }

        //通常スキルの処理
        Character character = null;
        if (characters != null && index >= 0 && index < characters.Count)
        {
            character = characters[index];
        }
        
        BuffInstance buff = new BuffInstance(buffBase);
        buff.baseData.sourceCharacter = selectedCharacter;
        buff.remainingTurns = buffBase.duration;
        buffApply(buff, character);

        selectedCharacter.StatusFlag = StatusFlag.End;
        isActionPending = true;
    }

    #endregion

    #region 行動処理の実装
    
    /// <summary>
    /// 連撃ダメージを順次表示するコルーチン
    /// </summary>
    private IEnumerator ShowRengekiDamage(Character enemy, int damage, int hitCount)
    {
        for (int i = 1; i < hitCount; i++)
        {
            // 各ヒットの間に待機（連撃感を演出）
            
            yield return new WaitForSeconds(0.15f);
            if (enemy != null && enemy.CharacterObj != null && DamageEffectUI.Instance != null)
            {
                Debug.Log($"連撃ダメージ処理 {i}/{hitCount}");
                DamageEffectUI.Instance.ShowDamageEffectOnEnemy(enemy.CharacterObj, damage);
                
                // HPを減少
                enemy.hp -= damage;
                if (enemy.hp < 0) enemy.hp = 0;
            }
        }
    }
    
    /// <summary>
    /// 攻撃処理（ダメージ計算・撃破処理）
    /// true;敵が残っている、false:敵が撃破された
    /// </summary>
    private bool ApplyAttack(Character enemy, SkillData skill, int Attackbuff = 0)
    {
        if (enemy == null || skill == null) return true; // nullチェック追加
        // 攻撃アニメーション再生
        playerSideAnimator = selectedCharacter.PlayerAnimator;
        if (playerSideAnimator != null)
        {
            playerSideAnimator.SetTrigger("Attack");
        }

        //アニメーションが流れるのを待つ
        new WaitForSeconds(2.5f);

        // バフ適用後の攻撃力と防御力を取得
        int effectiveAtk = selectedCharacter.GetEffectiveAttack(skill.isIntSansyou);
        int effectiveDef = enemy.GetEffectiveDefense();

        float random = 0;
        var finalDamage = 0;

            finalDamage = (int)(effectiveAtk - effectiveDef);

        Debug.Log(skill.skillName + "のatk - def計算ダメージ★" + finalDamage);

        //スキルがダメージボーナスを持つ場合
        if (skill.DamageBonusFlg == true)
        {
            //ダメージ乱数
            random = UnityEngine.Random.Range(1, effectiveAtk + 1);
            random = random / 10;
            Debug.Log("乱数:" + random);
            //基本ダメージ計算（バフ適用後の攻撃力を使用）
            var damage = effectiveAtk * random;
            //最終計算（バフ適用後の防御力を使用）
           finalDamage = (int)(damage - effectiveDef);

            Debug.Log(skill.skillName + "のDB後最終ダメージ★" + finalDamage);
        }

        Debug.Log(skill.skillName + "の最終ダメージ★" + finalDamage);
              
        var hp = enemy.hp - finalDamage;
        enemy.hp = (int)math.floor(hp);
        
        // ダメージエフェクトを表示（敵の位置の前に表示）
        if (DamageEffectUI.Instance != null && enemy.CharacterObj != null)
        {
            DamageEffectUI.Instance.ShowDamageEffectOnEnemy(enemy.CharacterObj, finalDamage);
            
            // スキルのVFXを発火
            DamageEffectUI.Instance.PlaySkillVFX(skill, enemy.CharacterObj);
        }

        //SEならす
        if (selectedSkill.soundEffect != null)
        {
            Debug.Log(selectedSkill);
            Debug.Log(selectedSkill?.soundEffect);
            seSource.PlayOneShot(selectedSkill.soundEffect);
        }

        // 連撃処理（コルーチンで遅延表示）
        if (skill.rengeki == true)
        {
            Debug.Log("連撃ダメージ開始:連撃カウント:" + skill.rengekiCount);
            StartCoroutine(ShowRengekiDamage(enemy, finalDamage, skill.rengekiCount));
        }
        
        // ダメージ適用後、ボスイベントをチェック
        if (enemyManager != null)
        {
            Debug.Log("ボスイベントをチェックします");
            enemyManager.CheckBossEvents();
        }
        else
        {
            Debug.Log("enemyManagerがnullなのでチェックできません");
        }

        // 攻撃アニメーション再生
        enemyAnimator = enemy.EnemyAnimator;

        if (enemyAnimator != null)
            enemyAnimator.SetTrigger("Diffence");

        //アニメーションが流れるのを待つ
        new WaitForSeconds(0.6f);

        // 攻撃後自己回復
        if(skill.atkAftHeal == true && skill.wariai > 0)
        {
            // 与えたダメージの一定割合を回復量として計算
            int healAmount = Mathf.RoundToInt(finalDamage / skill.wariai);
            skill.wariaiHeal = healAmount;
            
            if (selectedCharacter != null && healAmount > 0)
            {
                int beforeHp = selectedCharacter.hp;
                selectedCharacter.hp += healAmount;
                
                // 最大HPを超えないように制限
                if (selectedCharacter.hp > selectedCharacter.maxHp)
                {
                    selectedCharacter.hp = selectedCharacter.maxHp;
                }
                
                int actualHeal = selectedCharacter.hp - beforeHp;
                Debug.Log($"[吸血] {selectedCharacter.charactername} が {actualHeal}HP 回復！");
                
                // 回復エフェクトを表示
                if (VFXManager.Instance != null && selectedCharacter.CharacterObj != null)
                {
                    VFXManager.Instance.PlayHealEffect(selectedCharacter.CharacterObj);
                }
                
                // 回復テキストを表示
                if (DamageEffectUI.Instance != null && selectedCharacter.CharacterObj != null)
                {
                    DamageEffectUI.Instance.ShowHealEffectOnCharacter(selectedCharacter.CharacterObj, actualHeal);
                }
            }
        }

        if (enemy.hp <= 0)
        {
            // エネミーが撃破された処理（成功時）
            //エネミーの体力を0にする
            enemy.hp = 0;
            turnManager.enemys.Remove(enemy.gameObject);
            turnManager.turnList.Remove(enemy.gameObject);
            
            //エネミーのGameObjectを削除
            Destroy(enemy.CharacterObj);

            //経験値を付与する
            foreach (var playerChar in GameManager.Instance.PlayerData)
            {
                GameManager.Instance.AddExperience(playerChar, enemy.exp); 
            }
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// 回復処理
    /// </summary>
    private void ApplyHeal(Character character, SkillData skill)
    {
        if (character == null || skill == null) return; // nullチェック追加
        
        int beforeHp = character.hp;
        var hp = 0;
        
        //スキルがInt値を参照するならば
        if (skill.isIntSansyou)
        {
            hp = (int)(character.hp + character.Int);
        }
        else
        {
            hp = (int)(character.hp + skill.power);
        }
        
        character.hp = (int)math.floor(hp);
        if (character.hp > character.maxHp)
        {
            character.hp = character.maxHp;
        }
        
        // 実際の回復量を計算
        int actualHealAmount = character.hp - beforeHp;
        
        // 回復エフェクトを再生
        if (VFXManager.Instance != null && character.CharacterObj != null)
        {
            VFXManager.Instance.PlayHealEffect(character.CharacterObj);
        }
        
        // 回復テキストを表示
        if (DamageEffectUI.Instance != null && character.CharacterObj != null && actualHealAmount > 0)
        {
            DamageEffectUI.Instance.ShowHealEffectOnCharacter(character.CharacterObj, actualHealAmount);
            
            // スキルのVFXを発火
            DamageEffectUI.Instance.PlaySkillVFX(skill, character.CharacterObj);
        }
    }

    /// <summary>
    /// バフ効果の適用
    /// 各キャラクターのCharacterBuffManagerに委譲
    /// </summary>
    private void buffApply(BuffInstance buff, Character target)
    {
        // || buff.baseData.duration==0
        if (buff == null)
        {
            Debug.LogWarning("バフ適用失敗: バフインスタンスがnullですまたは、そのターンのみの処理です。");
            return;
        }

        // バフスキルかどうかをチェック（effectTypeがBuffの場合のみエフェクト表示）
        bool isBuffSkill = selectedSkill != null && selectedSkill.effectType == SkillEffectType.Buff;
        
        switch (buff.buffRange)
        {
            case BuffRange.Self:
                target = selectedCharacter;
                if (target != null)
                {
                    target.ApplyBuff(buff, selectedCharacter);
                    if (isBuffSkill) PlayBuffVFX(buff, target);
                }
                break;
            case BuffRange.Ally:
            case BuffRange.Enemy:
                //単一の選択対象(この場合はtargetに対応)
                if (target != null)
                {
                    target.ApplyBuff(buff,target);
                    if (isBuffSkill) PlayBuffVFX(buff, target);
                }
                break;
            case BuffRange.AllAllies:
                var players = getPlayer();
                foreach (var player in players)
                {
                    if (player != null)
                    {
                        if (!buff.baseData.isSelfTarget)
                            if (player == selectedCharacter)
                                continue;
                        // 各プレイヤー用に新しいバフインスタンスを作成
                        BuffInstance playerBuff = new BuffInstance(buff.baseData);
                        playerBuff.remainingTurns = buff.remainingTurns;
                        player.ApplyBuff(playerBuff, player);
                        if (isBuffSkill) PlayBuffVFX(buff, player);
                    }
                }
                break;
            case BuffRange.AllEnemies:
                var enemies = getEnemy();
                foreach (var enemy in enemies)
                {
                    if (enemy != null)
                    {
                        // 各敵用に新しいバフインスタンスを作成
                        BuffInstance enemyBuff = new BuffInstance(buff.baseData);
                        enemyBuff.remainingTurns = buff.remainingTurns;
                        enemy.ApplyBuff(enemyBuff, enemy);
                        if (isBuffSkill) PlayBuffVFX(buff, enemy);
                    }
                }
                break;
        }
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
                VFXManager.Instance.PlayBuffEffect(target.CharacterObj);
                break;
                
            case StatusEffect.Poison:
            case StatusEffect.Burn:
                VFXManager.Instance.PlayPoisonEffect(target.CharacterObj);
                break;
                
            case StatusEffect.SpdDown:
            case StatusEffect.MagicDamageDown:
                VFXManager.Instance.PlayDebuffEffect(target.CharacterObj);
                break;
                
            case StatusEffect.MPRecovery:
                VFXManager.Instance.PlayHealEffect(target.CharacterObj);
                break;
                
            default:
                // デフォルトはバフエフェクト
                VFXManager.Instance.PlayBuffEffect(target.CharacterObj);
                break;
        }
    }
    
    /// <summary>
    /// バフの効果ターン管理
    /// 全てのキャラクターのバフを更新
    /// </summary>
    private void buffTurnManage()
    {
        // プレイヤーキャラクターのバフを更新
        var players = getPlayer();
        foreach (var player in players)
        {
            if (player != null)
            {
                player.TickBuffTurn();
            }
        }
        
        // 敵キャラクターのバフを更新
        var enemies = getEnemy();
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.TickBuffTurn();
            }
        }
    }

    /// <summary>
    /// バフをセットする
    /// </summary>
    private void SetBuff()
    {
        if (selectedSkill.buffEffect.Count > 0)
        {
            List<BuffBase> buffBase = selectedSkill.buffEffect;

            foreach (var buff in buffBase)
            {
                if (buff.isSelfTarget)
                {
                    OnBuffSelected(new List<Character>() { selectedCharacter }, 0, buff);
                    continue;
                }
                // Heal対象選択パネル対象選択パネル
                switch (buff.buffRange)
                {
                    case BuffRange.Self:
                        OnBuffSelected(new List<Character>() { selectedCharacter }, 0, buff);
                        break;
                    case BuffRange.AllAllies:
                    case BuffRange.AllEnemies:
                        OnBuffSelected(null, 0, buff);
                        break;
                    case BuffRange.Ally:
                        List<Character> buffcharacters = getPlayer();
                        var buffEvent = new UnityEvent<int>();
                        buffEvent.AddListener((index) => OnBuffSelected(buffcharacters, index, buff));
                        uiTest.Inputs(buffEvent, buffcharacters.Count - 1, buffcharacters);
                        break;
                    case BuffRange.Enemy:
                        if (buff.isSelfTarget)
                        {
                            OnBuffSelected(null, 0, buff);
                            break;
                        }
                        List<Character> buffenemies = getEnemy();
                        var buffEvents = new UnityEvent<int>();
                        buffEvents.AddListener((index) => OnBuffSelected(buffenemies, index, buff));
                        uiTest.Inputs(buffEvents, buffenemies.Count - 1, buffenemies);
                        break;
                }
            }
        }
        else
        {
            Debug.Log("バフ効果がありません");
            selectedCharacter.StatusFlag = StatusFlag.End;
            isActionPending = true;
            return;
        }
    }
    /// <summary>
    /// 攻撃対象選択パネルの敵キャラクター取得
    /// </summary>
    private List<Character> getEnemy()
    {
        // 攻撃対象選択パネル
        List<Character> enemies = new List<Character>();
        foreach (var enemyObj in turnManager.enemys)
        {
            var characterData = enemyObj.GetComponent<Character>();
            if (characterData != null)
            {
                enemies.Add(characterData);
            }
        }
        return enemies;
    }

    /// <summary>
    /// 攻撃対象選択パネルの味方キャラクター取得
    /// </summary>
    private List<Character> getPlayer()
    {
        // 攻撃対象選択パネル
        List<Character> players = new List<Character>();
        foreach (var playerObj in turnManager.players)
        {
            var characterData = playerObj.GetComponent<Character>();
            if (characterData != null)
            {
                players.Add(characterData);
            }
        }
        return players;
    }
    #endregion

}
