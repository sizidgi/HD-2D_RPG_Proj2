using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace RyotaSuzuki.UI
{
    /// <summary>
    /// ESCキーでのメニューUI表示/非表示とボタンクリック時のパネル管理を行うクラス
    /// </summary>
    public class MenuUIManager : MonoBehaviour
    {
        [Header("メニューUI設定")]
        [SerializeField, Header("メニューUICanvas")] 
        private GameObject menuUICanvas;
        
        [SerializeField, Header("メニューUIパネル")] 
        private GameObject menuUIPanel;
        
        [SerializeField, Header("背景パネル（暗転用）")] 
        private GameObject backgroundPanel;

        [Header("メニューボタン設定")]
        
        [SerializeField, Header("装備ボタン")] 
        private Button soubiButton;
        
        [SerializeField, Header("閉じるボタン")] 
        private Button closeButton;

        [Header("パネル設定")]
        
        [SerializeField, Header("装備パネル")] 
        private GameObject soubiPanel;

        [Header("アニメーション設定")]
        [SerializeField, Header("フェードイン時間")] 
        private float fadeInDuration = 0.3f;
        
        [SerializeField, Header("フェードアウト時間")] 
        private float fadeOutDuration = 0.2f;
        
        [SerializeField, Header("パネル切り替え時間")] 
        private float panelSwitchDuration = 0.25f;

        [Header("デバッグ")]
        [SerializeField, Header("デバッグログを表示")] 
        private bool showDebugLog = true;

        private bool isMenuOpen = false;
        private GameState previousGameState;
        private CanvasGroup menuCanvasGroup;
        private CanvasGroup backgroundCanvasGroup;
        private GameObject currentActivePanel = null;

        void Start()
        {
            InitializeMenu();
            SetupButtons();
        }

        void Update()
        {
            HandleMenuInput();
        }

        /// <summary>
        /// メニューの初期化
        /// </summary>
        void InitializeMenu()
        {
            // CanvasGroupコンポーネントを取得または追加
            if (menuUICanvas != null)
            {
                menuCanvasGroup = menuUICanvas.GetComponent<CanvasGroup>();
                if (menuCanvasGroup == null)
                {
                    menuCanvasGroup = menuUICanvas.AddComponent<CanvasGroup>();
                }
            }

            if (backgroundPanel != null)
            {
                backgroundCanvasGroup = backgroundPanel.GetComponent<CanvasGroup>();
                if (backgroundCanvasGroup == null)
                {
                    backgroundCanvasGroup = backgroundPanel.AddComponent<CanvasGroup>();
                }
            }

            // 初期状態でメニューを非表示
            HideMenuImmediate();
            HideAllPanels();

            if (showDebugLog)
            {
                Debug.Log("[MenuUIManager] メニューUI初期化完了");
            }
        }

        /// <summary>
        /// ボタンのイベントリスナーを設定
        /// </summary>
                void SetupButtons()
        {
            // EventSystem チェック
            var eventSystems = FindObjectsOfType<UnityEngine.EventSystems.EventSystem>();
            Debug.Log($"[MenuUIManager] EventSystem数: {eventSystems.Length}");
            
            if (eventSystems.Length == 0)
            {
                // EventSystemが存在しない場合は作成
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[MenuUIManager] EventSystemを自動生成しました");
            }
            else
            {
                for (int i = 0; i < eventSystems.Length; i++)
                {
                    Debug.Log($"[MenuUIManager] EventSystem {i}: {eventSystems[i].name} - アクティブ: {eventSystems[i].gameObject.activeInHierarchy}");
                }
            }

            Debug.Log($"[MenuUIManager] soubiButton: {(soubiButton != null ? soubiButton.name : "NULL")}");
            Debug.Log($"[MenuUIManager] closeButton: {(closeButton != null ? closeButton.name : "NULL")}");

            // 装備ボタン
            if (soubiButton != null)
            {
                Debug.Log($"[MenuUIManager] 装備ボタンのinteractable: {soubiButton.interactable}");
                soubiButton.onClick.AddListener(() => OnMenuButtonClicked("Soubi"));
            }

            // 閉じるボタン
            if (closeButton != null)
            {
                Debug.Log($"[MenuUIManager] 閉じるボタンのinteractable: {closeButton.interactable}");
                closeButton.onClick.AddListener(CloseMenu);
            }

            if (showDebugLog)
            {
                Debug.Log("[MenuUIManager] ボタンイベント設定完了");
            }
        }

        /// <summary>
        /// キー入力の処理
        /// </summary>
        void HandleMenuInput()
        {
            // ESCキーでメニューの表示/非表示を切り替え
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentActivePanel != null)
                {
                    // パネルが開いている場合は、パネルを閉じる
                    HideCurrentPanel();
                }
                else
                {
                    // パネルが開いていない場合は、メニュー全体を切り替え
                    ToggleMenu();
                }
            }
        }

        /// <summary>
        /// メニューボタンがクリックされた時の処理
        /// </summary>
        /// <param name="buttonType">ボタンの種類</param>
        public void OnMenuButtonClicked(string buttonType)
        {
            if (showDebugLog)
            {
                Debug.Log($"[MenuUIManager] {buttonType}ボタンがクリックされました");
            }

            switch (buttonType)
            {

                case "Soubi":
                    ShowPanel(soubiPanel, "装備");
                    break;
                default:
                    Debug.LogWarning($"[MenuUIManager] 未知のボタンタイプ: {buttonType}");
                    break;
            }
        }

        /// <summary>
        /// 指定されたパネルを表示
        /// </summary>
        /// <param name="panel">表示するパネル</param>
        /// <param name="panelName">パネル名（デバッグ用）</param>
        public void ShowPanel(GameObject panel, string panelName = "")
        {
            if (panel == null)
            {
                Debug.LogWarning($"[MenuUIManager] {panelName}パネルが設定されていません");
                return;
            }

            // 既に同じパネルが開いている場合は何もしない
            if (currentActivePanel == panel && panel.activeInHierarchy)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[MenuUIManager] {panelName}パネルは既に表示されています");
                }
                return;
            }

            // 他のパネルを非表示にしてから新しいパネルを表示
            StartCoroutine(SwitchPanelCoroutine(panel, panelName));
        }

        /// <summary>
        /// パネル切り替えのコルーチン
        /// </summary>
        System.Collections.IEnumerator SwitchPanelCoroutine(GameObject newPanel, string panelName)
        {
            // 現在のパネルをフェードアウト
            if (currentActivePanel != null && currentActivePanel.activeInHierarchy)
            {
                yield return StartCoroutine(HidePanelWithAnimation(currentActivePanel));
            }

            // 少し待機
            yield return new WaitForSecondsRealtime(0.1f);

            // 新しいパネルを表示
            currentActivePanel = newPanel;
            yield return StartCoroutine(ShowPanelWithAnimation(newPanel, panelName));
        }

        /// <summary>
        /// パネルをアニメーション付きで表示
        /// </summary>
        System.Collections.IEnumerator ShowPanelWithAnimation(GameObject panel, string panelName)
        {
            panel.SetActive(true);

            // CanvasGroupを取得または追加
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }

            // フェードインアニメーション
            canvasGroup.alpha = 0f;
            panel.transform.localScale = Vector3.zero;

            var fadeAnim = canvasGroup.DOFade(1f, panelSwitchDuration).SetUpdate(true);
            var scaleAnim = panel.transform.DOScale(Vector3.one, panelSwitchDuration)
                .SetEase(Ease.OutBack).SetUpdate(true);

            yield return fadeAnim.WaitForCompletion();

            if (showDebugLog)
            {
                Debug.Log($"[MenuUIManager] {panelName}パネルを表示しました");
            }
        }

        /// <summary>
        /// パネルをアニメーション付きで非表示
        /// </summary>
        System.Collections.IEnumerator HidePanelWithAnimation(GameObject panel)
        {
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                panel.SetActive(false);
                yield break;
            }

            var fadeAnim = canvasGroup.DOFade(0f, panelSwitchDuration).SetUpdate(true);
            var scaleAnim = panel.transform.DOScale(Vector3.zero, panelSwitchDuration)
                .SetEase(Ease.InBack).SetUpdate(true);

            yield return fadeAnim.WaitForCompletion();
            panel.SetActive(false);
        }

        /// <summary>
        /// 現在のパネルを非表示
        /// </summary>
        public void HideCurrentPanel()
        {
            if (currentActivePanel != null)
            {
                StartCoroutine(HidePanelWithAnimation(currentActivePanel));
                currentActivePanel = null;

                if (showDebugLog)
                {
                    Debug.Log("[MenuUIManager] 現在のパネルを閉じました");
                }
            }
        }

        /// <summary>
        /// すべてのパネルを即座に非表示
        /// </summary>
        void HideAllPanels()
        {
            if (soubiPanel != null) soubiPanel.SetActive(false);
            currentActivePanel = null;
        }

        /// <summary>
        /// メニューの表示/非表示を切り替え
        /// </summary>
        public void ToggleMenu()
        {
            if (isMenuOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        /// <summary>
        /// メニューを開く
        /// </summary>
        public void OpenMenu()
        {
            if (isMenuOpen) return;

            // 現在のゲーム状態を保存
            if (GameManager.Instance != null)
            {
                previousGameState = GameManager.Instance.CurrentGameState;
                GameManager.Instance.SetGameState(GameState.Menu);
            }

            isMenuOpen = true;

            // メニューUIを表示
            if (menuUICanvas != null)
            {
                menuUICanvas.SetActive(true);
            }

            if (menuUIPanel != null)
            {
                menuUIPanel.SetActive(true);
            }

                    // フェードインアニメーション
        ShowMenuWithAnimation();

        // ゲーム時間を停止
        Time.timeScale = 0f;

        // FloatingButtonAnimationを再開始（Time.timeScale = 0でも動作するように）
        RestartFloatingButtons();

        if (showDebugLog)
        {
            Debug.Log("[MenuUIManager] メニューを開きました");
        }
        }

        /// <summary>
        /// メニューを閉じる
        /// </summary>
        public void CloseMenu()
        {
            if (!isMenuOpen) return;

            isMenuOpen = false;

            // 開いているパネルを閉じる
            HideAllPanels();

            // フェードアウトアニメーション
            HideMenuWithAnimation();

            // ゲーム時間を再開
            Time.timeScale = 1f;

            // 前のゲーム状態に戻す
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(previousGameState);
            }

            if (showDebugLog)
            {
                Debug.Log("[MenuUIManager] メニューを閉じました");
            }
        }

        /// <summary>
        /// フェードインアニメーション付きでメニュー表示
        /// </summary>
        void ShowMenuWithAnimation()
        {
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 0f;
                menuCanvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
            }

            if (backgroundCanvasGroup != null)
            {
                backgroundCanvasGroup.alpha = 0f;
                backgroundCanvasGroup.DOFade(0.8f, fadeInDuration).SetUpdate(true);
            }

            // パネルのスケールアニメーション
            if (menuUIPanel != null)
            {
                menuUIPanel.transform.localScale = Vector3.zero;
                menuUIPanel.transform.DOScale(Vector3.one, fadeInDuration)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
        }

        /// <summary>
        /// フェードアウトアニメーション付きでメニュー非表示
        /// </summary>
        void HideMenuWithAnimation()
        {
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.DOFade(0f, fadeOutDuration)
                    .SetUpdate(true)
                    .OnComplete(() => {
                        if (menuUICanvas != null)
                            menuUICanvas.SetActive(false);
                    });
            }

            if (backgroundCanvasGroup != null)
            {
                backgroundCanvasGroup.DOFade(0f, fadeOutDuration).SetUpdate(true);
            }

            // パネルのスケールアニメーション
            if (menuUIPanel != null)
            {
                menuUIPanel.transform.DOScale(Vector3.zero, fadeOutDuration)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true);
            }
        }

        /// <summary>
        /// アニメーションなしで即座にメニューを非表示
        /// </summary>
        void HideMenuImmediate()
        {
            if (menuUICanvas != null)
            {
                menuUICanvas.SetActive(false);
            }

            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 0f;
            }

            if (backgroundCanvasGroup != null)
            {
                backgroundCanvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// ボタンのイベントリスナーを解除
        /// </summary>
        void OnDestroy()
        {
            if (soubiButton != null)
                soubiButton.onClick.RemoveAllListeners();
            
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// メニューが開いているかどうか
        /// </summary>
        public bool IsMenuOpen => isMenuOpen;

        /// <summary>
        /// FloatingButtonAnimationを再開始する
        /// </summary>
        void RestartFloatingButtons()
        {
            // メニューUI内のFloatingButtonAnimationを全て再開始
            var floatingButtons = menuUICanvas.GetComponentsInChildren<RyotaSuzuki.FloatingButtonAnimation>(true);
            foreach (var button in floatingButtons)
            {
                if (button != null)
                {
                    button.StartFloatingAnimation();
                    if (showDebugLog)
                        Debug.Log($"[MenuUIManager] FloatingButton再開始: {button.name}");
                }
            }
        }

        /// <summary>
        /// 外部からメニューを強制的に閉じる
        /// </summary>
        public void ForceCloseMenu()
        {
            if (isMenuOpen)
            {
                CloseMenu();
            }
        }

        //ゲーム終了(ボタンイベントから呼び出す)
        public void ExitGame()
        {
            Application.Quit();

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }
} 