using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using DG.Tweening;

/// <summary>
/// プレイヤーのステータス情報を表示するUIパネル
/// </summary>
public class PlayerStatusPanel : MonoBehaviour
{
    [Header("基本情報UI")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image playerIcon;

    [Header("HP表示")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image hpBarBackground;
    [SerializeField] private Image hpBarFill;

    [Header("MP表示")]
    [SerializeField] private TextMeshProUGUI mpText;
    [SerializeField] private Image mpBarBackground;
    [SerializeField] private Image mpBarFill;

    [Header("ステータス表示")]
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI defenseText;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("経験値表示")]
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private Image expBarBackground;
    [SerializeField] private Image expBarFill;

    [Header("カラー設定")]
    [SerializeField] private Color hpColor = Color.green;
    [SerializeField] private Color hpLowColor = Color.red;
    [SerializeField] private Color mpColor = Color.blue;
    [SerializeField] private Color expColor = Color.yellow;
    
    [Header("アニメーション設定")]
    [SerializeField] private float barAnimationDuration = 0.5f;
    [SerializeField] private Ease barAnimationEase = Ease.OutQuad;
    [SerializeField] private bool enableColorTransition = true;
    [SerializeField] private float colorTransitionDuration = 0.3f;

    [Header("UnityEvents")]
    [SerializeField] private UnityEvent<PlayerData> OnPlayerDataReceived;

    [Header("バフアイコン表示")]
    [SerializeField] private Transform buffIconContainer; // アイコンを並べる親オブジェクト
    [SerializeField] private GameObject buffIconPrefab; // アイコン1つ分のプレハブ
    [SerializeField] private float iconSize = 32f; // アイコンのサイズ

    private PlayerData currentPlayerData;
    private BuffIconDisplay buffIconDisplay;

    void Awake()
    {
        buffIconDisplay = GetComponent<BuffIconDisplay>();
        if (buffIconDisplay == null)
        {
            buffIconDisplay = gameObject.AddComponent<BuffIconDisplay>();
        }
        buffIconDisplay.Configure(buffIconContainer, buffIconPrefab, iconSize);
    }

    void Start()
    {
        // 初期設定
        InitializeBars();
        
        // ダミーデータで初期化（テスト用）
        //UpdatePlayerStatus(PlayerData.CreateDummyData(0));
    }

    /// <summary>
    /// ゲージバーの初期設定
    /// </summary>
    private void InitializeBars()
    {
        // HPバーの初期化
        if (hpBarFill != null)
        {
            hpBarFill.color = hpColor;
            hpBarFill.fillAmount = 1f;
            hpBarFill.type = Image.Type.Filled;
            hpBarFill.fillMethod = Image.FillMethod.Radial360;
        }

        // MPバーの初期化
        if (mpBarFill != null)
        {
            mpBarFill.color = mpColor;
            mpBarFill.fillAmount = 1f;
            mpBarFill.type = Image.Type.Filled;
            mpBarFill.fillMethod = Image.FillMethod.Horizontal;
        }

        // EXPバーの初期化
        if (expBarFill != null)
        {
            expBarFill.color = expColor;
            expBarFill.fillAmount = 0f;
            expBarFill.type = Image.Type.Filled;
            expBarFill.fillMethod = Image.FillMethod.Horizontal;
        }
    }

    /// <summary>
    /// プレイヤーステータスを更新（UnityEventから呼び出し可能）
    /// </summary>
    /// <param name="playerData">表示するプレイヤーデータ</param>
    public void UpdatePlayerStatus(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogWarning("PlayerStatusPanel: PlayerDataがnullです");
            return;
        }

        currentPlayerData = playerData;

        // 基本情報更新
        UpdateBasicInfo();
        
        // HP/MP更新
        UpdateHealthAndMana();
        
        // ステータス更新
        UpdateStats();
        
        // 経験値更新
        UpdateExperience();

        // UnityEventを発火
        OnPlayerDataReceived?.Invoke(playerData);
    }

