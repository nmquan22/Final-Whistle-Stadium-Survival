// Assets/Editor/PartFSetup.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class PartFSetup
{
    const string MENU = "Tools/Football/Run Part F Setup";

    [MenuItem(MENU)]
    public static void Run()
    {
        EnsureFolders();

        // 1) Viết các script runtime nếu chưa có (FSM + AI + States + Helpers)
        WriteIfMissing("Assets/Scripts/AI/IState.cs", ISTATE);
        WriteIfMissing("Assets/Scripts/AI/StateMachine.cs", STATEMACHINE);
        WriteIfMissing("Assets/Scripts/AI/WorldContext.cs", WORLD_CONTEXT);
        WriteIfMissing("Assets/Scripts/AI/WorldBootstrap.cs", WORLD_BOOTSTRAP);
        WriteIfMissing("Assets/Scripts/AI/TeamAI.cs", TEAM_AI);
        WriteIfMissing("Assets/Scripts/AI/Tactic.cs", TACTIC_SO);
        WriteIfMissing("Assets/Scripts/AI/AIPlayer.cs", AI_PLAYER);

        // States (stub – để bạn điền dần)
        WriteIfMissing("Assets/Scripts/AI/States/IdleState.cs", IDLE);
        WriteIfMissing("Assets/Scripts/AI/States/ChaseBallState.cs", CHASE);
        WriteIfMissing("Assets/Scripts/AI/States/DribbleState.cs", DRIBBLE);
        WriteIfMissing("Assets/Scripts/AI/States/PassState.cs", PASS);
        WriteIfMissing("Assets/Scripts/AI/States/ShootState.cs", SHOOT);
        WriteIfMissing("Assets/Scripts/AI/States/MarkOpponentState.cs", MARK);
        WriteIfMissing("Assets/Scripts/AI/States/ReturnHomeState.cs", RETURN_HOME);
        WriteIfMissing("Assets/Scripts/AI/States/GoalkeeperState.cs", GOALKEEPER);

        WriteIfMissing("Assets/Scripts/AI/AIDebugGizmos.cs", AI_DEBUG);

        AssetDatabase.Refresh();

        // 2) Tạo asset tactic mẫu (nếu có type)
        EnsureTacticAsset();

        // 3) Gắn WorldBootstrap lên Managers để nạp Ball/Goals sẵn có
        var managers = GameObject.Find("Managers") ?? new GameObject("Managers");
        if (!managers.GetComponent<AudioSource>()) managers.AddComponent<AudioSource>();
        AddComponentIfTypeExists(managers, "WorldBootstrap"); // an toàn khi type đã compile

        // 4) Gắn AI cho đội máy (AwayTeam mặc định)
        var away = GameObject.Find("AwayTeam") ?? GameObject.Find("Team_Away");
        if (away != null) AttachAIToChildren(away.transform, isHome: false);

        // 5) (Tuỳ chọn) Gắn AI cho thủ môn nếu có child tên Player_GK
        TryAttachGK(away);

        EditorUtility.DisplayDialog("Part F",
            "Đã tạo khung AI (FSM + States) và gắn NavMeshAgent + AIPlayer cho đội Away.\n" +
            "Nếu đây là lần đầu tạo file, Unity sẽ compile — chạy lại menu một lần để nạp đầy đủ type/references.",
            "OK");
    }

    static void EnsureFolders()
    {
        string[] dirs = {
            "Assets/Scripts/AI","Assets/Scripts/AI/States",
            "Assets/ScriptableObjects","Assets/Editor"
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

    static void EnsureTacticAsset()
    {
        var tType = FindType("Tactic");
        if (tType == null) { Debug.Log("Tactic.cs chưa compile – tactic asset sẽ tạo ở lần chạy sau."); return; }
        var path = "Assets/ScriptableObjects/Default_Tactic.asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path)) return;
        var so = ScriptableObject.CreateInstance(tType) as ScriptableObject;
        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();

        // set các field cơ bản nếu có
        SetFieldOrPropIfExists(so, "style", EnumParseIfExists(tType, "Style", "Balanced"));
        SetFieldOrPropIfExists(so, "pressDistance", 12f);
        SetFieldOrPropIfExists(so, "passRisk", 0.5f);
    }

    static object EnumParseIfExists(Type owner, string enumName, string value)
    {
        var e = owner.GetNestedType(enumName);
        if (e == null) return null;
        try { return Enum.Parse(e, value); } catch { return null; }
    }

    static void AttachAIToChildren(Transform teamRoot, bool isHome)
    {
        foreach (Transform child in teamRoot)
        {
            // bỏ qua object không phải cầu thủ
            if (child.name.Contains("Goal")) continue;

            // NavMeshAgent
            var agent = child.GetComponent<NavMeshAgent>();
            if (!agent) agent = child.gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.angularSpeed = 720f;
            agent.acceleration = 20f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // Tốc độ: nếu PlayerController có field speed/sprint, dùng speed làm base
            var pcType = FindType("PlayerController");
            float baseSpeed = 6f;
            var pc = pcType != null ? child.GetComponent(pcType) : null;
            if (pc != null)
            {
                var sp = pcType.GetField("walkSpeed") ?? pcType.GetField("speed");
                if (sp != null) baseSpeed = Convert.ToSingle(sp.GetValue(pc));
            }
            agent.speed = baseSpeed;

            // AIPlayer
            var aiType = FindType("AIPlayer");
            if (aiType != null && child.GetComponent(aiType) == null)
            {
                var ai = child.gameObject.AddComponent(aiType);
                SetFieldOrPropIfExists(ai, "agent", agent);
                SetFieldOrPropIfExists(ai, "isHome", isHome);
                // homeSpot = vị trí hiện tại (điểm formation)
                var t = child.gameObject;
                // tạo empty child ghi lại vị trí gốc làm "homeSpot"
                var hs = new GameObject("HomeSpot");
                hs.transform.SetParent(t.transform.parent, false);
                hs.transform.position = t.transform.position;
                SetFieldOrPropIfExists(ai, "homeSpot", hs.transform);
            }
        }
    }

    static void TryAttachGK(GameObject team)
    {
        if (!team) return;
        // tìm thủ môn bằng tên hoặc tag GK
        var gk = team.GetComponentsInChildren<Transform>(true)
                     .FirstOrDefault(t => t.name.Contains("GK") || (t.CompareTag("GK")));
        if (gk == null) return;

        var aiType = FindType("AIPlayer");
        var gkAI = aiType != null ? gk.GetComponent(aiType) : null;
        if (gkAI == null && aiType != null)
        {
            gkAI = gk.gameObject.AddComponent(aiType);
        }
        // đặt homeSpot = chính vị trí hiện tại
        if (gkAI != null)
        {
            var hs = new GameObject("GK_HomeSpot");
            hs.transform.SetParent(gk.parent, false);
            hs.transform.position = gk.position;
            SetFieldOrPropIfExists(gkAI, "homeSpot", hs.transform);
            SetFieldOrPropIfExists(gkAI, "isHome", false);
        }

        // đảm bảo có NavMeshAgent
        var agent = gk.GetComponent<NavMeshAgent>() ?? gk.gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f; agent.speed = 5f; agent.angularSpeed = 720f; agent.acceleration = 20f;
    }

    // ---------- Helpers ----------
    static void WriteIfMissing(string path, string content)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content);
        Debug.Log("Created " + path);
    }

    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.FullName == name || t.Name == name);

    static Component AddComponentIfTypeExists(GameObject go, string typeName)
    {
        var t = FindType(typeName);
        if (t == null || !typeof(Component).IsAssignableFrom(t)) return null;
        return go.GetComponent(t) ?? go.AddComponent(t);
    }

    static void SetFieldOrPropIfExists(object target, string name, object value)
    {
        if (target == null || value == null) return;
        var t = target is Component c ? c.GetType() : target.GetType();
        var f = t.GetField(name); if (f != null) { f.SetValue(target is Component cc ? (object)cc : target, value); return; }
        var p = t.GetProperty(name); if (p != null && p.CanWrite) p.SetValue(target is Component cc2 ? (object)cc2 : target, value);
    }

    // ---------- RUNTIME CONTENTS ----------
    const string ISTATE =
