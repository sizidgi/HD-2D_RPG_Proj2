using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM管理クラス（シングルトン）
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [Header("AudioSource")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGMクリップ")]
    [Tooltip("通常戦闘BGM")]
    public AudioClip battleNormalBGM;
    
    [Tooltip("ボス戦BGM")]
    public AudioClip battleBossBGM;
    
    [Tooltip("フィールドBGM")]
    public AudioClip fieldBGM;

    [Header("BGM設定")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float fadeInDuration = 1f;

    // 現在再生中のBGM名
    private string currentBGMName = "";
    
    // フェード処理用のコルーチン
    private Coroutine fadeCoroutine;

    void Awake()
    {
        // シングルトンパターン
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[BGMManager] BGMManagerを初期化しました");
        }
        else
        {
            Debug.Log("[BGMManager] 既存のBGMManagerが存在するため、このインスタンスを破棄します");
            Destroy(gameObject);
            return;
        }

        // AudioSourceの初期化
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            Debug.Log("[BGMManager] AudioSourceを自動生成しました");
        }
        
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;
        
        Debug.Log($"[BGMManager] AudioSource設定完了 - Volume: {bgmVolume}");
    }

    /// <summary>
    /// BGMを再生（クリップ名で指定）
    /// </summary>
    /// <param name="bgmName">BGM名（例: "BattleNormal", "BattleBoss", "Field"）</param>
    /// <param name="fade">フェード効果を使用するか</param>
    public void PlayBGM(string bgmName, bool fade = true)
    {
        if (string.IsNullOrEmpty(bgmName))
        {
            Debug.LogWarning("[BGMManager] BGM名が空です");
            return;
        }

        // 同じBGMが既に再生中の場合は何もしない
        if (currentBGMName == bgmName && bgmSource.isPlaying)
        {
            Debug.Log($"[BGMManager] {bgmName} は既に再生中です");
            return;
        }

        AudioClip clipToPlay = GetBGMClip(bgmName);
        
        if (clipToPlay == null)
        {
            Debug.LogWarning($"[BGMManager] BGMクリップが見つかりません: {bgmName}");
            return;
        }

        if (fade)
        {
            // フェード付きで切り替え
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeAndPlayBGM(clipToPlay, bgmName));
        }
        else
        {
            // 即座に切り替え
            bgmSource.Stop();
            bgmSource.clip = clipToPlay;
            bgmSource.Play();
            currentBGMName = bgmName;
            Debug.Log($"[BGMManager] BGM再生: {bgmName}");
        }
    }

    /// <summary>
    /// BGM名からAudioClipを取得
    /// </summary>
    private AudioClip GetBGMClip(string bgmName)
    {
        switch (bgmName.ToLower())
        {
            case "battlenormal":
            case "battle_normal":
                return battleNormalBGM;
                
            case "battleboss":
            case "battle_boss":
                return battleBossBGM;
                
            case "field":
                return fieldBGM;
                
            default:
                // Resourcesフォルダから動的に読み込む試み
                AudioClip clip = Resources.Load<AudioClip>($"Audio/BGM/{bgmName}");
                if (clip != null)
                {
                    return clip;
                }
                return null;
        }
    }

    /// <summary>
    /// フェードアウトしてから新しいBGMをフェードインで再生
    /// </summary>
    private IEnumerator FadeAndPlayBGM(AudioClip newClip, string newBGMName)
    {
        // フェードアウト
        if (bgmSource.isPlaying)
        {
            float startVolume = bgmSource.volume;
            float elapsedTime = 0f;

            while (elapsedTime < fadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / fadeOutDuration);
                yield return null;
            }

            bgmSource.Stop();
        }

        // 新しいBGMを設定
        bgmSource.clip = newClip;
        bgmSource.Play();
        currentBGMName = newBGMName;

        // フェードイン
        float targetVolume = bgmVolume;
        float fadeInTime = 0f;

        while (fadeInTime < fadeInDuration)
        {
            fadeInTime += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, fadeInTime / fadeInDuration);
            yield return null;
        }

        bgmSource.volume = targetVolume;
        Debug.Log($"[BGMManager] BGM再生完了: {newBGMName}");
    }

    /// <summary>
    /// BGMを停止
    /// </summary>
    /// <param name="fade">フェード効果を使用するか</param>
    public void StopBGM(bool fade = true)
    {
        if (fade)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeOutAndStop());
        }
        else
        {
            bgmSource.Stop();
            currentBGMName = "";
            Debug.Log("[BGMManager] BGM停止");
        }
    }

    /// <summary>
    /// フェードアウトしてBGMを停止
    /// </summary>
    private IEnumerator FadeOutAndStop()
    {
        float startVolume = bgmSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / fadeOutDuration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.volume = bgmVolume; // ボリュームを元に戻す
        currentBGMName = "";
        Debug.Log("[BGMManager] BGM停止（フェードアウト完了）");
    }

    /// <summary>
    /// BGMの音量を設定
    /// </summary>
    public void SetVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    /// <summary>
    /// 現在のBGM名を取得
    /// </summary>
    public string GetCurrentBGMName()
    {
        return currentBGMName;
    }

    /// <summary>
    /// BGMが再生中かどうか
    /// </summary>
    public bool IsPlaying()
    {
        return bgmSource != null && bgmSource.isPlaying;
    }
}
