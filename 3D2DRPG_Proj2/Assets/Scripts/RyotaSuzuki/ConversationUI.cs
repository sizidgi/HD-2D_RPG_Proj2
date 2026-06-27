using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class DialogueData
{
    public string characterName;
    public string dialogueText;
    public Sprite characterImage;

    public DialogueData(string name, string text)
    {
        characterName = name;
        dialogueText = text;
        characterImage = null;
    }
}

[System.Serializable]
public class DialogueLogEntry
{
    public string characterName;
    public string dialogueText;
    public string timestamp;

    public DialogueLogEntry(string name, string text)
    {
        characterName = name;
        dialogueText = text;
        timestamp = System.DateTime.Now.ToString("HH:mm:ss");
    }
}

public class ConversationUI : MonoBehaviour
{
    [Header("UI要素")]
    [SerializeField, Header("会話UIのパネル")] public GameObject dialoguePanel;
    [SerializeField, Header("キャラクター名が入るテキストオブジェクト")] public TextMeshProUGUI characterNameText;
    [SerializeField, Header("会話内容が入るテキストオブジェクト")] public TextMeshProUGUI dialogueText;
    [SerializeField, Header("キャラクター画像")] public Image characterImage;

    [Header("アニメーション設定")]
    [Header("フェードイン設定設定")] public float fadeInDuration = 0.5f;
    [Header("テキストの流れる速さ設定設定")] public float typewriterSpeed = 0.05f;
    [Header("ログエントリアニメーション設定")] public float logEntrySlideInDuration = 0.3f;
    [Header("ログエントリアニメーション間隔")] public float logEntryAnimationDelay = 0.1f;

    [Header("CSVファイル設定")]
    [Header("CSVの格納されているフォルダ名")] public string csvFolderName = "ScenarioCSV";
    [Header("CSVのファイル名")] public string csvFileName = "scenario01.csv";
    [Header("CSVファイルを使用する？")] public bool useCSVFile = true;

    [Header("会話終了後の動作")]
    [SerializeField, Header("会話終了後にシーン遷移するか")] private bool loadSceneOnEnd = false;
    [SerializeField, Header("遷移先のシーン名")] private string nextSceneName = "Map";
    
    [Header("会話終了イベント")]
    [SerializeField, Header("会話終了時に実行するイベント")] public UnityEvent onDialogueEnd;
    
    [Header("プレイヤー制御")]
    [SerializeField, Tooltip("会話終了時にプレイヤー操作を再開するか")]
    private bool enablePlayerControlOnEnd = true;
    
    [SerializeField, Tooltip("プレイヤーオブジェクト（nullなら自動検索）")]
    private GameObject playerObject;
    
    [Header("Timeline連携")]
    [SerializeField, Tooltip("会話終了時にTimelineも停止するか")]
    private bool stopTimelineOnEnd = false;
    
    [SerializeField, Tooltip("停止するPlayableDirector（Inspectorで設定可能）")]
    private UnityEngine.Playables.PlayableDirector defaultPlayableDirector;
    
    // 現在再生中のPlayableDirector（動的に設定）
    private UnityEngine.Playables.PlayableDirector currentPlayableDirector;

    [Header("ログUI設定")]
    [SerializeField, Header("ログビューアパネル")] public GameObject logPanel;
    [SerializeField, Header("ログコンテンツ（スクロールビュー内）")] public Transform logContent;
    [SerializeField, Header("ログエントリのプレハブ")] public GameObject logEntryPrefab;
    [SerializeField, Header("ログビューアのスクロールビュー")] public ScrollRect logScrollRect;

    [Header("テスト用会話データ")]
    [TextArea(3, 10)]
    public List<string> testDialogues = new List<string>();

    //ボス用のオブジェクトを格納する
    [SerializeField] public GameObject BossObject;
    
    [System.Serializable]
    public class BattleEventData
    {
        [Tooltip("会話CSV名（.csvなし）")]
        public string dialogueCSV;
        [Tooltip("戦闘後に再生する会話CSV（オプション）")]
        public string postBattleDialogueCSV;
        [Tooltip("このイベントがボス戦かどうか")]
        public bool isBossBattle = true;
        public bool isMidBossBattle = false;
        
        [Header("戦闘中イベント")]
        [Tooltip("戦闘中に発生するイベント")]
        public List<GameManager.BattleMidEvent> battleMidEvents = new List<GameManager.BattleMidEvent>();
    }
    
    [Header("戦闘イベント設定")]
    [SerializeField, Tooltip("戦闘イベントのリスト")]
    public List<BattleEventData> battleEvents = new List<BattleEventData>();

    private List<DialogueData> dialogues = new List<DialogueData>();
    private List<DialogueLogEntry> dialogueLog = new List<DialogueLogEntry>();
    private int currentDialogueIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    private const int MAX_LOG_ENTRIES = 50;

