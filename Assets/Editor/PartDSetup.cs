// Assets/Editor/PartDSetup.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public static class PartDSetup
{
    const string MENU = "Tools/Football/Run Part D Setup";

    [MenuItem(MENU)]
    public static void Run()
    {
        EnsureFolders();
        EnsureTagsAndLayers();
        CreateMaterials();                      // Home_Mat / Away_Mat
        CreateInputActionsAsset();              // Input/Controls.inputactions
        WriteRuntimeScriptsIfMissing();         // tạo 7 file runtime nếu thiếu

        // Sau khi tạo file, có thể Unity cần compile lại để các type xuất hiện.
        // Ta cố gắng tiếp tục; nếu type chưa có, hàm AddComponentByName/FindType sẽ bỏ qua & log.
        var playerPrefab = CreateOrUpdatePlayerPrefab();            // Player.prefab
        var gkPrefab = CreateOrUpdateGoalkeeperPrefab(playerPrefab);// Player_GK.prefab
        var firstPlayer = BuildTeamsInScene(playerPrefab, gkPrefab);// spawn 5v5 + GK
        HookCamera(firstPlayer);                                    // Cinemachine (nếu có) hoặc CameraFollow

        EditorUtility.DisplayDialog("Part D",
            "Setup hoàn tất (28→42). Nếu vừa mới tạo script lần đầu, Unity sẽ compile — hãy chạy lại menu này 1 lần nữa để auto gắn hết component (nếu thấy log đã skip).",
            "OK");
    }

    // ---------- Folders ----------
    static void EnsureFolders()
    {
        string[] dirs = {
            "Assets/Editor","Assets/Scripts","Assets/Scripts/Player","Assets/Scripts/Gameplay",
            "Assets/ScriptableObjects","Assets/Input","Assets/Materials","Assets/Prefabs"
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

    // ---------- Tags & Layers ----------
    static void EnsureTagsAndLayers()
    {
        AddTag("Ball"); AddTag("Player"); AddTag("Goal"); AddTag("GK");
        AddLayer("Player"); AddLayer("Ball"); AddLayer("Field"); AddLayer("Goal"); AddLayer("UIRaycast");
    }

    static void AddTag(string tag)
    {
        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.arraySize++;
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
    }

    static void AddLayer(string layer)
    {
        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = so.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (p.stringValue == layer) return;
            if (string.IsNullOrEmpty(p.stringValue))
            { p.stringValue = layer; so.ApplyModifiedProperties(); return; }
        }
    }

    // ---------- Materials ----------
    static void CreateMaterials()
    {
        CreateMatIfMissing("Assets/Materials/Home_Mat.mat", new Color(0.1f, 0.4f, 0.9f));
        CreateMatIfMissing("Assets/Materials/Away_Mat.mat", new Color(0.9f, 0.2f, 0.2f));
    }
    static void CreateMatIfMissing(string path, Color c)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path)) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        AssetDatabase.CreateAsset(m, path);
    }

    // ---------- Input Actions ----------
    static void CreateInputActionsAsset()
    {
        string path = "Assets/Input/Controls.inputactions";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)) return;

        string json = @"{
    ""name"": ""Controls"",
    ""maps"": [
        { ""name"": ""Gameplay"", ""actions"": [
            { ""name"": ""Move"", ""type"": ""Value"", ""expectedControlType"": ""Vector2"" },
            { ""name"": ""Look"", ""type"": ""Value"", ""expectedControlType"": ""Vector2"" },
            { ""name"": ""Sprint"", ""type"": ""Button"" },
            { ""name"": ""Kick"", ""type"": ""Button"" },
            { ""name"": ""Pass"", ""type"": ""Button"" },
            { ""name"": ""SwitchPlayer"", ""type"": ""Button"" }
        ],
        ""bindings"": [
            { ""name"": ""WASD"", ""id"": ""1"", ""path"": ""2DVector"", ""action"": ""Move"", ""isComposite"": true },
            { ""name"": ""up"",   ""path"": ""<Keyboard>/w"", ""action"": ""Move"", ""isPartOfComposite"": true },
            { ""name"": ""down"", ""path"": ""<Keyboard>/s"", ""action"": ""Move"", ""isPartOfComposite"": true },
            { ""name"": ""left"", ""path"": ""<Keyboard>/a"", ""action"": ""Move"", ""isPartOfComposite"": true },
            { ""name"": ""right"",""path"": ""<Keyboard>/d"", ""action"": ""Move"", ""isPartOfComposite"": true },

            { ""path"": ""<Gamepad>/leftStick"", ""action"": ""Move"" },
            { ""path"": ""<Mouse>/delta"", ""action"": ""Look"" },
            { ""path"": ""<Gamepad>/rightStick"", ""action"": ""Look"" },

            { ""path"": ""<Keyboard>/leftShift"", ""action"": ""Sprint"" },
            { ""path"": ""<Gamepad>/leftStickPress"", ""action"": ""Sprint"" },

            { ""path"": ""<Mouse>/leftButton"", ""action"": ""Kick"" },
            { ""path"": ""<Gamepad>/buttonSouth"", ""action"": ""Kick"" },

            { ""path"": ""<Mouse>/rightButton"", ""action"": ""Pass"" },
            { ""path"": ""<Gamepad>/buttonEast"", ""action"": ""Pass"" },

            { ""path"": ""<Keyboard>/tab"", ""action"": ""SwitchPlayer"" },
            { ""path"": ""<Gamepad>/rightShoulder"", ""action"": ""SwitchPlayer"" }
        ] }
    ],
    ""controlSchemes"": []
}";
        File.WriteAllText(path, json);
        AssetDatabase.ImportAsset(path);
        Debug.Log("Created Input/Controls.inputactions");
    }

    // ---------- Create runtime scripts ----------
    static void WriteRuntimeScriptsIfMissing()
    {
        WriteIfMissing("Assets/Scripts/Player/PlayerController.cs", PLAYER_CONTROLLER);
        WriteIfMissing("Assets/Scripts/Player/PlayerInputRelay.cs", PLAYER_INPUT_RELAY);
        WriteIfMissing("Assets/Scripts/Gameplay/BallInteractor.cs", BALL_INTERACTOR);
        WriteIfMissing("Assets/Scripts/Gameplay/Goalkeeper.cs", GOALKEEPER);
        WriteIfMissing("Assets/Scripts/Gameplay/Billboard.cs", BILLBOARD);
        WriteIfMissing("Assets/Scripts/Gameplay/CameraFollow.cs", CAMERA_FOLLOW);
        WriteIfMissing("Assets/Scripts/Player/PlayerArchetype.cs", ARCHETYPE);
        AssetDatabase.Refresh();

        // Tạo ScriptableObject PlayerArchetype bằng tên (không generic -> không lỗi compile Editor)
        string soPath = "Assets/ScriptableObjects/PlayerArchetype.asset";
        if (!AssetDatabase.LoadAssetAtPath<ScriptableObject>(soPath))
        {
            var so = ScriptableObject.CreateInstance("PlayerArchetype");
            if (so != null)
            {
                AssetDatabase.CreateAsset(so, soPath);
            }
            else
            {
                Debug.Log("PlayerArchetype chưa biên dịch (sẽ tạo asset ở lần chạy sau).");
            }
        }
    }

    static void WriteIfMissing(string path, string content)
    {
        if (File.Exists(path)) return;
        File.WriteAllText(path, content);
        Debug.Log("Created " + path);
    }

    // ---------- Prefabs ----------
    static GameObject CreateOrUpdatePlayerPrefab()
    {
        string prefabPath = "Assets/Prefabs/Player.prefab";
        GameObject root;

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing) root = PrefabUtility.InstantiatePrefab(existing) as GameObject;
        else root = GameObject.CreatePrimitive(PrimitiveType.Capsule);

        root.name = "Player";
        root.tag = "Player";
        TrySetLayer(root, "Player");

        // Visual (1.8m)
        root.transform.localScale = Vector3.one;
        var capCol = root.GetComponent<CapsuleCollider>(); if (capCol) UnityEngine.Object.DestroyImmediate(capCol);

        // CharacterController
        var cc = root.GetComponent<CharacterController>(); if (!cc) cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0);

        // Add runtime scripts by name (không generic)
        AddComponentByName(root, "PlayerController");
        AddComponentByName(root, "PlayerInputRelay");
        AddComponentByName(root, "BallInteractor");

        // PlayerInput
        var pi = root.GetComponent<PlayerInput>(); if (!pi) pi = root.AddComponent<PlayerInput>();
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/Controls.inputactions");
        pi.actions = asset; pi.defaultActionMap = "Gameplay"; pi.notificationBehavior = PlayerNotifications.SendMessages;

        // KickPoint
        var kp = root.transform.Find("KickPoint");
        if (!kp)
        {
            var go = new GameObject("KickPoint");
            kp = go.transform; kp.SetParent(root.transform, false);
            kp.localPosition = new Vector3(0, 0.5f, 0.7f);
        }

        // Arrow billboard
        var arrow = root.transform.Find("Arrow");
        if (!arrow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Arrow"; arrow = go.transform; arrow.SetParent(root.transform, false);
            arrow.localScale = new Vector3(0.08f, 0.02f, 0.08f);
            arrow.localPosition = new Vector3(0, 1.1f, 0);
            AddComponentByName(go, "Billboard");
        }

        // material
        var mr = root.GetComponentInChildren<MeshRenderer>();
        var homeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Home_Mat.mat");
        if (mr && homeMat) mr.sharedMaterial = homeMat;

        // Save prefab
        if (existing)
        {
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(root);
            return existing;
        }
        else
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
    }

    static GameObject CreateOrUpdateGoalkeeperPrefab(GameObject basePlayerPrefab)
    {
        string prefabPath = "Assets/Prefabs/Player_GK.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing) return existing;

        var inst = PrefabUtility.InstantiatePrefab(basePlayerPrefab) as GameObject;
        inst.name = "Player_GK"; inst.tag = "GK"; TrySetLayer(inst, "Player");

        // change material
        var mr = inst.GetComponentInChildren<MeshRenderer>();
        var away = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Away_Mat.mat");
        if (mr && away) mr.sharedMaterial = away;

        AddComponentByName(inst, "Goalkeeper");

        // reduce speed if PlayerController exists (safe reflection)
        var pcType = FindType("PlayerController");
        var pc = pcType != null ? inst.GetComponent(pcType) : null;
        if (pc != null)
        {
            var sp = pcType.GetField("speed"); if (sp != null) sp.SetValue(pc, 4.5f);
            var sprint = pcType.GetField("sprint"); if (sprint != null) sprint.SetValue(pc, 6.0f);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        UnityEngine.Object.DestroyImmediate(inst);
        return prefab;
    }

    // ---------- Build Teams ----------
    static Transform BuildTeamsInScene(GameObject playerPrefab, GameObject gkPrefab)
    {
        var homeRoot = GameObject.Find("HomeTeam")?.transform ?? new GameObject("HomeTeam").transform;
        var awayRoot = GameObject.Find("AwayTeam")?.transform ?? new GameObject("AwayTeam").transform;

        float zHome = -20f, zAway = 20f;
        float[] xs = { -20, -10, 0, 10, 20 };

        // home 5
        for (int i = 0; i < 5; i++)
        {
            var p = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            p.transform.SetParent(homeRoot, false);
            p.transform.position = new Vector3(xs[i], 0, zHome + (i % 2 == 0 ? -2f : 2f));
            p.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var mr = p.GetComponentInChildren<MeshRenderer>();
            var home = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Home_Mat.mat");
            if (mr && home) mr.sharedMaterial = home;
        }
        // home GK
        {
            var gk = PrefabUtility.InstantiatePrefab(gkPrefab) as GameObject;
            gk.transform.SetParent(homeRoot, false);
            gk.transform.position = new Vector3(0, 0, -31.5f);
            var gkType = FindType("Goalkeeper");
            var gks = gkType != null ? gk.GetComponent(gkType) : null;
            if (gks != null)
            {
                var f = gkType.GetField("goalZ"); if (f != null) f.SetValue(gks, -32f);
            }
            gk.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        // away 5
        for (int i = 0; i < 5; i++)
        {
            var p = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            p.transform.SetParent(awayRoot, false);
            p.transform.position = new Vector3(xs[i], 0, zAway + (i % 2 == 0 ? 2f : -2f));
            p.transform.rotation = Quaternion.LookRotation(Vector3.back);
            var mr = p.GetComponentInChildren<MeshRenderer>();
            var away = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Away_Mat.mat");
            if (mr && away) mr.sharedMaterial = away;
        }
        // away GK
        {
            var gk = PrefabUtility.InstantiatePrefab(gkPrefab) as GameObject;
            gk.transform.SetParent(awayRoot, false);
            gk.transform.position = new Vector3(0, 0, 31.5f);
            var gkType = FindType("Goalkeeper");
            var gks = gkType != null ? gk.GetComponent(gkType) : null;
            if (gks != null)
            {
                var f = gkType.GetField("goalZ"); if (f != null) f.SetValue(gks, 32f);
            }
            gk.transform.rotation = Quaternion.LookRotation(Vector3.back);
        }

        return homeRoot.childCount > 0 ? homeRoot.GetChild(0) : null;
    }

    // ---------- Camera hook ----------
    static void HookCamera(Transform target)
    {
        if (!target) { Debug.LogWarning("No player to follow."); return; }

        var mainCam = Camera.main ? Camera.main.gameObject : GameObject.FindWithTag("MainCamera");
        if (!mainCam)
        {
            var go = new GameObject("Main Camera");
            mainCam = go;
            var cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 12, -12);
            cam.transform.LookAt(target);
        }

        // Try Cinemachine by type name (không cần using)
        var vcamType = FindType("Cinemachine.CinemachineVirtualCamera");
        if (vcamType != null)
        {
            var vcamObj = UnityEngine.Object.FindObjectOfType(vcamType) as Component;
            if (vcamObj != null)
            {
                var followProp = vcamType.GetProperty("Follow");
                var lookAtProp = vcamType.GetProperty("LookAt");
                followProp?.SetValue(vcamObj, target);
                lookAtProp?.SetValue(vcamObj, target);
                return;
            }
        }

        // Fallback: simple CameraFollow
        AddComponentByName(mainCam, "CameraFollow");
        var cfType = FindType("CameraFollow");
        var cf = cfType != null ? mainCam.GetComponent(cfType) : null;
        if (cf != null)
        {
            var f = cfType.GetField("target");
            f?.SetValue(cf, target);
        }
    }

    // ---------- Utils ----------
    static void TrySetLayer(GameObject go, string layer)
    {
        int id = LayerMask.NameToLayer(layer);
        if (id >= 0) go.layer = id;
    }

    static Type FindType(string fullOrShort)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => {
                Type[] types = Type.EmptyTypes;
                try { types = a.GetTypes(); } catch { }
                return types;
            })
            .FirstOrDefault(t => t.FullName == fullOrShort || t.Name == fullOrShort);
    }

    static Component AddComponentByName(GameObject go, string typeName)
    {
        var t = FindType(typeName);
        if (t == null || !typeof(Component).IsAssignableFrom(t))
        {
            Debug.Log($"[PartDSetup] Skip AddComponent {typeName}: type chưa sẵn sàng (sẽ có sau khi Unity compile).");
            return null;
        }
        var c = go.GetComponent(t);
        return c ? c : go.AddComponent(t);
    }

    // ---------------- RUNTIME SCRIPT CONTENTS ----------------
    private const string PLAYER_CONTROLLER =
