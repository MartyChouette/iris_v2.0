/**
 * @file FlowerGradingUI.cs
 * @brief End-of-session grading screen (happy/sad layouts, score/days/reason).
 *
 * @details
 * Intent:
 * - Presentation layer for end-of-session grading.
 * - Subscribes to @ref FlowerSessionController.OnResult and displays:
 *   score, days alive, and optional game-over reason.
 *
 * Subscription model:
 * - If @ref session is null, attempts auto-find via FindFirstObjectByType on enable.
 * - OnEnable subscribes; OnDisable unsubscribes (prevents duplicate listeners).
 *
 * Visibility model:
 * - Uses a CanvasGroup root for fade-in.
 * - Can either deactivate the root GameObject or simply hide it via alpha + raycast flags.
 *
 * Mood rule:
 * - isGood := eval.scoreNormalized >= goodThresholdNormalized
 * - happyRoot is enabled when isGood, sadRoot when not.
 *
 * Authoring gotchas:
 * - If root CanvasGroup is missing, grading cannot show (logs warn).
 * - Auto-find is convenient but brittle in multi-session scenes—prefer wiring in production.
 *
 * Performance:
 * - Event-driven; no Update() work.
 *
 * @ingroup flowers_ui
 */

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FlowerGradingUI : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Session controller that drives this grading screen. If null, will try FindFirstObjectByType on enable.")]
    public FlowerSessionController session;

    [Header("Root Canvas Group")]
    [Tooltip("CanvasGroup for the whole grading panel. If null, we'll try GetComponentInChildren on Awake.")]
    public CanvasGroup root;

    [Min(0f)]
    public float fadeTime = 0.5f;

    [Tooltip("If true, hide by disabling the root GameObject. If false, hide via CanvasGroup flags.")]
    public bool disableRootGameObjectWhenHidden = true;

    [Header("Happy / Sad Layout")]
    [Tooltip("Shown when the score is above the good threshold.")]
    public GameObject happyRoot;

    [Tooltip("Shown when the score is below the good threshold.")]
    public GameObject sadRoot;

    [Header("Score Thresholds")]
    [Tooltip("Normalized score (0–1) at or above this is considered a 'good' grade (happy screen).")]
    [Range(0f, 1f)]
    public float goodThresholdNormalized = 0.7f;

    [Header("Text References")]
    public TMP_Text titleLabel;
    public TMP_Text scoreLabel;
    public TMP_Text daysLabel;
    public TMP_Text reasonLabel;

    [Header("Colors")]
    public Color happyColor = new Color(0.3f, 1f, 0.4f, 1f);
    public Color sadColor = new Color(1f, 0.3f, 0.25f, 1f);

    [Header("Optional Audio")]
    [Tooltip("Optional AudioSource for happy / sad grading jingles.")]
    public AudioSource audioSource;
    public AudioClip happyClip;
    public AudioClip sadClip;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool _visible;
    private Coroutine _fadeRoutine;

    void Awake()
    {
        if (root == null)
        {
            root = GetComponentInChildren<CanvasGroup>(true);

            if (debugLogs)
            {
                if (root != null) Debug.Log("[FlowerGradingUI] Auto-found CanvasGroup on " + root.gameObject.name, this);
                else Debug.LogWarning("[FlowerGradingUI] No CanvasGroup assigned or found. Grading UI cannot show.", this);
            }
        }

        // Start hidden
        SetVisible(false, instant: true);

        if (happyRoot != null) happyRoot.SetActive(false);
        if (sadRoot != null) sadRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (session == null)
        {
            session = FindFirstObjectByType<FlowerSessionController>();

            if (session != null)
            {
                if (debugLogs) Debug.Log("[FlowerGradingUI] Auto-found FlowerSessionController on " + session.gameObject.name, this);
            }
            else
            {
                Debug.LogError("[FlowerGradingUI] No FlowerSessionController found in scene. " +
                    "OnResult will never fire. Wire the 'session' field in the Inspector.", this);
            }
        }

        if (session != null)
        {
            session.OnResult.AddListener(OnResult);

            if (debugLogs)
                Debug.Log("[FlowerGradingUI] Subscribed to session.OnResult.", this);
        }
    }

    void OnDisable()
    {
        if (session != null)
        {
            session.OnResult.RemoveListener(OnResult);

            if (debugLogs)
                Debug.Log("[FlowerGradingUI] Unsubscribed from session.OnResult.", this);
        }
    }

    public void OnResult(FlowerGameBrain.EvaluationResult eval, int finalScore, int daysAlive)
    {
        if (debugLogs)
            Debug.Log($"[FlowerGradingUI] OnResult → gameOver={eval.isGameOver}, norm={eval.scoreNormalized:0.###}, score={finalScore}, days={daysAlive}", this);

        if (root == null)
        {
            if (debugLogs)
                Debug.LogWarning("[FlowerGradingUI] OnResult called but root CanvasGroup is null; cannot show grading screen.", this);
            return;
        }

        bool isGood = eval.scoreNormalized >= goodThresholdNormalized;

        if (happyRoot != null) happyRoot.SetActive(isGood);
        if (sadRoot != null) sadRoot.SetActive(!isGood);

        if (titleLabel != null)
        {
            if (isGood)
            {
                titleLabel.text = eval.isGameOver ? "Beautiful, But Doomed" : "Lovely Trim";
                titleLabel.color = happyColor;
            }
            else
            {
                titleLabel.text = eval.isGameOver ? "Fatal Cut" : "Botched Trim";
                titleLabel.color = sadColor;
            }
        }

        if (scoreLabel != null)
        {
            scoreLabel.text = $"Score: {finalScore}";
            scoreLabel.color = isGood ? happyColor : sadColor;
        }

        if (daysLabel != null)
        {
            daysLabel.text = $"Days: {daysAlive}";
            daysLabel.color = isGood ? happyColor : sadColor;
        }

        if (reasonLabel != null)
        {
            if (eval.isGameOver && !string.IsNullOrEmpty(eval.gameOverReason))
            {
                reasonLabel.text = eval.gameOverReason;
                reasonLabel.gameObject.SetActive(true);
            }
            else
            {
                reasonLabel.text = "";
                reasonLabel.gameObject.SetActive(false);
            }
        }

        if (audioSource != null)
        {
            AudioClip clip = isGood ? happyClip : sadClip;
            if (clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
        }

        Show();
    }

    public void Show()
    {
        SetVisible(true, instant: false);
    }

    public void HideAndResume()
    {
        SetVisible(false, instant: true);
        // Release the game-over pause so gameplay can resume.
        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_GAME_OVER);
    }

    private void SetVisible(bool visible, bool instant)
    {
        if (root == null) return;

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _visible = visible;

        if (disableRootGameObjectWhenHidden)
        {
            root.gameObject.SetActive(visible || !instant); // keep active if we want to fade in
        }
        else
        {
            // keep active always; just change group flags
            root.gameObject.SetActive(true);
            root.interactable = visible;
            root.blocksRaycasts = visible;
        }

        if (instant || fadeTime <= 0f)
        {
            root.alpha = visible ? 1f : 0f;
            if (disableRootGameObjectWhenHidden && !visible)
                root.gameObject.SetActive(false);
            return;
        }

        // Coroutine safety: if not active, snap instead of crashing.
        if (!isActiveAndEnabled)
        {
            root.alpha = visible ? 1f : 0f;
            if (disableRootGameObjectWhenHidden && !visible)
                root.gameObject.SetActive(false);
            return;
        }

        _fadeRoutine = StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        if (root == null) yield break;

        float t = 0f;
        root.alpha = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            root.alpha = Mathf.Lerp(0f, 1f, t / fadeTime);
            yield return null;
        }

        root.alpha = 1f;

        if (debugLogs)
            Debug.Log("[FlowerGradingUI] Fade-in complete.", this);

        _fadeRoutine = null;
    }
}
