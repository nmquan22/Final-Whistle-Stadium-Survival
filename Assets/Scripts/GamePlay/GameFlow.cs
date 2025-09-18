using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;


public enum PlayMode { Exhibition, Tournament }
public enum PlayType { VsAI, VsLocal, VsNetwork }


public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }


    // === Scene names ===
    public const string START_SCENE = "StartScene";
    public const string MODE_SCENE = "ModeSelectScene";
    public const string GAME_SCENE = "Stadium";
    public const string END_SCENE = "EndScene";


    // === Global options ===
    public PlayMode Mode { get; private set; } = PlayMode.Exhibition;
    public PlayType Type { get; private set; } = PlayType.VsAI;


    public bool UseVCamDefault = true; // read by GameScene
    public bool ShowHandGuiDefault = true;
    [Range(0f, 1f)] public float VolumeDefault = 0.8f; // 0..1


    // === Match info ===
    public struct MatchInfo { public string home, away; public bool playerIsHome; }
    public MatchInfo CurrentMatch { get; private set; }


    // === Tournament (4-team mini cup) ===
    public List<string> Teams = new() { "Lions", "Tigers", "Eagles", "Wolves" };
    private int tourRound = 0; // 0,1: semis ; 2: final ; 3: done
    private List<MatchInfo> bracket = new();

    // === Last result ===
    public int LastHomeScore { get; private set; }
    public int LastAwayScore { get; private set; }
    public bool PlayerWonLast { get; private set; }

    // === Weather (StartScene picks, used later in match) ===
    public enum WeatherKind { Clear, Rain, Snow, Foggy, Night }
    public WeatherKind SelectedWeather = WeatherKind.Clear;
    [UnityEngine.Range(0f, 1f)] public float TimeOfDay01 = 0.5f; // 0 = đêm, 0.5 = trưa


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }


    // ====== API: called from menus ======
    public void SetPlayType(PlayType t) => Type = t;
    public void SetMode(PlayMode m) => Mode = m;


    public void StartQuickMatch(string home = "Home", string away = "Away", bool playerIsHome = true)
    {
        Mode = PlayMode.Exhibition;
        CurrentMatch = new MatchInfo { home = home, away = away, playerIsHome = playerIsHome };
        SceneManager.LoadScene(GAME_SCENE);
    }

    public void StartTournament(int playerTeamIndex = 0)
    {
        Mode = PlayMode.Tournament;
        // Build 4-team bracket: (0 vs 1), (2 vs 3), winners → final
        if (Teams.Count < 4) { Teams = new() { "Lions", "Tigers", "Eagles", "Wolves" }; }
        bracket.Clear();
        tourRound = 0;

        // Player picks team index, set player side in first match if present
        string A = Teams[0], B = Teams[1], C = Teams[2], D = Teams[3];

        // Semi 1
        bracket.Add(new MatchInfo { home = A, away = B, playerIsHome = (playerTeamIndex == 0) });
        // Semi 2
        bracket.Add(new MatchInfo { home = C, away = D, playerIsHome = (playerTeamIndex == 2) });
        // Final placeholder (filled after semis)
        bracket.Add(new MatchInfo { home = "Winner S1", away = "Winner S2", playerIsHome = true });

        LoadCurrentTournamentMatch();
    }

    void LoadCurrentTournamentMatch()
    {
        // tourRound: 0 → semi1 ; 1 → semi2 ; 2 → final
        if (tourRound <= 2)
        {
            CurrentMatch = bracket[tourRound];
            SceneManager.LoadScene(GAME_SCENE);
        }
        else
        {
            // tournament completed → end scene shows champion
            SceneManager.LoadScene(END_SCENE);
        }
    }

    // ====== API: call from GameScene when a match ends ======
    public void ReportMatchEnd(bool playerWon, int homeScore, int awayScore)
    {
        PlayerWonLast = playerWon;
        LastHomeScore = homeScore;
        LastAwayScore = awayScore;

        if (Mode == PlayMode.Tournament)
        {
            // record winners into final slots
            if (tourRound == 0)
            {
                // winner of semi1 becomes final.home
                var winnerName = (homeScore >= awayScore) ? bracket[0].home : bracket[0].away;
                bracket[2] = new MatchInfo { home = winnerName, away = bracket[2].away, playerIsHome = bracket[2].playerIsHome };
            }
            else if (tourRound == 1)
            {
                // winner of semi2 becomes final.away
                var winnerName = (homeScore >= awayScore) ? bracket[1].home : bracket[1].away;
                bracket[2] = new MatchInfo { home = bracket[2].home, away = winnerName, playerIsHome = bracket[2].playerIsHome };
            }
            else if (tourRound == 2)
            {
                // final done → finished
            }

            tourRound++;
            SceneManager.LoadScene(END_SCENE);
        }
        else
        {
            // Exhibition → go to EndScene
            SceneManager.LoadScene(END_SCENE);
        }
    }

    // ====== API: called from EndScene buttons ======
    public void NextMatchOrFinish()
    {
        if (Mode == PlayMode.Tournament && tourRound <= 2)
        {
            LoadCurrentTournamentMatch();
        }
        else
        {
            // back to start after finishing tournament
            SceneManager.LoadScene(START_SCENE);
        }
    }

    public void Rematch()
    {
        SceneManager.LoadScene(GAME_SCENE);
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(START_SCENE);
    }
}
