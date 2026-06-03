using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ネガポジ反転（RGB を 1 から引く）の Volume。強度 0〜1。
/// ザワールド等は <see cref="SetRuntimeIntensity"/> で Profile を 0 のまま制御できる。
/// </summary>
[Serializable]
[VolumeComponentMenuForRenderPipeline("Custom/Color Invert", typeof(UniversalRenderPipeline))]
public sealed class ColorInvertVolume : VolumeComponent, IPostProcessComponent
{
    /// <summary>-1 のとき Profile / Volume の intensity を使用。</summary>
    private static float s_RuntimeIntensity = -1f;

    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    public static void SetRuntimeIntensity(float value)
    {
        s_RuntimeIntensity = value;
    }

    public static float GetEffectiveIntensity()
    {
        if (s_RuntimeIntensity >= 0f)
        {
            return s_RuntimeIntensity;
        }

        var stack = VolumeManager.instance.stack.GetComponent<ColorInvertVolume>();
        return stack != null ? stack.intensity.value : 0f;
    }

    public static bool ShouldApply()
    {
        return GetEffectiveIntensity() > 0.001f;
    }

    public bool IsActive() => active && GetEffectiveIntensity() > 0.001f;

    public bool IsTileCompatible() => false;
}
