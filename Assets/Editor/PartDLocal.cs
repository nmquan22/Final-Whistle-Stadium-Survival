#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PartDLocal
{
    [MenuItem("Tools/Football/Set Selected As Local Player")]
    public static void SetSelectedAsLocal()
    {
        var go = Selection.activeGameObject;
        if (!go || go.tag != "Player")
        {
            EditorUtility.DisplayDialog("Set Local Player",
                "Hãy chọn 1 GameObject Player trong Hierarchy (tag = Player) rồi chạy lại.", "OK");
            return;
        }

        // 1) Tắt PlayerInput của TẤT CẢ cầu thủ khác
        foreach (var pi in UnityEngine.Object.FindObjectsOfType<UnityEngine.InputSystem.PlayerInput>())
        {
            if (pi.gameObject != go) pi.enabled = false;
        }
        // 2) Bật PlayerInput cho cầu thủ được chọn
        var myPI = go.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (myPI) myPI.enabled = true;

        // 3) Buộc camera follow người này
        HookCamera(go.transform);

        EditorUtility.DisplayDialog("Set Local Player",
            $"Đã đặt '{go.name}' làm Local Player và gán camera Follow/LookAt.", "OK");
    }

    static void HookCamera(Transform target)
    {
        if (!target) return;

        // Ưu tiên Cinemachine vcam nếu có
        var vcamType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.FullName == "Cinemachine.CinemachineVirtualCamera");
        if (vcamType != null)
        {
            var vcam = UnityEngine.Object.FindObjectOfType(vcamType) as Component;
            if (vcam != null)
            {
                var follow = vcamType.GetProperty("Follow");
                var lookAt = vcamType.GetProperty("LookAt");
                follow?.SetValue(vcam, target);
                lookAt?.SetValue(vcam, target);

                // Nâng góc nhìn kiểu broadcast (nếu có Transposer)
                var getC = vcamType.GetMethod("GetCinemachineComponent")
                    ?.MakeGenericMethod(AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                        .FirstOrDefault(t => t.FullName == "Cinemachine.CinemachineTransposer"));
                var transposer = getC?.Invoke(vcam, null);
                var offProp = transposer?.GetType().GetProperty("m_FollowOffset");
                if (offProp != null)
                {
                    // góc cao: (x=0,y=12,z=-16)
                    offProp.SetValue(transposer, new Vector3(0, 12, -16));
                }
                return;
            }
        }

        // Fallback: CameraFollow
        var camGo = Camera.main ? Camera.main.gameObject : GameObject.FindWithTag("MainCamera");
        if (!camGo)
        {
            camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
        }

        var cfType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == "CameraFollow");
        if (cfType != null)
        {
            var cf = camGo.GetComponent(cfType) ?? camGo.AddComponent(cfType);
            cfType.GetField("target")?.SetValue(cf, target);
            cfType.GetField("offset")?.SetValue(cf, new Vector3(0, 12, -16));
        }
        else
        {
            // nếu chưa có script CameraFollow, đặt camera tay cho dễ nhìn
            camGo.transform.position = target.position + new Vector3(0, 12, -16);
            camGo.transform.LookAt(target);
        }
    }
}
#endif
