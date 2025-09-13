using Unity.Netcode;

public class PlayerMeta : NetworkBehaviour
{
    public NetworkVariable<byte> teamId = new(); // 0=Home, 1=Away
    public NetworkVariable<byte> indexInTeam = new(); // 0 hoặc 1
}
