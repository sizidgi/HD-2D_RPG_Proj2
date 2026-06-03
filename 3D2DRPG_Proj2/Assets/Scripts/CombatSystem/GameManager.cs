using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static GameManager;

/// <summary>
/// ゲーム全体を管理するシングルトンマネージャー
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    [Header("ゲーム状態")]
    [SerializeField] private GameState currentGameState = GameState.GameField;

    [Header("シーン管理")]
    [SerializeField] private string gameFieldSceneName = "GameField";
    [SerializeField] private string battleSceneName = "turnTestScene";
    [SerializeField] private string titleSceneName = "GameTitle";

    [Header("バトル設定")]
    [SerializeField] private bool enableBattleTransition = true;
    [SerializeField] private float battleTransitionDelay = 0.5f;

    [Header("UnityEvents")]
    [SerializeField] private UnityEvent OnBattleStart;
    [SerializeField] private UnityEvent OnBattleEnd;
    [SerializeField] private UnityEvent<GameState> OnGameStateChanged;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLog = true;
    [SerializeField, Tooltip("ONのとき戦闘の当たり判定枠などデバッグ表示を有効化")]
    private bool debugMode = false;

    /// <summary>デバッグ用ビジュアル（当たり判定枠など）の表示可否</summary>
    public bool IsDebugMode => debugMode;
    public int isi = 1; // 既存の変数を保持

    [Header("バトルデータ")]
    [SerializeField, Header("全キャラクターマスターデータ")]
    public List<CharacterData> AllCharacterData; // 全キャラクターのマスターデータ（デバッグ用）
    
    [SerializeField, Header("現在のパーティーメンバー")]
    public List<CharacterData> PlayerData; // 現在パーティーに加入しているキャラクター
    
    [SerializeField, Header("エネミー")]
    public List<CharacterData> EnemyData; // 既存の変数を保持

    // 戦闘履歴管理（倒した敵のIDを記録）
    [SerializeField, Header("戦闘履歴管理")]
    private SerializableStringHashSet defeatedEnemyIds = new SerializableStringHashSet();
    
    // グループID単位の戦闘履歴管理
    [SerializeField, Header("エンカウントグループ戦闘履歴")]
    private HashSet<int> defeatedEncounterGroups = new HashSet<int>();
    
    // 戦闘開始時の敵オブジェクト参照
    [NonSerialized]
    public GameObject currentBattleEnemy = null;
    
    // 戦闘開始前のキャラクターステータス（経験値アニメーション用）
    [System.Serializable]
    public class PreBattleSnapshot
    {
        public string characterName;
        public int level;
        public int exp;
        public int requiredExp;
        public int totalExp; // 累積経験値（レベル1から現在までの合計）
    }
    
    [NonSerialized]
    public List<PreBattleSnapshot> preBattleSnapshots = new List<PreBattleSnapshot>();

    [NonSerialized]
    public bool BattleWin = false; // 既存の変数を保持
    [NonSerialized]
    public Vector3 PlayerBackPosition; // 既存の変数を保持

    [SerializeField]
    private string EventIDName;
    [SerializeField]
    private bool EventFlag;

    [Header("戦闘後イベント")]
    [NonSerialized]
    public string postBattleDialogueCSV = ""; // 戦闘後に再生する会話CSV
    [NonSerialized]
    public bool hasPostBattleDialogue = false; // 戦闘後会話フラグ

    [System.Serializable]
    public class BattleMidEvent
    {
        [Tooltip("HP閾値（％）")]
        [Range(0, 100)]
        public int hpThreshold = 50;
        
        [Tooltip("再生する会話CSV")]
        public string dialogueCSV;
        
        [Tooltip("会話中に戦闘を一時停止するか")]
        public bool pauseBattle = true;
    }

    [Header("戦闘中イベント")]
    [NonSerialized]
    public bool isBossBattle = false;  // ボス戦フラグ
    [NonSerialized]
    public bool isMidBossBattle = false;  // 中ボス戦フラグ
    [NonSerialized]
    public List<BattleMidEvent> battleMidEvents = new List<BattleMidEvent>();
    
    [Header("戦闘BGM")]
    [NonSerialized]
    public string currentBattleBGM = "BattleNormal";  // 現在の戦闘BGM名

    [Header("経験値・レベル管理")]
    [SerializeField] private Dictionary<int, int> ExpTable = new Dictionary<int, int>();

    // プロパティ
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                SetupInstance();
            }
            return instance;
        }
    }

    public GameState CurrentGameState => currentGameState;
    public bool IsBattleTransitionEnabled => enableBattleTransition;

    // 内部変数
    private Vector3 lastFieldPosition;
    private bool isTransitioning = false;
    private void Awake()
    {
        if (instance == null)
        {
            InitializeExpTable();

            instance = this;
            DontDestroyOnLoad(this.gameObject);

            // シーン変更時のイベント登録
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            // EnemyDataを初期化（エディタで設定されているデータをクリア）
            if (EnemyData == null)
            {
                EnemyData = new List<CharacterData>();
            }
            else
            {
                EnemyData.Clear();
            }

            if (showDebugLog)
            {
                //Debug.Log("GameManager: シングルトンインスタンスを作成しました");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        EventFlag = false;
        
        // PlayerDataが未初期化の場合は空リストを作成
        if (PlayerData == null)
        {
            PlayerData = new List<CharacterData>();
        }
    }
    public void PlayerDataSetStatus(List<CharacterData> characterData)
    {
        PlayerData.Clear();
        
        // ScriptableObjectを複製してランタイムインスタンスとして追加
        foreach (var data in characterData)
        {
            if (data != null)
            {
                var runtimeCopy = Instantiate(data);
                PlayerData.Add(runtimeCopy);
            }
        }
        
        if (showDebugLog)
        {
            Debug.Log($"[GameManager] PlayerData初期化完了: {PlayerData.Count}人のキャラクターを複製して追加");
        }
    }
    public void EnemyDataClear()
    {
        EnemyData.Clear();
    }
    private void OnDestroy()
    {
        // イベントの登録解除
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static void SetupInstance()
    {
        instance = FindObjectOfType<GameManager>();

        if (instance == null)
        {
            GameObject gameObj = new GameObject();
            gameObj.name = "GameManager";
            instance = gameObj.AddComponent<GameManager>();
            DontDestroyOnLoad(gameObj);
        }
    }

    ///// <summary>
    /// 
    /// イベントフラグを取得
    /// 
    ///// </summary>
    public void GetEventID(string eventID)
    {
        EventIDName = eventID;
        EventFlag = true;
    }

    ///// <summary>
    ///
    /// イベントがTrueの時にイベントIDを返す
    /// 
    ///// </summary>
    public String SetEventIDFlag()
    {
        if (EventFlag)
        {
            EventFlag = false;
            return EventIDName;
        }
        else
        {
            return null;
        }
    }


    /// <summary>
    /// シーンロード時の処理
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (showDebugLog)
        {
            Debug.Log($"GameManager: シーンロード完了 - {scene.name}");
        }

        // シーン名に基づいてゲーム状態を更新
        UpdateGameStateFromScene(scene.name);
        
        // 戦闘シーンに入った時の処理
        if (scene.name == battleSceneName)
        {
            BGMManager bgm = BGMManager.EnsureInstance();
            // 真のボス戦（中ボスではない）の場合のみボスBGMを再生
            string bgmToPlay = (isBossBattle && !isMidBossBattle) ? "BattleBoss" : currentBattleBGM;

            bgm.PlayBGM(bgmToPlay, fade: true);

            if (showDebugLog)
            {
                Debug.Log($"[GameManager] 戦闘BGM再生: {bgmToPlay} (isBossBattle={isBossBattle}, isMidBossBattle={isMidBossBattle})");
            }
        }
        
        // タイトルシーンに戻った時の処理
        if (scene.name == titleSceneName)
        {
            BGMManager bgm = BGMManager.EnsureInstance();
            bgm.StopBGM(fade: true);

            if (showDebugLog)
            {
                Debug.Log("[GameManager] タイトルシーン：BGMを停止しました");
            }
        }

        // GameFieldシーンに戻った時の処理
        if (scene.name == gameFieldSceneName)
        {
            // 戦闘勝利後、敵オブジェクトを削除
            if (BattleWin && currentBattleEnemy != null)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[GameManager] 戦闘勝利後、フィールドに復帰: 敵オブジェクトを削除 {currentBattleEnemy.name}");
                }
                
                Destroy(currentBattleEnemy);
                currentBattleEnemy = null;
            }
            
            // フィールドBGMを再生
            BGMManager bgm = BGMManager.EnsureInstance();
            bgm.PlayBGM("Field", fade: true);

            if (showDebugLog)
            {
                Debug.Log("[GameManager] フィールドBGM再生");
            }
            
            // 戦闘後会話イベントをチェック
            if (hasPostBattleDialogue && !string.IsNullOrEmpty(postBattleDialogueCSV))
            {
                StartCoroutine(StartPostBattleDialogue());
            }
        }
    }
    
    /// <summary>
    /// 戦闘後の会話イベントを開始
    /// </summary>
    private IEnumerator StartPostBattleDialogue()
    {
        // シーン遷移のフェードイン完了を待つ
        yield return new WaitForSeconds(1f);
        
        if (showDebugLog)
        {
            Debug.Log($"[GameManager] 戦闘後会話イベント開始: {postBattleDialogueCSV}");
        }
        //敵を停止させる。
        SimpleEventTrigger simpleEventTrigger = new SimpleEventTrigger();
        simpleEventTrigger.DisableAllEnemies();

        // ConversationUIを探して会話開始
        ConversationUI conversationUI = FindObjectOfType<ConversationUI>();
        if (conversationUI != null)
        {
            conversationUI.StartDialogueWithCSV(postBattleDialogueCSV);
        }
        else
        {
            Debug.LogWarning("[GameManager] ConversationUIが見つかりません");
        }
        
        // フラグをリセット
        hasPostBattleDialogue = false;
        postBattleDialogueCSV = "";
    }

    /// <summary>
    /// シーン名からゲーム状態を更新
    /// </summary>
    private void UpdateGameStateFromScene(string sceneName)
    {
        GameState newState = currentGameState;

        if (sceneName == battleSceneName)
        {
            newState = GameState.Battle;
        }
        else if (sceneName == gameFieldSceneName)
        {
            newState = GameState.GameField;
        }
        else if (sceneName == titleSceneName)
        {
            newState = GameState.Title;
        }

        if (newState != currentGameState)
        {
            SetGameState(newState);
        }
    }

    /// <summary>
    /// ゲーム状態を変更
    /// </summary>
    public void SetGameState(GameState newState)
    {
        if (currentGameState == newState) return;

        GameState previousState = currentGameState;
        currentGameState = newState;

        if (showDebugLog)
        {
            Debug.Log($"GameManager: ゲーム状態変更 {previousState} → {newState}");
        }

        // UnityEventを発火
        OnGameStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// バトル開始（プレイヤーが敵に接触した時に呼び出される）
    /// </summary>
    public void StartBattle(Vector3 playerPosition, GameObject enemy = null)
    {
        if (isTransitioning)
        {
            if (showDebugLog)
            {
                Debug.Log("GameManager: シーン遷移中のため、バトル開始をスキップ");
            }
            return;
        }

        if (!enableBattleTransition)
        {
            if (showDebugLog)
            {
                Debug.Log("GameManager: バトル遷移が無効化されています");
            }
            return;
        }

        // フィールドでの最後の位置を記録
        lastFieldPosition = playerPosition;
        
        // 戦闘開始前のキャラクターステータスを記録
        SavePreBattleSnapshots();

        if (showDebugLog)
        {
            Debug.Log($"GameManager: バトル開始 - プレイヤー位置: {playerPosition}");
            if (enemy != null)
            {
                Debug.Log($"遭遇した敵: {enemy.name}");
            }
        }

        // バトル開始イベントを発火
        OnBattleStart?.Invoke();

        // バトルシーンに遷移
        StartCoroutine(TransitionToBattle());
    }

    /// <summary>
    /// バトル終了（バトルシーンから呼び出される）
    /// </summary>
    public void EndBattle()
    {
        if (showDebugLog)
        {
            Debug.Log("GameManager: バトル終了");
        }

        // バトル終了イベントを発火
        OnBattleEnd?.Invoke();
        
        // ボス戦データをクリア
        ClearBossBattleData();

        BattleWin = true;
        
        // 戦闘勝利時、フィールドに戻る前に敵オブジェクトを削除予約
        if (currentBattleEnemy != null)
        {
            if (showDebugLog)
            {
                Debug.Log($"[GameManager] 戦闘勝利: 敵オブジェクトを削除予約 {currentBattleEnemy.name}");
            }
        }
        
        // フィールドに戻る
        StartCoroutine(TransitionToGameField());
    }

    /// <summary>
    /// 敗北時：タイトル画面へ戻る（フィールド復帰・勝利イベントは発生しない）
    /// </summary>
    public void ReturnToTitle()
    {
        if (isTransitioning)
        {
            if (showDebugLog)
            {
                Debug.Log("[GameManager] シーン遷移中のため、タイトル復帰をスキップします");
            }
            return;
        }

        if (showDebugLog)
        {
            Debug.Log("[GameManager] 敗北：タイトル画面へ戻ります");
        }

        BattleWin = false;
        ClearBossBattleData();
        hasPostBattleDialogue = false;
        postBattleDialogueCSV = "";
        currentBattleEnemy = null;
        currentBattleBGM = "BattleNormal";

        ResetPartyStatusAfterDefeat();
        EnemyDataClear();
        preBattleSnapshots.Clear();

        BGMManager.EnsureInstance().StopBGM(fade: true);

        OnBattleEnd?.Invoke();

        StartCoroutine(TransitionToTitle());
    }

    /// <summary>
    /// 敗北時：パーティのHP/MPを全回復（DontDestroyOnLoadのPlayerDataを次戦闘用にリセット）
    /// </summary>
    private void ResetPartyStatusAfterDefeat()
    {
        if (PlayerData == null || PlayerData.Count == 0)
        {
            return;
        }

        foreach (var character in PlayerData)
        {
            if (character == null) continue;

            character.hp = character.maxHp;
            character.mp = character.maxMp;
            character.StatusFlag = StatusFlag.End;
        }

        if (showDebugLog)
        {
            Debug.Log($"[GameManager] 敗北後パーティステータスをリセットしました（{PlayerData.Count}人）");
        }
    }

    /// <summary>
    /// タイトルシーンへの遷移
    /// </summary>
    private IEnumerator TransitionToTitle()
    {
        isTransitioning = true;

        if (SceneTransitionManager.Instance != null)
        {
            if (showDebugLog)
            {
                Debug.Log($"[GameManager] SceneTransitionManagerでタイトルへ遷移: {titleSceneName}");
            }

            currentGameState = GameState.Title;
            SceneTransitionManager.Instance.LoadSceneWithFade(titleSceneName);
            yield return new WaitForSeconds(1.5f);
        }
        else
        {
            if (showDebugLog)
            {
                Debug.LogWarning("[GameManager] SceneTransitionManagerが見つかりません。直接タイトルへ遷移します。");
            }

            currentGameState = GameState.Title;
            SceneManager.LoadScene(titleSceneName);
            yield return null;
        }

        isTransitioning = false;
    }

    /// <summary>
    /// バトルシーンへの遷移
    /// </summary>
    private IEnumerator TransitionToBattle()
    {
        isTransitioning = true;

        // 遷移前の待機時間
        if (battleTransitionDelay > 0)
        {
            yield return new WaitForSeconds(battleTransitionDelay);
        }

        PlayerBackPosition = lastFieldPosition;
        
        // SceneTransitionManagerを使用して斜めワイプで遷移
        if (SceneTransitionManager.Instance != null)
        {
            if (showDebugLog)
            {
                Debug.Log("[GameManager] SceneTransitionManagerで斜めワイプトランジションを使用");
            }
            
            currentGameState = GameState.Battle;
            SceneTransitionManager.Instance.LoadSceneWithDiagonalFade(battleSceneName);
            
            // トランジション完了まで待機
            yield return new WaitForSeconds(1.5f);
        }
        else
        {
            // フォールバック: SceneTransitionManagerがない場合は直接遷移
            if (showDebugLog)
            {
                Debug.LogWarning("[GameManager] SceneTransitionManagerが見つかりません。直接遷移します。");
            }
            
            currentGameState = GameState.Battle;
            SceneManager.LoadScene(battleSceneName);
        }

        isTransitioning = false;
    }

    /// <summary>
    /// ゲームフィールドへの遷移
    /// </summary>
    private IEnumerator TransitionToGameField()
    {
        isTransitioning = true;

        // シーン遷移
        SceneManager.LoadScene(gameFieldSceneName);

        // シーン遷移完了まで待機
        yield return null;

        isTransitioning = false;
    }

    /// <summary>
    /// プレイヤーの最後のフィールド位置を取得
    /// </summary>
    public Vector3 GetLastFieldPosition()
    {
        return lastFieldPosition;
    }

    public void SetLastFieldPosition(Vector3 vec3)
    {
        lastFieldPosition = vec3;
    }

    /// <summary>
    /// バトル遷移の有効/無効を切り替え
    /// </summary>
    public void SetBattleTransitionEnabled(bool enabled)
    {
        enableBattleTransition = enabled;
        if (showDebugLog)
        {
            Debug.Log($"GameManager: バトル遷移を{(enabled ? "有効" : "無効")}にしました");
        }
    }

    /// <summary>
    /// 指定シーンに遷移
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (isTransitioning)
        {
            if (showDebugLog)
            {
                Debug.Log("GameManager: 既にシーン遷移中です");
            }
            return;
        }

        if (showDebugLog)
        {
            Debug.Log($"GameManager: {sceneName} に遷移中...");
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// テスト用：強制的にバトル開始
    /// </summary>
    [ContextMenu("テスト用バトル開始")]
    public void TestStartBattle()
    {
        StartBattle(Vector3.zero);
    }

    /// <summary>
    /// テスト用：強制的にバトル終了
    /// </summary>
    [ContextMenu("テスト用バトル終了")]
    public void TestEndBattle()
    {
        EndBattle();
    }

    /// <summary>
    /// 敵を倒したことを記録
    /// </summary>
    public void RecordEnemyDefeat(CharacterData enemyData)
    {
        if (enemyData != null)
        {
            defeatedEnemyIds.Add(enemyData.charactername);
            if (showDebugLog)
            {
                Debug.Log($"GameManager: 敵を倒した記録 - {enemyData.charactername}");
            }
        }
    }

    /// <summary>
    /// 敵を倒したことがあるかチェック
    /// </summary>
    public bool HasDefeatedEnemy(CharacterData enemyData)
    {
        if (enemyData == null) return false;
        return defeatedEnemyIds.Contains(enemyData.charactername);
    }

    /// <summary>
    /// 敵リストを倒したことがあるかチェック（リスト内のすべての敵を倒したことがある場合）
    /// </summary>
    public bool HasDefeatedAllEnemies(List<CharacterData> enemyDataList)
    {
        if (enemyDataList == null || enemyDataList.Count == 0) return false;

        foreach (var enemyData in enemyDataList)
        {
            if (enemyData != null && !HasDefeatedEnemy(enemyData))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// エンカウントグループを倒したことを記録
    /// </summary>
    public void RecordGroupDefeat(int groupId)
    {
        if (!defeatedEncounterGroups.Contains(groupId))
        {
            defeatedEncounterGroups.Add(groupId);
            if (showDebugLog)
            {
                Debug.Log($"[GameManager] エンカウントグループ撃破記録: グループID={groupId}");
            }
        }
    }

    /// <summary>
    /// エンカウントグループを倒したことがあるかチェック
    /// </summary>
    public bool HasDefeatedGroup(int groupId)
    {
        return defeatedEncounterGroups.Contains(groupId);
    }

    /// <summary>
    /// 戦闘開始時に敵オブジェクトを記録
    /// </summary>
    public void SetCurrentBattleEnemy(GameObject enemy)
    {
        currentBattleEnemy = enemy;
    }

    /// <summary>
    /// バトル開始（CharacterDataを受け取るオーバーロード）
    /// </summary>
    public void StartBattleWithEnemyData(Vector3 playerPosition, List<CharacterData> enemyCharacterDataList, string bgmName = "BattleNormal")
    {
        if (isTransitioning || !enableBattleTransition) return;

        lastFieldPosition = playerPosition;

        // EnemyDataをクリアして新しい敵データを設定
        EnemyData.Clear();
        if (enemyCharacterDataList != null)
        {
            EnemyData.AddRange(enemyCharacterDataList);
        }
        
        // BGM名を設定
        currentBattleBGM = bgmName;
        
        // 戦闘開始前のキャラクターステータスを記録
        SavePreBattleSnapshots();

        if (showDebugLog)
        {
            Debug.Log($"GameManager: バトル開始 - 敵数: {enemyCharacterDataList?.Count ?? 0}, BGM: {bgmName}");
        }

        OnBattleStart?.Invoke();
        StartCoroutine(TransitionToBattle());
    }
    
    /// <summary>
    /// 戦闘後に会話イベントを発生させるバトル開始
    /// </summary>
    public void StartBattleWithPostDialogue(Vector3 playerPosition, List<CharacterData> enemyCharacterDataList, string dialogueCSV)
    {
        // 戦闘後会話を設定
        postBattleDialogueCSV = dialogueCSV;
        hasPostBattleDialogue = true;
        
        if (showDebugLog)
        {
            Debug.Log($"[GameManager] 戦闘後会話設定: {dialogueCSV}");
        }
        
        // 通常の戦闘開始
        StartBattleWithEnemyData(playerPosition, enemyCharacterDataList);
    }
    
    /// <summary>
    /// ボス戦を開始（戦闘中イベント付き）
    /// </summary>
    public void StartBossBattle(
        Vector3 playerPosition, 
        List<CharacterData> enemyData, 
        List<BattleMidEvent> midEvents = null,
        string postBattleDialogue = "",
        string bgmName = "BattleBoss",
        bool isMidBoss = false)
    {
        // ボス戦フラグを設定
        isBossBattle = true;
        isMidBossBattle = isMidBoss;
        
        // 戦闘中イベントを設定
        battleMidEvents.Clear();
        if (midEvents != null)
        {
            battleMidEvents.AddRange(midEvents);
        }
        
        // 戦闘後会話を設定
        if (!string.IsNullOrEmpty(postBattleDialogue))
        {
            postBattleDialogueCSV = postBattleDialogue;
            hasPostBattleDialogue = true;
        }
        
        if (showDebugLog)
        {
            Debug.Log($"[GameManager] ボス戦開始: 戦闘中イベント{battleMidEvents.Count}個, 戦闘後会話: {postBattleDialogue}, BGM: {bgmName}, 中ボス: {isMidBoss}");
        }
        
        // 通常の戦闘開始処理（BGM付き）
        StartBattleWithEnemyData(playerPosition, enemyData, bgmName);
    }
    
    /// <summary>
    /// 戦闘終了時にボス戦フラグをリセット
    /// </summary>
    public void ClearBossBattleData()
    {
        isBossBattle = false;
        isMidBossBattle = false;
        battleMidEvents.Clear();
        
        if (showDebugLog)
        {
            Debug.Log("[GameManager] ボス戦データをクリアしました");
        }
    }

    private void InitializeExpTable()
    {
        // レベル1-10
        ExpTable[1] = 100;
        ExpTable[2] = 150;
        ExpTable[3] = 200;
        ExpTable[4] = 250;
        ExpTable[5] = 300;
        ExpTable[6] = 350;
        ExpTable[7] = 400;
        ExpTable[8] = 450;
        ExpTable[9] = 500;
        ExpTable[10] = 500;  // 550→500に変更
        
        // レベル11-20
        ExpTable[11] = 550;  // 600→550に変更
        ExpTable[12] = 600;  // 650→600に変更
        ExpTable[13] = 650;  // 700→650に変更
        ExpTable[14] = 700;  // 750→700に変更
        ExpTable[15] = 750;  // 800→750に変更
        ExpTable[16] = 800;  // 850→800に変更
        ExpTable[17] = 850;  // 900→850に変更
        ExpTable[18] = 900;  // 950→900に変更
        ExpTable[19] = 950;  // 1000→950に変更
        ExpTable[20] = 1000; // 1050→1000に変更
    }
    /// <summary>
    /// 経験値を付与してレベルアップ処理（非推奨：代わりにCharacter.GainExp()を使用）
    /// </summary>
    [System.Obsolete("Character.GainExp()を使用してください。このメソッドは後方互換性のために残されています。")]
    public void AddExperience(CharacterData character, int expAmount)
    {
        if (character == null || expAmount <= 0) return;
        
        Debug.LogWarning($"[GameManager] AddExperience()は非推奨です。Character.GainExp()を使用してください。");
        
        character.exp += expAmount;
        
        if (showDebugLog)
        {
            Debug.Log($"[GameManager] {character.charactername} が {expAmount}EXP 獲得！ (合計: {character.exp}EXP)");
        }

        // レベルアップチェック
        while (character.exp >= GetRequiredExp(character.level))
        {
            character.exp -= GetRequiredExp(character.level);
            LevelUp(character);
        }
    }
    
    public int GetRequiredExp(int currentLevel)
    {
        return ExpTable.ContainsKey(currentLevel) ? ExpTable[currentLevel] : 999999;
    }
    
    /// <summary>
    /// レベルアップ処理（LevelUpTableを使用、なければデフォルト）
    /// </summary>
    public void LevelUp(CharacterData character)
    {
        character.level++;
        
        Debug.Log($"★ {character.charactername} がレベル {character.level} にアップ！ ★");
        
        // レベルアップテーブルが設定されていればダイスロールシステムを使用
        if (character.levelUpTable != null)
        {
            var levelUpData = character.levelUpTable.GetLevelUpData(character.level);
        
            if (levelUpData != null)
            {
                // ステータス上昇（ダイスロール）
                int hpGain = levelUpData.hpGain?.Roll() ?? 0;
                int mpGain = levelUpData.mpGain?.Roll() ?? 0;
                int atkGain = levelUpData.atkGain?.Roll() ?? 0;
                int defGain = levelUpData.defGain?.Roll() ?? 0;
                int spdGain = levelUpData.spdGain?.Roll() ?? 0;
                int intGain = levelUpData.intGain?.Roll() ?? 0;
                
                // ステータスに加算
                if (hpGain > 0)
                {
                    character.maxHp += hpGain;
                    character.hp = character.maxHp; // 現在HPも回復
                    Debug.Log($"  HP +{hpGain} (最大HP: {character.maxHp})");
                }
                
                if (mpGain > 0)
                {
                    character.maxMp += mpGain;
                    character.mp = character.maxMp; // 現在MPも回復
                    Debug.Log($"  MP +{mpGain} (最大MP: {character.maxMp})");
                }
                
                if (atkGain > 0)
                {
                    character.atk += atkGain;
                    Debug.Log($"  STR(ATK) +{atkGain} (ATK: {character.atk})");
                }
                
                if (defGain > 0)
                {
                    character.def += defGain;
                    Debug.Log($"  DEF +{defGain} (DEF: {character.def})");
                }
                
                if (spdGain > 0)
                {
                    character.spd += spdGain;
                    Debug.Log($"  SPD +{spdGain} (SPD: {character.spd})");
                }
                
                if (intGain > 0)
                {
                    character.Int += intGain;
                    Debug.Log($"  INT +{intGain} (INT: {character.Int})");
                }
                
                // スキル習得
                if (levelUpData.learnedSkill != null)
                {
                    LearnSkill(character, levelUpData.learnedSkill);
                }
            }
            else
            {
                Debug.LogWarning($"[GameManager] レベル{character.level}のLevelUpDataが見つかりません");
            }
        }
        else
        {
            // LevelUpTableが設定されていない場合はデフォルト処理
            Debug.LogWarning($"[GameManager] {character.charactername} のLevelUpTableが未設定。デフォルト成長を使用");
            character.maxHp += 10;
            character.hp += 10;
            character.atk += 2;
            character.def += 2;
        }
    }
    
    /// <summary>
    /// スキル習得処理
    /// </summary>
    private void LearnSkill(CharacterData character, SkillData newSkill)
    {
        // 既に習得済みかチェック
        foreach (var skill in character.skills)
        {
            if (skill == newSkill)
            {
                Debug.LogWarning($"[GameManager] {character.charactername} は既に {newSkill.skillName} を習得しています");
                return;
            }
        }
        
        // スキル配列を拡張して追加
        var skillList = new List<SkillData>(character.skills);
        skillList.Add(newSkill);
        character.skills = skillList.ToArray();
        
        Debug.Log($"★★ {character.charactername} は新しいスキル【{newSkill.skillName}】を習得した！ ★★");
    }
    
    
    #region パーティーメンバー管理
    
    /// <summary>
    /// パーティーにキャラクターを加入させる
    /// </summary>
    public void AddPartyMember(CharacterData character)
    {
        if (character == null)
        {
            Debug.LogError("[GameManager] 加入させるキャラクターがnullです");
            return;
        }
        
        // 既にパーティーに加入しているかチェック（名前で判定）
        if (PlayerData.Exists(c => c.charactername == character.charactername))
        {
            Debug.LogWarning($"[GameManager] {character.charactername} は既にパーティーに加入しています");
            return;
        }
        
        // ScriptableObjectを複製してランタイムインスタンスとして追加
        var runtimeCopy = Instantiate(character);
        PlayerData.Add(runtimeCopy);
        
        if (showDebugLog)
        {
            Debug.Log($"[GameManager] {character.charactername} がパーティーに加入しました！ (現在のメンバー: {PlayerData.Count}人)");
        }
    }
    
    /// <summary>
    /// パーティーからキャラクターを離脱させる
    /// </summary>
    public void RemovePartyMember(CharacterData character)
    {
        if (character == null)
        {
            Debug.LogError("[GameManager] 離脱させるキャラクターがnullです");
            return;
        }
        
        // 名前で検索（複製されたインスタンスなので、参照比較ではなく名前で判定）
        var targetCharacter = PlayerData.Find(c => c.charactername == character.charactername);
        
        if (targetCharacter == null)
        {
            Debug.LogWarning($"[GameManager] {character.charactername} はパーティーに加入していません");
            return;
        }
        
        PlayerData.Remove(targetCharacter);
        
        if (showDebugLog)
        {
            Debug.Log($"[GameManager] {character.charactername} がパーティーから離脱しました (現在のメンバー: {PlayerData.Count}人)");
        }
    }
    
    /// <summary>
    /// 名前でキャラクターを検索してパーティーに加入させる
    /// </summary>
    public void AddPartyMemberByName(string characterName)
    {
        if (AllCharacterData == null || AllCharacterData.Count == 0)
        {
            Debug.LogError("[GameManager] AllCharacterDataが設定されていません");
            return;
        }
        
        CharacterData character = AllCharacterData.Find(c => c.charactername == characterName);
        
        if (character == null)
        {
            Debug.LogError($"[GameManager] キャラクター '{characterName}' が見つかりません");
            return;
        }
        
        AddPartyMember(character);
    }
    
    /// <summary>
    /// 名前でキャラクターを検索してパーティーから離脱させる
    /// </summary>
    public void RemovePartyMemberByName(string characterName)
    {
        CharacterData character = PlayerData.Find(c => c.charactername == characterName);
        
        if (character == null)
        {
            Debug.LogError($"[GameManager] パーティーにキャラクター '{characterName}' が見つかりません");
            return;
        }
        
        RemovePartyMember(character);
    }
    
    /// <summary>
    /// キャラクターがパーティーに加入しているかチェック
    /// </summary>
    public bool IsInParty(CharacterData character)
    {
        // 複製されたインスタンスなので、名前で判定
        return character != null && PlayerData.Exists(c => c.charactername == character.charactername);
    }
    
    /// <summary>
    /// キャラクターがパーティーに加入しているかチェック（名前で検索）
    /// </summary>
    public bool IsInPartyByName(string characterName)
    {
        return PlayerData.Exists(c => c.charactername == characterName);
    }
    
    /// <summary>
    /// 現在のパーティーメンバー数を取得
    /// </summary>
    public int GetPartyMemberCount()
    {
        return PlayerData != null ? PlayerData.Count : 0;
    }
    
    /// <summary>
    /// 現在のパーティーメンバーリストを取得（読み取り専用）
    /// </summary>
    public List<CharacterData> GetActivePartyMembers()
    {
        return new List<CharacterData>(PlayerData);
    }
    
    #endregion
    
    #region 戦闘前スナップショット管理
    
    /// <summary>
    /// 戦闘開始前のキャラクターステータスを保存
    /// </summary>
    public void SavePreBattleSnapshots()
    {
        preBattleSnapshots.Clear();
        
        Debug.Log($"[GameManager] SavePreBattleSnapshots開始 PlayerData数: {PlayerData.Count}");
        
        foreach (var character in PlayerData)
        {
            if (character != null)
            {
                var snapshot = new PreBattleSnapshot
                {
                    characterName = character.charactername,
                    level = character.level,
                    exp = character.exp,
                    requiredExp = GetRequiredExp(character.level),
                    totalExp = CalculateTotalExp(character.level, character.exp)
                };
                preBattleSnapshots.Add(snapshot);
                
                Debug.Log($"[GameManager] スナップショット保存: {snapshot.characterName} Lv.{snapshot.level} EXP:{snapshot.exp}/{snapshot.requiredExp} (累積:{snapshot.totalExp})");
            }
            else
            {
                Debug.LogWarning($"[GameManager] PlayerDataにnullキャラクターが含まれています");
            }
        }
        
        Debug.Log($"[GameManager] SavePreBattleSnapshots完了 保存数: {preBattleSnapshots.Count}");
    }
    
    /// <summary>
    /// 累積経験値を計算（レベル1から現在までの合計経験値）
    /// </summary>
    public int CalculateTotalExp(int currentLevel, int currentExp)
    {
        int totalExp = currentExp; // 現在の経験値
        
        // レベル1→2, 2→3, ... currentLevel-1→currentLevel までの必要経験値を加算
        for (int lv = 1; lv < currentLevel; lv++)
        {
            totalExp += GetRequiredExp(lv);
        }
        
        return totalExp;
    }
    
    /// <summary>
    /// 戦闘前のスナップショットを取得
    /// </summary>
    public PreBattleSnapshot GetPreBattleSnapshot(string characterName)
    {
        return preBattleSnapshots.Find(s => s.characterName == characterName);
    }
    
    #endregion
}

/// <summary>
/// ゲームの状態を表す列挙型
/// </summary>
public enum GameState
{
    Title,      // タイトル画面
    GameField,  // フィールド画面
    Battle,     // バトル画面
    Menu,       // メニュー画面
    Pause       // ポーズ状態
}
