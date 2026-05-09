using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// NavMeshを使って徘徊し、プレイヤーを発見したら追跡する敵のAI
/// </summary>
public class EnemyWanderAI : MonoBehaviour
{
    [Header("徘徊設定")]
    [SerializeField] private float wanderRadius = 10f;          // 徘徊範囲
    [SerializeField] private float wanderTimer = 5f;           // 次の目標地点までの時間
    [SerializeField] private float minWaitTime = 1f;           // 最小待機時間
    [SerializeField] private float maxWaitTime = 3f;           // 最大待機時間
    
    [Header("プレイヤー検知設定")]
    [SerializeField] private float detectionRadius = 8f;       // プレイヤー検知範囲
    [SerializeField] private float viewAngle = 120f;           // 視野角度
    [SerializeField] private float chaseSpeed = 5f;            // 追跡時の速度
    [SerializeField] private float losePlayerDistance = 15f;   // プレイヤーを見失う距離
    [SerializeField] private float losePlayerTime = 3f;        // プレイヤーを見失うまでの時間
    [SerializeField] private string playerTag = "Player";      // プレイヤーのタグ
    [SerializeField] private LayerMask obstacleLayer;          // 障害物レイヤー
    
    [Header("!マーク設定")]
    [SerializeField] private GameObject alertMarkPrefab;       // !マークのPrefab
    [SerializeField] private Vector3 alertMarkOffset = new Vector3(0, 2f, 0); // !マークの位置オフセット
    [SerializeField] private float alertMarkDuration = 1f;     // !マーク表示時間
    [SerializeField] private bool useSimpleAlert = true;       // シンプルな!マークを使用
    
    [Header("デバッグ設定")]
    [SerializeField] private bool showDebugInfo = true;        // デバッグ情報表示
    [SerializeField] private bool showWanderRange = true;      // 徘徊範囲表示
    [SerializeField] private bool showDetectionRange = true;   // 検知範囲表示
    [SerializeField] private Color debugColor = Color.red;     // デバッグ色

    [Header("エネミー情報")]
    [SerializeField] private List<CharacterData> enemyData;    // エネミーデータリスト
    [SerializeField] private int encounterGroupId = -1;        // エンカウントグループID
    [SerializeField] private string bgmName = "BattleNormal";  // 戦闘BGM名

    [Header("イベントフラグ")]
    [SerializeField]private string detectionFlagName = "EnemyDetectedPlayer"; // プレイヤー検知フラグ名
    [SerializeField]private bool setFlagOnDetection = true; // 検知時にフラグを設定するか

    private NavMeshAgent navMeshAgent;
    private Vector3 initialPosition;
    private float timer;
    private bool isWaiting = false;
    
    // 追跡関連
    private Transform playerTransform;
    private bool isChasing = false;
    private float normalSpeed;
    private float losePlayerTimer = 0f;
    private GameObject currentAlertMark;
    
    void Start()
    {
        // NavMeshAgentを取得
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgent が見つかりません！");
            enabled = false;
            return;
        }
        
        // 初期位置を記録
        initialPosition = transform.position;
        normalSpeed = navMeshAgent.speed;
        
