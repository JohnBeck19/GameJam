using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneChanger : MonoBehaviour
{
    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void Play(string gameplaySceneName = "Game")
    {
        // Start a fresh run: reset game state and ensure player is ready
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.ResetForNewRun();
        }
        // Load gameplay scene
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void GoToTitle(string titleSceneName = "Title")
    {
        // Optional: full reset on returning to title
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.ResetForNewRun();
            gm.DestroyPersistentPlayer();
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    public void PlayAgain(string gameplaySceneName = "Game")
    {
        // From Game Over: start a new run
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.ResetForNewRun();
        }
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void QuitGame()
    {
        // Works in build; in editor this does nothing
        Application.Quit();
    }
}