@"public interface IState { void Enter(); void Tick(); void Exit(); }";

    const string STATEMACHINE =
@"public class StateMachine {
    IState _state;
    public void Set(IState s){ _state?.Exit(); _state = s; _state?.Enter(); }
    public void Tick(){ _state?.Tick(); }
}";

    const string WORLD_CONTEXT =
@"using UnityEngine;
public static class WorldContext
{
    public static Transform BallTransform;
    public static Rigidbody BallBody;
    public static Transform GoalHome;
    public static Transform GoalAway;
    public static Transform LastPossessor;

    public static Vector3 BallPos => BallTransform ? BallTransform.position : Vector3.zero;

    public static void Kick(Vector3 impulse)
    {
        if (BallBody) BallBody.AddForce(impulse, ForceMode.Impulse);
        else if (BallTransform && BallTransform.TryGetComponent<Rigidbody>(out var rb))
        { BallBody = rb; BallBody.AddForce(impulse, ForceMode.Impulse); }
    }
}";

    const string WORLD_BOOTSTRAP =
@"using UnityEngine;
public class WorldBootstrap : MonoBehaviour
{
    void Start(){
        // Ball từ Part C
        var ball = GameObject.FindWithTag(""Ball"") ?? GameObject.Find(""Ball"");
        if (ball){
            WorldContext.BallTransform = ball.transform;
            if (ball.TryGetComponent<Rigidbody>(out var rb)) WorldContext.BallBody = rb;
        }
        // Goals có sẵn
        var gh = GameObject.Find(""Goal_Home"") ?? GameObject.Find(""HomeGoal"");
        var ga = GameObject.Find(""Goal_Away"") ?? GameObject.Find(""AwayGoal"");
        if (gh) WorldContext.GoalHome = gh.transform;
        if (ga) WorldContext.GoalAway = ga.transform;
    }
}";

    const string TEAM_AI =
