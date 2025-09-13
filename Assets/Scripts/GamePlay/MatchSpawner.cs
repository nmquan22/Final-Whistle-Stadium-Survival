using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class MatchSpawner : NetworkBehaviour
{
    [Header("Prefabs & Spawns")]
    public GameObject playerPrefab;
    public GameObject ballPrefab;
    public Transform homeSpawn, awaySpawn;
    public bool enableAIForAwayWhenSolo = true;


    GameObject ballInstance;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) { Debug.LogError("[MatchSpawner] No NetworkManager in scene."); return; }

        nm.OnClientConnectedCallback += OnClientConnected;

 
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null) nm.OnClientConnectedCallback -= OnClientConnected;
    }

    void OnClientConnected(ulong _)
    {
        if (!IsServer) return;
        // Mỗi khi có client mới vào, respawn cho sạch
        Invoke(nameof(SpawnAll), 0.2f);
    }


    void SpawnAll()
    {
        if (!IsServer) return;
        if (!ValidateRefs()) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) { Debug.LogError("[MatchSpawner] No NetworkManager."); return; }

        // Dọn cũ
        foreach (var meta in FindObjectsOfType<PlayerMeta>())
        {
            var no = meta.GetComponent<NetworkObject>();
            if (no && no.IsSpawned) no.Despawn(true);
        }
        foreach (var b in FindObjectsOfType<BallNetwork>())
        {
            var no = b.GetComponent<NetworkObject>();
            if (no && no.IsSpawned) no.Despawn(true);
        }
        ballInstance = null;

        // Spawn bóng
        Vector3 mid = Vector3.Lerp(homeSpawn.position, awaySpawn.position, 0.5f) + Vector3.up * 0.2f;
        ballInstance = Instantiate(ballPrefab, mid, Quaternion.identity);
        ballInstance.GetComponent<NetworkObject>().Spawn(true);

        // Xác định owner
        ulong serverId = NetworkManager.ServerClientId; // 0
        ulong hostClientId = nm.LocalClientId;              // ID của host dưới vai trò client
        var clientIds = nm.ConnectedClientsIds.ToList();

        // SOLO: chỉ có host-client
        if (nm.IsHost && clientIds.Count == 1)
        {
            // Home = host-client (bạn), Away = server (AI)
            SpawnTeam(hostClientId, homeSpawn, team: 0, asAI: false);
            SpawnTeam(serverId, awaySpawn, team: 1, asAI: enableAIForAwayWhenSolo);
            Debug.Log("[MatchSpawner] SOLO: Home=host-client, Away=AI(server).");
            return;
        }

        // PvP: host-client vs client đầu tiên khác
        if (clientIds.Count >= 2)
        {
            ulong awayOwner = clientIds.First(id => id != hostClientId);
            SpawnTeam(hostClientId, homeSpawn, team: 0, asAI: false);
            SpawnTeam(awayOwner, awaySpawn, team: 1, asAI: false);
            Debug.Log("[MatchSpawner] PvP: Home(host-client) vs Away(client).");
            return;
        }

        Debug.Log("[MatchSpawner] No clients yet. Waiting…");
    }


bool ValidateRefs()
    {
        if (!playerPrefab || !ballPrefab || !homeSpawn || !awaySpawn)
        {
            Debug.LogError("[MatchSpawner] Assign PlayerPrefab, BallPrefab, HomeSpawn, AwaySpawn in Inspector.");
            return false;
        }
        return true;
    }

    void SpawnTeam(ulong ownerId, Transform baseSpawn, int team, bool asAI)
    {
        Vector3[] offs = { new(-1.5f, 0, 0), new(1.5f, 0, 0) };  // 2 người
        for (int i = 0; i < 2; i++)
        {
            var go = Instantiate(playerPrefab, baseSpawn.position + offs[i], baseSpawn.rotation);
            var no = go.GetComponent<NetworkObject>();
            no.SpawnWithOwnership(ownerId, true);

            var meta = go.GetComponent<PlayerMeta>();
            if (meta) { meta.teamId.Value = (byte)team; meta.indexInTeam.Value = (byte)i; }

            // BẬT AI nếu là đội Away trong chế độ solo
            if (asAI && team == 1)
            {
                // tắt controller người
                var humanCtl = go.GetComponent<PlayerControllerNet>();
                if (humanCtl) humanCtl.enabled = false;

                // gắn AI (nếu prefab chưa có)
                var ai = go.GetComponent<AIPlayerController>();
                if (!ai) ai = go.AddComponent<AIPlayerController>();
                ai.teamId = 1;
            }
        }
    }
}
