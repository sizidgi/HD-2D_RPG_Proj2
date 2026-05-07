using UnityEngine;

/// <summary>
/// BGM の AudioClip をまとめて保持するアセット。
/// Resources に配置し、ランタイム生成された BGMManager から読み込む。
/// 例: Assets/Resources/BGM/BGMAudioLibrary.asset（Load パスは BGM/BGMAudioLibrary）
/// </summary>
[CreateAssetMenu(fileName = "BGMAudioLibrary", menuName = "RyotaSuzuki/Audio/BGM Audio Library")]
public class BGMAudioLibrary : ScriptableObject
{
    public AudioClip battleNormalBGM;
    public AudioClip battleBossBGM;
    public AudioClip fieldBGM;

    [Tooltip("BGMManager と同じフェード・音量を使う場合は未使用でよい")]
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
}