    /// <summary>
    /// 基本情報（名前、レベル）を更新
    /// </summary>
    private void UpdateBasicInfo()
    {
        //Debug.Log($"Updating Basic Info: Name={currentPlayerData.playerName}, Level={currentPlayerData.level}");
        if (playerNameText != null)
            playerNameText.text = currentPlayerData.playerName;

        if (levelText != null)
            levelText.text = $"Lv.{currentPlayerData.level}";

        if (playerIcon != null)
            playerIcon.sprite = currentPlayerData.playerIcon;
    }

    /// <summary>
    /// HP/MPを更新
    /// </summary>
    private void UpdateHealthAndMana()
    {
        // HP更新
        if (hpText != null)
            hpText.text = $"HP: {currentPlayerData.currentHP}/{currentPlayerData.maxHP}";

        if (hpBarFill != null)
        {
            float hpRatio = currentPlayerData.GetHPRatio();
            
            // HPバーをアニメーション付きで更新
            hpBarFill.DOFillAmount(hpRatio, barAnimationDuration)
                .SetEase(barAnimationEase);
            
            // HPが低い場合は色を変更
            if (enableColorTransition)
            {
                Color targetColor = hpRatio <= 0.3f ? hpLowColor : hpColor;
                hpBarFill.DOColor(targetColor, colorTransitionDuration);
            }
        }

        // MP更新
        if (mpText != null)
            mpText.text = $"MP: {currentPlayerData.currentMP}/{currentPlayerData.maxMP}";

        if (mpBarFill != null)
        {
            float mpRatio = currentPlayerData.GetMPRatio();
            
            // MPバーをアニメーション付きで更新
            mpBarFill.DOFillAmount(mpRatio, barAnimationDuration)
                .SetEase(barAnimationEase);
        }
    }

    /// <summary>
    /// ステータス（攻撃力、防御力、素早さ）を更新
    /// </summary>
    private void UpdateStats()
    {
        if (attackText != null)
            attackText.text = $"攻撃: {currentPlayerData.attack}";

        if (defenseText != null)
            defenseText.text = $"防御: {currentPlayerData.defense}";

        if (speedText != null)
            speedText.text = $"素早さ: {currentPlayerData.speed}";
    }

    /// <summary>
    /// 経験値を更新
    /// </summary>
    private void UpdateExperience()
    {
        if (expText != null)
            expText.text = $"EXP: {currentPlayerData.currentEXP}/{currentPlayerData.nextLevelEXP}";

        if (expBarFill != null)
        {
            float expRatio = currentPlayerData.GetEXPRatio();
            
            // EXPバーをアニメーション付きで更新
            expBarFill.DOFillAmount(expRatio, barAnimationDuration)
                .SetEase(barAnimationEase);
        }
    }

    /// <summary>
    /// 外部からキャラクター情報を設定するためのメソッド（UnityEventから呼び出し可能）
    /// </summary>
    /// <param name="characterIndex">キャラクターのインデックス（0-2）</param>
    public void SetCharacterByIndex(int characterIndex)
    {
        PlayerData dummyData = PlayerData.CreateDummyData(characterIndex);
        UpdatePlayerStatus(dummyData);
    }

    /// <summary>
    /// 現在のプレイヤーデータを取得
    /// </summary>
    public PlayerData GetCurrentPlayerData()
    {
        return currentPlayerData;
    }

    /// <summary>
    /// HPバーを直接アニメーション付きで更新
    /// </summary>
    /// <param name="targetRatio">目標の割合（0.0～1.0）</param>
    /// <param name="duration">アニメーション時間（省略時はデフォルト）</param>
    public void AnimateHPBar(float targetRatio, float? duration = null)
    {
        if (hpBarFill != null)
        {
            float animDuration = duration ?? barAnimationDuration;
            hpBarFill.DOFillAmount(Mathf.Clamp01(targetRatio), animDuration)
                .SetEase(barAnimationEase);
            
            // 色の変更も適用
            if (enableColorTransition)
            {
                Color targetColor = targetRatio <= 0.3f ? hpLowColor : hpColor;
                hpBarFill.DOColor(targetColor, colorTransitionDuration);
            }
        }
    }
    