    private bool disabledEnemy; //SimpleEventTriggerから引数を受けつぐ

    private bool sceneChangeFlg = false;
    private string sceneChangeParam = "";
    void Start()
    {
        // 初期化
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
            characterImage.enabled = false;

        if (logPanel != null)
            logPanel.SetActive(false);

        // CSVファイルから読み込むかテストデータを使用するか
        if (useCSVFile)
        {
            LoadDialogueFromCSV();
        }
        else
        {
            LoadTestDialogues();
        }
        
        // プレイヤーを自動検索
        if (playerObject == null && enablePlayerControlOnEnd)
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }
    }

    void LoadDialogueFromCSV()
    {
        StartCoroutine(LoadDialogueFromCSVCoroutine());
    }

    IEnumerator LoadDialogueFromCSVCoroutine()
    {
        string csvFolderPath = Path.Combine(Application.streamingAssetsPath, csvFolderName);
        string filePath = Path.Combine(csvFolderPath, csvFileName);

        // デバッグ用：パス情報を表示
        Debug.Log($"StreamingAssetsパス: {Application.streamingAssetsPath}");
        Debug.Log($"ScenarioCSVフォルダパス: {csvFolderPath}");
        Debug.Log($"CSVファイルパス: {filePath}");

        // UnityWebRequest を使用（ビルド後も動作する）
        string uri = filePath;
        
        // プラットフォームに応じて URI を調整
        #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_WEBGL)
        uri = Path.Combine(Application.streamingAssetsPath, csvFolderName, csvFileName);
        #else
        // Windows/Mac/Linux では file:// プロトコルが必要
        if (!uri.StartsWith("file://"))
        {
            uri = "file://" + uri;
        }
        #endif

        Debug.Log($"[ConversationUI] CSV読み込み URI: {uri}");

        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(uri))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ConversationUI] CSVファイルが見つかりません: {uri}");
                Debug.LogWarning($"[ConversationUI] エラー: {www.error}");
                LoadTestDialogues(); // テストデータを使用
                yield break;
            }

            try
            {
                string csvText = www.downloadHandler.text;
                string[] lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
                dialogues.Clear();

                Debug.Log($"[ConversationUI] CSV読み込み成功: {lines.Length}行");

                // ヘッダー行をスキップ（1行目）
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    DialogueData data = ParseCSVLine(line);
                    if (data != null)
                    {
                        dialogues.Add(data);
                    }
                }

                Debug.Log($"[ConversationUI] CSVファイルから{dialogues.Count}行の会話データを読み込みました。");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ConversationUI] CSVファイルの読み込みエラー: {e.Message}");
                Debug.LogError($"[ConversationUI] スタックトレース: {e.StackTrace}");
                LoadTestDialogues(); // エラー時はテストデータを使用
            }
        }
    }

    DialogueData ParseCSVLine(string csvLine)
    {
        // シンプルなCSVパーサー（カンマ区切り、クォート対応）
        List<string> fields = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < csvLine.Length; i++)
        {
            char c = csvLine[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.Trim());
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        fields.Add(currentField.Trim()); // 最後のフィールド

        if (fields.Count >= 2)
        {
            string name = fields[0].Replace("\"", ""); // クォートを削除
            string dialogue = fields[1].Replace("\"", ""); // クォートを削除
            
            // 特殊コマンドのチェック
            if (name.StartsWith("@"))
            {
                ProcessDialogueCommand(name, dialogue);
                return null; // コマンド行は会話データとして扱わない
            }
            
            // \nを実際の改行に変換
            dialogue = dialogue.Replace("\\n", "\n");
            
            // DialogueDataを作成
            DialogueData data = new DialogueData(name, dialogue);
            
            // キャラ名から画像を読み込む（キャラ名_0のスプライト）
            data.characterImage = LoadCharacterSprite(name);
            
            return data;
        }

        return null;
    }

    /// <summary>
    /// 会話イベント内の特殊コマンドを処理
    /// </summary>
    void ProcessDialogueCommand(string command, string parameter)
    {
        command = command.ToUpper().Trim();
        parameter = parameter.Trim();
        
        switch (command)
        {
            case "@ADD_MEMBER":
                // キャラクター加入
                if (!string.IsNullOrEmpty(parameter))
                {
                    GameManager.Instance.AddPartyMemberByName(parameter);
                    Debug.Log($"[ConversationUI] コマンド実行: {parameter} をパーティーに加入させました");
                }
                else
                {
                    Debug.LogWarning("[ConversationUI] @ADD_MEMBERコマンドにキャラクター名が指定されていません");
                }
                break;
                
            case "@REMOVE_MEMBER":
                // キャラクター離脱
                if (!string.IsNullOrEmpty(parameter))
                {
                    GameManager.Instance.RemovePartyMemberByName(parameter);
                    Debug.Log($"[ConversationUI] コマンド実行: {parameter} をパーティーから離脱させました");
                }
                else
                {
                    Debug.LogWarning("[ConversationUI] @REMOVE_MEMBERコマンドにキャラクター名が指定されていません");
                }
                break;
                
            case "@CLEAR_PARTY":
                // パーティー全員離脱
                if (GameManager.Instance != null && GameManager.Instance.PlayerData != null)
                {
                    int count = GameManager.Instance.PlayerData.Count;
                    GameManager.Instance.PlayerData.Clear();
                    Debug.Log($"[ConversationUI] コマンド実行: パーティーメンバーを全員離脱させました ({count}人)");
                }
                break;

            case "@SCENE_CHANGE":
                //シーンチェンジ
                new WaitForSeconds(2f);

                sceneChangeFlg = true;
                sceneChangeParam = parameter;

                break;

            default:
                Debug.LogWarning($"[ConversationUI] 未知のコマンド: {command}");
                break;
        }
    }

    /// <summary>
    /// キャラ名からスプライト画像を読み込む
    /// Resources/Image/Sanmenzu/{キャラ名}.png から {キャラ名}_0 のスプライトを取得
    /// </summary>
    Sprite LoadCharacterSprite(string characterName)
    {
        // Resources/Image/Sanmenzu/{キャラ名}_0 を読み込む
        string spritePath = $"Image/Sanmenzu/{characterName}立ち絵";
        
        // スライスされたスプライトを読み込む
        Sprite[] sprites = Resources.LoadAll<Sprite>(spritePath);
        
        if (sprites != null && sprites.Length > 0)
        {
            // {キャラ名}_0 のスプライトを探す
            foreach (Sprite sprite in sprites)
            {
                if (sprite.name == $"{characterName}立ち絵_0")
                {
                    Debug.Log($"キャラ画像読み込み成功: {sprite.name}");
                    return sprite;
                }
            }   
            
            // _0が見つからなければ最初のスプライトを返す
            Debug.Log($"キャラ画像読み込み（最初のスプライト）: {sprites[0].name}");
            return sprites[0];
        }
        
        Debug.LogWarning($"キャラ画像が見つかりません: {spritePath}");
        return null;
    }

    void CreateSampleCSV(string filePath)
    {
        string sampleContent = @"Name,Dialogue
                                主人公,こんにちは！はじめまして。
                                友達,よろしくお願いします！
                                主人公,今日はいい天気ですね。
                                友達,そうですね、散歩でもしませんか？
                                主人公,いいアイデアですね！";

        try
        {
            File.WriteAllText(filePath, sampleContent, System.Text.Encoding.UTF8);
            Debug.Log($"サンプルCSVファイルを作成しました: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"サンプルCSVファイルの作成エラー: {e.Message}");
        }
    }

    void LoadTestDialogues()
    {
        Debug.Log("[ConversationUI] LoadTestDialogues呼び出し - テストデータを使用します");
        
        // テスト用データをDialogueDataに変換
        dialogues.Clear();

        if (testDialogues.Count == 0)
        {
            Debug.LogWarning("[ConversationUI] testDialoguesが空です。デフォルトのテストデータを追加します。");
            testDialogues.Add("あ");
            testDialogues.Add("い");
            testDialogues.Add("う");
            testDialogues.Add("Thank you for playing!");
        }

        foreach (string text in testDialogues)
        {
            dialogues.Add(new DialogueData("システム", text));
        }
        
        Debug.Log($"[ConversationUI] テストデータ読み込み完了: {dialogues.Count}件");
    }

    public void StartDialogue(bool DisabledEnemyFlg)
    {
        Debug.Log($"[ConversationUI] StartDialogue呼び出し: dialogues.Count={dialogues.Count}");
        
        if (dialogues.Count == 0)
        {
            Debug.LogWarning("[ConversationUI] 会話データが0件のため、StartDialogueを中断します。CSVの読み込みに失敗している可能性があります。");
            return;
        }

        currentDialogueIndex = 0;
        
        // 前回のタイピングコルーチンを停止
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            isTyping = false;
        }
        
        // 前回の表示内容をクリア
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
        if (characterNameText != null)
        {
            characterNameText.text = "";
        }
        
        dialoguePanel.SetActive(true);
        characterImage.enabled = true;
        // パネルをフェードイン
        CanvasGroup canvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1, fadeInDuration);
        }

        disabledEnemy = DisabledEnemyFlg;
        
        // プレイヤーと敵を無効化
        if (DisabledEnemyFlg)
        {
            DisablePlayerControl();
            DisableAllEnemies();
        }

        ShowDialogue(currentDialogueIndex);
    }

    //StartDialogueのオーバーロード、ムービを使用するが敵が存在しないフィールドではフラグの受け渡しが不必要なため。
    public void StartDialogue()
    {
        Debug.Log($"[ConversationUI] StartDialogue呼び出し（オーバーロード）: dialogues.Count={dialogues.Count}");
        
        if (dialogues.Count == 0)
        {
            Debug.LogWarning("[ConversationUI] 会話データが0件のため、StartDialogueを中断します。CSVの読み込みに失敗している可能性があります。");
            return;
        }

        currentDialogueIndex = 0;
        
        // 前回のタイピングコルーチンを停止
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            isTyping = false;
        }
        
        // 前回の表示内容をクリア
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
        if (characterNameText != null)
        {
            characterNameText.text = "";
        }
        
        dialoguePanel.SetActive(true);
        characterImage.enabled = true;
        // パネルをフェードイン
        CanvasGroup canvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1, fadeInDuration);
        }

        ShowDialogue(currentDialogueIndex);
    }

    /// <summary>
    /// CSVファイルを指定して会話を開始（Timeline用）
    /// </summary>
    public void StartDialogueWithCSV(string csvFile)
    {
        Debug.Log($"========== StartDialogueWithCSV が呼ばれました！CSV: {csvFile} ==========");
        csvFileName = csvFile;
        currentPlayableDirector = null; // リセット
        StartCoroutine(ReloadCSVAndStartDialogue(true));
    }

    /// <summary>
    /// CSVファイルとPlayableDirectorを指定して会話を開始（Timeline用・拡張版）
    /// </summary>
    public void StartDialogueWithCSVAndTimeline(string csvFile, UnityEngine.Playables.PlayableDirector director)
    {
        Debug.Log($"========== StartDialogueWithCSVAndTimeline が呼ばれました！CSV: {csvFile} ==========");
        csvFileName = csvFile;
        currentPlayableDirector = director;
        StartCoroutine(ReloadCSVAndStartDialogue(false));
    }

    /// <summary>
    /// CSV読み込み完了後に会話を開始
    /// </summary>
    private IEnumerator ReloadCSVAndStartDialogue(bool disableEnemies)
    {
        if (useCSVFile)
        {
            yield return StartCoroutine(LoadDialogueFromCSVCoroutine());
            Debug.Log($"[ConversationUI] CSV読み込み完了。dialogues.Count={dialogues.Count}");
        }

        if (disableEnemies)
        {
            StartDialogue(true);
        }
        else
        {
            StartDialogue();
        }
    }

    void ShowDialogue(int index)
    {
        if (index >= dialogues.Count) return;

        DialogueData data = dialogues[index];
        
        // 表示前にテキストをクリア（前の会話が残らないように）
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }

        // キャラクター名設定
        if (characterNameText != null)
        {
            characterNameText.text = data.characterName;
            Debug.Log($"キャラクター名設定: {data.characterName}");
            Debug.Log($"CharacterNameText位置: {characterNameText.transform.position}");
            Debug.Log($"CharacterNameTextサイズ: {characterNameText.fontSize}");
            Debug.Log($"CharacterNameText色: {characterNameText.color}");
        }
        else
        {
            Debug.LogWarning("CharacterNameTextが設定されていません！");
        }

        // キャラクター画像設定
        if (characterImage != null && data.characterImage != null)
        {
            characterImage.enabled = true;

            // アスペクト比調整
            AspectRatioFitter aspect = characterImage.GetComponent<AspectRatioFitter>();
            float ratio = (float)data.characterImage.rect.width / data.characterImage.rect.height;
            aspect.aspectRatio = ratio;
            characterImage.sprite = data.characterImage;
        }
        else
        {
            characterImage.enabled = false;
        }

            // テキストをタイプライター効果で表示
            if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypewriterEffect(data.dialogueText));

        // ログに追加
        AddToLog(data.characterName, data.dialogueText);
    }

    IEnumerator TypewriterEffect(string text)
    {
        isTyping = true;
        dialogueText.text = "";

        for (int i = 0; i < text.Length; i++)
        {
            dialogueText.text += text[i];
            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
    }

    public void NextDialogue()
    {
        if (isTyping)
        {
            // タイピング中の場合は即座に全文表示
            if (typingCoroutine != null)
                StopCoroutine(typingCoroutine);

            DialogueData data = dialogues[currentDialogueIndex];
            dialogueText.text = data.dialogueText;
            isTyping = false;
            return;
        }

        currentDialogueIndex++;

        if (currentDialogueIndex >= dialogues.Count)
        {
            EndDialogue();
        }
        else
        {
            ShowDialogue(currentDialogueIndex);
        }
    }

    void EndDialogue()
    {
        // パネルをフェードアウト
        CanvasGroup canvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0, fadeInDuration)
                .OnComplete(() => {
                    dialoguePanel.SetActive(false);
                    OnDialogueComplete();
                });

            characterImage.enabled = false;
        }
        else
        {
            dialoguePanel.SetActive(false);
            characterImage.enabled = false;
            OnDialogueComplete();
        }
    }

    /// <summary>
    /// 会話完了時の処理
    /// </summary>
    void OnDialogueComplete()
    {

        Debug.Log("[ConversationUI] 会話が終了しました");
        
        // Timeline停止（設定されている場合）
        if (stopTimelineOnEnd)
        {
            StopActiveTimeline();
        }
        
        // プレイヤー操作と敵の動きを再開
        Debug.Log($"[ConversationUI] enablePlayerControlOnEnd={enablePlayerControlOnEnd}, playerObject={playerObject}");
        if (enablePlayerControlOnEnd && playerObject != null)
        {
            EnablePlayerControl();
            EnableAllEnemies();
        }
        else if (enablePlayerControlOnEnd && playerObject == null)
        {
            Debug.LogWarning("[ConversationUI] playerObjectがnullのためプレイヤー操作を再開できません");
        }
        
        // 会話終了イベントを発火
        onDialogueEnd?.Invoke();

        // デバッグ：現在のCSVファイル名を確認
        Debug.Log($"[ConversationUI] 現在のcsvFileName: '{csvFileName}'");

        // 戦闘イベントをチェック
        CheckAndStartBattleEvent();

        // シーン遷移
        if(sceneChangeFlg) //csvコマンドでSCENE_CHANGEコマンドがある場合
            SceneManager.LoadScene(sceneChangeParam);

        if (loadSceneOnEnd && !string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log($"会話終了。シーン遷移: {nextSceneName}");
            
            // SceneTransitionManagerがあれば使用、なければ通常遷移
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadSceneWithAutoTransition(nextSceneName);
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
    }
    
    /// <summary>
    /// 現在再生中のTimelineを停止
    /// </summary>
    private void StopActiveTimeline()
    {
        // 優先順位: currentPlayableDirector > defaultPlayableDirector > 自動検索
        UnityEngine.Playables.PlayableDirector directorToStop = null;
        
        if (currentPlayableDirector != null)
        {
            directorToStop = currentPlayableDirector;
        }
        else if (defaultPlayableDirector != null)
        {
            directorToStop = defaultPlayableDirector;
        }
        else
        {
            // 再生中のPlayableDirectorを自動検索
            var allDirectors = FindObjectsOfType<UnityEngine.Playables.PlayableDirector>();
            foreach (var director in allDirectors)
            {
                if (director.state == UnityEngine.Playables.PlayState.Playing)
                {
                    directorToStop = director;
                    Debug.Log($"[ConversationUI] 再生中のTimelineを自動検出: {director.name}");
                    break;
                }
            }
        }
        
        if (directorToStop != null)
        {
            Debug.Log($"[ConversationUI] Timelineを停止します: {directorToStop.name}");
            directorToStop.Stop();
            currentPlayableDirector = null;
        }
        else
        {
            Debug.LogWarning("[ConversationUI] 停止するTimelineが見つかりませんでした");
        }
    }
    
    /// <summary>
    /// プレイヤー操作を有効化
    /// </summary>
    /// <summary>
    /// プレイヤーの操作を無効化
    /// </summary>
    private void DisablePlayerControl()
    {
        if (playerObject == null)
        {
            // playerObjectがnullなら自動検索
            playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                Debug.LogWarning("[ConversationUI] プレイヤーが見つかりません");
                return;
            }
        }
        
        // MonoBehaviourコンポーネントを無効化
        var scripts = playerObject.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && script.enabled && script.GetType().Name.Contains("Player"))
            {
                script.enabled = false;
                Debug.Log($"[ConversationUI] {script.GetType().Name} を無効化しました");
            }
        }
        
        // CharacterController
        var characterController = playerObject.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        
        // Rigidbody
        var rb = playerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        
        // AnimatorをIdleにする
        SetPlayerAnimatorIdle();
    }
    
    /// <summary>
    /// プレイヤーのAnimatorをIdle状態にする
    /// </summary>
    private void SetPlayerAnimatorIdle()
    {
        if (playerObject == null) return;
        
        var animator = playerObject.GetComponent<Animator>();
        if (animator == null) return;
        
        // isMovingパラメータをfalseにする（存在する場合）
        foreach (var param in animator.parameters)
        {
            if (param.name == "isMoving" && param.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool("isMoving", false);
                Debug.Log($"[ConversationUI] Animator: isMoving を false に設定しました");
            }
            else if (param.name == "isDash" && param.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool("isDash", false);
            }
            else if (param.name == "Speed" && param.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat("Speed", 0f);
            }
            else if (param.name == "MoveSpeed" && param.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat("MoveSpeed", 0f);
            }
        }
    }
    
    /// <summary>
    /// プレイヤーの操作を有効化
    /// </summary>
    private void EnablePlayerControl()
    {
        if (playerObject == null) return;
        
        // MonoBehaviourコンポーネントを有効化
        var scripts = playerObject.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && !script.enabled && script.GetType().Name.Contains("Player"))
            {
                script.enabled = true;
                Debug.Log($"[ConversationUI] {script.GetType().Name} を有効化しました");
            }
        }
        
        // CharacterController
        var characterController = playerObject.GetComponent<CharacterController>();
        if (characterController != null && !characterController.enabled)
        {
            characterController.enabled = true;
        }
        
        // Rigidbody
        var rb = playerObject.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
        }
    }

    /// <summary>
    /// フィールド上の全ての敵を停止&非表示
    /// </summary>
    private void DisableAllEnemies()
    {
        // 全てのEnemyWanderAIを検索
        EnemyWanderAI[] enemies = FindObjectsOfType<EnemyWanderAI>();

        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                // 徘徊を停止
                enemy.StopWandering();

                // オブジェクトを非表示
                enemy.gameObject.SetActive(false);

                Debug.Log($"[ConversationUI] {enemy.gameObject.name} を停止&非表示にしました");
            }
        }
    }
    
    /// <summary>
    /// フィールド上の全ての敵を再開&表示
    /// </summary>
    public void EnableAllEnemies()
    {
        if (!disabledEnemy) return;

        // 非表示の敵を含めて全てを検索（includeInactive: true）
        EnemyWanderAI[] enemies = FindObjectsOfType<EnemyWanderAI>(true);

        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                // オブジェクトを表示
                enemy.gameObject.SetActive(true);

                // 徘徊を再開
                enemy.ResumeWandering();

            }
        }
    }

    void Update()
    {

        // ログビューアの表示切り替え：Lキー
        if (Input.GetKeyDown(KeyCode.L))
        {
            ToggleLogViewer();
        }

        // 会話中の操作：スペースキーまたはマウスクリックで次の会話に進める
        // ログ表示中は会話操作を無効にする
        bool isLogViewerOpen = logPanel != null && logPanel.activeInHierarchy;
        if (dialoguePanel.activeInHierarchy && !isLogViewerOpen &&
        (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            NextDialogue();
        }

        // ログビューア中の操作：ESCキーで閉じる
        if (logPanel != null && logPanel.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseLogViewer();
        }
    }

    // ログ機能
    void AddToLog(string characterName, string dialogueText)
    {
        DialogueLogEntry newEntry = new DialogueLogEntry(characterName, dialogueText);
        dialogueLog.Add(newEntry);

        // 最大件数を超えた場合は古いログを削除
        if (dialogueLog.Count > MAX_LOG_ENTRIES)
        {
            dialogueLog.RemoveAt(0);
        }

        Debug.Log($"ログに追加: {characterName} - {dialogueText}");
    }

    public void ToggleLogViewer()
    {
        if (logPanel == null) return;

        if (logPanel.activeInHierarchy)
        {
            CloseLogViewer();
        }
        else
        {
            OpenLogViewer();
        }
    }

        public void OpenLogViewer()
    {
        if (logPanel == null) return;
        
        logPanel.SetActive(true);
        
        // スクロールバーを非表示にする
        HideScrollbars();
        
        RefreshLogDisplay();
        
        // ログパネルをフェードイン
        CanvasGroup logCanvasGroup = logPanel.GetComponent<CanvasGroup>();
        if (logCanvasGroup != null)
        {
            logCanvasGroup.alpha = 0;
            logCanvasGroup.DOFade(1, fadeInDuration);
        }
    }

    public void CloseLogViewer()
    {
        if (logPanel == null) return;

        // ログパネルをフェードアウト
        CanvasGroup logCanvasGroup = logPanel.GetComponent<CanvasGroup>();
        if (logCanvasGroup != null)
        {
            logCanvasGroup.DOFade(0, fadeInDuration)
                .OnComplete(() => {
                    logPanel.SetActive(false);
                });
        }
        else
        {
            logPanel.SetActive(false);
        }
    }

    void RefreshLogDisplay()
    {
        Debug.Log($"RefreshLogDisplay開始 - ログ件数: {dialogueLog.Count}");
        Debug.Log($"logContent: {logContent}");
        Debug.Log($"logEntryPrefab: {logEntryPrefab}");

        if (logContent == null || logEntryPrefab == null)
        {
            Debug.LogError("logContentまたはlogEntryPrefabがnullです！");
            return;
        }

        // 既存のログエントリを削除
        foreach (Transform child in logContent)
        {
            Destroy(child.gameObject);
        }

        // Layout Groupを一時的に無効にしてアニメーション中の干渉を防ぐ
        LayoutGroup layoutGroup = logContent.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
        }

        // ログエントリを作成（新しい順に表示、右から左に流れるアニメーション）
        for (int i = dialogueLog.Count - 1; i >= 0; i--)
        {
            DialogueLogEntry entry = dialogueLog[i];
            Debug.Log($"ログエントリ作成中: {entry.characterName} - {entry.dialogueText}");

            GameObject logEntry = Instantiate(logEntryPrefab, logContent);
            Debug.Log($"プレハブ作成完了: {logEntry.name}");

            // RectTransformの設定（アニメーション用）
            RectTransform rect = logEntry.GetComponent<RectTransform>();
            if (rect != null)
            {
                Debug.Log($"RectTransform設定: {rect.anchoredPosition}, {rect.sizeDelta}");

                // 右から左へのスライドアニメーション（全部同時に）
                StartCoroutine(AnimateLogEntry(rect, 0));
            }

            // LogEntryUIコンポーネントがある場合はそれを使用
            LogEntryUI logEntryUI = logEntry.GetComponent<LogEntryUI>();
            if (logEntryUI != null)
            {
                Debug.Log("LogEntryUIコンポーネント使用");
                int displayIndex = dialogueLog.Count - 1 - i;
                bool isEvenRow = displayIndex % 2 == 0;
                logEntryUI.SetupLogEntry(entry.timestamp, entry.characterName, entry.dialogueText, isEvenRow);
            }
            else
            {
                Debug.Log("フォールバック：直接テキスト設定");
                // フォールバック：TextMeshProUGUIコンポーネントを直接設定
                TextMeshProUGUI[] texts = logEntry.GetComponentsInChildren<TextMeshProUGUI>();
                Debug.Log($"見つかったTextコンポーネント数: {texts.Length}");
                if (texts.Length >= 3)
                {
                    texts[0].text = entry.timestamp;          // 時刻
                    texts[1].text = entry.characterName;      // キャラクター名
                    texts[2].text = entry.dialogueText;       // 会話内容
                    Debug.Log($"テキスト設定完了: {texts[0].text}, {texts[1].text}, {texts[2].text}");
                }
            }
        }

        // スクロールを最上部に移動
        if (logScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            logScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>
    /// ログエントリの右から左へのスライドアニメーション（全部同時に流れるように）
    /// </summary>
    /// <param name="rectTransform">アニメーションするRectTransform</param>
    /// <param name="index">表示順のインデックス（遅延計算用、0で全部同時）</param>
    private IEnumerator AnimateLogEntry(RectTransform rectTransform, int index)
    {
        // Layout Groupを再有効化して正しい位置を計算
        LayoutGroup layoutGroup = logContent.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = true;
        }

        // Layout Groupが位置を設定するまで待つ
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); // 確実に位置が確定するまで待つ

        // Layout Group適用後の正しい位置を取得
        Vector2 finalPosition = rectTransform.anchoredPosition;

        // Layout Groupを再度無効にしてアニメーション中の干渉を防ぐ
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
        }

        // 初期位置を画面右側に設定（画面外から）
        Vector2 startPosition = new Vector2(Screen.width + 400f, finalPosition.y);
        rectTransform.anchoredPosition = startPosition;

        // 透明度を0にして非表示状態にする
        CanvasGroup canvasGroup = rectTransform.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f;

        // 全部同時に開始（遅延なし）
        yield return new WaitForSeconds(0f);

        // スライドイン + フェードインアニメーション
        Sequence animSequence = DOTween.Sequence();

        // 右から左へ流れるようにスライド
        animSequence.Append(rectTransform.DOAnchorPos(finalPosition, logEntrySlideInDuration)
            .SetEase(Ease.OutCubic));

        // 同時にフェードイン
        animSequence.Join(canvasGroup.DOFade(1f, logEntrySlideInDuration * 0.8f)
            .SetEase(Ease.OutQuad));

        // アニメーション完了後にLayout Groupを再有効化
        animSequence.OnComplete(() => {
            if (layoutGroup != null && index == 0) // 最初のアニメーションが完了したら
            {
                layoutGroup.enabled = true;
            }
        });

        // アニメーション開始
        animSequence.Play();
    }

    /// <summary>
    /// スクロールバーを非表示にして、縦スクロールのみに制限する
    /// </summary>
    private void HideScrollbars()
    {
        if (logScrollRect != null)
        {
            // 横スクロールを無効にして縦スクロールのみにする
            logScrollRect.horizontal = false;
            logScrollRect.vertical = true;
            
            // 垂直スクロールバーを非表示
            if (logScrollRect.verticalScrollbar != null)
            {
                logScrollRect.verticalScrollbar.gameObject.SetActive(false);
            }
            
            // 水平スクロールバーを非表示
            if (logScrollRect.horizontalScrollbar != null)
            {
                logScrollRect.horizontalScrollbar.gameObject.SetActive(false);
            }
        }
        
        // 念のため、子オブジェクトからScrollbarを探して非表示にする
        Scrollbar[] scrollbars = logPanel.GetComponentsInChildren<Scrollbar>();
        foreach (Scrollbar scrollbar in scrollbars)
        {
            scrollbar.gameObject.SetActive(false);
        }
    }
    
    // エディタ用：CSVファイルの再読み込み
    [ContextMenu("CSVファイルを再読み込み")]
    public void ReloadCSV()
    {
        if (useCSVFile)
        {
            LoadDialogueFromCSV();
            Debug.Log("CSVファイルを再読み込みしました。");
        }
    }

    // エディタ用：ログをクリア
    [ContextMenu("ログをクリア")]
    public void ClearLog()
    {
        dialogueLog.Clear();
        Debug.Log("ログをクリアしました。");
    }
    
    /// <summary>
    /// プレイヤーの位置を取得
    /// </summary>
    private Vector3 GetPlayerPosition()
    {
        // playerObjectが設定されている場合
        if (playerObject != null)
        {
            return playerObject.transform.position;
        }
        
        // playerObjectがnullの場合、自動検索
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerObject = player; // 次回のために保存
            return player.transform.position;
        }
        
        // プレイヤーが見つからない場合は原点を返す
        Debug.LogWarning("[ConversationUI] プレイヤーが見つかりません。原点を使用します。");
        return Vector3.zero;
    }
    
    /// <summary>
    /// 戦闘イベントをチェックして開始
    /// </summary>
    private void CheckAndStartBattleEvent()
    {
        // .csv拡張子を除去して正規化
        string normalizedCSVName = csvFileName.Replace(".csv", "");
        
        // 戦闘イベントリストから該当するイベントを検索
        BattleEventData battleEvent = battleEvents.Find(e => 
            e.dialogueCSV == normalizedCSVName || 
            e.dialogueCSV == csvFileName
        );
        
        if (battleEvent != null && battleEvent.isBossBattle || battleEvent != null && battleEvent.isMidBossBattle )
        {
            Debug.Log($"[ConversationUI] 戦闘イベント検出: {normalizedCSVName}");
            
            if (GameManager.Instance != null && BossObject != null)
            {
                // プレイヤーの位置を取得
                Vector3 playerPosition = GetPlayerPosition();
                Debug.Log($"[ConversationUI] プレイヤー位置: {playerPosition}");
                
                // BossObjectコンポーネントからエネミーデータを取得
                BossObject bossScript = BossObject.GetComponent<BossObject>();
                if (bossScript != null)
                {
                    var bossEnemyData = bossScript.GetEnemyData();
                    
                    //中ボスだったら中ボスのデータを入れる。
                    if (battleEvent.isMidBossBattle)
                    {
                        bossEnemyData = bossScript.GetMidEnemyData();
                    }
                        
                    if (bossEnemyData != null && bossEnemyData.Count > 0)
                    {
                        Debug.Log($"[ConversationUI] ボスエネミーデータ取得成功: {bossEnemyData.Count}体");
                        Debug.Log($"[ConversationUI] 戦闘中イベント数: {battleEvent.battleMidEvents.Count}");
                        
                        // BGM名を決定（中ボスの場合は通常戦闘BGM）
                        string bgmName = battleEvent.isMidBossBattle ? "BattleNormal" : "BattleBoss";
                        
                        // ボス戦として開始（戦闘中イベント付き）
                        GameManager.Instance.StartBossBattle(
                            playerPosition,
                            bossEnemyData,
                            battleEvent.battleMidEvents,
                            battleEvent.postBattleDialogueCSV,
                            bgmName,
                            battleEvent.isMidBossBattle  // 中ボスフラグを渡す
                        );
                    }
                    else
                    {
                        Debug.LogWarning("[ConversationUI] BossObjectにエネミーデータが設定されていません");
                    }
                }
                else
                {
                    Debug.LogWarning("[ConversationUI] BossObjectにBossObjectコンポーネントが見つかりません");
                }
            }
            else
            {
                if (GameManager.Instance == null)
                    Debug.LogWarning("[ConversationUI] GameManagerが見つかりません");
                if (BossObject == null)
                    Debug.LogWarning("[ConversationUI] BossObjectが設定されていません");
            }
        }
        else
        {
            Debug.Log($"[ConversationUI] 戦闘イベントではありません（csvFileName: '{normalizedCSVName}'）");
        }
    }
}
