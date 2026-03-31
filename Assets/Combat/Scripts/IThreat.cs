using UnityEngine;

namespace DungeonGenerator
{
    public interface IThreat
    {
        Transform ThreatTransform { get; }
        bool IsThreatTo(GameObject observer);
    }
}