    /// <summary>
    /// MPバーを直接アニメーション付きで更新
    /// </summary>
    /// <param name="targetRatio">目標の割合（0.0～1.0）</param>
    /// <param name="duration">アニメーション時間（省略時はデフォルト）</param>
    public void AnimateMPBar(float targetRatio, float? duration = null)
    {
        if (mpBarFill != null)
        {
            float animDuration = duration ?? barAnimationDuration;
            mpBarFill.DOFillAmount(Mathf.Clamp01(targetRatio), animDuration)
                .SetEase(barAnimationEase);
        }
    }
    
    /// <summary>
    /// EXPバーを直接アニメーション付きで更新
    /// </summary>
    /// <param name="targetRatio">目標の割合（0.0～1.0）</param>
    /// <param name="duration">アニメーション時間（省略時はデフォルト）</param>
    public void AnimateEXPBar(float targetRatio, float? duration = null)
    {
        if (expBarFill != null)
        {
            float animDuration = duration ?? barAnimationDuration;
            expBarFill.DOFillAmount(Mathf.Clamp01(targetRatio), animDuration)
                .SetEase(barAnimationEase);
        }
    }
    
    /// <summary>
    /// すべてのTweenを停止
    /// </summary>
    public void StopAllBarAnimations()
    {
        if (hpBarFill != null) hpBarFill.DOKill();
        if (mpBarFill != null) mpBarFill.DOKill();
        if (expBarFill != null) expBarFill.DOKill();
    }
    
    /// <summary>
    /// テスト用：ダミーデータでステータスを更新
    /// </summary>
    [ContextMenu("テスト用ダミーデータ更新")]
    public void TestUpdateWithDummyData()
    {
        int randomIndex = Random.Range(0, 3);
        SetCharacterByIndex(randomIndex);
        Debug.Log($"ダミーデータ更新: キャラクター{randomIndex}");
    }
    
    /// <summary>
    /// テスト用：HPダメージアニメーション
    /// </summary>
    [ContextMenu("テスト用HPダメージ")]
    public void TestHPDamage()
    {
        if (currentPlayerData != null)
        {
            float currentRatio = currentPlayerData.GetHPRatio();
            float damageRatio = Random.Range(0.1f, 0.3f);
            float newRatio = Mathf.Max(0f, currentRatio - damageRatio);
            
            AnimateHPBar(newRatio, 0.8f);
            Debug.Log($"HPダメージテスト: {currentRatio:F2} → {newRatio:F2}");
        }
    }
    
    /// <summary>
    /// テスト用：MP消費アニメーション
    /// </summary>
    [ContextMenu("テスト用MP消費")]
    public void TestMPConsume()
    {
        if (currentPlayerData != null)
        {
            float currentRatio = currentPlayerData.GetMPRatio();
            float consumeRatio = Random.Range(0.1f, 0.4f);
            float newRatio = Mathf.Max(0f, currentRatio - consumeRatio);
            
            AnimateMPBar(newRatio, 0.6f);
            Debug.Log($"MP消費テスト: {currentRatio:F2} → {newRatio:F2}");
        }
    }

    /// <summary>
    /// バフアイコンを更新（CharacterBuffManagerから呼び出される）
    /// </summary>
    public void UpdateBuffIcons(List<BuffInstance> buffs)
    {
        if (buffIconDisplay == null)
        {
            buffIconDisplay = GetComponent<BuffIconDisplay>();
            if (buffIconDisplay != null)
            {
                buffIconDisplay.Configure(buffIconContainer, buffIconPrefab, iconSize);
            }
        }

        buffIconDisplay?.UpdateBuffIcons(buffs);
    }
} 