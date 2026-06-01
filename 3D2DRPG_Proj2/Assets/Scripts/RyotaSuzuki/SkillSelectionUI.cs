using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using DG.Tweening;

/// <summary>
/// コンソールゲーム風の技選択UI
/// ▶カーソルでの選択とUnityEventでのコールバックをサポート
/// </summary>
public class SkillSelectionUI : MonoBehaviour
{
    [Header("UI要素")]
    [SerializeField] private GameObject skillPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private List<TextMeshProUGUI> skillTexts = new List<TextMeshProUGUI>();
    [SerializeField] private TextMeshProUGUI instructionText;

    [SerializeField] private GameObject skillWindowPanel;
    [SerializeField] private TextMeshProUGUI skillWindowText;

    [Header("スキル説明パネル")]
    [Tooltip("Pivot を中央 (0.5, 0.5) にすると中央から左右に開閉します")]
    [SerializeField] private RectTransform descriptionPanel;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private float descriptionAnimDuration = 0.3f;

    [Header("表示設定")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private string cursorSymbol = "▶";

    [Header("プレイヤーステータスパネル")]
    [SerializeField] private PlayerStatusPanel playerStatusPanel;

    [Header("UnityEvents")]
    [SerializeField] private UnityEvent<PlayerData> OnCharacterSelected;

    private List<SkillData> currentSkills;
    private UnityEvent<int> currentCallback;
    private int currentSelection = 0;
    private bool isActive = false;
    private PlayerData currentCharacterData;
    private Tween descriptionPanelTween;

    void Start()
    {
        // 初期状態では非表示
        if (skillPanel != null)
            skillPanel.SetActive(false);

        InitializeDescriptionPanelHidden();

        // 操作説明を設定
        if (instructionText != null)
            instructionText.text = "↑↓: 選択  Space: 決定  ESC: キャンセル";
    }

    private void OnDestroy()
    {
        descriptionPanelTween?.Kill();
    }

    private void InitializeDescriptionPanelHidden()
    {
        if (descriptionPanel == null) return;

        descriptionPanelTween?.Kill();
        descriptionPanel.localScale = new Vector3(0f, descriptionPanel.localScale.y, descriptionPanel.localScale.z);
        descriptionPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 技選択UIを表示して選択を開始する
    /// 使用例:
    /// UnityEvent<int> callback = new UnityEvent<int>();
    /// callback.AddListener(OnSkillSelected);
    /// skillSelectionUI.ShowSkillSelection(characterSkills, callback);
    /// </summary>
    /// <param name="skills">選択可能な技のリスト</param>
    /// <param name="onSkillSelected">技が選択されたときのコールバック (インデックス、-1でキャンセル)</param>
    public void ShowSkillSelection(List<SkillData> skills, UnityEvent<int> onSkillSelected)
    {
        if (skills == null || skills.Count == 0)
        {
            Debug.LogWarning("SkillSelectionUI: スキルリストが空です");
            onSkillSelected?.Invoke(-1);
            return;
        }

        currentSkills = skills;
        currentCallback = onSkillSelected;
        currentSelection = 0;
        isActive = true;

        // パネルを表示
        if (skillPanel != null)
            skillPanel.SetActive(true);

        // タイトルを設定
        if (titleText != null)
            titleText.text = "技を選択してください";

        // スキルリストを更新
        UpdateSkillDisplay();

        OpenDescriptionPanel();

        // 入力処理を開始
        StartCoroutine(HandleInput());
    }

    /// <summary>
    /// キャラクター情報付きで技選択UIを表示（オーバーロード）
    /// </summary>
    /// <param name="skills">選択可能な技のリスト</param>
    /// <param name="characterData">表示するキャラクターのデータ</param>
    /// <param name="onSkillSelected">技が選択されたときのコールバック</param>
    public void ShowSkillSelection(List<SkillData> skills, PlayerData characterData, UnityEvent<int> onSkillSelected)
    {
        // キャラクターデータを設定
        SetCharacterData(characterData);
        
        // 通常の技選択表示
        ShowSkillSelection(skills, onSkillSelected);
    }

    /// <summary>
    /// キャラクターデータを設定してステータスパネルを更新
    /// </summary>
    /// <param name="characterData">設定するキャラクターデータ</param>
    public void SetCharacterData(PlayerData characterData)
    {
        currentCharacterData = characterData;
        
        // ステータスパネルを更新
        if (playerStatusPanel != null && characterData != null)
        {
            playerStatusPanel.UpdatePlayerStatus(characterData);
        }
        
        // UnityEventを発火
        OnCharacterSelected?.Invoke(characterData);
    }

    /// <summary>
    /// キャラクターインデックスからダミーデータを設定（UnityEventから呼び出し可能）
    /// </summary>
    /// <param name="characterIndex">キャラクターのインデックス（0-2）</param>
    public void SetCharacterByIndex(int characterIndex)
    {
        PlayerData dummyData = PlayerData.CreateDummyData(characterIndex);
        SetCharacterData(dummyData);
    }

    /// <summary>
    /// 技選択UIを強制的に非表示にする
    /// </summary>
    public void HideSkillSelection()
    {
        isActive = false;
        CloseDescriptionPanel(() =>
        {
            if (skillPanel != null)
                skillPanel.SetActive(false);
        });
    }

    private void OpenDescriptionPanel()
    {
        if (descriptionPanel == null) return;

        UpdateDescriptionText();

        descriptionPanelTween?.Kill();
        descriptionPanel.gameObject.SetActive(true);
        descriptionPanel.localScale = new Vector3(0f, descriptionPanel.localScale.y, descriptionPanel.localScale.z);
        descriptionPanelTween = descriptionPanel
            .DOScaleX(1f, descriptionAnimDuration)
            .SetEase(Ease.OutQuad);
    }

    private void CloseDescriptionPanel(TweenCallback onComplete = null)
    {
        if (descriptionPanel == null || !descriptionPanel.gameObject.activeInHierarchy)
        {
            onComplete?.Invoke();
            return;
        }

        descriptionPanelTween?.Kill();
        descriptionPanelTween = descriptionPanel
            .DOScaleX(0f, descriptionAnimDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                descriptionPanel.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private void UpdateDescriptionText()
    {
        if (descriptionText == null || currentSkills == null || currentSkills.Count == 0)
            return;

        SkillData skill = currentSkills[currentSelection];
        if (skill == null)
        {
            descriptionText.text = string.Empty;
            return;
        }

        descriptionText.text = string.IsNullOrWhiteSpace(skill.description)
            ? skill.skillName
            : skill.description;
    }

    /// <summary>
    /// スキル表示を更新する（▶カーソルと色変更）
    /// </summary>
    private void UpdateSkillDisplay()
    {
        for (int i = 0; i < skillTexts.Count; i++)
        {
            if (i < currentSkills.Count)
            {
                skillTexts[i].gameObject.SetActive(true);

                // カーソルと技名を表示
                string cursorDisplay = (i == currentSelection) ? cursorSymbol : "　"; // 全角スペース
                string skillName = currentSkills[i].skillName;
                string mpCost = $"(MP: {currentSkills[i].mpCost})";

                skillTexts[i].text = $"{cursorDisplay} {skillName} {mpCost}";

                // 選択中の色を変更
                skillTexts[i].color = (i == currentSelection) ? selectedColor : normalColor;
            }
            else
            {
                skillTexts[i].gameObject.SetActive(false);
            }
        }

        UpdateDescriptionText();
    }

    /// <summary>
    /// キーボード入力を処理する（W/S or ↑↓で選択、Spaceで決定、ESCでキャンセル）
    /// </summary>
    private IEnumerator HandleInput()
    {
        while (isActive && skillPanel != null && skillPanel.activeInHierarchy)
        {
            // 上方向キー
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveCursor(-1);
            }
            // 下方向キー
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveCursor(1);
            }
            // 決定キー
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                SelectSkill();
                yield break;
            }
            // キャンセルキー
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelSelection();
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// カーソルを移動する（循環選択）
    /// </summary>
    private void MoveCursor(int direction)
    {
        int newSelection = currentSelection + direction;

        // 循環選択
        if (newSelection < 0)
            newSelection = currentSkills.Count - 1;
        else if (newSelection >= currentSkills.Count)
            newSelection = 0;

        currentSelection = newSelection;
        UpdateSkillDisplay();
    }

    /// <summary>
    /// 技を選択する
    /// </summary>
    private void SelectSkill()
    {
        // 選択されたインデックスでコールバックを呼び出し
        currentCallback?.Invoke(currentSelection);

        // UI非表示
        HideSkillSelection();

        SetSkillWindowText(currentSkills[currentSelection].skillName);
    }

    /// <summary>
    /// 表示する技名の設定
    /// </summary>
    public void SetSkillWindowText(string skillname)
    {
        // 選択スキル名設定
        skillWindowText.text = skillname;
    }
    /// <summary>
    /// 選択した技の表示
    /// </summary>
    public void ShowSkillWindow(bool isEnemy)
    {
        StartCoroutine(SkillWindowCoroutine(isEnemy));
    }
    private IEnumerator SkillWindowCoroutine(bool isEnemy)
    {

        if (isEnemy)
        {
            skillWindowPanel.GetComponent<Outline>().effectColor = Color.red;
        }
        else
        {
            skillWindowPanel.GetComponent<Outline>().effectColor = Color.blue;
        }

        skillWindowPanel.SetActive(true);

        yield return new WaitForSeconds(1.0f);

        skillWindowPanel.SetActive(false);

        yield break;
    }

    /// <summary>
    /// 選択をキャンセルする
    /// </summary>
    private void CancelSelection()
    {
        // -1でキャンセルを通知
        currentCallback?.Invoke(-1);

        // UI非表示
        HideSkillSelection();
    }

    /// <summary>
    /// 現在選択中の技のインデックスを取得
    /// </summary>
    public int GetCurrentSelection()
    {
        return currentSelection;
    }

    /// <summary>
    /// アクティブ状態かどうかを取得
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }

    /// <summary>
    /// 現在設定されているキャラクターデータを取得
    /// </summary>
    public PlayerData GetCurrentCharacterData()
    {
        return currentCharacterData;
    }

    /// <summary>
    /// テスト用：ダミーキャラクターでステータス表示をテスト
    /// </summary>
    [ContextMenu("テスト用キャラクター0")]
    public void TestCharacter0()
    {
        SetCharacterByIndex(0);
    }

    [ContextMenu("テスト用キャラクター1")]
    public void TestCharacter1()
    {
        SetCharacterByIndex(1);
    }

    [ContextMenu("テスト用キャラクター2")]
    public void TestCharacter2()
    {
        SetCharacterByIndex(2);
    }
}