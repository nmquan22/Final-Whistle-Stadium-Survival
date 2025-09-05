using UnityEngine;
using UnityEngine.InputSystem;
public class PauseMenu : MonoBehaviour
{
    public GameObject panel; bool paused;
    public void OnPause(InputValue v){ if(!v.isPressed) return; paused=!paused; Time.timeScale = paused?0f:1f; if(panel) panel.SetActive(paused); }
}