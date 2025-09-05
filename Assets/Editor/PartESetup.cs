// Assets/Editor/PartESetup.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PartESetup
{
    const string MENU = "Tools/Football/Run Part E Setup";

    [MenuItem(MENU)]
    public static void Run()
    {
        EnsureFolders();
        // 1) Ghi các runtime scripts nếu chưa có
        WriteRuntimeScriptsIfMissing();   // an toàn lần 1
        AssetDatabase.Refresh();

        // 2) Tạo asset/scene objects và gắn tham chiếu (an toàn lần 2)
        var formationAsset = EnsureSampleFormationAsset();     // ScriptableObject (không reference type)
        var managers = EnsureManagersRoot();                   // + GameManager (bằng tên)
        var (homeGoal, awayGoal) = EnsureGoals();              // Goal_Home/Away + GoalTrigger
        var center = EnsureCenterSpot();

        var (homeTM, awayTM) = EnsureTeams(formationAsset);    // Team_Home/Away + TeamManager (reflection)
        // Gắn goal vào TeamManager
        TrySetField(homeTM, "goal", homeGoal.transform);
        TrySetField(awayTM, "goal", awayGoal.transform);

        // Ball: ưu tiên dùng bóng hiện có của Part C
        var ball = GameObject.FindWithTag("Ball");
        if (!ball) ball = EnsureBallInScene(); // tạo đơn giản nếu chưa có
        // gắn Ball.cs nếu có (không bắt buộc, tôn trọng Part C)
        var ballType = FindType("Ball");
        if (ballType != null && ball.GetComponent(ballType) == null) ball.AddComponent(ballType);

        // Gắn refs cho GameManager
        var gm = managers.GetComponent(FindType("GameManager"));
        if (gm != null)
        {
            TrySetField(gm, "homeTeam", homeTM);
            TrySetField(gm, "awayTeam", awayTM);
            TrySetField(gm, "centerSpot", center.transform);
            TrySetField(gm, "ball", ball.GetComponent(ballType ?? typeof(Transform))); // nếu chưa có Ball.cs vẫn ok
            // AudioSource
            var sfx = managers.GetComponent<AudioSource>() ?? managers.AddComponent<AudioSource>();
            TrySetField(gm, "sfx", sfx);
        }

        EnsureUI();                           // Clock/Score/Pause/LineRenderer
        EnsureBootstrap(managers, homeTM, awayTM);

        EditorUtility.DisplayDialog("Part E",
            "Đã thiết lập Part E (Core Game Systems). Nếu đây là lần đầu tạo file, Unity vừa compile — chạy lại menu thêm 1 lần để gắn đủ tham chiếu.",
            "OK");
    }

    // ---------------- Folders ----------------
    static void EnsureFolders()
    {
        string[] dirs = {
            "Assets/Scripts","Assets/Scripts/Core","Assets/Scripts/Teams","Assets/Scripts/UI",
            "Assets/Prefabs","Assets/ScriptableObjects","Assets/Editor"
        };
        foreach (var d in dirs)
        {
            if (!AssetDatabase.IsValidFolder(d))
            {
                var parent = Path.GetDirectoryName(d).Replace("\\", "/");
                var leaf = Path.GetFileName(d);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }

    // ---------------- Write runtime scripts ----------------
    static void WriteRuntimeScriptsIfMissing()
    {
        WriteIfMissing("Assets/Scripts/Core/GameManager.cs", GAME_MANAGER);
        WriteIfMissing("Assets/Scripts/Core/Ball.cs", BALL);
        WriteIfMissing("Assets/Scripts/Core/GoalTrigger.cs", GOAL_TRIGGER);
        WriteIfMissing("Assets/Scripts/UI/MatchClockUI.cs", MATCH_CLOCK);
        WriteIfMissing("Assets/Scripts/Teams/Formation.cs", FORMATION);
        WriteIfMissing("Assets/Scripts/Teams/TeamManager.cs", TEAM_MANAGER);
        WriteIfMissing("Assets/Scripts/Teams/PlayerUserControl.cs", PLAYER_USER_CONTROL);
        WriteIfMissing("Assets/Scripts/UI/PossessionIndicator.cs", POSSESSION_INDICATOR);
        WriteIfMissing("Assets/Scripts/UI/PauseMenu.cs", PAUSE_MENU);
        WriteIfMissing("Assets/Scripts/Core/Bootstrap.cs", BOOTSTRAP);
    }

    static void WriteIfMissing(string path, string content)
    {
        if (File.Exists(path)) return;
        File.WriteAllText(path, content);
        Debug.Log("Created " + path);
    }

    // ---------------- Formation asset (no direct type ref) ----------------
    static ScriptableObject EnsureSampleFormationAsset()
    {
        string path = "Assets/ScriptableObjects/Sample_Formation.asset";
        var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        if (obj) return obj;

        // cố gắng tạo bằng type thật nếu đã compile, nếu chưa – tạo dummy rồi đợi lần 2
        var formationType = FindType("Formation");
        if (formationType == null)
        {
            Debug.Log("Formation.cs chưa compile – sẽ tạo asset ở lần chạy sau.");
            return null;
        }
        obj = ScriptableObject.CreateInstance(formationType) as ScriptableObject;
        AssetDatabase.CreateAsset(obj, path);
        AssetDatabase.SaveAssets();

        // set default fields
        TrySetField(obj, "homeSpawns", new Vector3[]{
            new(-20,0,-12), new(-8,0,-10), new(0,0,-8), new(8,0,-10), new(20,0,-12),
            new(0,0,-28) // GK
        });
        TrySetField(obj, "roles", new string[] { "DF", "MF", "ST", "MF", "DF", "GK" });

        return obj;
    }

    // ---------------- Managers root & GameManager ----------------
    static GameObject EnsureManagersRoot()
    {
        var root = GameObject.Find("Managers") ?? new GameObject("Managers");
        var gmType = FindType("GameManager");
        if (gmType != null && root.GetComponent(gmType) == null) root.AddComponent(gmType);
        if (root.GetComponent<AudioSource>() == null) root.AddComponent<AudioSource>();
        return root;
    }

    // ---------------- Teams & TeamManager ----------------
    static (Component home, Component away) EnsureTeams(ScriptableObject formationAsset)
    {
        var tmType = FindType("TeamManager");
        var home = GameObject.Find("Team_Home") ?? new GameObject("Team_Home");
        var away = GameObject.Find("Team_Away") ?? new GameObject("Team_Away");

        Component homeTM = tmType != null ? home.GetComponent(tmType) ?? home.AddComponent(tmType) : null;
        Component awayTM = tmType != null ? away.GetComponent(tmType) ?? away.AddComponent(tmType) : null;

        if (homeTM != null)
        {
            TrySetField(homeTM, "isHome", true);
            if (formationAsset) TrySetField(homeTM, "formation", formationAsset);
            // playerPrefab: dùng Player.prefab nếu có; nếu không tạo nhanh
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            if (!playerPrefab) playerPrefab = CreateMinimalPlayerPrefab();
            TrySetField(homeTM, "playerPrefab", playerPrefab);
        }
        if (awayTM != null)
        {
            TrySetField(awayTM, "isHome", false);
            if (formationAsset) TrySetField(awayTM, "formation", formationAsset);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            if (!playerPrefab) playerPrefab = CreateMinimalPlayerPrefab();
            TrySetField(awayTM, "playerPrefab", playerPrefab);
        }
        return (homeTM, awayTM);
    }

    static GameObject CreateMinimalPlayerPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Player";
        go.tag = "Player";
        if (go.TryGetComponent<CapsuleCollider>(out var cap)) UnityEngine.Object.DestroyImmediate(cap);
        var cc = go.AddComponent<CharacterController>();
        cc.center = new Vector3(0, 0.9f, 0); cc.height = 1.8f; cc.radius = 0.3f;

        // Optional components nếu đã compile
        var pcType = FindType("PlayerController");
        var pirType = FindType("PlayerInputRelay");
        var biType = FindType("BallInteractor");
        if (pcType != null) go.AddComponent(pcType);
        if (pirType != null) go.AddComponent(pirType);
        if (biType != null) go.AddComponent(biType);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Player.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------- Goals ----------------
    static (GameObject home, GameObject away) EnsureGoals()
    {
        var home = GameObject.Find("Goal_Home") ?? CreateGoal("Goal_Home", new Vector3(0, 0, -32f), true);
        var away = GameObject.Find("Goal_Away") ?? CreateGoal("Goal_Away", new Vector3(0, 0, 32f), false);
        return (home, away);

        GameObject CreateGoal(string name, Vector3 pos, bool isHome)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            var box = g.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(7.32f, 2.5f, 0.4f);
            var gtType = FindType("GoalTrigger");
            if (gtType != null)
            {
                var gt = g.AddComponent(gtType);
                TrySetField(gt, "isHomeGoal", isHome);
            }
            g.layer = LayerMask.NameToLayer("Goal");
            return g;
        }
    }

    // ---------------- Center spot ----------------
    static Transform EnsureCenterSpot()
    {
        var c = GameObject.Find("CenterSpot") ?? new GameObject("CenterSpot");
        c.transform.position = Vector3.zero;
        return c.transform;
    }

    // ---------------- Ball ----------------
    static GameObject EnsureBallInScene()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Ball";
        go.tag = "Ball";
        if (go.GetComponent<Rigidbody>() == null) go.AddComponent<Rigidbody>();
        return go;
    }

    // ---------------- UI ----------------
    static void EnsureUI()
    {
        var ui = GameObject.Find("UIRoot") ?? new GameObject("UIRoot");
        var canvas = ui.GetComponentInChildren<Canvas>();
        if (!canvas)
        {
            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(ui.transform);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.AddComponent<UnityEngine.UI.CanvasScaler>();
            cgo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        var clock = GameObject.Find("ClockText") ?? CreateText("ClockText", canvas.transform, "03:00", new Vector2(0.5f, 1f), new Vector2(0, -30));
        var score = GameObject.Find("ScoreText") ?? CreateText("ScoreText", canvas.transform, "0 - 0", new Vector2(0.5f, 1f), new Vector2(0, -60));

        // MatchClockUI
        var mcuType = FindType("MatchClockUI");
        if (mcuType != null)
        {
            var comp = ui.GetComponent(mcuType) ?? ui.AddComponent(mcuType);
            // hỗ trợ TMP hoặc UI.Text
            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            var clk = clock.GetComponent(tmpType ?? typeof(UnityEngine.UI.Text));
            var sc = score.GetComponent(tmpType ?? typeof(UnityEngine.UI.Text));
            TrySetField(comp, "clockText", clk);
            TrySetField(comp, "scoreText", sc);
        }

        // Pause panel & PauseMenu
        var panel = GameObject.Find("PausePanel");
        if (!panel)
        {
            panel = new GameObject("PausePanel");
            panel.transform.SetParent(canvas.transform, false);
            var img = panel.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0, 0, 0, 0.5f);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            panel.SetActive(false);
        }
        var pmType = FindType("PauseMenu");
        if (pmType != null)
        {
            var pm = ui.GetComponent(pmType) ?? ui.AddComponent(pmType);
            TrySetField(pm, "panel", panel);
        }

        // Possession line
        var poss = GameObject.Find("PossessionIndicator") ?? new GameObject("PossessionIndicator");
        if (!poss.GetComponent<LineRenderer>()) poss.AddComponent<LineRenderer>();
        var piType = FindType("PossessionIndicator");
        if (piType != null && poss.GetComponent(piType) == null) poss.AddComponent(piType);
    }

    static GameObject CreateText(string name, Transform parent, string text, Vector2 anchor, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        if (tmpType != null)
        {
            var tmp = go.AddComponent(tmpType);
            tmpType.GetProperty("fontSize")?.SetValue(tmp, 36f);
            tmpType.GetProperty("alignment")?.SetValue(tmp, Enum.Parse(Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro"), "Center"));
            tmpType.GetProperty("text")?.SetValue(tmp, text);
        }
        else
        {
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 36; t.alignment = TextAnchor.MiddleCenter; t.text = text; t.color = Color.white;
        }
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchor.x, anchor.y); rt.anchorMax = new Vector2(anchor.x, anchor.y);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(240, 40);
        return go;
    }

    // ---------------- Bootstrap ----------------
    static void EnsureBootstrap(GameObject managers, Component homeTM, Component awayTM)
    {
        var bsType = FindType("Bootstrap");
        if (bsType == null) return;
        var bs = managers.GetComponent(bsType) ?? managers.AddComponent(bsType);
        TrySetField(bs, "home", homeTM);
        TrySetField(bs, "away", awayTM);
    }

    // ---------------- Helpers ----------------
    static Type FindType(string fullOrShort) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.FullName == fullOrShort || t.Name == fullOrShort);

    static void TrySetField(object target, string name, object value)
    {
        if (target == null) return;
        var t = target is Component c ? c.GetType() : target.GetType();
        var f = t.GetField(name); if (f != null) { f.SetValue(target is Component cc ? (object)cc : target, value); return; }
        var p = t.GetProperty(name); if (p != null && p.CanWrite) p.SetValue(target is Component c2 ? (object)c2 : target, value);
    }

    // ================== RUNTIME SCRIPTS ==================
    // (giữ nguyên như bản trước, không đổi API — tương thích Part D/C)
    const string GAME_MANAGER =
@"using UnityEngine;
using System;
using System.Collections;

public enum MatchState { PreMatch, Kickoff, Playing, GoalScored, HalfTime, FullTime }

public class GameManager : MonoBehaviour
{
    public static GameManager I;
    [Header(""Match"")] public float halfDuration = 180f; public float kickoffDelay = 2f; public int home=0, away=0;
    [Header(""Refs"")] public TeamManager homeTeam, awayTeam; public Transform centerSpot; public Ball ball; public AudioSource sfx; public AudioClip whistleKickoff, whistleGoal, whistleFulltime;
    public MatchState State {get; private set;} = MatchState.PreMatch;
    public float timeLeft {get; private set;}
    public bool InputLocked {get; private set;} = true;
    public event Action OnKickoff, OnHalfTime, OnFullTime, OnGoal, OnScoreChanged, OnStateChanged;

    void Awake(){ I=this; }
    void Start(){ timeLeft = halfDuration; StartCoroutine(CoKickoff()); }

    void Update(){
        if (State==MatchState.Playing){
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f){ SetState(MatchState.HalfTime); StartCoroutine(CoHalfTime()); }
        }
    }

    public void Goal(bool homeScored){
        if(homeScored) home++; else away++;
        OnScoreChanged?.Invoke(); OnGoal?.Invoke();
        if (sfx && whistleGoal) sfx.PlayOneShot(whistleGoal);
        SetState(MatchState.GoalScored);
        StartCoroutine(CoKickoff());
    }

    IEnumerator CoKickoff(){
        LockInput(true); SetState(MatchState.Kickoff);
        homeTeam?.ResetForKickoff(true,  centerSpot? centerSpot.position : Vector3.zero);
        awayTeam?.ResetForKickoff(false, centerSpot? centerSpot.position : Vector3.zero);
        ball?.ResetBall(centerSpot? centerSpot.position : Vector3.zero);
        if (sfx && whistleKickoff) sfx.PlayOneShot(whistleKickoff);
        OnKickoff?.Invoke();
        yield return new WaitForSeconds(kickoffDelay);
        LockInput(false); SetState(MatchState.Playing);
    }

    IEnumerator CoHalfTime(){
        LockInput(true);
        var g = homeTeam.goal; homeTeam.goal = awayTeam.goal; awayTeam.goal = g;
        homeTeam.MirrorTeam(centerSpot?centerSpot.position:Vector3.zero);
        awayTeam.MirrorTeam(centerSpot?centerSpot.position:Vector3.zero);
        yield return new WaitForSeconds(2f);
        timeLeft = halfDuration;
        SetState(MatchState.Kickoff); OnKickoff?.Invoke();
        ball?.ResetBall(centerSpot?centerSpot.position:Vector3.zero);
        if (sfx && whistleKickoff) sfx.PlayOneShot(whistleKickoff);
        yield return new WaitForSeconds(kickoffDelay);
        LockInput(false); SetState(MatchState.Playing);
    }

    public void SetState(MatchState s){ State=s; OnStateChanged?.Invoke(); }
    public void EndMatch(){ LockInput(true); SetState(MatchState.FullTime); OnFullTime?.Invoke(); if (sfx && whistleFulltime) sfx.PlayOneShot(whistleFulltime); }
    public void LockInput(bool v){ InputLocked = v; }
}";

    const string BALL =
