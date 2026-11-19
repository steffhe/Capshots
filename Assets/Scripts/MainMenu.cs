using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{

    public Launcher launcher;

    public void Start()
    {
        Pause.paused = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        HideQuitButtons();
#endif
    }

    // A browser game can't quit itself, so hide any button wired to QuitGame
    private void HideQuitButtons()
    {
        foreach (UnityEngine.UI.Button button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == nameof(QuitGame))
                {
                    button.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }
    public void JoinMatch()
    {
        //SceneManager.LoadScene("Map");
        launcher.Join();
    }

    public void CreateMatch()
    {
        launcher.Create();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
