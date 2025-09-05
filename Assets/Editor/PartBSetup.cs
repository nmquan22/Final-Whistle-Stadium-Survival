// Assets/Editor/PartBSetup.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;           // NavMeshSurface (NavMeshComponents package)
using Cinemachine;
using TMPro;
using System.Linq;

public static class PartBSetup
{
    [MenuItem("Tools/Stadium Setup/Run Part B Setup")]
    public static void Run()
    {
        // --------- B7. LIGHTING (URP) ---------
        // 1) Ensure a Directional Light exists
        var sun = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        if (!sun)
        {
            var go = new GameObject("Directional Light");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            sun.shadows = LightShadows.Soft;
        }
        else
        {
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        }

        // 2) Global Volume (Bloom + Vignette)
        var volGO = GameObject.Find("GlobalVolume");
        if (!volGO) volGO = new GameObject("GlobalVolume");
        var vol = volGO.GetComponent<Volume>() ?? volGO.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 0; vol.weight = 1f;
        if (!vol.sharedProfile)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, "Assets/GlobalVolumeProfile.asset");
            vol.sharedProfile = profile;
        }
        VolumeProfile p = vol.sharedProfile;

        if (!p.TryGet(out Bloom bloom))
        {
            bloom = p.Add<Bloom>(true);
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.15f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 1.1f;
        }
        if (!p.TryGet(out Vignette vig))
        {
            vig = p.Add<Vignette>(true);
            vig.intensity.overrideState = true; vig.intensity.value = 0.15f;
        }

        // Hint for Skybox
        var skybox = RenderSettings.skybox;
        if (!skybox)
            Debug.LogWarning("⚠️ B7: Chưa gán Skybox. Mở Window → Rendering → Lighting → Environment → Skybox Material.");

        // --------- B8. NAVIGATION ---------
        var navRoot = GameObject.Find("NavRoot");
        if (!navRoot)
        {
            navRoot = new GameObject("NavRoot");
        }
        var nms = navRoot.GetComponent<NavMeshSurface>() ?? navRoot.AddComponent<NavMeshSurface>();
        nms.collectObjects = CollectObjects.All;

        // --------- B9. CINEMACHINE ---------
        var mainCam = Camera.main;
        if (!mainCam)
        {
            var camGO = new GameObject("Main Camera");
            mainCam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0, 10, -10);
            camGO.transform.rotation = Quaternion.Euler(20, 0, 0);
        }
        if (!mainCam.GetComponent<CinemachineBrain>())
            mainCam.gameObject.AddComponent<CinemachineBrain>();

        var vcam = Object.FindObjectOfType<CinemachineVirtualCamera>();
        if (!vcam)
        {
            var go = new GameObject("VCam_Broadcast");
            vcam = go.AddComponent<CinemachineVirtualCamera>();
            go.transform.position = new Vector3(0, 40, -40);
            go.transform.rotation = Quaternion.Euler(45, 0, 0);
            vcam.m_Lens.FieldOfView = 50f;
            vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            var comp = vcam.AddCinemachineComponent<CinemachineComposer>();
            comp.m_ScreenX = 0.5f; comp.m_ScreenY = 0.45f;
        }

        // Try to auto-assign Ball as Follow/LookAt (requires Tag "Ball")
        var ballGO = GameObject.FindGameObjectWithTag("Ball");
        if (ballGO)
        {
            vcam.Follow = ballGO.transform;
            vcam.LookAt = ballGO.transform;
        }
        else
        {
            Debug.LogWarning("⚠️ B9: Không tìm thấy GameObject Tag=Ball để gán Follow/LookAt cho VCam. Bạn có thể tạo Ball rồi bấm lại menu.");
        }

        // --------- B10. UI CANVAS + TMP ---------
        var canvas = Object.FindObjectOfType<Canvas>();
        if (!canvas)
        {
            var ui = new GameObject("UIRoot");
            canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = ui.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            ui.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // ScoreText
            var score = CreateTMP("ScoreText", ui.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), TextAlignmentOptions.Left, "Home 0 - 0 Away");
            // TimerText
            var timer = CreateTMP("TimerText", ui.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), TextAlignmentOptions.Center, "00:00");

            // Attach ScoreBoard
            var sb = ui.AddComponent<ScoreBoard>();
            sb.scoreText = score;
            sb.timerText = timer;
        }

        // --------- B11. PERFORMANCE HINTS ---------
        // SRP Batcher check (cannot force from script reliably). Just warn:
        Debug.Log("ℹ️ B11: Hãy bật SRP Batcher trong URP Asset → Advanced. Cân nhắc Static batching cho khán đài/khối lớn và bake Occlusion Culling.");

        // --------- B12. QUICK CHECKS ---------
        // Packages / Layers / Tags sanity notes
        CheckPackageNotes();
        CheckLayerTagHints();

        // Optionally: attempt a NavMesh bake (editor-only helper)
        try
        {
            // Only shows a hint; baking is user action in UI in most versions.
            Debug.Log("ℹ️ B8: NavMeshSurface đã sẵn sàng. Chọn NavRoot → Bake để tạo NavMesh.");
        }
        catch { }

        EditorUtility.DisplayDialog("Part B Setup", "Đã setup B7→B11. Xem Console để biết cảnh báo/hints (B12).", "OK");
    }

    static TMP_Text CreateTMP(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, TextAlignmentOptions align, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 44; tmp.alignment = align; tmp.enableWordWrapping = false;
        return tmp;
    }

    static void CheckPackageNotes()
    {
        // Cinemachine / TMP / NavMeshComponents reminders
        if (!TypeExists("Cinemachine.CinemachineVirtualCamera"))
            Debug.LogWarning("⚠️ Chưa cài Cinemachine package.");
        if (!TypeExists("TMPro.TextMeshProUGUI"))
            Debug.LogWarning("⚠️ Chưa import TextMeshPro Essentials.");
        if (!TypeExists("Unity.AI.Navigation.NavMeshSurface"))
            Debug.LogWarning("⚠️ Chưa cài NavMeshComponents (com.unity.ai.navigation).");
    }

    static void CheckLayerTagHints()
    {
        // Recommend Ball tag
        if (GameObject.FindGameObjectWithTag("Ball") == null)
            Debug.Log("ℹ️ Gợi ý: tạo Ball (Tag=Ball, Layer=Ball) để VCam follow.");
    }

    static bool TypeExists(string typeName)
    {
        return System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Any(t => t.FullName == typeName);
    }
}
#endif
