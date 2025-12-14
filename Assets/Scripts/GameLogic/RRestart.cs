using UnityEngine;
using UnityEngine.SceneManagement;

public class RRestart : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            // 1. IMPORTANT: Unpause the game before/during reload
            Time.timeScale = 1.0f;

            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.name);
        }
    }
}