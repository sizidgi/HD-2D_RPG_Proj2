using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲームオーバー（敗北）UI
/// UIManager 配下の Canvas にアタッチし、初期状態は非アクティブにしておく
/// </summary>
public class ResultLose : MonoBehaviour
{
    [SerializeField, Header("フェード制御（未設定なら自動取得）")]
    private CanvasGroup canvasGroup;

    [SerializeField, Header("背景画像")]
    private Image backgroundImage;

    [SerializeField, Header("Game Over 画像")]
    private Image gameOverImage;

    [SerializeField, Header("フェードイン時間")]
    private float fadeInDuration = 1.0f;

    [SerializeField, Header("フェードインのイージング")]
    private Ease fadeInEase = Ease.OutQuad;

    [SerializeField, Header("入力受付までの追加待機秒")]
    private float inputDelay = 0.3f;

    private bool canAcceptInput;
    private Tween fadeTween;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnEnable()
    {
        canAcceptInput = false;

        if (backgroundImage != null)
        {
            backgroundImage.enabled = true;
        }

        if (gameOverImage != null)
        {
            gameOverImage.enabled = true;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup
            .DOFade(1f, fadeInDuration)
            .SetEase(fadeInEase)
            .OnComplete(() => StartCoroutine(EnableInputAfterDelay()));
    }

    private void OnDisable()
    {
        canAcceptInput = false;
        fadeTween?.Kill();
        fadeTween = null;
        StopAllCoroutines();
    }

    private IEnumerator EnableInputAfterDelay()
    {
        yield return new WaitForSeconds(inputDelay);
        canAcceptInput = true;
        canvasGroup.interactable = true;
    }

    private void Update()
    {
        if (!canAcceptInput) return;

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetMouseButtonDown(0))
        {
            canAcceptInput = false;
            canvasGroup.interactable = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToTitle();
            }
            else
            {
                Debug.LogError("[ResultLose] GameManager.Instance が見つかりません");
            }
        }
    }
}