@"using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    Rigidbody rb;
    void Awake(){ rb = GetComponent<Rigidbody>(); }
    public void ResetBall(Vector3 pos){
        rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        transform.position = pos + Vector3.up*0.11f;
    }
}";

    const string GOAL_TRIGGER =
@"using UnityEngine;
public class GoalTrigger : MonoBehaviour
{
    public bool isHomeGoal = true;
    void OnTriggerEnter(Collider other){
        if (!other.CompareTag(""Ball"")) return;
        GameManager.I.Goal(homeScored: !isHomeGoal);
    }
}";

    const string MATCH_CLOCK =
@"using UnityEngine;
using TMPro;
public class MatchClockUI : MonoBehaviour
{
    public TMP_Text clockText; public TMP_Text scoreText;
    void OnEnable(){ if(GameManager.I){ GameManager.I.OnScoreChanged += RefreshScore; GameManager.I.OnStateChanged += RefreshClock; } }
    void OnDisable(){ if(GameManager.I){ GameManager.I.OnScoreChanged -= RefreshScore; GameManager.I.OnStateChanged -= RefreshClock; } }
    void Update(){ if(GameManager.I && GameManager.I.State==MatchState.Playing) RefreshClock(); }
    void RefreshClock(){ float t=Mathf.Max(0, GameManager.I.timeLeft); int m=Mathf.FloorToInt(t/60f), s=Mathf.FloorToInt(t%60); if(clockText) clockText.text=$""{m:00}:{s:00}""; }
    void RefreshScore(){ if(scoreText) scoreText.text=$""{GameManager.I.home} - {GameManager.I.away}""; }
}";

    const string FORMATION =
