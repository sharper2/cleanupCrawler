using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Billboards a <see cref="SpriteRenderer"/> to one of four compass sprites from the enemy's flat facing
    /// (<see cref="Transform.forward"/> after <see cref="EnemyStateController"/> turns toward the player).
    /// Works with existing <see cref="DamageModelFlash"/> and <see cref="EnemyAttackTelegraphTint"/> (same MaterialPropertyBlock tint path).
    /// Note: roaming movement does not rotate the enemy yet, so the sprite may stay on the last combat facing until a new FaceThreat/FaceDirection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpriteFacing : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Facing +world Z (grid \"north\" / forward).")]
        [SerializeField] private Sprite north;

        [Tooltip("Facing +world X (grid \"east\" / right).")]
        [SerializeField] private Sprite east;

        [Tooltip("Facing -world Z (grid \"south\" / back).")]
        [SerializeField] private Sprite south;

        [Tooltip("Facing -world X (grid \"west\" / left).")]
        [SerializeField] private Sprite west;

        [SerializeField] private bool hideIfSpriteMissing = true;

        private static readonly Vector3[] CardinalFlats =
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back,
            Vector3.left
        };

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            var f = transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f)
            {
                return;
            }

            f.Normalize();

            var best = 0;
            var bestDot = float.NegativeInfinity;
            for (var i = 0; i < 4; i++)
            {
                var d = Vector3.Dot(f, CardinalFlats[i]);
                if (d > bestDot)
                {
                    bestDot = d;
                    best = i;
                }
            }

            var s = best switch
            {
                0 => north,
                1 => east,
                2 => south,
                3 => west,
                _ => north
            };

            if (s == null)
            {
                if (hideIfSpriteMissing)
                {
                    spriteRenderer.enabled = false;
                }

                return;
            }

            spriteRenderer.enabled = true;
            if (spriteRenderer.sprite != s)
            {
                spriteRenderer.sprite = s;
            }
        }
    }
}
