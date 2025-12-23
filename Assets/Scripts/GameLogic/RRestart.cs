/**
 * @file RRestart.cs
 * @brief RRestart script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;
using UnityEngine.SceneManagement;
/**
 * @class RRestart
 * @brief RRestart component.
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

public class RRestart : MonoBehaviour
{
    void Update()
    {
        RestartGame();
    }


    public void RestartGame()
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