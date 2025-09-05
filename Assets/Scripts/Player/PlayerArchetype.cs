using UnityEngine;
[CreateAssetMenu(menuName="Player/PlayerArchetype")]
public class PlayerArchetype : ScriptableObject
{
    public string role = "Default";
    public float speed = 6f;
    public float kickPower = 10f;
    public float stamina = 100f;
}