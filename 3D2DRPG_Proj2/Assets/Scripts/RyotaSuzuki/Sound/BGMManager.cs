using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM管理クラス（シングルトン）
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    private static BGMAudioLibrary _cachedLibrary;

    /// <summary>
    /// GameField より前に戦闘などが始まる経路でも BGM を再生できるようにする。
    /// 優先: シーン内の BGMManager → Resources のプレハブ → 空オブジェクト + BGMAudioLibrary / AudioClip。
    /// </summary>
    public static BGMManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        BGMManager existing = FindObjectOfType<BGMManager>();
        if (existing != null)
        {
            if (!existing.gameObject.activeInHierarchy)
                existing.gameObject.SetActive(true);
            if (Instance == null)
                Instance = existing;
            return Instance;
        }

        // Inspector 設定済みのプレハブ（Resources に置く）
        string[] prefabPaths = { "BGMManager", "Prefabs/BGMManager", "BGM/BGMManager" };
        foreach (string path in prefabPaths)
        {
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
            {
                GameObject go = Object.Instantiate(prefab);
                go.name = "BGMManager";
                if (Instance != null)
                    return Instance;
                Debug.Log($"[BGMManager] Resources プレハブから生成しました: {path}");
                return Instance;
            }
        }

        GameObject empty = new GameObject("BGMManager");
        empty.AddComponent<BGMManager>();
        return Instance;
    }

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

    private bool HasAnyClipConfigured()
    {
        return battleNormalBGM != null || battleBossBGM != null || fieldBGM != null;
    }

    /// <summary>
    /// クリップ参照が空のとき、BGMAudioLibrary（Resources）で埋める。
    /// </summary>
    private void TryFillClipsFromLibrary()
    {
        if (HasAnyClipConfigured())
            return;

        if (_cachedLibrary == null)
        {
            _cachedLibrary = Resources.Load<BGMAudioLibrary>("BGM/BGMAudioLibrary");
            if (_cachedLibrary == null)
                _cachedLibrary = Resources.Load<BGMAudioLibrary>("BGMAudioLibrary");
        }

        if (_cachedLibrary == null)
            return;

        if (battleNormalBGM == null)
            battleNormalBGM = _cachedLibrary.battleNormalBGM;
        if (battleBossBGM == null)
            battleBossBGM = _cachedLibrary.battleBossBGM;
        if (fieldBGM == null)
            fieldBGM = _cachedLibrary.fieldBGM;
        if (_cachedLibrary.bgmVolume > 0f)
            bgmVolume = _cachedLibrary.bgmVolume;

        Debug.Log("[BGMManager] BGMAudioLibrary（Resources）からクリップを参照しました");
    }

    private void EnsureAudioSource()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            Debug.Log("[BGMManager] AudioSourceを自動生成しました");
        }

        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;
    }

    void Awake()
    {
        TryFillClipsFromLibrary();
        EnsureAudioSource();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[BGMManager] BGMManagerを初期化しました");
            Debug.Log($"[BGMManager] AudioSource設定完了 - Volume: {bgmVolume}");
            return;
        }

        if (Instance == this)
            return;

        // EnsureInstance で先行生成された「クリップ未設定」の側より、Inspector で設定した側を優先
        if (HasAnyClipConfigured() && !Instance.HasAnyClipConfigured())
        {
            BGMManager stale = Instance;
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (stale != null && stale != this)
                Destroy(stale.gameObject);
            Debug.Log("[BGMManager] Inspector設定済みのBGMManagerに差し替えました（先行ランタイム生成の救済）");
            return;
        }

        Debug.Log("[BGMManager] 既存のBGMManagerが存在するため、このインスタンスを破棄します");
        Destroy(gameObject);
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

        if (!HasAnyClipConfigured())
            TryFillClipsFromLibrary();

        AudioClip clipToPlay = GetBGMClip(bgmName);
        
        if (clipToPlay == null)
        {
            Debug.LogWarning($"[BGMManager] BGMクリップが見つかりません: {bgmName}（Resources の BGMAudioLibrary / BGMManager プレハブ、または Audio/BGM/{bgmName} を確認してください）");
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
    private static AudioClip TryLoadFromResources(string relativePath)
    {
        return Resources.Load<AudioClip>(relativePath);
    }

    private AudioClip GetBGMClip(string bgmName)
    {
        switch (bgmName.ToLower())
        {
            case "battlenormal":
            case "battle_normal":
                if (battleNormalBGM != null)
                    return battleNormalBGM;
                return TryLoadFromResources("Audio/BGM/BattleNormal")
                    ?? TryLoadFromResources("BGM/BattleNormal");

            case "battleboss":
            case "battle_boss":
                if (battleBossBGM != null)
                    return battleBossBGM;
                return TryLoadFromResources("Audio/BGM/BattleBoss")
                    ?? TryLoadFromResources("BGM/BattleBoss");

            case "field":
                if (fieldBGM != null)
                    return fieldBGM;
                return TryLoadFromResources("Audio/BGM/Field")
                    ?? TryLoadFromResources("BGM/Field");

            default:
                AudioClip clip = TryLoadFromResources($"Audio/BGM/{bgmName}");
                if (clip != null)
                    return clip;
                return TryLoadFromResources($"BGM/{bgmName}");
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
