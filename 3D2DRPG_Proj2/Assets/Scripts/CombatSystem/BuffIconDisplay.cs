using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// バフ／デバフアイコンを表示する共通コンポーネント（味方パネル・敵ワールドUI共用）
/// </summary>
public class BuffIconDisplay : MonoBehaviour
{
    [Header("アイコン設定")]
    [SerializeField] private Transform buffIconContainer;
    [SerializeField] private GameObject buffIconPrefab;
    [SerializeField] private float iconSize = 32f;

    [Header("色分け（任意）")]
    [SerializeField] private bool tintByEffectType = true;
    [SerializeField] private Color buffTint = Color.white;
    [SerializeField] private Color debuffTint = new Color(1f, 0.65f, 0.65f, 1f);

    [Header("ワールド空間表示")]
    [SerializeField] private bool faceCamera;

    private readonly List<GameObject> activeBuffIcons = new List<GameObject>();
    private Camera targetCamera;

    private void Awake()
    {
        if (buffIconContainer != null)
        {
            buffIconContainer.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!faceCamera || buffIconContainer == null || !buffIconContainer.gameObject.activeInHierarchy)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            transform.rotation = targetCamera.transform.rotation;
        }
    }

    /// <summary>
    /// 既存UI（PlayerStatusPanel等）から参照を渡して初期化
    /// </summary>
    public void Configure(Transform container, GameObject prefab, float size = 32f)
    {
        buffIconContainer = container;
        buffIconPrefab = prefab;
        iconSize = size;

        if (buffIconContainer != null)
        {
            buffIconContainer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 敵キャラにワールド空間のバフアイコンUIを追加
    /// </summary>
    public static BuffIconDisplay AttachToWorldSpaceCharacter(
        GameObject owner,
        GameObject prefab,
        Vector3 localOffset,
        float canvasScale = 0.01f)
    {
        if (owner == null || prefab == null)
        {
            Debug.LogWarning("[BuffIconDisplay] 敵へのバフアイコン追加に失敗: owner または prefab が null です");
            return null;
        }

        var canvasGo = new GameObject("BuffIconCanvas");
        canvasGo.transform.SetParent(owner.transform, false);
        canvasGo.transform.localPosition = localOffset;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220f, 48f);
        canvasRect.localScale = Vector3.one * canvasScale;

        var containerGo = new GameObject("BuffIconContainer");
        containerGo.transform.SetParent(canvasGo.transform, false);

        var containerRect = containerGo.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 1f);
        containerRect.anchorMax = new Vector2(0f, 1f);
        containerRect.pivot = new Vector2(0f, 1f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(220f, 48f);

        var layout = containerGo.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 4f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var display = canvasGo.AddComponent<BuffIconDisplay>();
        display.Configure(containerGo.transform, prefab, 32f);
        display.faceCamera = true;
        return display;
    }

    /// <summary>
    /// バフアイコンを更新（CharacterBuffManager.OnBuffsChanged から呼び出す）
    /// </summary>
    public void UpdateBuffIcons(List<BuffInstance> buffs)
    {
        ClearBuffIcons();

        if (buffIconContainer == null || buffIconPrefab == null)
        {
            return;
        }

        if (buffs == null || buffs.Count == 0)
        {
            buffIconContainer.gameObject.SetActive(false);
            return;
        }

        buffIconContainer.gameObject.SetActive(true);

        foreach (var buff in buffs)
        {
            if (buff?.baseData == null || buff.baseData.icon == null)
            {
                continue;
            }

            CreateBuffIcon(buff.baseData.icon, buff);
        }
    }

    private void CreateBuffIcon(Sprite icon, BuffInstance buff)
    {
        GameObject iconObj = Instantiate(buffIconPrefab, buffIconContainer);

        if (iconSize > 0f)
        {
            var layoutElement = iconObj.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = iconObj.AddComponent<LayoutElement>();
            }
            layoutElement.preferredWidth = iconSize;
            layoutElement.preferredHeight = iconSize;
        }

        Image iconImage = iconObj.GetComponent<Image>();
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            if (tintByEffectType)
            {
                iconImage.color = IsDebuff(buff.baseData) ? debuffTint : buffTint;
            }
        }

        TextMeshProUGUI turnText = iconObj.GetComponentInChildren<TextMeshProUGUI>();
        if (turnText != null && buff.remainingTurns > 0)
        {
            turnText.text = buff.remainingTurns.ToString();
        }

        activeBuffIcons.Add(iconObj);
    }

    private static bool IsDebuff(BuffBase buffData)
    {
        switch (buffData.statusEffect)
        {
            case StatusEffect.Poison:
            case StatusEffect.Stun:
            case StatusEffect.Burn:
            case StatusEffect.Freeze:
            case StatusEffect.Sleep:
            case StatusEffect.Silent:
            case StatusEffect.SpdDown:
            case StatusEffect.MagicDamageDown:
            case StatusEffect.Makituki:
            case StatusEffect.LockIn:
                return true;
            default:
                return false;
        }
    }

    private void ClearBuffIcons()
    {
        foreach (var icon in activeBuffIcons)
        {
            if (icon != null)
            {
                Destroy(icon);
            }
        }
        activeBuffIcons.Clear();
    }
}
