using UnityEngine;
using UnityEngine.UI;

public class ModeSelectMenu : MonoBehaviour
{
    [Header("Mode")]
    public Toggle exhibitionToggle;
    public Toggle tournamentToggle;

    [Header("Play Type")]
    public Toggle vsAiToggle;
    public Toggle vsLocalToggle;
    public Toggle vsNetworkToggle;

    [Header("Tournament Pick")]
    public Dropdown teamDropdown; // 0..3; use TMP_Dropdown if you prefer

    void Start()
    {
        // Defaults
        if (exhibitionToggle) exhibitionToggle.isOn = true;
        if (vsAiToggle) vsAiToggle.isOn = true;

        if (teamDropdown)
        {
            teamDropdown.ClearOptions();
            teamDropdown.AddOptions(GameFlow.Instance.Teams);
            teamDropdown.value = 0; teamDropdown.RefreshShownValue();
        }
    }

    public void OnConfirm()
    {
        var gf = GameFlow.Instance;
        gf.SetMode(tournamentToggle && tournamentToggle.isOn ? PlayMode.Tournament : PlayMode.Exhibition);
        if (vsLocalToggle && vsLocalToggle.isOn) gf.SetPlayType(PlayType.VsLocal);
        else if (vsNetworkToggle && vsNetworkToggle.isOn) gf.SetPlayType(PlayType.VsNetwork);
        else gf.SetPlayType(PlayType.VsAI);

        if (gf.Mode == PlayMode.Tournament)
        {
            int idx = teamDropdown ? teamDropdown.value : 0;
            gf.StartTournament(idx);
        }
        else
        {
            gf.StartQuickMatch();
        }
    }

    public void OnBack()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(GameFlow.START_SCENE);
    }
}
