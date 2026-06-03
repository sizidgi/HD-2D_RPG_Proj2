using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// ザ・ワールド風の画面演出。白い矩形が等間隔で連なり、1本の螺旋レール上を先頭が引いて動く。
/// </summary>
public class ZaWarudoScreenEffect : ScreenSpaceSkillVfx
{
    private sealed class ChainPanel
    {
        public RectTransform Rect;
        public Image Image;
        public int Index;
    }

    private static Sprite _cachedWhiteSprite;
    private static readonly int InvertIntensityId = Shader.PropertyToID("_Intensity");
    private static Shader _invertOverlayShader;

    [Header("タイミング")]
    [SerializeField] private float convergeDuration = 0.9f;
    [SerializeField] private float holdDuration = 4f;
    [SerializeField] private float scatterDuration = 1.1f;

    [Header("連鎖螺旋")]
    [SerializeField] private int panelCount = 14;
    [SerializeField] private Vector2 panelSize = new Vector2(64f, 180f);
    [Tooltip("螺旋レール上の隣接矩形の間隔（0.05〜0.1 推奨。小さいほど □□□ に近い）")]
    [SerializeField] private float chainInterval = 0.065f;
    [SerializeField] private float startRadius = 1350f;
    [SerializeField] private float spiralTurns = 1.15f;
    [SerializeField] private float angleOffsetDegrees = -18f;
    [SerializeField] private bool rotateWithSpiral = true;
    [SerializeField] private int canvasSortOrder = 8000;
    [SerializeField] private float flashPeakAlpha = 0.18f;

    [Header("吸い込み")]
    [Range(0.5f, 1f)]
    [SerializeField] private float vanishFadeStart = 0.78f;

    [Header("終了：中心から四方へ散開")]
    [SerializeField] private float scatterMaxRadius = 1400f;
    [Tooltip("枚ごとの遅れ（秒）。0 なら一斉に飛び出す")]
    [SerializeField] private float scatterStaggerPerPanel = 0.02f;
    [Range(0.35f, 0.95f)]
    [Tooltip("各矩形の進行の何割から外側でフェードするか")]
    [SerializeField] private float scatterFadeStartAt = 0.6f;
    [SerializeField] private float scatterStartScale = 0.35f;

    [Header("ポストプロセス（未指定時は BattlePostProcessing を検索）")]
    [SerializeField] private Volume battleVolume;

    [Header("ポストプロセス（控えめ）")]
    [Range(0f, 1f)]
    [SerializeField] private float postEffectStrength = 0.45f;
    [SerializeField] private float targetSaturation = -55f;
    [SerializeField] private float targetContrastBoost = 4f;
    [SerializeField] private float targetVignetteBoost = 0.18f;
    [SerializeField] private float targetPostExposure = -0.12f;
    [Header("ネガ反転（ザワールド中のみ・UIオーバーレイ）")]
    [Range(0f, 1f)]
    [SerializeField] private float targetInvertIntensity = 1f;

    private readonly List<ChainPanel> _panels = new List<ChainPanel>();

    private Image _invertOverlay;
    private Material _invertMaterial;
    private Image _flashOverlay;
    private Canvas _canvas;
    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;

    private float _origSaturation;
    private float _origContrast;
    private float _origVignetteIntensity;
    private float _origPostExposure;
    private float _targetSaturation;
    private float _targetContrast;
    private float _targetVignette;
    private float _targetExposure;
    private float _targetInvert;
    private bool _hasOrigColor;
    private bool _hasOrigVignette;
    private bool _hasOrigExposure;

    private float _angleOffsetRad;
    private float _chainTailOffset;
    private Sequence _sequence;
    private bool _volumeCaptured;
    private bool _hasTeardown;

    private void Start()
    {
        Play();
    }

    private void OnDestroy()
    {
        Teardown();
    }

    public void Play()
    {
        _targetInvert = targetInvertIntensity;
        BuildScreenUi();
        CaptureVolumeState();
        ApplyVolumeBlend(0f);
        RunSequence();
    }

