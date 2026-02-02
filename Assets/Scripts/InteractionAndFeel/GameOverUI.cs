/**
 * @file GameOverUI.cs
 * @brief GameOverUI script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;
using UnityEngine.SceneManagement;
/**
 * @class GameOverUI
 * @brief GameOverUI component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup tools
 */

public class GameOverUI : MonoBehaviour
{
    public CanvasGroup root;      // whole game-over panel
    public float fadeTime = 0.5f;

    void Awake()
    {
        if (root != null)
        {
            root.gameObject.SetActive(false);
            root.alpha = 0f;
        }
    }

    // Called by FlowerSessionController.OnGameOver
    public void ShowGameOver()
    {
        if (root == null) return;

        root.gameObject.SetActive(true);
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_GAME_OVER, 0f);
        StartCoroutine(FadeIn());
    }

    System.Collections.IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            root.alpha = Mathf.Lerp(0f, 1f, t / fadeTime);
            yield return null;
        }
        root.alpha = 1f;
    }

    // Button hooks
    public void RestartLevel()
    {
        TimeScaleManager.ClearAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToTitle(string titleSceneName)
    {
        TimeScaleManager.ClearAll();
        SceneManager.LoadScene(titleSceneName);
    }
}