@"using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 6f, sprint = 9f, rotationSpeed = 720f;
    CharacterController cc; Vector2 moveInput; bool sprinting;

    void Awake(){ cc = GetComponent<CharacterController>(); }

    public void OnMove(InputValue v){ moveInput = v.Get<Vector2>(); }
    public void OnSprint(InputValue v){ sprinting = v.isPressed; }

    void Update(){
        Vector3 dir = new Vector3(moveInput.x, 0, moveInput.y);
        if(dir.sqrMagnitude > 0.0001f){
            float spd = sprinting ? sprint : speed;
            var cam = Camera.main ? Camera.main.transform : null;
            Vector3 world = cam ? cam.TransformDirection(dir) : dir;
            world.y = 0; world.Normalize();
            cc.Move(world * spd * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(world), rotationSpeed * Time.deltaTime);
        } else {
            cc.Move(Physics.gravity * Time.deltaTime);
        }
    }
}";

    private const string PLAYER_INPUT_RELAY =
@"using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BallInteractor))]
public class PlayerInputRelay : MonoBehaviour
{
    BallInteractor interactor;
    void Awake(){ interactor = GetComponent<BallInteractor>(); }
    public void OnKick(InputValue v){ if(v.isPressed) interactor.KickButton(); }
    public void OnPass(InputValue v){ if(v.isPressed) interactor.PassButton(); }
    public void OnSwitchPlayer(InputValue v){ /* TODO */ }
}";

    private const string BALL_INTERACTOR =