    private void BuildScreenUi()
    {
        var canvasGo = new GameObject("ZaWarudoCanvas");
        canvasGo.transform.SetParent(null, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = canvasSortOrder;
        _canvas.overrideSorting = true;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var sprite = GetWhiteSprite();

        CreateInvertOverlay(canvasGo.transform, sprite);

        _flashOverlay = CreateImage(canvasGo.transform, "Flash", sprite, new Color(1f, 1f, 1f, 0f));
        StretchFullScreen(_flashOverlay.rectTransform);

        int count = Mathf.Max(4, panelCount);
        _angleOffsetRad = angleOffsetDegrees * Mathf.Deg2Rad;
        _chainTailOffset = (count - 1) * chainInterval;

        for (int i = 0; i < count; i++)
        {
            var image = CreateImage(_canvas.transform, $"Chain_{i}", sprite, new Color(1f, 1f, 1f, 0f));
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = panelSize;
            rect.localScale = Vector3.one;

            _panels.Add(new ChainPanel
            {
                Rect = rect,
                Image = image,
                Index = i
            });
        }

        ApplyChainHead(0f, inward: true);
    }

    private void CreateInvertOverlay(Transform parent, Sprite sprite)
    {
        if (_invertOverlayShader == null)
        {
            _invertOverlayShader = Shader.Find("RyotaSuzuki/UI/ScreenInvertOverlay");
        }

        if (_invertOverlayShader == null)
        {
            Debug.LogWarning("[ZaWarudoScreenEffect] ScreenInvertOverlay シェーダが見つかりません。ネガ反転はスキップします。");
            return;
        }

        _invertMaterial = new Material(_invertOverlayShader);
        _invertOverlay = CreateImage(parent, "ScreenInvert", sprite, Color.white);
        _invertOverlay.raycastTarget = false;
        _invertOverlay.material = _invertMaterial;
        StretchFullScreen(_invertOverlay.rectTransform);
        SetInvertOverlayIntensity(0f);
    }

    private void SetInvertOverlayIntensity(float intensity)
    {
        if (_invertMaterial != null)
        {
            _invertMaterial.SetFloat(InvertIntensityId, Mathf.Clamp01(intensity));
        }

        if (_invertOverlay != null)
        {
            _invertOverlay.enabled = intensity > 0.001f;
        }
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite GetWhiteSprite()
    {
        if (_cachedWhiteSprite != null)
        {
            return _cachedWhiteSprite;
        }

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        _cachedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        return _cachedWhiteSprite;
    }

    /// <summary>螺旋レール上の位置（pathS: 0=外周, 1=中心）</summary>
    private Vector2 GetSpiralPosition(float pathS)
    {
        float radius = Mathf.Lerp(startRadius, 0f, pathS);
        float theta = _angleOffsetRad + spiralTurns * Mathf.PI * 2f * pathS;
        return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radius;
    }

    private float GetSpiralRotationZ(float pathS)
    {
        float theta = _angleOffsetRad + spiralTurns * Mathf.PI * 2f * pathS;
        return theta * Mathf.Rad2Deg + 90f;
    }

    private void SetPanelHidden(ChainPanel panel)
    {
        if (panel.Image != null)
        {
            panel.Image.color = new Color(1f, 1f, 1f, 0f);
        }
        panel.Rect.anchoredPosition = Vector2.zero;
        panel.Rect.localScale = Vector3.zero;
    }

    private void ApplyChainHead(float headPath, bool inward)
    {
        foreach (var panel in _panels)
        {
            float localPath = headPath - panel.Index * chainInterval;
            ApplyPanelOnPath(panel, localPath, inward);
        }
    }

    private void ApplyPanelOnPath(ChainPanel panel, float pathS, bool inward)
    {
        if (pathS < 0f || pathS > 1f)
        {
            SetPanelHidden(panel);
            return;
        }

        float alpha = 1f;
        float scale = 1f;

        if (inward)
        {
            if (pathS >= vanishFadeStart)
            {
                float vanishT = (pathS - vanishFadeStart) / (1f - vanishFadeStart);
                alpha = 1f - vanishT;
                scale = 1f - vanishT;
            }
        }
        if (alpha <= 0.01f)
        {
            SetPanelHidden(panel);
            return;
        }

        panel.Rect.anchoredPosition = GetSpiralPosition(pathS);
        panel.Rect.localScale = Vector3.one * scale;

        if (panel.Image != null)
        {
            panel.Image.color = new Color(1f, 1f, 1f, alpha);
        }

        if (rotateWithSpiral)
        {
            panel.Rect.localEulerAngles = new Vector3(0f, 0f, GetSpiralRotationZ(pathS));
        }
    }

    private void ApplyRadialBurst(float globalT)
    {
        int count = _panels.Count;
        if (count == 0)
        {
            return;
        }

        float maxStagger = Mathf.Max(0f, (count - 1) * scatterStaggerPerPanel);
        float denom = Mathf.Max(0.001f, scatterDuration - maxStagger);

        foreach (var panel in _panels)
        {
            float delay = panel.Index * scatterStaggerPerPanel;
            float localT = Mathf.Clamp01((globalT * scatterDuration - delay) / denom);
            float moveT = DOVirtual.EasedValue(0f, 1f, localT, Ease.OutCubic);

            if (localT <= 0f)
            {
                SetPanelAtCenter(panel, scatterStartScale);
                continue;
            }

            float angle = _angleOffsetRad + (Mathf.PI * 2f * panel.Index / count);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = scatterMaxRadius * moveT;

            float alpha = 1f;
            if (localT >= scatterFadeStartAt)
            {
                float fadeT = (localT - scatterFadeStartAt) / (1f - scatterFadeStartAt);
                alpha = 1f - fadeT;
            }

            float scale = Mathf.Lerp(scatterStartScale, 1f, Mathf.Min(1f, moveT * 2.5f));

            if (alpha <= 0.01f)
            {
                SetPanelHidden(panel);
                continue;
            }

            panel.Rect.anchoredPosition = dir * radius;
            panel.Rect.localScale = Vector3.one * scale;

            if (panel.Image != null)
            {
                panel.Image.color = new Color(1f, 1f, 1f, alpha);
            }

            if (rotateWithSpiral)
            {
                panel.Rect.localEulerAngles = new Vector3(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            }
        }
    }

    private void SetPanelAtCenter(ChainPanel panel, float scale)
    {
        panel.Rect.anchoredPosition = Vector2.zero;
        panel.Rect.localScale = Vector3.one * scale;
        if (panel.Image != null)
        {
            panel.Image.color = Color.white;
        }
    }

    private Tween CreateRadialBurstTween(float duration)
    {
        float t = 0f;
        return DOTween.To(() => t, value =>
            {
                t = value;
                ApplyRadialBurst(t);
            }, 1f, duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true);
    }

    private Tween CreateChainDriveTween(float headFrom, float headTo, float duration, bool inward)
    {
        float head = headFrom;
        return DOTween.To(() => head, value =>
            {
                head = value;
                ApplyChainHead(head, inward);
            }, headTo, duration)
            .SetEase(inward ? Ease.InCubic : Ease.OutQuad)
            .SetUpdate(true);
    }

    private void CaptureVolumeState()
    {
        if (battleVolume == null)
        {
            battleVolume = FindBattleVolume();
        }

        if (battleVolume == null || battleVolume.profile == null)
        {
            Debug.LogWarning("[ZaWarudoScreenEffect] 戦闘用 Volume が見つかりません。UI演出のみ再生します。");
            return;
        }

        _volumeCaptured = true;

        if (battleVolume.profile.TryGet(out _colorAdjustments))
        {
            _origSaturation = _colorAdjustments.saturation.value;
            _origContrast = _colorAdjustments.contrast.value;
            _origPostExposure = _colorAdjustments.postExposure.value;
            _hasOrigColor = true;
            _hasOrigExposure = true;

            _targetSaturation = Mathf.Lerp(_origSaturation, targetSaturation, postEffectStrength);
            _targetContrast = _origContrast + targetContrastBoost * postEffectStrength;
            _targetExposure = _origPostExposure + targetPostExposure * postEffectStrength;
        }

        if (battleVolume.profile.TryGet(out _vignette))
        {
            _origVignetteIntensity = _vignette.intensity.value;
            _hasOrigVignette = true;
            _targetVignette = _origVignetteIntensity + targetVignetteBoost * postEffectStrength;
        }

    }

    private static Volume FindBattleVolume()
    {
        var volumes = FindObjectsOfType<Volume>();
        foreach (var volume in volumes)
        {
            if (volume == null || !volume.isActiveAndEnabled) continue;
            if (volume.gameObject.name.Contains("BattlePostProcessing"))
            {
                return volume;
            }
        }

        foreach (var volume in volumes)
        {
            if (volume != null && volume.isGlobal && volume.isActiveAndEnabled)
            {
                return volume;
            }
        }

        return null;
    }

    private void RunSequence()
    {
        _sequence?.Kill();
        _hasTeardown = false;
        _sequence = DOTween.Sequence().SetUpdate(true);

        // 連鎖全体が外周から入り、先頭が中心へ（□が等間隔で続く）
        float convergeHeadEnd = 1f + _chainTailOffset;

        var converge = DOTween.Sequence().SetUpdate(true);
        converge.Append(CreateChainDriveTween(0f, convergeHeadEnd, convergeDuration, inward: true));

        if (_flashOverlay != null)
        {
            converge.Join(_flashOverlay
                .DOFade(flashPeakAlpha, convergeDuration * 0.55f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true));
        }

        converge.Join(CreateVolumeBlendTween(convergeDuration, 0f, 1f));
        converge.AppendCallback(() =>
        {
            foreach (var panel in _panels)
            {
                SetPanelHidden(panel);
            }
        });

        _sequence.Append(converge);
        _sequence.AppendInterval(holdDuration);

        // 中心に溜まった矩形が、等角度で四方へ飛び散る
        var scatter = DOTween.Sequence().SetUpdate(true);
        scatter.AppendCallback(() =>
        {
            foreach (var panel in _panels)
            {
                SetPanelAtCenter(panel, scatterStartScale);
            }
        });
        scatter.Append(CreateRadialBurstTween(scatterDuration));

        if (_flashOverlay != null)
        {
            scatter.Join(_flashOverlay.DOFade(0f, scatterDuration).SetUpdate(true));
        }

        scatter.Join(CreateVolumeBlendTween(scatterDuration, 1f, 0f));

        _sequence.Append(scatter);
        _sequence.OnComplete(() =>
        {
            Teardown();
            Destroy(gameObject);
        });
    }

    private void Teardown()
    {
        if (_hasTeardown)
        {
            return;
        }

        _hasTeardown = true;
        _sequence?.Kill();
        _sequence = null;

        RestoreVolumeImmediate();

        if (_invertMaterial != null)
        {
            Destroy(_invertMaterial);
            _invertMaterial = null;
        }

        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
            _canvas = null;
        }

        _invertOverlay = null;
        _flashOverlay = null;
    }

    private Tween CreateVolumeBlendTween(float duration, float fromBlend, float toBlend)
    {
        float blend = fromBlend;
        return DOTween.To(() => blend, value =>
            {
                blend = value;
                ApplyVolumeBlend(blend);
            }, toBlend, duration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true);
    }

    private void ApplyVolumeBlend(float t)
    {
        SetInvertOverlayIntensity(Mathf.Lerp(0f, _targetInvert, t));

        if (!_volumeCaptured)
        {
            return;
        }

        if (_hasOrigColor)
        {
            _colorAdjustments.saturation.Override(Mathf.Lerp(_origSaturation, _targetSaturation, t));
            _colorAdjustments.contrast.Override(Mathf.Lerp(_origContrast, _targetContrast, t));
            _colorAdjustments.postExposure.Override(Mathf.Lerp(_origPostExposure, _targetExposure, t));
        }

        if (_hasOrigVignette && _vignette != null)
        {
            _vignette.intensity.Override(Mathf.Lerp(_origVignetteIntensity, _targetVignette, t));
        }
    }

    private void RestoreVolumeImmediate()
    {
        SetInvertOverlayIntensity(0f);

        if (!_volumeCaptured) return;

        if (_hasOrigColor && _colorAdjustments != null)
        {
            _colorAdjustments.saturation.Override(_origSaturation);
            _colorAdjustments.contrast.Override(_origContrast);
            _colorAdjustments.postExposure.Override(_origPostExposure);
        }

        if (_hasOrigVignette && _vignette != null)
        {
            _vignette.intensity.Override(_origVignetteIntensity);
        }
    }
}
