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
/// 戦闘不能になった味方の退避データ
/// </summary>
[Serializable]
public class DefeatedAllyInfo
{
    public CharacterData characterData;
    public int panelIndex;
    public Vector3 spawnPosition;
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
    [SerializeField, Header("蘇生タイミングUI")]
    private TimingUI reviveTimingUI;
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
    // 戦闘不能になった味方
    private readonly List<DefeatedAllyInfo> defeatedAllies = new List<DefeatedAllyInfo>();

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
            SpawnPlayerCharacter(playerCharacters[i], i, spawnPositions[i]);
        }
        
        Debug.Log($"[PlayerManager] {playerCharacters.Count}人のキャラクターを戦闘に配置しました");
    }

    /// <summary>
    /// プレイヤーキャラクターを生成してUIを接続
    /// </summary>
    private GameObject SpawnPlayerCharacter(CharacterData data, int panelIndex, Vector3 position)
    {
        var obj = Instantiate(data.CharacterObj, position, Quaternion.identity);
        obj.AddComponent<Character>().init(data);
        obj.transform.parent = transform;
        characterObjects.Add(obj);
        characterToPanelIndex[obj] = panelIndex;
        SetupPlayerStatusPanel(obj, panelIndex);
        return obj;
    }

    private void SetupPlayerStatusPanel(GameObject obj, int panelIndex)
    {
        if (panelIndex < 0 || panelIndex >= playerStatusPanel.Count || playerStatusPanel[panelIndex] == null)
        {
            return;
        }

        playerStatusPanel[panelIndex].gameObject.SetActive(true);
        PlayerData playerData = new PlayerData(obj.GetComponent<Character>());
        playerStatusPanel[panelIndex].UpdatePlayerStatus(playerData);

        CharacterBuffManager buffManager = obj.GetComponent<CharacterBuffManager>();
        if (buffManager != null)
        {
            buffManager.OnBuffsChanged.AddListener(playerStatusPanel[panelIndex].UpdateBuffIcons);
            Debug.Log($"[PlayerManager] バフアイコン接続完了: {obj.GetComponent<Character>().charactername} → PlayerStatusPanel[{panelIndex}]");
        }
    }

    /// <summary>
    /// プレイヤー撃破時の共通処理（CharacterData退避 → リスト削除 → Destroy）
    /// </summary>
    public void ProcessPlayerDefeat(Character target)
    {
        if (target == null || target.enemyCheckFlag) return;

        target.hp = 0;

        int panelIndex = -1;
        if (target.CharacterObj != null && characterToPanelIndex.TryGetValue(target.CharacterObj, out int idx))
        {
            panelIndex = idx;
        }

        CharacterData data = target.GetCharacterData();
        Vector3 spawnPosition = panelIndex >= 0 && panelIndex < spawnPositions.Count
            ? spawnPositions[panelIndex]
            : target.CharacterTransfrom;

        if (data != null)
        {
            data.hp = 0;
            defeatedAllies.Add(new DefeatedAllyInfo
            {
                characterData = data,
                panelIndex = panelIndex,
                spawnPosition = spawnPosition
            });
        }

        if (target.CharacterObj != null)
        {
            characterToPanelIndex.Remove(target.CharacterObj);
            characterObjects.Remove(target.CharacterObj);
        }

        if (panelIndex >= 0 && panelIndex < playerStatusPanel.Count && playerStatusPanel[panelIndex] != null)
        {
            playerStatusPanel[panelIndex].gameObject.SetActive(false);
        }

        if (turnManager != null)
        {
            if (turnManager.players.Contains(target.gameObject))
            {
                turnManager.players.Remove(target.gameObject);
            }
            if (turnManager.turnList.Contains(target.gameObject))
            {
                turnManager.turnList.Remove(target.gameObject);
            }
            turnManager.RemoveCharacterFromTurnList(target);
        }

        GameObject toDestroy = target.CharacterObj != null ? target.CharacterObj : target.gameObject;
        Destroy(toDestroy);

        Debug.Log($"[PlayerManager] {target.charactername} を戦闘不能として退避しました（蘇生可能: {defeatedAllies.Count}人）");
    }

    public int GetDefeatedAllyCount() => defeatedAllies.Count;

    
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
             selectedCharacter.StatusFlag == StatusFlag.Buff ||
             selectedCharacter.StatusFlag == StatusFlag.Revive))
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
            case StatusFlag.Revive:
                PlayerReviveSelect();
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

        if (GetFlagAllAttack(selectedSkill))
        {
            OnAttackSelected(getEnemy(), 0);
            return;
        }
        
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
    /// 蘇生対象（戦闘不能の味方）を選択するフェイズ
    /// </summary>
    private void PlayerReviveSelect()
    {
        if (defeatedAllies.Count == 0)
        {
            Debug.LogWarning("[PlayerManager] 蘇生対象の戦闘不能キャラがいません");
            selectedCharacter.StatusFlag = StatusFlag.Select;
            isActionPending = true;
            return;
        }

        skillSelectionUI.ShowSkillWindow(false);

        List<Vector3> positions = new List<Vector3>();
        foreach (var ally in defeatedAllies)
        {
            positions.Add(ally.spawnPosition);
        }

        var reviveEvent = new UnityEvent<int>();
        reviveEvent.AddListener(OnReviveSelected);
        uiTest.InputsAtPositions(reviveEvent, positions);
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
    private const float ActionEndFallbackSeconds = 1f;
    private const float ActionEndMaxWaitSeconds = 8f;
    private const float ActionEndMinWaitSeconds = 0.35f;

    private IEnumerator AttackEndTick()
    {
        yield return WaitForPlayerActionAnimationComplete();
        // 継続ダメージ・バフターン減算は TurnManager（前に出る直前）のみで処理
        // キャラクターを開始位置に戻る
        selectedCharacter.CharacterObj.transform.DOMove(StartPosition, 1f).OnComplete(() =>
        {
            selectedCharacter.GetBuffManager()?.TryExpireReloadBuffAtTurnEnd();
            selectedCharacter.StatusFlag = StatusFlag.None;
            // ターン処理を終了
            turnManager.FlagChange();
        });
    }

    /// <summary>
    /// 行動アニメーションの完了を待つ（全スキル共通・開始位置へ戻る前）
    /// </summary>
    private IEnumerator WaitForPlayerActionAnimationComplete()
    {
        Animator animator = selectedCharacter?.PlayerAnimator;
        if (animator == null || !animator.isActiveAndEnabled)
        {
            yield return new WaitForSeconds(ActionEndFallbackSeconds);
            yield break;
        }

        const int layer = 0;
        yield return null;
        yield return null;

        float elapsed = 0f;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);

        // 攻撃モーション未開始（バフ・回復のみ等で Idle ループのまま）
        if (!animator.IsInTransition(layer) && state.loop)
        {
            yield return new WaitForSeconds(ActionEndMinWaitSeconds);
            yield break;
        }

        while (elapsed < ActionEndMaxWaitSeconds)
        {
            if (animator.IsInTransition(layer))
            {
                yield return null;
                elapsed += Time.deltaTime;
                continue;
            }

            state = animator.GetCurrentAnimatorStateInfo(layer);

            if (!state.loop && state.normalizedTime >= 0.95f)
                break;

            if (state.loop && elapsed > 0.1f)
                break;

            yield return null;
            elapsed += Time.deltaTime;
        }

        while (animator.IsInTransition(layer) && elapsed < ActionEndMaxWaitSeconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
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
                if (GetFlagAllAttack(selectedSkill))
                {
                    OnAttackSelected(getEnemy(), 0);
                }
                else
                {
                    selectedCharacter.StatusFlag = StatusFlag.Attack;
                }
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
                ApplySelectedBuffSkill();
                break;
            case SkillEffectType.Revive:
                if (defeatedAllies.Count == 0)
                {
                    Debug.LogWarning("[PlayerManager] 蘇生対象がいないためスキルを使用できません");
                    if (cancelSoundEffect != null && seSource != null)
                    {
                        seSource.PlayOneShot(cancelSoundEffect);
                    }
                    selectedCharacter.StatusFlag = StatusFlag.Select;
                    isActionPending = true;
                    return;
                }
                Debug.Log("StatusFlag.Reviveに移行");
                selectedCharacter.StatusFlag = StatusFlag.Revive;
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

        bool isReloadAttack = IsReloadEnhancedNormalAttack(selectedSkill);
        

        // 全の攻撃スキルの場合、すべての敵に攻撃を適用
        if (GetFlagAllAttack(selectedSkill))
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

            ConsumeReloadBuffIfUsed(selectedSkill);
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

        //コンボ以外（リロード攻撃はバフ消費前に判定済み）
        if(!selectedSkill.canCombo || isReloadAttack)
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
        int attackBonus = 0;
        if (!IsComboBurstSkill(selectedSkill))
        {
            attackBonus = comboCount * selectedSkill.ComboDamage;
        }
        var enemy = selectedEnemy;
        return ApplyAttack(enemy, selectedSkill, attackBonus);
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
    /// 蘇生対象選択時のコールバック
    /// </summary>
    private void OnReviveSelected(int index)
    {
        if (index < 0 || index >= defeatedAllies.Count)
        {
            selectedCharacter.StatusFlag = StatusFlag.Revive;
            isActionPending = true;
            return;
        }

        if (selectedCharacter.mp < selectedSkill.mpCost)
        {
            selectedCharacter.StatusFlag = StatusFlag.Select;
            isActionPending = true;
            return;
        }

        selectedCharacter.mp -= selectedSkill.mpCost;
        StartCoroutine(ReviveSequence(defeatedAllies[index]));
    }

    private IEnumerator ReviveSequence(DefeatedAllyInfo targetInfo)
    {
        bool perfectTiming = false;

        if (reviveTimingUI != null)
        {
            reviveTimingUI.Show(selectedSkill.timingWindowStart, selectedSkill.timingWindowEnd);
            float maxWait = selectedSkill.timingWindowStart + selectedSkill.timingWindowEnd + 2f;
            float timer = 0f;
            bool inputReceived = false;

            while (!inputReceived && timer < maxWait)
            {
                if (reviveTimingUI.IsTimingSuccess())
                {
                    perfectTiming = reviveTimingUI.CheckTimingSuccess();
                    inputReceived = true;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            reviveTimingUI.Hide();
        }

        ReviveAlly(targetInfo, perfectTiming);

        if (selectedSkill != null && selectedSkill.soundEffect != null && seSource != null)
        {
            seSource.PlayOneShot(selectedSkill.soundEffect);
        }

        selectedCharacter.StatusFlag = StatusFlag.End;
        isActionPending = true;
    }

    /// <summary>
    /// 戦闘不能の味方を蘇生する
    /// </summary>
    private void ReviveAlly(DefeatedAllyInfo info, bool perfectTiming)
    {
        if (info == null || info.characterData == null)
        {
            Debug.LogWarning("[PlayerManager] 蘇生対象データが無効です");
            return;
        }

        float hpRatio = perfectTiming ? 1f : (selectedSkill != null && selectedSkill.power > 0f ? selectedSkill.power : 0.5f);
        int reviveHp = Mathf.Max(1, Mathf.RoundToInt(info.characterData.maxHp * hpRatio));
        info.characterData.hp = reviveHp;

        GameObject obj = SpawnPlayerCharacter(info.characterData, info.panelIndex, info.spawnPosition);
        Character revivedCharacter = obj.GetComponent<Character>();
        revivedCharacter.hp = reviveHp;

        if (turnManager != null)
        {
            turnManager.RegisterRevivedPlayer(obj);
        }

        defeatedAllies.Remove(info);

        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.PlayHealEffect(obj);
        }
        if (DamageEffectUI.Instance != null)
        {
            DamageEffectUI.Instance.ShowHealEffectOnCharacter(obj, reviveHp);
            if (selectedSkill != null)
            {
                DamageEffectUI.Instance.PlaySkillVFX(selectedSkill, obj);
            }
        }

        Debug.Log($"[PlayerManager] {revivedCharacter.charactername} を蘇生しました HP={reviveHp}/{revivedCharacter.maxHp} (ベスト:{perfectTiming})");
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

    #region リロードバフ

    private bool IsReloadEnhancedNormalAttack(SkillData skill)
    {
        if (skill == null || skill.skillName != ReloadBuff.NormalAttackSkillName)
        {
            return false;
        }

        if (selectedCharacter == null)
        {
            return false;
        }

        var buffManager = selectedCharacter.GetBuffManager();
        return buffManager != null && buffManager.HasReloadBuff();
    }

    private bool GetFlagAllAttack(SkillData skill)
    {
        if (skill == null || selectedCharacter == null)
        {
            return false;
        }

        return skill.targetScope == TargetScope.All
            || selectedCharacter.AllAttack
            || IsReloadEnhancedNormalAttack(skill);
    }

    private void ConsumeReloadBuffIfUsed(SkillData skill)
    {
        if (!IsReloadEnhancedNormalAttack(skill))
        {
            return;
        }

        selectedCharacter.GetBuffManager()?.ConsumeReloadBuff();
    }

    private void ApplySelectedBuffSkill()
    {
        if (selectedSkill == null || selectedSkill.buffEffect == null || selectedSkill.buffEffect.Count == 0)
        {
            return;
        }

        BuffBase buff = selectedSkill.buffEffect[0];
        if (buff == null)
        {
            return;
        }

        if (buff.buffRange == BuffRange.Self || buff.isSelfTarget)
        {
            OnBuffSelected(new List<Character>() { selectedCharacter }, 0, buff);
            return;
        }

        OnBuffSelected(getPlayer(), 0, buff);
    }

    #endregion

    #region コンボバーストスキル（グレネードガン・ビッグバン）

    private const string GrenadeGunSkillName = "グレネードガン";
    private const string BigBangSkillName = "ビッグバン";

    private bool IsComboBurstSkill(SkillData skill)
    {
        if (skill == null) return false;
        return skill.skillName == GrenadeGunSkillName || skill.skillName == BigBangSkillName;
    }

    /// <summary>
    /// (基礎値 + DB) × 1.0～2.0 × コンボ補正(1ヒット目100%、以降+10%) − 防御
    /// </summary>
    private int CalculateComboBurstDamage(SkillData skill, Character enemy, int comboHitCount)
    {
        if (selectedCharacter == null || enemy == null || skill == null) return 0;
        if (enemy.IsInvincible()) return 0;

        int basePower = (int)skill.power;
        int intStat = Mathf.Max(1, selectedCharacter.Int);
        int db = UnityEngine.Random.Range(1, intStat + 1);
        float rate = UnityEngine.Random.Range(1.0f, 2.0f);
        float comboMultiplier = 1f + 0.1f * (Mathf.Max(1, comboHitCount) - 1);

        int effectiveDef = enemy.GetEffectiveDefense();
        float raw = (basePower + db) * rate * comboMultiplier;
        return Mathf.Max(0, Mathf.RoundToInt(raw - effectiveDef));
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
            yield return new WaitForSeconds(0.15f);

            if (enemy == null || enemy.hp <= 0)
                yield break;

            if (enemy.CharacterObj != null && DamageEffectUI.Instance != null)
            {
                Debug.Log($"連撃ダメージ処理 {i}/{hitCount}");
                int appliedDamage = enemy.TakeDamage(damage);
                DamageEffectUI.Instance.ShowDamageEffectOnEnemy(enemy.CharacterObj, appliedDamage);

                if (enemy.hp <= 0)
                {
                    ProcessEnemyDefeat(enemy);
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// 敵撃破時の共通処理（リスト削除・UI更新・Destroy）
    /// </summary>
    private void ProcessEnemyDefeat(Character enemy)
    {
        if (enemy == null || enemy.hp > 0) return;
        if (turnManager != null && !turnManager.enemys.Contains(enemy.gameObject)) return;

        enemy.hp = 0;
        Debug.Log($"[PlayerManager] {enemy.charactername} を撃破しました");
        turnManager.enemys.Remove(enemy.gameObject);
        turnManager.turnList.Remove(enemy.gameObject);
        turnManager.RemoveCharacterFromTurnList(enemy);

        Destroy(enemy.CharacterObj);

        if (GameManager.Instance != null)
        {
            foreach (var playerChar in GameManager.Instance.PlayerData)
            {
                GameManager.Instance.AddExperience(playerChar, enemy.exp);
            }
        }

        // コンボ中に撃破した場合はタイミング UI 付きコンボを終了
        if (comboUI != null && selectedEnemy == enemy)
        {
            comboUI.RequestEndCombo();
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

        int effectiveDef = enemy.GetEffectiveDefense();
        var finalDamage = 0;

        if (IsComboBurstSkill(skill))
        {
            finalDamage = CalculateComboBurstDamage(skill, enemy, comboCount);
            Debug.Log($"{skill.skillName} コンボ{comboCount}発目: バーストダメージ={finalDamage}");
        }
        else
        {
            // バフ適用後の攻撃力と防御力を取得
            int effectiveAtk = selectedCharacter.GetEffectiveAttack(skill.isIntSansyou);

            finalDamage = (int)(effectiveAtk - effectiveDef);

            Debug.Log(skill.skillName + "のatk - def計算ダメージ★" + finalDamage);

            //スキルがダメージボーナスを持つ場合
            if (skill.DamageBonusFlg == true)
            {
                //ダメージ乱数
                float random = UnityEngine.Random.Range(1, effectiveAtk + 1);
                random = random / 10;
                Debug.Log("乱数:" + random);
                //基本ダメージ計算（バフ適用後の攻撃力を使用）
                var damage = effectiveAtk * random;
                //最終計算（バフ適用後の防御力を使用）
                finalDamage = (int)(damage - effectiveDef);

                Debug.Log(skill.skillName + "のDB後最終ダメージ★" + finalDamage);
            }

            if (Attackbuff > 0)
            {
                finalDamage += Attackbuff;
            }
        }

        if (IsReloadEnhancedNormalAttack(skill))
        {
            var reloadBuff = selectedCharacter.GetBuffManager()?.GetReloadBuffInstance()?.baseData as ReloadBuff;
            if (reloadBuff != null)
            {
                finalDamage = Mathf.Max(0, Mathf.RoundToInt(finalDamage * reloadBuff.damageMultiplier));
                Debug.Log($"[Reload] 最終ダメージ補正後: {finalDamage}");
            }
        }

        finalDamage = Mathf.Max(0, finalDamage);
        int appliedDamage = enemy.TakeDamage(finalDamage);
        Debug.Log(skill.skillName + "の最終ダメージ★" + appliedDamage);
        
        // ダメージエフェクトを表示（敵の位置の前に表示）
        if (DamageEffectUI.Instance != null && enemy.CharacterObj != null)
        {
            DamageEffectUI.Instance.ShowDamageEffectOnEnemy(enemy.CharacterObj, appliedDamage);
            
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

        // 連撃処理（生存時のみ。撃破済みなら追加ヒット不要）
        if (skill.rengeki == true && enemy.hp > 0)
        {
            Debug.Log("連撃ダメージ開始:連撃カウント:" + skill.rengekiCount);
            StartCoroutine(ShowRengekiDamage(enemy, appliedDamage, skill.rengekiCount));
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
            int healAmount = Mathf.RoundToInt(appliedDamage / skill.wariai);
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
            ProcessEnemyDefeat(enemy);
            return false;
        }

        return true;
    }

    /// <summary>
    /// プレイヤー側ヒールの回復量を算出（蘇生は対象外）
    /// 基本値 × 使用者のLv
    /// </summary>
    private int CalculatePlayerHealAmount(Character target, SkillData skill)
    {
        int baseHeal = skill.isIntSansyou ? target.Int : (int)skill.power;
        int casterLevel = selectedCharacter != null ? Mathf.Max(1, selectedCharacter.level) : 1;
        return baseHeal * casterLevel;
    }

    /// <summary>
    /// 回復処理
    /// </summary>
    private void ApplyHeal(Character character, SkillData skill)
    {
        if (character == null || skill == null) return; // nullチェック追加
        
        int beforeHp = character.hp;
        int healAmount = CalculatePlayerHealAmount(character, skill);
        int hp = character.hp + healAmount;
        
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
                    if (isBuffSkill) PlayBuffSkillVFX(selectedSkill, target, buff);
                }
                break;
            case BuffRange.Ally:
            case BuffRange.Enemy:
                //単一の選択対象(この場合はtargetに対応)
                if (target != null)
                {
                    target.ApplyBuff(buff,target);
                    if (isBuffSkill) PlayBuffSkillVFX(selectedSkill, target, buff);
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
                        if (isBuffSkill) PlayBuffSkillVFX(selectedSkill, player, buff);
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
                        if (isBuffSkill) PlayBuffSkillVFX(selectedSkill, enemy, buff);
                    }
                }
                break;
        }
    }
    
    /// <summary>
    /// バフスキル用VFX。SkillData.vfxPrefab があればそれを優先、なければバフ種別のデフォルト。
    /// </summary>
    private void PlayBuffSkillVFX(SkillData skill, Character target, BuffInstance buff)
    {
        if (target == null || target.CharacterObj == null) return;

        if (skill != null && skill.vfxPrefab != null && DamageEffectUI.Instance != null)
        {
            DamageEffectUI.Instance.PlaySkillVFX(skill, target.CharacterObj);
            return;
        }

        if (buff != null)
        {
            PlayBuffVFX(buff, target);
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

            case StatusEffect.None:
                // VFX なし（バフ効果のみ）
                break;
                
            default:
                // デフォルトはバフエフェクト
                VFXManager.Instance.PlayBuffEffect(target.CharacterObj);
                break;
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