@"using UnityEngine;
[CreateAssetMenu(menuName=""Football/Formation"")]
public class Formation : ScriptableObject
{
    public Vector3[] homeSpawns;
    public string[] roles;
}";

    const string TEAM_MANAGER =
@"using UnityEngine;
using System.Linq;
using System.Collections.Generic;
public class TeamManager : MonoBehaviour
{
    public Formation formation;
    public Transform goal;
    public bool isHome=true;
    public GameObject playerPrefab;
    public List<PlayerController> players = new();
    public PlayerController goalkeeper;

    public void Setup(){
        ClearChildren();
        if (!playerPrefab || formation==null || formation.homeSpawns==null) return;
        for(int i=0;i<formation.homeSpawns.Length;i++){
            var pos = formation.homeSpawns[i]; if(!isHome) pos.x=-pos.x;
            var go = Instantiate(playerPrefab, pos, Quaternion.identity, transform);
            var pc = go.GetComponent<PlayerController>(); players.Add(pc);
            if (formation.roles!=null && i<formation.roles.Length && formation.roles[i]==""GK"") goalkeeper=pc;
        }
        if (!goalkeeper && players.Count>0) goalkeeper = players.OrderByDescending(p=>Vector3.Distance(p.transform.position, goal?goal.position:Vector3.zero)).First();
    }
    public void ResetForKickoff(bool weKickoff, Vector3 center){
        for(int i=0;i<players.Count;i++){
            var pos=formation.homeSpawns[Mathf.Clamp(i,0,formation.homeSpawns.Length-1)]; if(!isHome) pos.x=-pos.x;
            players[i].transform.position=pos; players[i].transform.rotation=Quaternion.LookRotation((center-pos).normalized);
        }
    }
    public void MirrorTeam(Vector3 around){
        foreach(var p in players){ var pos=p.transform.position; pos.x=-pos.x; p.transform.position=pos; p.transform.rotation=Quaternion.LookRotation((around-pos).normalized); }
        isHome=!isHome;
    }
    public PlayerController GetNearestToBall(Vector3 ballPos){
        float best=float.MaxValue; PlayerController bestP=null;
        foreach(var p in players){ float d=(p.transform.position-ballPos).sqrMagnitude; if(d<best){best=d; bestP=p;} }
        return bestP;
    }
    void ClearChildren(){ for(int i=transform.childCount-1;i>=0;i--) DestroyImmediate(transform.GetChild(i).gameObject); players.Clear(); goalkeeper=null; }
}";

    const string PLAYER_USER_CONTROL =