        // プレイヤーを検索
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerTransform = player.transform;
        }
        
        // 最初の目標地点を設定
        SetRandomDestination();
        
        if (showDebugInfo)
        {
            Debug.Log($"EnemyWanderAI: {gameObject.name} の徘徊開始");
            Debug.Log($"徘徊範囲: {wanderRadius}m, 移動間隔: {wanderTimer}秒");
            Debug.Log($"プレイヤー検知範囲: {detectionRadius}m, 視野角: {viewAngle}度");
        }
    }
    
    void Update()
    {
        // プレイヤーを検知
        if (!isChasing && playerTransform != null)
        {
            if (CanSeePlayer())
            {
                StartChasing();
            }
        }
        
        // 追跡中の処理
        if (isChasing)
        {
            ChasePlayer();
        }
        // 徘徊中の処理
        else if (!isWaiting)
        {
            // タイマー更新
            timer += Time.deltaTime;
            
            // 目標地点に到着したか、時間が経過したら新しい目標を設定
            if ((!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f) || timer >= wanderTimer)
            {
                StartCoroutine(WaitAndSetNewDestination());
            }
        }


        // VFXテスト用（開発中のみ）
        if (Input.GetKeyDown(testVFXKey))
        {
            TestExplosionVFX();
        }
    }


    /// <summary>
    /// エネミーデータを設定を取得
    /// </summary>
    public List<CharacterData> GetEnemyData()
    {
        return enemyData;
    }

    /// <summary>
    /// エンカウントグループIDを取得
    /// </summary>
    public int GetEncounterGroupId()
    {
        return encounterGroupId;
    }

    /// <summary>
    /// エンカウントグループIDを設定（Respawnから呼ばれる）
    /// </summary>
    public void SetEncounterGroupId(int groupId)
    {
        encounterGroupId = groupId;
    }

    /// <summary>
    /// エネミーデータを設定（Respawnから呼ばれる）
    /// </summary>
    public void SetEnemyData(List<CharacterData> data)
    {
        enemyData = data;
    }

    /// <summary>
    /// BGM名を取得
    /// </summary>
    public string GetBGMName()
    {
        return bgmName;
    }

    /// <summary>
    /// BGM名を設定（Respawnから呼ばれる）
    /// </summary>
    public void SetBGMName(string name)
    {
        bgmName = name;
    }


    /// <summary>
    /// ランダムな目標地点を設定
    /// </summary>
    private void SetRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += initialPosition;
        
        NavMeshHit hit;
        Vector3 finalPosition = Vector3.zero;
        
        // NavMesh上の有効な地点を探す
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1))
        {
            finalPosition = hit.position;
        }
        else
        {
            // 有効な地点が見つからない場合は初期位置周辺を再試行
            for (int i = 0; i < 10; i++)
            {
                Vector3 fallbackDirection = Random.insideUnitSphere * wanderRadius * 0.5f;
                fallbackDirection += initialPosition;
                
                if (NavMesh.SamplePosition(fallbackDirection, out hit, wanderRadius, 1))
                {
                    finalPosition = hit.position;
                    break;
                }
            }
        }
        
        // 有効な目標地点が見つかった場合のみ移動
        if (finalPosition != Vector3.zero)
        {
            navMeshAgent.SetDestination(finalPosition);
            timer = 0f;
            
            if (showDebugInfo)
            {
                Debug.Log($"{gameObject.name}: 新しい目標地点 {finalPosition}");
            }
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"{gameObject.name}: 有効な目標地点が見つかりませんでした");
            }
        }
    }
    
    /// <summary>
    /// 待機してから新しい目標地点を設定
    /// </summary>
    private IEnumerator WaitAndSetNewDestination()
    {
        isWaiting = true;
        navMeshAgent.ResetPath(); // 現在の移動を停止
        
        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: {waitTime:F1}秒待機中...");
        }
        
        yield return new WaitForSeconds(waitTime);
        
        isWaiting = false;
        SetRandomDestination();
    }
    
    /// <summary>
    /// プレイヤーを視認できるかチェック
    /// </summary>
    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;
        
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;
        
        // 検知範囲内かチェック
        if (distanceToPlayer > detectionRadius) return false;
        
        // 視野角内かチェック
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > viewAngle * 0.5f) return false;
        
        // 障害物チェック（オプション）
        if (obstacleLayer.value != 0)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToPlayer.normalized, out hit, distanceToPlayer, obstacleLayer))
            {
                return false; // 障害物がある
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 追跡を開始
    /// </summary>
    private void StartChasing()
    {
        isChasing = true;
        isWaiting = false;
        StopAllCoroutines();
        
        // 速度を変更
        navMeshAgent.speed = chaseSpeed;
        
        // !マークを表示
        ShowAlertMark();
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: プレイヤー発見！追跡開始");
        }
    }
    
    /// <summary>
    /// プレイヤーを追跡
    /// </summary>
    private void ChasePlayer()
    {
        if (playerTransform == null)
        {
            StopChasing();
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
        // プレイヤーを見失った判定
        if (distanceToPlayer > losePlayerDistance)
        {
            losePlayerTimer += Time.deltaTime;
            
            if (losePlayerTimer >= losePlayerTime)
            {
                StopChasing();
                return;
            }
        }
        else
        {
            losePlayerTimer = 0f;
        }
        
        // プレイヤーの位置を目標に設定
        navMeshAgent.SetDestination(playerTransform.position);
    }
    
    /// <summary>
    /// 追跡を停止して徘徊に戻る
    /// </summary>
    private void StopChasing()
    {
        isChasing = false;
        losePlayerTimer = 0f;
        navMeshAgent.speed = normalSpeed;
        
        SetRandomDestination();
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: プレイヤーを見失った。徘徊に戻る");
        }
    }
    
    /// <summary>
    /// !マークを表示
    /// </summary>
    private void ShowAlertMark()
    {
        // 既存の!マークを削除
        if (currentAlertMark != null)
        {
            Destroy(currentAlertMark);
        }
        
        if (useSimpleAlert)
        {
            // シンプルな3Dテキストで!マークを表示
            GameObject alertMark = new GameObject("AlertMark");
            alertMark.transform.position = transform.position + alertMarkOffset;
            alertMark.transform.SetParent(transform);
            
            TextMesh textMesh = alertMark.AddComponent<TextMesh>();
            textMesh.text = "!";
            textMesh.fontSize = 100;
            textMesh.characterSize = 0.1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.red;
            textMesh.fontStyle = FontStyle.Bold;
            
            currentAlertMark = alertMark;
            StartCoroutine(HideAlertMarkAfterDelay());
        }
        else if (alertMarkPrefab != null)
        {
            // Prefabから!マークを生成
            currentAlertMark = Instantiate(alertMarkPrefab, transform.position + alertMarkOffset, Quaternion.identity);
            currentAlertMark.transform.SetParent(transform);
            StartCoroutine(HideAlertMarkAfterDelay());
        }
    }
    
    /// <summary>
    /// !マークを一定時間後に非表示
    /// </summary>
    private IEnumerator HideAlertMarkAfterDelay()
    {
        yield return new WaitForSeconds(alertMarkDuration);
        
        if (currentAlertMark != null)
        {
            Destroy(currentAlertMark);
            currentAlertMark = null;
        }
    }
    
    /// <summary>
    /// 徘徊を停止
    /// </summary>
    public void StopWandering()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
        }
        
        StopAllCoroutines();
        isWaiting = false;
        isChasing = false;
        enabled = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: 徘徊停止");
        }
    }
    
    /// <summary>
    /// 徘徊を再開
    /// </summary>
    public void ResumeWandering()
    {
        enabled = true;
        isWaiting = false;
        SetRandomDestination();
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: 徘徊再開");
        }
    }
    
    /// <summary>
    /// 徘徊範囲を変更
    /// </summary>
    public void SetWanderRadius(float newRadius)
    {
        wanderRadius = newRadius;
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: 徘徊範囲を {newRadius}m に変更");
        }
    }
    
    /// <summary>
    /// 移動速度を変更
    /// </summary>
    public void SetMoveSpeed(float speed)
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = speed;
            if (showDebugInfo)
            {
                Debug.Log($"{gameObject.name}: 移動速度を {speed} に変更");
            }
        }
    }
    
    /// <summary>
    /// 現在の状態を取得
    /// </summary>
    public bool IsWandering()
    {
        return enabled && !isWaiting && !isChasing;
    }
    
    /// <summary>
    /// 追跡中かどうか
    /// </summary>
    public bool IsChasing()
    {
        return isChasing;
    }
    
    /// <summary>
    /// デバッグ用ギズモ表示
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? initialPosition : transform.position;
        
        // 徘徊範囲を表示
        if (showWanderRange)
        {
            Gizmos.color = debugColor;
            Gizmos.DrawWireSphere(center, wanderRadius);
            
            // 現在の目標地点を表示
            if (Application.isPlaying && navMeshAgent != null && navMeshAgent.hasPath && !isChasing)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(navMeshAgent.destination, 0.5f);
                Gizmos.DrawLine(transform.position, navMeshAgent.destination);
            }
        }
        
        // プレイヤー検知範囲を表示
        if (showDetectionRange)
        {
            Gizmos.color = isChasing ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            
            // 視野角を表示
            Vector3 forward = Application.isPlaying ? transform.forward : Vector3.forward;
            Vector3 rightBoundary = Quaternion.Euler(0, viewAngle * 0.5f, 0) * forward * detectionRadius;
            Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * forward * detectionRadius;
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
            
            // プレイヤーへのラインを表示（追跡中）
            if (Application.isPlaying && isChasing && playerTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }
    }

    [Header("VFXテスト")]
    [SerializeField] private KeyCode testVFXKey = KeyCode.E;  // テスト用キー
    /// <summary>
    /// 爆発エフェクトのテスト
    /// </summary>
    private void TestExplosionVFX()
    {
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.PlayExplosion(gameObject);
            Debug.Log($"{gameObject.name}で爆発エフェクトをテスト再生");
        }
        else
        {
            Debug.LogWarning("VFXManagerが見つかりません");
        }
    }
} 