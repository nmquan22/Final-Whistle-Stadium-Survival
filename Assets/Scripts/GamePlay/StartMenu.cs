using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    public StartWeatherUI weatherUI;

    public void Play() 
    {
        if (weatherUI) weatherUI.CommitToGameFlow();
        UnityEngine.SceneManagement.SceneManager.LoadScene(GameFlow.GAME_SCENE);
    }

    public void Quit() { Application.Quit(); }
}
