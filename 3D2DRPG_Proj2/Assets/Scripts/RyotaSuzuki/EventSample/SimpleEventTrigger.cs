using UnityEngine;

/// <summary>
/// シンプルなイベントトリガー
/// 既存のConversationUIと連携して会話イベントを発生させる
/// </summary>
[RequireComponent(typeof(Collider))]
public class SimpleEventTrigger : MonoBehaviour
{
    [Header("イベント設定")]
    [SerializeField,Header("任意のイベントID")] public string eventId = "";
    [SerializeField,Header("任意のイベント名")] private string eventName = "";
    [SerializeField,Header("イベントタイプ")] private EventType eventType = EventType.Conversation;
    
    [Header("発動条件")]
    [SerializeField, Header("発動条件にタグを使用する")] private bool requirePlayerTag = true;
    [SerializeField, Header("タグ名")] private string playerTag = "Player";
    [SerializeField, Header("指定のフラグが全てtrueなら発動")] private string[] requiredFlags; // このフラグがtrueなら発動
    [SerializeField, Header("指定のフラグがtrueなら発動しない")] private string[] prohibitedFlags; // このフラグがtrueなら発動しない

    [Header("会話設定")]
    [SerializeField] private ConversationUI conversationUI;
    [SerializeField,Header("発動するイベントcsv")] private string csvFileName = "scenario01.csv";
    
    [Header("ムービー設定")]
    [SerializeField] private CineController cineController;
    
    [Header("プレイヤー制御")]
    [SerializeField, Tooltip("イベント中プレイヤー操作を無効化するか")]
    private bool disablePlayerControl = true;
    
    [SerializeField, Tooltip("プレイヤーオブジェクト（nullなら自動検索）")]
    private GameObject playerObject;
    
    [Header("発動後の処理")]
    [SerializeField, Header("発動後、以下の処理を行うか")] private bool setFlagAfterEvent = true;
    [SerializeField, Header("処理を行うイベントID")] private string flagToSet = "";
    [SerializeField, Header("一度のみのフラグ立てにする？")] private bool oneTimeOnly = true; // 一度だけ発動
    [SerializeField, Header("イベント後フラグを削除する？")] private bool destroyAfterTrigger = false; // 発動後に削除
    
    [Header("発動方法")]
    [SerializeField] private TriggerType triggerType = TriggerType.AutoOnEnter;

    [Header("敵制御")]
    [SerializeField, Tooltip("イベント中に敵を停止&非表示にするか")]
    private bool disableEnemies = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLog = true;
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private Color gizmoColor = Color.yellow;
    
    private bool hasTriggered = false;
    private bool playerInRange = false;
    
    public enum TriggerType
    {
        AutoOnEnter,    // 入った瞬間に自動発動
        ButtonPress     // 範囲内でボタン押したら発動
    }
    
    public enum EventType
    {
        Conversation,   // 会話のみ
        Movie           // ムービーのみ
    }
    
