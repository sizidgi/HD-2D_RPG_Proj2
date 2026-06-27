using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;


public class PlayerController : MonoBehaviour
{

    [SerializeField,Header("歩く速度"),Range(0,10)]
    private float Speed;

    [SerializeField, Header("走る速度"), Range(1, 5)]
    private float DashSpeed;

    [Header("クイックタイム戦闘")]
    [SerializeField] private QuickTimeCombatUI quickTimeCombatUI;

    private Rigidbody rb;

    private Animator animator;
    [SerializeField] private InputActionAsset inputActions;
    private InputAction moveAction;
    private InputAction dashAction;
    private InputAction selectAction;

    [Header("移動基準カメラ")]
    [SerializeField] private Transform moveReferenceCamera;

    private static bool isFirstLoad = true;
    // 戦闘処理の多重発火防止フラグ
    private bool isBattleTriggering = false;
    
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // フィールドに戻ってきた時は必ずフラグをリセット
        isBattleTriggering = false;

        if(GameManager.Instance != null)
        {
            if(GameManager.Instance.BattleWin)
            {
                
                GameManager.Instance.BattleWin = false;
                this.gameObject.transform.position=GameManager.Instance.PlayerBackPosition;
                Debug.Log("PlayerControllerがGameManagerを取得しました。");
            }
            Debug.LogWarning("GameManagerが見つかりません。");
        }

        animator = GetComponent<Animator>();
        var controllers = Input.GetJoystickNames();