@"using UnityEngine;
public static class TeamAI
{
    // Trả về Transform gần bóng nhất trong mảng players
    public static Transform ClosestToBall(Transform[] players)
    {
        if (players == null || players.Length == 0) return null;
        float best = float.MaxValue; Transform bestT = null;
        Vector3 bp = WorldContext.BallPos;
        foreach (var t in players){
            if (!t) continue;
            float d = (t.position - bp).sqrMagnitude;
            if (d < best){ best = d; bestT = t; }
        }
        return bestT;
    }
}";

    const string TACTIC_SO =
@"using UnityEngine;
[CreateAssetMenu(menuName=""Football/Tactic"", fileName=""Tactic"")]
public class Tactic : ScriptableObject
{
    public enum Style { UltraDefensive, Defensive, Balanced, Offensive, UltraOffensive }
    public Style style = Style.Balanced;
    [Range(5f, 35f)] public float pressDistance = 12f;
    [Range(0f, 1f)] public float passRisk = 0.5f;
}";

    const string AI_PLAYER =
@"using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIPlayer : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform homeSpot;
    public bool isHome;
    public float kickPower = 8f;

    StateMachine fsm = new StateMachine();

    void Awake(){ if(!agent) agent = GetComponent<NavMeshAgent>(); }
    void Start(){ fsm.Set(new IdleState(this)); }
    void Update(){ fsm.Tick(); }

    public void KickToward(Vector3 target){
        Vector3 dir = (target - WorldContext.BallPos); dir.y = 0f;
        WorldContext.Kick(dir.normalized * kickPower);
    }
}";

    const string IDLE =
@"using UnityEngine;
public class IdleState : IState
{
    readonly AIPlayer ai;
    float _repathTime;
    public IdleState(AIPlayer a){ ai = a; }
    public void Enter(){ _repathTime = 0f; }
    public void Tick(){
        _repathTime -= Time.deltaTime;
        if (_repathTime <= 0f){
            _repathTime = 0.5f;
            if (ai.homeSpot) ai.agent.SetDestination(ai.homeSpot.position);
        }
        // ví dụ chuyển trạng thái: nếu mình gần bóng nhất -> ChaseBall (bạn sẽ cài ở Part F chi tiết)
        // ai.fsm.Set(new ChaseBallState(ai));  // khi đã viết logic phân công pressing
    }
    public void Exit(){}
}";

    const string CHASE =
