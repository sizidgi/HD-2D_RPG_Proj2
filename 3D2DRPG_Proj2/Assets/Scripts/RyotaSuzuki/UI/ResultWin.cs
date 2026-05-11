using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ResultWin : MonoBehaviour
{
    [SerializeField, Header("")]
    private PlayerManager playerManager;

    private List<GameObject> players;

    [SerializeField, Header("Prefab")]
    private GameObject UIPrefab;
    [SerializeField, Header("UIParent")]
    private Transform UIParent;

    [Header("スグル")]
    [SerializeField] private Image SuguruExpBarBackground;
    [SerializeField] private Image SuguruExpFill;
    [SerializeField] private TextMeshProUGUI SuguruExpText;
    [SerializeField] private TextMeshProUGUI SuguruLevelText;

    [Header("照")]
    [SerializeField] private Image TeruExpBarBackground;
    [SerializeField] private Image TeruExpFill;
    [SerializeField] private TextMeshProUGUI TeruExpText;
    [SerializeField] private TextMeshProUGUI TeruLevelText;
    
    [Header("Expアニメ")]
    [SerializeField] private float expAnimationDuration = 2f;
    [SerializeField] private Ease expAnimationEase = Ease.OutQuad;

    void Start()
    {
        players = playerManager.GetPlayerCharacters();
        StartCoroutine(AnimateExpGain());
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Space) || (Input.GetKey(KeyCode.Return))) 
            GameManager.Instance.EndBattle();
    }

    private IEnumerator AnimateExpGain()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null)
        {
            Debug.LogError("[ResultWin] GameManagerPlayerDatanull");
            yield break;
        }
        
        Debug.Log($"[ResultWin] GameManager.PlayerData: {GameManager.Instance.PlayerData.Count}");
        
        foreach (var playerChar in GameManager.Instance.PlayerData)
        {
            if (playerChar == null) continue;
            
            Debug.Log($"[ResultWin] 処理開始: {playerChar.charactername} 現在Lv.{playerChar.level} EXP:{playerChar.exp}");
            
            
            var snapshot = GameManager.Instance.GetPreBattleSnapshot(playerChar.charactername);
            
            if (snapshot == null)
            {
                Debug.LogError($"[ResultWin] スナップショットが見つかりません: {playerChar.charactername} - フォールバック処理を使用（戦闘後のレベル {playerChar.level} を使用するため不正確）");
                
                snapshot = new GameManager.PreBattleSnapshot
                {
                    characterName = playerChar.charactername,
                    level = playerChar.level,
                    exp = 0,
                    requiredExp = GameManager.Instance.GetRequiredExp(playerChar.level),
                    totalExp = GameManager.Instance.CalculateTotalExp(playerChar.level, 0)
                };
            }
            else
            {
                Debug.Log($"[ResultWin] スナップショット取得成功: {playerChar.charactername} 戦闘前Lv.{snapshot.level} EXP:{snapshot.exp}/{snapshot.requiredExp}");
            }
            
            Debug.Log($"[ResultWin] アニメーション開始値: {playerChar.charactername} Lv.{snapshot.level} {snapshot.exp}/{snapshot.requiredExp}");
            
            Image expFill = null;
            TextMeshProUGUI expText = null;
            TextMeshProUGUI levelText = null;
            
            if (playerChar.charactername == "スグル")
            {
                expFill = SuguruExpFill;
                expText = SuguruExpText;
                levelText = SuguruLevelText;
            }
            else if (playerChar.charactername == "照")
            {
                expFill = TeruExpFill;
                expText = TeruExpText;
                levelText = TeruLevelText;
            }
            
            if (expFill != null)
            {
                Debug.Log($"[ResultWin]: {playerChar.charactername}");
                StartCoroutine(AnimateExpForCharacter(playerChar, snapshot, expFill, expText, levelText));
            }
            else
            {
                Debug.LogWarning($"[ResultWin] {playerChar.charactername} �̌o���l�o�[��null�ł�");
            }
        }
    }
    
    private IEnumerator AnimateExpForCharacter(
        CharacterData character,
        GameManager.PreBattleSnapshot snapshot,
        Image expFill,
        TextMeshProUGUI expText,
        TextMeshProUGUI levelText)
    {
        int currentLevel = snapshot.level;
        int currentExp = snapshot.exp;
        int requiredExp = snapshot.requiredExp;
        
        int currentTotalExp = GameManager.Instance.CalculateTotalExp(character.level, character.exp);
        int gainedExp = currentTotalExp - snapshot.totalExp;
        
        Debug.Log($"[ResultWin] {character.charactername} : Lv.{currentLevel} {currentExp}/{requiredExp}EXP +{gainedExp}EXP{snapshot.totalExp}/{currentTotalExp})");
        
        if (levelText != null)
        {
            levelText.text = $"Lv.{currentLevel}";
        }
        
        if (expText != null)
        {
            expText.text = $"{currentExp}/{requiredExp}";
        }
        
        if (requiredExp > 0)
        {
            expFill.fillAmount = (float)currentExp / requiredExp;
        }
        
        if (gainedExp <= 0)
        {
            Debug.Log($"[ResultWin] {character.charactername}");
            yield break;
        }
        
        int remainingExp = gainedExp; // 残りの獲得経験値
        
        while (remainingExp > 0)
        {
            int expToGain = Mathf.Min(remainingExp, requiredExp - currentExp);
            int finalExp = currentExp + expToGain;
            float targetRatio = requiredExp > 0 ? (float)finalExp / requiredExp : 0;
            
            float animDuration = expAnimationDuration * ((float)expToGain / Mathf.Max(gainedExp, 1));
            
            Debug.Log($"[ResultWin]  {currentExp} → {finalExp} / {requiredExp} (獲得: +{expToGain})");
            
            yield return expFill.DOFillAmount(targetRatio, animDuration)
                .SetEase(expAnimationEase)
                .OnUpdate(() =>
                {
                    if (expText != null && requiredExp > 0)
                    {
                        int displayExp = Mathf.RoundToInt(expFill.fillAmount * requiredExp);
                        expText.text = $"{displayExp}/{requiredExp}";
                    }
                })
                .WaitForCompletion();
            
            currentExp = finalExp;
            remainingExp -= expToGain;
            
            if (currentExp >= requiredExp)
            {
                currentLevel++;
                currentExp -= requiredExp;
                requiredExp = GameManager.Instance.GetRequiredExp(currentLevel);
                
                
                if (levelText != null)
                {
                    levelText.text = $"Lv.{currentLevel}";
                    levelText.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f);
                }
                
                expFill.fillAmount = 0f;
                yield return new WaitForSeconds(0.3f);
            }
            
            if (expText != null)
            {
                expText.text = $"{currentExp}/{requiredExp}";
            }
        }
        
        
    }
}
