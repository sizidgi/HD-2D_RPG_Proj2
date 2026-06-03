using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ネガポジ反転（RGB を 1 から引く）の Volume。強度 0〜1。
/// </summary>
[Serializable]
[VolumeComponentMenuForRenderPipeline("Custom/Color Invert", typeof(UniversalRenderPipeline))]
public sealed class ColorInvertVolume : VolumeComponent, IPostProcessComponent
{
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    public bool IsActive() => active && intensity.value > 0.001f;

    public bool IsTileCompatible() => false;
}