@"using UnityEngine;
public class ChaseBallState : IState
{
    readonly AIPlayer ai;
    public ChaseBallState(AIPlayer a){ ai = a; }
    public void Enter(){}
    public void Tick(){
        ai.agent.SetDestination(WorldContext.BallPos);
        // điều kiện kết thúc/đổi state: khi chạm bóng -> Dribble/Pass/Shoot...
    }
    public void Exit(){}
}";

    const string DRIBBLE =
@"using UnityEngine;
public class DribbleState : IState
{
    readonly AIPlayer ai; float timer;
    public DribbleState(AIPlayer a){ ai = a; }
    public void Enter(){ timer = Random.Range(0.6f, 1.0f); }
    public void Tick(){
        // move về phía khung: ví dụ hướng tới goal đối phương
        var goal = ai.isHome ? WorldContext.GoalAway : WorldContext.GoalHome;
        if (goal) ai.agent.SetDestination(goal.position);

        timer -= Time.deltaTime;
        if (timer <= 0f){
            timer = Random.Range(0.6f, 1.0f);
            if (goal) ai.KickToward(goal.position); // gentle push
        }
    }
    public void Exit(){}
}";

    const string PASS =
@"using UnityEngine;
public class PassState : IState
{
    readonly AIPlayer ai; Transform target;
    public PassState(AIPlayer a, Transform mate){ ai=a; target=mate; }
    public void Enter(){}
    public void Tick(){
        if (target){ ai.KickToward(target.position); }
        // rồi quay lại Idle/ReturnHome
    }
    public void Exit(){}
}";

    const string SHOOT =
@"using UnityEngine;
public class ShootState : IState
{
    readonly AIPlayer ai;
    public ShootState(AIPlayer a){ ai=a; }
    public void Enter(){}
    public void Tick(){
        var goal = ai.isHome ? WorldContext.GoalAway : WorldContext.GoalHome;
        if (goal){ ai.KickToward(goal.position); }
        // đổi về ReturnHome sau khi sút
    }
    public void Exit(){}
}";

    const string MARK =
@"using UnityEngine;
public class MarkOpponentState : IState
{
    readonly AIPlayer ai; Transform markTarget;
    public MarkOpponentState(AIPlayer a, Transform opp){ ai=a; markTarget=opp; }
    public void Enter(){}
    public void Tick(){
        if(markTarget) ai.agent.SetDestination(markTarget.position);
    }
    public void Exit(){}
}";

    const string RETURN_HOME =
@"using UnityEngine;
public class ReturnHomeState : IState
{
    readonly AIPlayer ai;
    public ReturnHomeState(AIPlayer a){ ai=a; }
    public void Enter(){}
    public void Tick(){ if (ai.homeSpot) ai.agent.SetDestination(ai.homeSpot.position); }
    public void Exit(){}
}";

    const string GOALKEEPER =
@"using UnityEngine;
public class GoalkeeperState : IState
{
    readonly AIPlayer ai;
    public GoalkeeperState(AIPlayer a){ ai=a; }
    public void Enter(){}
    public void Tick(){
        // đơn giản: bám X theo bóng, đứng gần goalLine
        var g = ai.isHome ? WorldContext.GoalHome : WorldContext.GoalAway;
        if (!g) return;
        var p = g.position; p.x = WorldContext.BallPos.x;
        ai.agent.SetDestination(p);
    }
    public void Exit(){}
}";

    const string AI_DEBUG =
@"using UnityEngine;
public class AIDebugGizmos : MonoBehaviour
{
    public static bool Enabled;
    void Update(){ if (Input.GetKeyDown(KeyCode.F8)) Enabled = !Enabled; }
    void OnDrawGizmos(){
        if (!Enabled) return;
        if (WorldContext.BallTransform){
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(WorldContext.BallPos + Vector3.up*0.1f, 0.1f);
        }
    }
}";
}
#endif
