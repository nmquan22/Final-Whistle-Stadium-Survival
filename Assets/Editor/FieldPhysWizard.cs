// Assets/Editor/FieldPhysWizard.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class FieldPhysWizard
{
    [MenuItem("Tools/Stadium Setup/Create Field_Phys from Selection")]
    public static void CreateFieldPhys()
    {
        var sel = Selection.activeGameObject;
        if (!sel) { Debug.LogError("Chọn object sân (có MeshRenderer) trước đã."); return; }

        var mr = sel.GetComponentInChildren<MeshRenderer>();
        if (!mr) { Debug.LogError("Không thấy MeshRenderer để lấy bounds."); return; }

        // Lấy bounds theo world, nhưng tạo collider làm con cùng parent để giữ local gọn
        var parent = sel.transform.parent;
        var go = new GameObject("Field_Phys");
        if (parent) go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("Field"); // nếu chưa có layer Field sẽ là -1 (bỏ qua)

        // Đặt vị trí & kích thước theo bounds
        Bounds b = mr.bounds;
        // Đưa về local của parent
        Vector3 centerLocal = (parent ? parent.InverseTransformPoint(b.center) : b.center);
        go.transform.localPosition = new Vector3(centerLocal.x, centerLocal.y, centerLocal.z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // Thêm BoxCollider mỏng làm nền vật lý
        var bc = go.AddComponent<BoxCollider>();
        // Đổi size sang local: vì BoxCollider size ở local space của GO vừa tạo (không scale)
        bc.size = new Vector3(b.size.x, 0.2f, b.size.z); // dày 0.2m
        bc.center = Vector3.zero;

        // Ẩn phần nhìn: không thêm MeshRenderer; chỉ collider
        // (Nếu muốn chắn chắc nằm đúng cao độ: ép Y = 0)
        go.transform.localPosition = new Vector3(go.transform.localPosition.x, 0f, go.transform.localPosition.z);

        Debug.Log("✅ Field_Phys đã tạo (BoxCollider). Hãy chơi thử: bóng sẽ không còn xuyên.");
    }
}
#endif
