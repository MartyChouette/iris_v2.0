using UnityEngine;
using UnityEngine.SceneManagement;

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
        Time.timeScale = 0f;  // optional: pause game
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
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToTitle(string titleSceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }
}