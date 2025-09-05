using UnityEngine;
[CreateAssetMenu(menuName="Football/Tactic", fileName="Tactic")]
public class Tactic : ScriptableObject
{
    public enum Style { UltraDefensive, Defensive, Balanced, Offensive, UltraOffensive }
    public Style style = Style.Balanced;
    [Range(5f, 35f)] public float pressDistance = 12f;
    [Range(0f, 1f)] public float passRisk = 0.5f;
}