@"using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(PlayerController))]
public class PlayerUserControl : MonoBehaviour
{
    PlayerController pc; public bool isUserControlled;
    void Awake(){ pc = GetComponent<PlayerController>(); }
    void Update(){ if(GameManager.I) pc.enabled = isUserControlled && !GameManager.I.InputLocked; }
    public void OnSwitchPlayer(InputValue v){
        if(!v.isPressed) return;
        var team = GetComponentInParent<TeamManager>();
        var ball = GameObject.FindWithTag(""Ball"");
        var target = team?.GetNearestToBall(ball?ball.transform.position:Vector3.zero);
        if(target && target!=pc){
            var old = team.players.Find(p=>p.GetComponent<PlayerUserControl>()?.isUserControlled==true);
            if(old) old.GetComponent<PlayerUserControl>().isUserControlled=false;
            target.GetComponent<PlayerUserControl>().isUserControlled=true;
        }
    }
}";

    const string POSSESSION_INDICATOR =
@"using UnityEngine;
[RequireComponent(typeof(LineRenderer))]
public class PossessionIndicator : MonoBehaviour
{
    public TeamManager home, away; public float maxDist=2f;
    LineRenderer lr; Transform ball;
    void Awake(){ lr=GetComponent<LineRenderer>(); var b=GameObject.FindWithTag(""Ball""); if(b) ball=b.transform; }
    void Update(){
        if(!ball){ var b=GameObject.FindWithTag(""Ball""); if(b) ball=b.transform; else { lr.enabled=false; return; } }
        Transform best=null; float bestD=maxDist*maxDist;
        void Try(TeamManager t){ if(t==null) return; foreach(var p in t.players){ float d=(p.transform.position-ball.position).sqrMagnitude; if(d<bestD){bestD=d; best=p.transform;} } }
        Try(home); Try(away);
        if(!best){ lr.enabled=false; return; }
        lr.enabled=true; lr.positionCount=2; lr.widthMultiplier=0.05f;
        lr.SetPosition(0, ball.position); lr.SetPosition(1, best.position+Vector3.up*1.6f);
    }
}";

    const string PAUSE_MENU =
@"using UnityEngine;
using UnityEngine.InputSystem;
public class PauseMenu : MonoBehaviour
{
    public GameObject panel; bool paused;
    public void OnPause(InputValue v){ if(!v.isPressed) return; paused=!paused; Time.timeScale = paused?0f:1f; if(panel) panel.SetActive(paused); }
}";

    const string BOOTSTRAP =
@"using UnityEngine;
public class Bootstrap : MonoBehaviour
{
    public TeamManager home, away;
    void Start(){ if(home) home.Setup(); if(away) away.Setup(); }
}";
}
#endif
