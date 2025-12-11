using UnityEngine;
using UnityEngine.SceneManagement; // Required for reloading scenes

public class RRestart : MonoBehaviour
{
    void Update()
    {
        // Press 'R' to restart
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Get the currently active scene
            Scene currentScene = SceneManager.GetActiveScene();
            
            // Reload it
            SceneManager.LoadScene(currentScene.name);
        }
    }
}