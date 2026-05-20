using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class TurnManager : MonoBehaviour
{
    #region 変数宣言
    [SerializeField, Header("プレイヤーマネージャー")]
    private PlayerManager playerManager;
    [SerializeField, Header("エネミーマネージャー")]
    private EnemyManager enemyManager;
    [SerializeField, Header("プレイヤーのデータ")]
    public List<GameObject> players;
    [SerializeField, Header("エネミーのデータ")]
    public List<GameObject> enemys;
    [SerializeField, Header("ターン順リスト")]
    public List<GameObject> turnList = new List<GameObject>();// プレイヤーとエネミーをまとめたリスト
    [SerializeField]
    private List<GameObject> sortedTurnList = new List<GameObject>();// SPD順にソートされたリスト
    [SerializeField]
    private List<GameObject> nextTurnList = new List<GameObject>();// 次のターン用リスト
    [SerializeField, Header("ResultWinキャンバス")]
    public GameObject ResultWinCanvas; // 勝利時キャンバス
    
    [SerializeField, Header("勝利演出")]
    public UnityEngine.UI.Image victoryCharacterImage; // Canvas/Panel/Image（立ち絵表示用）
    
    //現在のターンオブジェクト
    public GameObject currentTurnObject;
    private bool turnChangeFlag = false; // ターン順変更フラグ
    private int turnNumber = 0; // 現在のターン数
    private bool turnFlag; // ターン開始していいかどうかのフラグ
    
    [Header("戦闘中イベント")]
    private bool battlePaused = false; // 戦闘一時停止フラグ
    
    [Header("最後に行動したキャラ")]
    private Character lastActionCharacter = null; // 最後に行動したプレイヤーキャラ

    //シングルトンパターン
    private static TurnManager instance;
    public static TurnManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<TurnManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("TurnManager");
                    instance = obj.AddComponent<TurnManager>();
                }
            }
            return instance;
        }
    }
    private void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion
    
    #region 初期化
    private void Start()
    {

        // 変数の初期化
        turnFlag = true;
        turnNumber = 0;
        turnChangeFlag = false;
        battlePaused = false;

        // 初期化
        Initialization();
    }

    // 初期化処理
    private void Initialization()
    {
        // 戦闘開始時にスナップショットを保存（まだ保存されていない場合）
        if (GameManager.Instance != null)
        {
            // スナップショットリストが空なら保存
            if (GameManager.Instance.preBattleSnapshots == null || GameManager.Instance.preBattleSnapshots.Count == 0)
            {
                Debug.Log("[TurnManager] 戦闘開始時にスナップショットを保存します");
                GameManager.Instance.SavePreBattleSnapshots();
            }
        }
        
        // プレイヤーを取得
        players = playerManager.GetPlayerCharacters();
        // エネミーを取得
        enemys = enemyManager.GetEnemyData();

        // パーティーメンバーが0人の場合はエラー
        if (players == null || players.Count == 0)
        {
            Debug.LogError("[TurnManager] パーティーメンバーが0人です。戦闘を開始できません。");
            // 戦闘を終了してフィールドに戻る
            if (GameManager.Instance != null)
            {
                StartCoroutine(ReturnToFieldAfterError());
            }
            return;
        }
        
        // 敵がいない場合もエラー
        if (enemys == null || enemys.Count == 0)
        {
            Debug.LogError("[TurnManager] 敵が0体です。戦闘を開始できません。");
            if (GameManager.Instance != null)
            {
                StartCoroutine(ReturnToFieldAfterError());
            }
            return;
        }

        // プレイヤーとエネミーをまとめてSPD順に並び替える
        turnList.Clear();
        turnList.AddRange(players);
        turnList.AddRange(enemys);
        // SPD順にソート
        turnList.Sort((a, b) => {
            var charA = a.GetComponent<Character>();
            var charB = b.GetComponent<Character>();

            // バフマネージャーから有効なSPDを取得
            int spdA = charA.GetBuffManager()?.GetEffectiveSpeed() ?? charA.spd;
            int spdB = charB.GetBuffManager()?.GetEffectiveSpeed() ?? charB.spd;

            return spdB.CompareTo(spdA); // SPD降順でソート
        });
        nextTurnList = new List<GameObject>(turnList);
        sortedTurnList = new List<GameObject>(turnList);
        // Spd 降順でソート（降順）
        //List<GameObject> sorted = turnList.OrderByDescending(c => c.GetComponent<Character>().Spd).ToList();
        // UIに設定
        // UIに現在のターン順の状態を表示する
        UIManager.Instance.UpdateTurnUI(sortedTurnList, turnNumber);
        // 状態のデータをUIに渡す
        
        // ターン処理をスタート（コルーチンで非同期実行）
        StartCoroutine(InitializeBattle());
    }

    /// <summary>
    /// 戦闘開始処理（HP100%イベント対応）
    /// </summary>
    private IEnumerator InitializeBattle()
    {
        // ボスイベントをチェック（戦闘開始時、HP100%のイベント用）
        if (enemyManager != null)
        {
            enemyManager.CheckBossEvents();
        }
        
        // 戦闘が一時停止中の場合は、再開されるまで待機
        while (battlePaused)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // ターン処理を開始
        StartCoroutine(TurnController());
    }

    // ターン管理
    // この処理はUpdateで実行する必要がある....
    private IEnumerator TurnController()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);
            // 次の処理を待つ
            if (turnFlag)
            {
                //ターン開始
                if (players.Count == 0 || enemys.Count == 0)
                {
                    EndTurnManager();
                    yield break;
                    //break;
                }
                //Debug.Log("ターン開始:" + turnNumber);
                
                // ボスイベントをチェック（ターン開始時）
                if (enemyManager != null)
                {
                    enemyManager.CheckBossEvents();
                }
                
                // イベントで戦闘が一時停止された場合は待機
                while (battlePaused)
                {
                    yield return new WaitForSeconds(0.1f);
                }
                
                // フラグを立てる
                turnFlag = false;
                // Turnリストを取得
                var nextCharacterStatus = sortedTurnList[turnNumber];
                currentTurnObject = nextCharacterStatus;
                // Characterのステータスを変更
                if (nextCharacterStatus == null)
                {
                    //Debug.Log("ターン対象が存在しません");
                    turnFlag = true;
                    turnNumber = (turnNumber + 1) % sortedTurnList.Count;
                    continue;
                }
                
                // 行動開始前にそのキャラクターの継続ダメージ処理
                var character = nextCharacterStatus.GetComponent<Character>();
                if (character != null)
                {
                    var buffManager = character.GetBuffManager();
                    if (buffManager != null)
                    {
                        buffManager.TickTurn(); // 継続ダメージ処理
                    }
                    
                    // 継続ダメージで倒れた場合はスキップ
                    if (character.hp <= 0)
                    {
                        Debug.Log($"[TurnManager] {character.charactername} は継続ダメージで行動不能になりました");
                        turnFlag = true;
                        turnNumber++;
                        if (turnNumber < sortedTurnList.Count)
                        {
                            UIManager.Instance.NextTurn();
                        }
                        continue;
                    }
                }
                
                // True:Enemy False:Player                
                if (nextCharacterStatus.GetComponent<Character>().enemyCheckFlag)
                {
                    // Enemy処理
                    enemyManager.Test(nextCharacterStatus.GetComponent<Character>());
                    //Debug.Log("StartEnemy");
                }
                else
                {
                    // Player処理
                    Character playerCharacter = nextCharacterStatus.GetComponent<Character>();
                    playerCharacter.StatusFlag = StatusFlag.Move;
                    
                    // 最後に行動したプレイヤーキャラを記録
                    lastActionCharacter = playerCharacter;
                    
                    playerManager.StartPlayerAction(playerCharacter);
                    //Debug.Log("StartPlayer");
                }
                //現在のターンのリストから削除
                //sortedTurnList[turnNumber] = null;
                // ターンカウンター
                turnNumber++;
                if (turnNumber < sortedTurnList.Count)
                {
                    UIManager.Instance.NextTurn();
                }

            }
            else
            {
                //Debug.Log("ターン待機");
            }


        }
    }
    #endregion

    #region ターン順リスト操作(ターン処理）
    //ターン順リストの順序を変更
    public void TurnChange(Character character, int chageNum)
    {
        //ターン順リスト変更フラグを立てる
        turnChangeFlag = true;
        if (character == null)
            Debug.Log("ターン順リスト変更:対象キャラクターが存在しません");
        var changeobj = character.CharacterObj;

        Debug.Log("ターン順リスト変更:" + changeobj.name + "を" + chageNum + "番目に移動");
        var objectToMove = nextTurnList.FirstOrDefault(obj => obj == changeobj);
        if (objectToMove != null)
        {
            nextTurnList.Remove(objectToMove);
            //指定された位置に挿入
            nextTurnList.Insert(chageNum, objectToMove);
        }
    }
    //ターン順リストからキャラクターを削除
    public void RemoveCharacterFromTurnList(Character character)
    {
        var removeobj = character.CharacterObj;
        //ターン順リストから削除（全てのリストから削除する）
        if (sortedTurnList.Contains(removeobj))
        {
            sortedTurnList.Remove(removeobj);
            Debug.Log($"[TurnManager] {character.charactername} をsortedTurnListから削除");
        }
        if (nextTurnList.Contains(removeobj))
        {
            nextTurnList.Remove(removeobj);
            Debug.Log($"[TurnManager] {character.charactername} をnextTurnListから削除");
        }
    }

    //ターン開始してフラグ
    public void FlagChange()
    {
        // 戦闘が一時停止中は処理しない
        if (battlePaused)
        {
            Debug.Log("[TurnManager] 戦闘一時停止中のため、ターン進行を停止します");
            return;
        }
        
        // 全員の行動が完了したかチェック
        if (turnNumber >= sortedTurnList.Count)
        {
            // ラウンド終了処理
            turnNumber = 0;

            // 次のラウンドのターンリストを生成
            GenerateNextRoundTurnList();

            // UI更新
            UIManager.Instance.UpdateTurnUI(sortedTurnList, turnNumber);
        }

        turnFlag = true;
    }
    
    /// <summary>
    /// 戦闘を一時停止
    /// </summary>
    public void PauseBattle()
    {
        battlePaused = true;
        Debug.Log("[TurnManager] 戦闘を一時停止しました");
    }
    
    /// <summary>
    /// 戦闘を再開
    /// </summary>
    public void ResumeBattle()
    {
        battlePaused = false;
        Debug.Log("[TurnManager] 戦闘を再開しました");
        // FlagChange()は呼ばない（イベント前の状態から継続する）
    }
    
    /// <summary>
    /// 戦闘が一時停止中かどうか
    /// </summary>
    public bool IsBattlePaused()
    {
        return battlePaused;
    }

    // 新規メソッド: 次ラウンドのターンリスト生成
    private void GenerateNextRoundTurnList()
    {
        sortedTurnList.Clear();

        if (turnChangeFlag)
        {
            turnChangeFlag = false;
            // ターン順変更があった場合
            sortedTurnList.AddRange(nextTurnList);
            nextTurnList.Clear();
            nextTurnList.AddRange(turnList);
        }
        else
        {
            // 通常の場合: SPD順（バフ考慮）でソート
            sortedTurnList.AddRange(turnList);

            sortedTurnList.Sort((a, b) => {
                var charA = a.GetComponent<Character>();
                var charB = b.GetComponent<Character>();

                int spdA = charA.GetBuffManager()?.GetEffectiveSpeed() ?? charA.spd;
                int spdB = charB.GetBuffManager()?.GetEffectiveSpeed() ?? charB.spd;

                return spdB.CompareTo(spdA);
            });
        }
    }
    #endregion

    #region 勝利・敗北処理
    //勝利、敗北時に呼び出し
    public void EndTurnManager()
    {
        //敗北処理
        if (players.Count == 0)
            DefeatProcess();
        //勝利処理
        if (enemys.Count == 0)
            VictoryProcess();

        //コルーチンを停止
        StopAllCoroutines();
    }

    //敗北処理
    private void DefeatProcess()
    {
        Debug.Log("敗北処理");
        GameManager.Instance.EndBattle();
    }

    //勝利処理
    private void VictoryProcess()
    {
        Debug.Log("勝利処理");

        // 最後に行動したプレイヤーキャラの立ち絵を設定
        SetVictoryCharacterImage();

        // 敵全滅時のイベントをチェック（HP 0%イベント）
        bool hasZeroPercentEvent = false;
        if (enemyManager != null)
        {
            hasZeroPercentEvent = enemyManager.CheckEnemyDefeatedEvent(() =>
            {
                // 0%イベント終了後にResultUIと経験値処理を実行
                ShowResultAndCalculateExp();
            });
        }

        // 0%イベントがない場合は即座にResultUIを表示
        if (!hasZeroPercentEvent)
        {
            ShowResultAndCalculateExp();
        }
    }

    /// <summary>
    /// 最後に行動したキャラの立ち絵を設定
    /// </summary>
    private void SetVictoryCharacterImage()
    {
        // 最後に行動したプレイヤーキャラがいる場合のみ立ち絵を設定
        if (lastActionCharacter != null && victoryCharacterImage != null)
        {
            // キャラクターデータを取得
            CharacterData characterData = GameManager.Instance.PlayerData.Find(c => c.charactername == lastActionCharacter.charactername);
            
            if (characterData != null && characterData.standingImage != null)
            {
                Debug.Log($"[TurnManager] 勝利立ち絵設定: {characterData.charactername}");
                
                // 立ち絵を設定
                victoryCharacterImage.sprite = characterData.standingImage;
                victoryCharacterImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[TurnManager] 立ち絵が設定されていません: {lastActionCharacter.charactername}");
            }
        }
    }

    /// <summary>
    /// 勝利結果UIを表示し、経験値を計算
    /// </summary>
    private void ShowResultAndCalculateExp()
    {
        Debug.Log("勝利結果を表示します");
        
        ResultWinCanvas.SetActive(true);

        Debug.Log($"[経験値計算前] GameManager.EnemyData数: {GameManager.Instance.EnemyData.Count}");
        Debug.Log($"[経験値計算前] enemyManager.enemyData数: {enemyManager.enemyData.Count}");
        
        GameManager.Instance.EnemyData.AddRange(enemyManager.enemyData);
        
        Debug.Log($"[経験値計算後] GameManager.EnemyData数: {GameManager.Instance.EnemyData.Count}");

        // 倒した敵を記録し、経験値を計算
        int totalExp = 0;
        Debug.Log($"=== 経験値計算開始 ===");
        //if (GameManager.Instance != null && GameManager.Instance.EnemyData != null)
        //{
        foreach (var enemyData in GameManager.Instance.EnemyData)
        {
            Debug.Log($"倒した敵の確認: {enemyData.charactername}");
            if (enemyData != null)
            {
                GameManager.Instance.RecordEnemyDefeat(enemyData);
                //Debug.Log($"敵を倒した記録: {enemyData.charactername}");
                // 敵の経験値を合計
                int enemyExp = CalculateEnemyExp(enemyData);
                totalExp += enemyExp;
                Debug.Log($"  {enemyData.charactername}: {enemyExp}EXP (累計: {totalExp}EXP)");
            }
        }
        //}
        Debug.Log($"★ 総獲得経験値: {totalExp}");
        // プレイヤーに経験値を配布
        if (totalExp > 0)
        {
            DistributeExperienceToPlayers(totalExp);
        }

        //遷移処理はResultWin.csへ移行

        //GameManager.Instance.EnemyDataClear();  
    }

    /// <summary>
    /// 敵から獲得できる経験値を計算
    /// </summary>
    /// <param name="enemyData">敵のデータ</param>
    /// <returns>獲得経験値</returns>
    private int CalculateEnemyExp(CharacterData enemyData)
    {
        if (enemyData == null) return 0;
        int baseExp = enemyData.exp;

        return baseExp;
    }

    /// <summary>
    /// プレイヤーキャラクターに経験値を配布
    /// </summary>
    /// <param name="totalExp">配布する総経験値</param>
    private void DistributeExperienceToPlayers(int totalExp)
    {
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("プレイヤーキャラクターが見つかりません");
            return;
        }

        // 生存しているプレイヤーキャラクターに経験値を配布
        int alivePlayerCount = 0;
        foreach (var playerObj in players)
        {
            if (playerObj != null)
            {
                var character = playerObj.GetComponent<Character>();
                if (character != null && !character.enemyCheckFlag && character.hp > 0)
                {
                    alivePlayerCount++;
                }
            }
        }

        if (alivePlayerCount == 0)
        {
            Debug.LogWarning("生存しているプレイヤーキャラクターがいません");
            return;
        }

        // 各プレイヤーに均等に経験値を配布
        int expPerPlayer = totalExp / alivePlayerCount;

        Debug.Log($"=== 経験値配布 ===");
        Debug.Log($"  総経験値: {totalExp}");
        Debug.Log($"  生存プレイヤー数: {alivePlayerCount}");
        Debug.Log($"  1人あたり: {expPerPlayer}EXP");
        Debug.Log($"==================");

        foreach (var playerObj in players)
        {
            if (playerObj != null)
            {
                var character = playerObj.GetComponent<Character>();
                if (character != null && !character.enemyCheckFlag && character.hp > 0)
                {
                    int oldLevel = character.level;
                    character.GainExp(expPerPlayer);

                    if (character.level > oldLevel)
                    {
                        Debug.Log($"{character.charactername} がレベル {oldLevel} から {character.level} にレベルアップしました！");
                    }
                }
            }
        }
        
        // 戦闘終了時に全キャラクターのステータス（HP/MP/EXP/Level等）をCharacterDataに保存
        foreach (var playerObj in players)
        {
            if (playerObj != null)
            {
                var character = playerObj.GetComponent<Character>();
                if (character != null && !character.enemyCheckFlag)
                {
                    character.GetCharacterData(); // CharacterDataを更新
                    Debug.Log($"[TurnManager] {character.charactername} のステータスを保存: HP={character.hp}/{character.maxHp}, MP={character.mp}/{character.maxMp}, Lv={character.level}, EXP={character.exp}");
                }
            }
        }
    }
    
    /// <summary>
    /// エラー時にフィールドに戻る
    /// </summary>
    private IEnumerator ReturnToFieldAfterError()
    {
        yield return new WaitForSeconds(2f);
        
        Debug.Log("[TurnManager] エラーが発生したためフィールドに戻ります");
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndBattle();
        }
    }
    
    //End of TurnManager
    #endregion
}