    void Start()
    {
        // Colliderの設定確認
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // ConversationUIを自動検索
        if (conversationUI == null && eventType == EventType.Conversation)
        {
            conversationUI = FindObjectOfType<ConversationUI>();
            if (conversationUI == null && showDebugLog)
            {
                Debug.LogWarning($"[{eventName}] ConversationUIが見つかりません！");
            }
        }
        
        // CineControllerを自動検索
        if (cineController == null && eventType == EventType.Movie)
        {
            cineController = FindObjectOfType<CineController>();
            if (cineController == null && showDebugLog)
            {
                Debug.LogWarning($"[{eventName}] CineControllerが見つかりません！");
            }
        }
        
        // プレイヤーを自動検索
        if (playerObject == null && disablePlayerControl)
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null && showDebugLog)
            {
                Debug.LogWarning($"[{eventName}] Playerタグのオブジェクトが見つかりません！");
            }
        }
    }
    
    void Update()
    {
        // ボタン押下タイプの場合
        if (triggerType == TriggerType.ButtonPress && playerInRange && !hasTriggered)
        {
            // Eキーまたはスペースで発動
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            {
                TriggerEvent();
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // プレイヤータグチェック
        if (requirePlayerTag && !other.CompareTag(playerTag))
            return;
        
        playerInRange = true;
        
        // 自動発動タイプの場合
        if (triggerType == TriggerType.AutoOnEnter)
        {
            TriggerEvent();
        }
        else if (showDebugLog)
        {
            Debug.Log($"[{eventName}] プレイヤーが範囲内に入りました（Eキーで発動）");
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (requirePlayerTag && !other.CompareTag(playerTag))
            return;
        
        playerInRange = false;
        
        if (showDebugLog && triggerType == TriggerType.ButtonPress)
        {
            Debug.Log($"[{eventName}] プレイヤーが範囲外に出ました");
        }
    }

    /// <summary>
    /// 他スクリプトから直接イベントを発火させる場合
    /// </summary>
    public void StartEvent(string EventID)
    {
        this.eventId = EventID;

        TriggerEvent();
    }

    /// <summary>
    /// イベントを発動
    /// </summary>
    void TriggerEvent()
    {
        // EventFlagManagerのフラグで発動済みかチェック
        if (oneTimeOnly && !string.IsNullOrEmpty(eventId))
        {
            string triggeredFlagName = $"{eventId}_triggered";
            if (EventFlagManager.Instance.GetFlag(triggeredFlagName))
            {
                if (showDebugLog)
                {
                    Debug.Log($"[{eventName}] 既に発動済みです");
                }
                return;
            }
        }

        // フラグチェック：必須フラグ
        if (requiredFlags != null && requiredFlags.Length > 0)
        {
            foreach (string flag in requiredFlags)
            {
                if (!EventFlagManager.Instance.GetFlag(flag))
                {
                    if (showDebugLog)
                    {
                        Debug.Log($"[{eventName}] 必須フラグ「{flag}」がfalseのため発動しません");
                    }
                    return;
                }
            }
        }
        
        // フラグチェック：禁止フラグ
        if (prohibitedFlags != null && prohibitedFlags.Length > 0)
        {
            foreach (string flag in prohibitedFlags)
            {
                if (EventFlagManager.Instance.GetFlag(flag))
                {
                    if (showDebugLog)
                    {
                        Debug.Log($"[{eventName}] 禁止フラグ「{flag}」がtrueのため発動しません");
                    }
                    return;
                }
            }
        }
        
        // イベントタイプに応じて実行
        switch (eventType)
        {
            case EventType.Conversation:
                ExecuteConversation();
                break;
                
            case EventType.Movie:
                ExecuteMovie();
                break;
        }

        // 発動フラグを記録
        if (oneTimeOnly && !string.IsNullOrEmpty(eventId))
        {
            string triggeredFlagName = $"{eventId}_triggered";
            EventFlagManager.Instance.SetFlag(triggeredFlagName, true);
        }

        // 別フラグを設定
        if (setFlagAfterEvent && !string.IsNullOrEmpty(flagToSet))
        {
            EventFlagManager.Instance.SetFlag(flagToSet, true);
        }
        
        hasTriggered = true;
        
        // 発動後に削除
        if (destroyAfterTrigger)
        {
            Destroy(gameObject, 0.5f);
        }
    }
    
    /// <summary>
    /// 会話イベントを実行
    /// </summary>
    private void ExecuteConversation()
    {
        if (conversationUI != null)
        {
            // プレイヤー操作を無効化
            if (disablePlayerControl)
            {
                DisablePlayerControl();
            }

            //全敵を停止
            DisableAllEnemies();

            // CSV読み込み完了を待ってから会話を開始
            conversationUI.StartDialogueWithCSV(csvFileName);
            
            if (showDebugLog)
            {
                Debug.Log($"[{eventName}] 会話イベント発動！CSV: {csvFileName}");
            }
        }
        else
        {
            Debug.LogError($"[{eventName}] ConversationUIがnullです！");
        }
    }
    
    /// <summary>
    /// ムービーイベントを実行
    /// </summary>
    private void ExecuteMovie()
    {
        if (cineController != null)
        {
            //全敵を停止
            DisableAllEnemies();

            cineController.PlayMovie(disableEnemies);
            
            if (showDebugLog)
            {
                Debug.Log($"[{eventName}] ムービーイベント発動！");
            }
        }
        else
        {
            Debug.LogError($"[{eventName}] CineControllerがnullです！");
        }
    }
    
    /// <summary>
    /// プレイヤー操作を無効化
    /// </summary>
    private void DisablePlayerControl()
    {
        if (playerObject == null) return;
        
        // MonoBehaviourコンポーネントを無効化
        var scripts = playerObject.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && script.enabled && script.GetType().Name.Contains("Player"))
            {
                script.enabled = false;
                if (showDebugLog)
                {
                    Debug.Log($"[{eventName}] {script.GetType().Name} を無効化しました");
                }
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
                if (showDebugLog)
                {
                    Debug.Log($"[{eventName}] Animator: isMoving を false に設定しました");
                }
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
    /// フィールド上の全ての敵を停止&非表示
    /// </summary>
    public void DisableAllEnemies()
    {
        if (!disableEnemies) return;

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

                if (showDebugLog)
                {
                    Debug.Log($"[{eventName}] {enemy.gameObject.name} を停止&非表示にしました");
                }
            }
        }
    }

    /// <summary>
    /// 外部から強制発動
    /// </summary>
    [ContextMenu("イベントを強制発動")]
    public void ForceTrigger()
    {
        hasTriggered = false;
        TriggerEvent();
    }
    
    /// <summary>
    /// リセット（再発動可能にする）
    /// </summary>
    [ContextMenu("イベントをリセット")]
    public void ResetTrigger()
    {
        hasTriggered = false;
        if (showDebugLog)
        {
            Debug.Log($"[{eventName}] イベントをリセットしました");
        }
    }
    
    /// <summary>
    /// ギズモ表示
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmo) return;
        
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
}
