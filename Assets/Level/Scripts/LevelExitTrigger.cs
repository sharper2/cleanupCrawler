using UnityEngine;
using DungeonGenerator;

namespace CleanupCrawler.Levels
{
    public class LevelExitTrigger : MonoBehaviour
    {
        [SerializeField] private LevelController levelController;

        public void Initialize(LevelController controller)
        {
            levelController = controller;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (levelController == null || other == null || !TryGetPlayerController(other.gameObject, out var player))
            {
                return;
            }

            levelController.TryUseExit(player.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (levelController == null || collision == null || !TryGetPlayerController(collision.gameObject, out var player))
            {
                return;
            }

            levelController.TryUseExit(player.gameObject);
        }

        private static bool TryGetPlayerController(GameObject source, out DungeonGridPlayerController player)
        {
            player = null;
            if (source == null)
            {
                return false;
            }

            player = source.GetComponent<DungeonGridPlayerController>()
                     ?? source.GetComponentInParent<DungeonGridPlayerController>()
                     ?? source.GetComponentInChildren<DungeonGridPlayerController>(true);

            return player != null;
        }
    }
}