@"using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class BallInteractor : MonoBehaviour
{
    public float interactRadius = 2.2f;
    public float kickPower = 10f;
    public float passPower = 7f;
    public Transform kickPoint;

    void Reset(){ if(!kickPoint) kickPoint = transform.Find(""KickPoint""); }

    public void KickButton(){
        var ball = FindNearestBall();
        if (!ball) return;
        Vector3 origin = kickPoint ? kickPoint.position : transform.position + transform.forward*0.6f + Vector3.up*0.3f;
        Vector3 dir = (ball.transform.position - origin); dir.y = 0; dir = dir.sqrMagnitude>0.001f ? dir.normalized : transform.forward;
        var rb = ball.GetComponent<Rigidbody>(); if(!rb) return;
        rb.AddForce(dir * kickPower, ForceMode.Impulse);
    }

    public void PassButton(){
        var ball = FindNearestBall();
        if (!ball) return;
        var mate = FindNearestTeammate();
        Vector3 dir = mate ? (mate.position - (kickPoint?kickPoint.position:transform.position)) : transform.forward;
        dir.y = 0; dir = dir.sqrMagnitude>0.001f ? dir.normalized : transform.forward;
        var rb = ball.GetComponent<Rigidbody>(); if(!rb) return;
        rb.AddForce(dir * passPower, ForceMode.Impulse);
    }

    GameObject FindNearestBall(){
        GameObject best = null; float d2best = float.MaxValue;
        foreach (var b in GameObject.FindGameObjectsWithTag(""Ball"")){
            float d2 = (b.transform.position - (kickPoint?kickPoint.position:transform.position)).sqrMagnitude;
            if (d2 < interactRadius*interactRadius && d2 < d2best){ d2best = d2; best = b; }
        }
        return best;
    }

    Transform FindNearestTeammate(){
        Transform best = null; float d2best = 99999f;
        foreach (var p in GameObject.FindGameObjectsWithTag(""Player"")){
            if (p == gameObject) continue;
            float d2 = (p.transform.position - transform.position).sqrMagnitude;
            if (d2 < d2best){ d2best = d2; best = p.transform; }
        }
        return best;
    }
}";

    private const string GOALKEEPER =