        // Rigidbodyの設定
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation; // 回転を固定
        }
        if (inputActions != null)
        {
            var playerActionMap = inputActions.FindActionMap("Player");
            moveAction = playerActionMap.FindAction("Move");
            dashAction = playerActionMap.FindAction("Dash");
            selectAction = playerActionMap.FindAction("Select");

            moveAction.Enable();
            dashAction.Enable();
            selectAction.Enable();

            Debug.Log("inputActionsを正常に取り込んだ");
            foreach(var device in InputSystem.devices)
            {
                Debug.Log(device.name + ":" + device.GetType().Name);
            }

        }

        if (isFirstLoad)
        {
            Debug.Log("[PlayerController] : 初回位置調整");
            // 初回だけ初期位置
            transform.position = new Vector3(-202.44f, 1.52f, 3.11f);
            isFirstLoad = false;
        }

    }

    // Update is called once per frame
    //void Update()
    //{

    //    Vector2 inputVector = moveAction.ReadValue<Vector2>();
    //    // 移動方向を計算
    //    Vector3 moveDirection = Vector3.zero;
    //    bool isMoving = false;
    //    bool isDash = false;
    //    //PadTest(inputVector);

    //    if (Input.GetKey(KeyCode.D) || inputVector.x > 0.5f)
    //    {
    //        moveDirection.z -= 1;
    //        animator.SetInteger("direction", 3);
    //        isMoving = true;
    //    }
    //    if (Input.GetKey(KeyCode.A) || inputVector.x < -0.5f)
    //    {
    //        moveDirection.z += 1;
    //        animator.SetInteger("direction", 2);
    //        isMoving = true;
    //    }
    //    if (Input.GetKey(KeyCode.W) || inputVector.y > 0.5f)
    //    {
    //        moveDirection.x += 1;
    //        animator.SetInteger("direction", 0);
    //        isMoving = true;
    //    }
    //    if (Input.GetKey(KeyCode.S) || inputVector.y < -0.5f)
    //    {
    //        moveDirection.x -= 1;
    //        animator.SetInteger("direction", 1);
    //        isMoving = true;
    //    }

    //    animator.SetBool("isMoving", isMoving);

    //    // ダッシュ判定
    //    float currentSpeed = Speed;
    //    if (Input.GetKey(KeyCode.LeftShift) || dashAction.IsPressed())
    //    {
    //        currentSpeed *= DashSpeed;
    //        isDash = true;

    //    }

    //    animator.SetBool("isDash", isDash);

    //    // Rigidbodyで移動
    //    if (rb != null && moveDirection != Vector3.zero)
    //    {
    //        Vector3 newPosition = rb.position + moveDirection.normalized * currentSpeed * Time.deltaTime;
    //        rb.MovePosition(newPosition);
    //    }
    //}
    void Update()
    {
        Vector2 inputVector = moveAction.ReadValue<Vector2>();

        // 移動方向を計算（カメラ基準）
        Vector3 moveDirection = GetCameraRelativeMoveDirection(inputVector);

        bool isMoving = false;
        bool isDash = false;

        // direction の更新は今まで通り入力ベース
        if (Input.GetKey(KeyCode.RightArrow) ||Input.GetKey(KeyCode.D) || inputVector.x > 0.5f)
        {
            animator.SetInteger("direction", 3);
            isMoving = true;
        }
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || inputVector.x < -0.5f)
        {
            animator.SetInteger("direction", 2);
            isMoving = true;
        }
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || inputVector.y > 0.5f)
        {
            animator.SetInteger("direction", 0);
            isMoving = true;
        }
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || inputVector.y < -0.5f)
        {
            animator.SetInteger("direction", 1);
            isMoving = true;
        }

        // スティック入力だけでも移動扱いにする
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            isMoving = true;
        }

        animator.SetBool("isMoving", isMoving);

        // ダッシュ判定
        float currentSpeed = Speed;
        if (Input.GetKey(KeyCode.LeftShift) || dashAction.IsPressed())
        {
            currentSpeed *= DashSpeed;
            isDash = true;
        }

        animator.SetBool("isDash", isDash);

        // Rigidbodyで移動
        if (rb != null && moveDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 newPosition = rb.position + moveDirection.normalized * currentSpeed * Time.deltaTime;
            rb.MovePosition(newPosition);
        }
    }

    void OnEnable()
    {
        moveAction?.Enable();
        dashAction?.Enable();
        selectAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        dashAction?.Disable();
        selectAction?.Disable();
    }

    void PadTest(Vector2 vector)
    {
        Debug.Log("x" + vector.x);
        Debug.Log("y" + vector.y);
         
    }

    /// <summary>
    /// 敵との衝突検知
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // 多重発火防止: 既に戦闘処理が動いている場合はスキップ
        if (isBattleTriggering) return;
        
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // 敵と衝突した場合
            Debug.Log("Enemyと衝突");
            // GameManagerを通してバトル開始
            if (GameManager.Instance != null)
            {
                isBattleTriggering = true;
                GetEnemyStetas(collision.gameObject);
            }
            else
            {
                // GameManagerが存在しない従来の方法でシーン遷移
                Debug.LogWarning("GameManagerが見つかりません。直接シーン遷移します、E");
                SceneManager.LoadScene("turnTestScene");
            }
        }
    }
    /// <summary>
    /// 敵のステータスを取得
    /// </summary>
    private void GetEnemyStetas(GameObject enemy)
    {
        Debug.Log("敵と戦ったことがあるかチェック");
        if (enemy != null)
        {
            EnemyWanderAI enemyAI = enemy.GetComponent<EnemyWanderAI>();
            if (enemyAI != null)
            {
                var enemyDataList = enemyAI.GetEnemyData();
                int encounterGroupId = enemyAI.GetEncounterGroupId();
                string bgmName = enemyAI.GetBGMName();
                
                if (enemyDataList != null && enemyDataList.Count > 0 && encounterGroupId >= 0)
                {
                    // グループIDで戦ったことがあるかチェック
                    bool hasDefeatedGroup = GameManager.Instance.HasDefeatedGroup(encounterGroupId);
                    
                    if (hasDefeatedGroup && quickTimeCombatUI != null)
                    {
                        DisableAllEnemies();
                        Debug.Log($"[PlayerController] グループID={encounterGroupId}と戦ったことがある。クイックタイム戦闘開始");
                        // クイックタイム戦闘を開始
                        StartQuickTimeCombat(enemy, enemyDataList, encounterGroupId, bgmName);
                    }
                    else
                    {
                        Debug.Log($"[PlayerController] グループID={encounterGroupId}は初遭遇。通常戦闘シーンへ遷移");
                        // 通常の戦闘シーンに移動
                        StartNormalBattle(enemy, enemyDataList, encounterGroupId, bgmName);
                    }
                }
                else
                {
                    // EnemyDataが設定されていない場合は従来の処理
                    if (GameManager.Instance != null)
                    {
                        Debug.Log("PlayerController-GetEnemyStetas-EnemyDataが設定されていない");
                        GameManager.Instance.StartBattle(transform.position, enemy);
                    }
                    else
                    {
                        Debug.Log("GameManagerが見つかりません。直接シーン遷移します。");
                        SceneManager.LoadScene("turnTestScene");
                    }
                }
            }
            else
            {
                // EnemyWanderAIがない場合は従来の処理
                if (GameManager.Instance != null)
                {
                    Debug.Log("PlayerController-GetEnemyStetas-EnemyWanderAIがない");
                    GameManager.Instance.StartBattle(transform.position, enemy);
                }
                else
                {
                    Debug.Log("GameManagerが見つかりません。直接シーン遷移します。");
                    SceneManager.LoadScene("turnTestScene");
                }
            }
        }
        else
        {
            Debug.Log("PlayerController-GetEnemyStetas-enemyがnull");
        }
    }

    /// <summary>
    /// クイックタイム戦闘を開始
    /// </summary>
    private void StartQuickTimeCombat(GameObject enemyObject, List<CharacterData> enemyDataList, int encounterGroupId, string bgmName = "BattleNormal")
    {
        if (quickTimeCombatUI == null)
        {
            Debug.LogWarning("QuickTimeCombatUIが設定されていません。通常戦闘に移行します。");
            StartNormalBattle(enemyObject, enemyDataList, encounterGroupId, bgmName);
            return;
        }
        
        // プレイヤーの移動を停止
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
        }
        
        // 敵のAIを一時停止
        EnemyWanderAI enemyAI = enemyObject.GetComponent<EnemyWanderAI>();
        if (enemyAI != null)
        {
            enemyAI.StopWandering();
        }
        
        // クイックタイム戦闘UIを開始（敵オブジェクトを渡す）
        quickTimeCombatUI.StartQuickTimeCombat((success) =>
        {
            if (success)
            {
                // タイミング成功：敵を倒す（演出後に呼ばれる）
                OnQuickTimeCombatSuccess(enemyObject, enemyDataList, encounterGroupId);
                // 成功時はフィールドに戻るのでフラグリセット
                isBattleTriggering = false;
            }
            else
            {
                // タイミング失敗：通常戦闘に移行（遷移するのでフラグリセット不要）
                StartNormalBattle(enemyObject, enemyDataList, encounterGroupId, bgmName);
            }
            
            // クイックタイム戦闘終了後に全ての敵を再開
            EnableAllEnemies();
        }, enemyObject);
    }

    /// <summary>
    /// クイックタイム戦闘成功時の処理
    /// </summary>
    private void OnQuickTimeCombatSuccess(GameObject enemyObject, List<CharacterData> enemyDataList, int encounterGroupId)
    {
        // グループIDを記録（すでに記録済みのはず）
        GameManager.Instance.RecordGroupDefeat(encounterGroupId);
        
        // 敵を倒した記録（旧システム互換のため）
        if (enemyDataList != null)
        {
            foreach (var enemyData in enemyDataList)
            {
                if (enemyData != null)
                {
                    GameManager.Instance.RecordEnemyDefeat(enemyData);
                }
            }
        }
        
        // 敵を削除
        Destroy(enemyObject);
        
        Debug.Log($"[PlayerController] クイックタイム戦闘成功！グループID={encounterGroupId}の敵を倒しました。");
        
        // ここに経験値獲得などの処理を追加可能
    }

    /// <summary>
    /// 通常の戦闘を開始
    /// </summary>
    private void StartNormalBattle(GameObject enemyObject, List<CharacterData> enemyDataList, int encounterGroupId, string bgmName = "BattleNormal")
    {
        if (GameManager.Instance != null)
        {
            // EnemyDataを設定
            GameManager.Instance.EnemyData = new List<CharacterData>();
            if (enemyDataList != null)
            {
                GameManager.Instance.EnemyData.AddRange(enemyDataList);
            }
            
            // 敵オブジェクトを記録（戦闘勝利後に削除するため）
            GameManager.Instance.SetCurrentBattleEnemy(enemyObject);
            
            // グループIDを記録（初遭遇時に記録）
            GameManager.Instance.RecordGroupDefeat(encounterGroupId);
            
            Debug.Log($"[PlayerController] 通常戦闘開始: グループID={encounterGroupId}, BGM={bgmName}");
            
            // BGM情報を含めて戦闘開始
            GameManager.Instance.StartBattleWithEnemyData(transform.position, enemyDataList, bgmName);
        }
        else
        {
            Debug.LogWarning("GameManagerが見つかりません。直接シーン遷移します。");
            SceneManager.LoadScene("turnTestScene");
        }
    }

    private Vector3 GetCameraRelativeMoveDirection(Vector2 inputVector)
    {
        // WASD単押しでも動くようにキーボード入力を補完
        float x = inputVector.x;
        float y = inputVector.y;

        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x = 1f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x = -1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) y = 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) y = -1f;

        Vector2 finalInput = new Vector2(x, y);

        if (finalInput.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Transform cam = moveReferenceCamera != null
            ? moveReferenceCamera
            : (Camera.main != null ? Camera.main.transform : null);

        // カメラが見つからない場合は従来の向きにフォールバック
        if (cam == null)
        {
            Vector3 fallback = Vector3.zero;
            fallback.x = finalInput.y;
            fallback.z = -finalInput.x;
            return fallback;
        }

        Vector3 forward = cam.forward;
        Vector3 right = cam.right;

        // 地面に投影
        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f || right.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        forward.Normalize();
        right.Normalize();

        // 上入力でカメラ前方、右入力でカメラ右方向へ進む
        return forward * finalInput.y + right * finalInput.x;
    }

    /// <summary>
    /// フィールド上の全ての敵を停止&非表示
    /// </summary>
    private void DisableAllEnemies()
    {
        // 全てのEnemyWanderAIを検索
        EnemyWanderAI[] enemies = FindObjectsOfType<EnemyWanderAI>();
        Debug.Log("[PlayerController] : DisableAllEnemies");
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                // 徘徊を停止
                enemy.StopWandering();

                // オブジェクトを非表示
                enemy.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// フィールド上の全ての敵を再開&表示
    /// </summary>
    public void EnableAllEnemies()
    {

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
}
