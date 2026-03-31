using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerThreatSource : MonoBehaviour, IThreat
    {
        [SerializeField] private bool isThreat = true;

        public Transform ThreatTransform => transform;

        public bool IsThreatTo(GameObject observer)
        {
            return isThreat && observer != gameObject;
        }
    }
}