@"using UnityEngine;
public class Goalkeeper : MonoBehaviour
{
    public float patrolWidth = 10f;
    public float goalZ = 32f;
    public float moveSpeed = 3.5f;

    void Update(){
        var pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -patrolWidth, patrolWidth);
        pos.z = Mathf.MoveTowards(pos.z, Mathf.Sign(goalZ)*Mathf.Abs(goalZ), Time.deltaTime * moveSpeed);
        transform.position = pos;
        var look = new Vector3(0, pos.y, 0);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look - transform.position), 360 * Time.deltaTime);
    }
}";

    private const string BILLBOARD =
@"using UnityEngine;
public class Billboard : MonoBehaviour
{
    void LateUpdate(){ if (Camera.main) transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward); }
}";

    private const string CAMERA_FOLLOW =
@"using UnityEngine;
public class CameraFollow : MonoBehaviour
{
    public Transform target; public Vector3 offset = new Vector3(0,12,-12); public float smooth = 8f;
    void LateUpdate(){
        if(!target) return;
        var desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * smooth);
        transform.rotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
    }
}";

    private const string ARCHETYPE =
@"using UnityEngine;
[CreateAssetMenu(menuName=""Player/PlayerArchetype"")]
public class PlayerArchetype : ScriptableObject
{
    public string role = ""Default"";
    public float speed = 6f;
    public float kickPower = 10f;
    public float stamina = 100f;
}";
}
#endif
