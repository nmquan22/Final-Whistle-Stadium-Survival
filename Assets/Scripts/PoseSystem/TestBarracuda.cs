using UnityEngine;
using Unity.Barracuda;

public class TestBarracuda : MonoBehaviour
{
    void Start()
    {
        // Nếu Barracuda hoạt động, đoạn này compile và chạy không lỗi
        Debug.Log("✅ Barracuda namespace OK");

        // Tạo model trống test thử
        var model = new Model();
        var worker = WorkerFactory.CreateWorker(WorkerFactory.Type.CSharpBurst, model);
        Debug.Log("✅ Worker created: " + worker);
        worker.Dispose();
    }
}
