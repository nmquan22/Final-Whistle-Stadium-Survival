#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PartDFix
{
    [MenuItem("Tools/Football/Rebuild Teams (3v3)")]
    public static void Rebuild3v3()
    {
        // 1) Tìm/cấp root 2 đội
        var homeRoot = GameObject.Find("HomeTeam")?.transform ?? new GameObject("HomeTeam").transform;
        var awayRoot = GameObject.Find("AwayTeam")?.transform ?? new GameObject("AwayTeam").transform;

        // 2) Clear toàn bộ con cũ
        ClearChildren(homeRoot);
        ClearChildren(awayRoot);

        // 3) Load prefabs
        var playerPrefab = LoadPrefab("Assets/Prefabs/Player.prefab", "Player");
        var gkPrefab = LoadPrefab("Assets/Prefabs/Player_GK.prefab", "Player_GK");
        if (!playerPrefab || !gkPrefab) { Debug.LogError("Không tìm được Player.prefab / Player_GK.prefab"); return; }

        // 4) Lấy bounds sân & các trục
        if (!TryGetPitch(out var pitchBounds, out var fieldTr))
        {
            Debug.LogError("Không tìm thấy mặt sân (Layer=Field hoặc tên chứa 'Field').");
            return;
        }
        GetPitchAxes(pitchBounds, out var center, out var longDir, out var shortDir, out var halfLen, out var halfWid);

        // 5) Tham số 3v3 (tỉ lệ theo sân)
        // 1 GK đứng trên vạch gôn; 2 cầu thủ cách vạch gôn 8m & 16m (theo chiều dài),
        // rải sang trái/phải 30% bề ngang sân.
        float insetGoal = 1.2f;         // GK lùi vào trong khung
        float row1 = 8f;           // hàng 1 cách vạch gôn
        float row2 = 16f;          // hàng 2 cách vạch gôn
        float laneOffset = Mathf.Max(3f, halfWid * 0.30f);

        // HOME side = hướng longDir âm (min)
        float homeLine = -halfLen + insetGoal;
        SpawnSide(homeRoot, playerPrefab, gkPrefab, center, longDir, shortDir,
                  side: -1, line: homeLine, row1: row1, row2: row2, lane: laneOffset,
                  matPath: "Assets/Materials/Home_Mat.mat");

        // AWAY side = hướng longDir dương (max)
        float awayLine = +halfLen - insetGoal;
        SpawnSide(awayRoot, playerPrefab, gkPrefab, center, longDir, shortDir,
                  side: +1, line: awayLine, row1: row1, row2: row2, lane: laneOffset,
                  matPath: "Assets/Materials/Away_Mat.mat");

        // 6) Hook camera theo cầu thủ đầu tiên team nhà
        var firstHome = homeRoot.childCount > 0 ? homeRoot.GetChild(0) : null;
        HookCamera(firstHome);

        Debug.Log("✅ Rebuild 3v3 hoàn tất.");
    }

    // ---------------- helpers ----------------
    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; --i)
            UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    static GameObject LoadPrefab(string path, string nameHint)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (pf) return pf;

        // fallback: tìm theo tên trong Assets/Prefabs
        var guids = AssetDatabase.FindAssets($"{nameHint} t:prefab");
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileName(p).Equals(nameHint + ".prefab", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    static bool TryGetPitch(out Bounds b, out Transform fieldTr)
    {
        b = new Bounds(Vector3.zero, Vector3.zero); fieldTr = null;

        // ưu tiên layer Field
        var all = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
        var cand = all.FirstOrDefault(m => m.gameObject.layer == LayerMask.NameToLayer("Field"));
        if (!cand)
            cand = all.FirstOrDefault(m => m.name.ToLower().Contains("field") ||
                                           (m.transform.parent && m.transform.parent.name.ToLower().Contains("field")));
        if (!cand) return false;

        b = cand.bounds; fieldTr = cand.transform;
        return true;
    }

    static void GetPitchAxes(Bounds b, out Vector3 center, out Vector3 longDir, out Vector3 shortDir, out float halfLen, out float halfWid)
    {
        center = b.center;
        bool xIsLong = b.size.x >= b.size.z;
        longDir = (xIsLong ? Vector3.right : Vector3.forward);
        shortDir = (xIsLong ? Vector3.forward : Vector3.right);
        halfLen = (xIsLong ? b.extents.x : b.extents.z);
        halfWid = (xIsLong ? b.extents.z : b.extents.x);
    }

    static void SpawnSide(Transform root, GameObject playerPrefab, GameObject gkPrefab,
                          Vector3 center, Vector3 longDir, Vector3 shortDir,
                          int side, float line, float row1, float row2, float lane, string matPath)
    {
        // GK
        {
            var gk = PrefabUtility.InstantiatePrefab(gkPrefab) as GameObject;
            gk.transform.SetParent(root, false);
            var pos = center + longDir * (side * line);
            gk.transform.position = pos;
            gk.transform.rotation = Quaternion.LookRotation((center - pos).normalized, Vector3.up);
            SetupGK(gk, center, longDir, shortDir, side, halfLen: Mathf.Abs(line), halfWid: lane * 2f);
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        // 2 cầu thủ: row1 & row2, trái/phải
        Vector3[] offs =
        {
            shortDir * (-lane),
            shortDir * (+lane)
        };
        float[] rows = { row1, row2 };

        for (int i = 0; i < 2; i++)
        {
            var p = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            p.transform.SetParent(root, false);
            var pos = center + longDir * (side * (line - Mathf.Sign(side) * rows[i])) + offs[i];
            p.transform.position = pos;
            p.transform.rotation = Quaternion.LookRotation((center - pos).normalized, Vector3.up);

            var mr = p.GetComponentInChildren<MeshRenderer>();
            if (mr && mat) mr.sharedMaterial = mat;
        }
    }

    // GK cũ (goalZ) hay GK mới (InitAxis) đều support
    static void SetupGK(GameObject gk, Vector3 center, Vector3 longDir, Vector3 shortDir, int side, float halfLen, float halfWid)
    {
        var gkType = FindType("Goalkeeper");
        if (gkType == null) return;
        var comp = gk.GetComponent(gkType);
        if (comp == null) return;

        var fPatrol = gkType.GetField("patrolWidth");
        if (fPatrol != null) fPatrol.SetValue(comp, Mathf.Max(6f, halfWid * 0.6f));

        // bản GK “axis-aware”
        var mInit = gkType.GetMethod("InitAxis");
        if (mInit != null) { mInit.Invoke(comp, new object[] { center, longDir, shortDir, side, halfLen, halfWid }); return; }

        // fallback GK cũ: chỉnh về “goalZ” gần đúng theo longDir (nếu sân dọc Z)
        var fGoalZ = gkType.GetField("goalZ");
        if (fGoalZ != null)
        {
            // nếu longDir ≈ +Z hoặc -Z → map vào goalZ; nếu theo X thì vẫn hoạt động tạm
            float signZ = Vector3.Dot(longDir, Vector3.forward) >= 0 ? 1f : -1f;
            fGoalZ.SetValue(comp, side * signZ * Mathf.Abs(halfLen));
        }
    }

    static Type FindType(string fullOrShort)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
            })
            .FirstOrDefault(t => t.FullName == fullOrShort || t.Name == fullOrShort);
    }

    // Camera: ưu tiên Cinemachine vcam nếu có, nếu không thì gắn CameraFollow (runtime script của bạn)
    static void HookCamera(Transform target)
    {
        if (!target) return;

        var camGo = Camera.main ? Camera.main.gameObject : GameObject.FindWithTag("MainCamera");
        if (!camGo)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.AddComponent<Camera>();
            go.transform.position = new Vector3(0, 12, -12);
            go.transform.LookAt(target);
            camGo = go;
        }

        var vcamType = FindType("Cinemachine.CinemachineVirtualCamera");
        if (vcamType != null)
        {
            var vcam = UnityEngine.Object.FindObjectOfType(vcamType) as Component;
            if (vcam != null)
            {
                var follow = vcamType.GetProperty("Follow");
                var lookAt = vcamType.GetProperty("LookAt");
                follow?.SetValue(vcam, target);
                lookAt?.SetValue(vcam, target);
                return;
            }
        }

        // fallback
        var cfType = FindType("CameraFollow");
        if (cfType != null)
        {
            var cf = camGo.GetComponent(cfType) ?? camGo.AddComponent(cfType);
            var f = cfType.GetField("target");
            f?.SetValue(cf, target);
        }
    }
}
#endif
