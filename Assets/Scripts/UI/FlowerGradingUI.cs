using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FlowerGradingUI : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Session controller that drives this grading screen. If null, will try FindObjectOfType on enable.")]
    public FlowerSessionController session;

    [Header("Root Canvas Group")]
    [Tooltip("CanvasGroup for the whole grading panel. If null, we'll try GetComponentInChildren on awake.")]
    public CanvasGroup root;
    public float fadeTime = 0.5f;

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

    bool _visible;

    void Awake()
    {
        if (root == null)
        {
            root = GetComponentInChildren<CanvasGroup>(true);
            if (debugLogs)
            {
                if (root != null)
                    Debug.Log("[FlowerGradingUI] Auto-found CanvasGroup on " + root.gameObject.name, this);
                else
                    Debug.LogWarning("[FlowerGradingUI] No CanvasGroup assigned or found. Grading UI cannot show.", this);
            }
        }

        if (root != null)
        {
            root.gameObject.SetActive(false);
            root.alpha = 0f;
        }

        if (happyRoot != null) happyRoot.SetActive(false);
        if (sadRoot != null) sadRoot.SetActive(false);
    }

    void OnEnable()
    {
        // Auto-find session if not wired.
        if (session == null)
        {
            session = FindObjectOfType<FlowerSessionController>();
            if (debugLogs)
            {
                if (session != null)
                    Debug.Log("[FlowerGradingUI] Auto-found FlowerSessionController on " + session.gameObject.name, this);
                else
                    Debug.LogWarning("[FlowerGradingUI] No FlowerSessionController found in scene. OnResult will never fire.", this);
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
        {
            Debug.Log($"[FlowerGradingUI] OnResult received → gameOver={eval.isGameOver}, norm={eval.scoreNormalized:0.###}, score={finalScore}, days={daysAlive}", this);
        }

        if (root == null)
        {
            if (debugLogs)
                Debug.LogWarning("[FlowerGradingUI] OnResult called but root CanvasGroup is null; cannot show grading screen.", this);
            return;
        }

        // Choose happy vs sad by normalized score.
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

        // Play jingle
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
        if (root == null)
        {
            if (debugLogs)
                Debug.LogWarning("[FlowerGradingUI] Show() called but root is null.", this);
            return;
        }

        if (_visible)
            return;

        _visible = true;
        root.gameObject.SetActive(true);

        // If you want grading to pause gameplay, uncomment:
        // Time.timeScale = 0f;

        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
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
    }

    // Optional button hook to close grading and resume gameplay / return to menu.
    public void HideAndResume()
    {
        if (root == null) return;

        _visible = false;
        root.gameObject.SetActive(false);

        // If you paused time when showing, resume here:
        // Time.timeScale = 1f;
    }
}
