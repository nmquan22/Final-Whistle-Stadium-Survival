using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene(GameFlow.MODE_SCENE);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
