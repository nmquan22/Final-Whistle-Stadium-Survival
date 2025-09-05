#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class FixVCamTool
{
    [MenuItem("Tools/Football/Fix Broadcast VCam")]
    public static void FixVCam()
    {
        // 1) Target: ưu tiên Player đang chọn, fallback cầu thủ đầu tiên
        Transform target = Selection.activeGameObject && Selection.activeGameObject.CompareTag("Player")
            ? Selection.activeGameObject.transform
            : GameObject.FindGameObjectsWithTag("Player").FirstOrDefault()?.transform;

        // 2) Main Camera + CinemachineBrain
        var cam = Camera.main ? Camera.main.gameObject : GameObject.FindWithTag("MainCamera");
        if (!cam) { cam = new GameObject("Main Camera"); cam.tag = "MainCamera"; cam.AddComponent<Camera>(); }

        var brainT = TypeByName("Cinemachine.CinemachineBrain");
        if (brainT != null && !cam.GetComponent(brainT)) cam.AddComponent(brainT);

        // 3) Tạo/tìm VCam
        var vcamT = TypeByName("Cinemachine.CinemachineVirtualCamera");
        if (vcamT == null) { Debug.LogError("❌ Chưa cài Cinemachine (không tìm thấy CinemachineVirtualCamera)."); return; }

        var vcam = UnityEngine.Object.FindObjectOfType(vcamT) as Component;
        if (!vcam) { var go = new GameObject("VCam_Broadcast"); vcam = (Component)go.AddComponent(vcamT); }

        // 4) Follow / LookAt
        if (target)
        {
            vcamT.GetProperty("Follow")?.SetValue(vcam, target);
            vcamT.GetProperty("LookAt")?.SetValue(vcam, target);
        }

        // 5) BODY = Transposer -> m_FollowOffset = (0,12,-16)
        var transposerT = TypeByName("Cinemachine.CinemachineTransposer");
        if (transposerT != null)
        {
            var getCGeneric = GetGenericMethodDef(vcamT, "GetCinemachineComponent");
            var addCGeneric = GetGenericMethodDef(vcamT, "AddCinemachineComponent");

            object body = getCGeneric?.MakeGenericMethod(transposerT).Invoke(vcam, null);
            if (body == null) body = addCGeneric?.MakeGenericMethod(transposerT).Invoke(vcam, null);

            var followOffsetF = transposerT.GetField("m_FollowOffset", BindingFlags.Public | BindingFlags.Instance);
            if (followOffsetF != null) followOffsetF.SetValue(body, new Vector3(0, 12, -16));
        }

        // 6) AIM = Composer (screen Y ~ 0.45, soft zone 0.8)
        var composerT = TypeByName("Cinemachine.CinemachineComposer");
        if (composerT != null)
        {
            var getCGeneric = GetGenericMethodDef(vcamT, "GetCinemachineComponent");
            var addCGeneric = GetGenericMethodDef(vcamT, "AddCinemachineComponent");

            object aim = getCGeneric?.MakeGenericMethod(composerT).Invoke(vcam, null);
            if (aim == null) aim = addCGeneric?.MakeGenericMethod(composerT).Invoke(vcam, null);

            SetFieldIfExists(composerT, aim, "m_SoftZoneWidth", 0.8f);
            SetFieldIfExists(composerT, aim, "m_SoftZoneHeight", 0.8f);
            SetFieldIfExists(composerT, aim, "m_ScreenY", 0.45f);
        }

        // 7) FOV dễ nhìn
        var lens = vcamT.GetProperty("m_Lens", BindingFlags.Public | BindingFlags.Instance);
        if (lens != null)
        {
            var lensObj = lens.GetValue(vcam);
            var fovProp = lensObj.GetType().GetProperty("FieldOfView");
            fovProp?.SetValue(lensObj, 50f);
            lens.SetValue(vcam, lensObj); // ghi lại struct
        }

        Debug.Log("✅ VCam fixed: Follow/LookAt set, Offset (0,12,-16), Composer OK.");
    }

    // ---- helpers ----
    static Type TypeByName(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.FullName == fullName || t.Name == fullName);

    static MethodInfo GetGenericMethodDef(Type owner, string name) =>
        owner.GetMethods(BindingFlags.Public | BindingFlags.Instance)
             .FirstOrDefault(m => m.Name == name && m.IsGenericMethodDefinition);

    static void SetFieldIfExists(Type t, object instance, string field, object value)
    {
        var f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null) f.SetValue(instance, value);
    }
}
#endif
