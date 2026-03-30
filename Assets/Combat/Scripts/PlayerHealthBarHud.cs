using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerHealthBarHud : MonoBehaviour
    {
        [SerializeField] private HealthComponent playerHealth;
        [SerializeField] private Vector2 position = new Vector2(24f, 24f);
        [SerializeField] private Vector2 size = new Vector2(260f, 20f);
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color fillColor = new Color(0.85f, 0.15f, 0.15f, 0.9f);

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<HealthComponent>();
            }
        }

        private void OnGUI()
        {
            if (playerHealth == null)
            {
                return;
            }

            var bgRect = new Rect(position.x, position.y, size.x, size.y);
            DrawRect(bgRect, backgroundColor);

            var ratio = playerHealth.MaxHealth <= 0f ? 0f : Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth);
            var fillRect = new Rect(position.x + 2f, position.y + 2f, (size.x - 4f) * ratio, size.y - 4f);
            DrawRect(fillRect, fillColor);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
