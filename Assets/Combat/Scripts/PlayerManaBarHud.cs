using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerManaBarHud : MonoBehaviour
    {
        [SerializeField] private ManaComponent playerMana;
        [SerializeField] private Vector2 margin = new(24f, 24f);
        [SerializeField] private Vector2 size = new(260f, 20f);
        [SerializeField] private Color backgroundColor = new(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color fillColor = new(0.2f, 0.5f, 0.95f, 0.9f);
        [SerializeField] private Color textColor = Color.white;

        private void Awake()
        {
            if (playerMana == null)
            {
                playerMana = GetComponent<ManaComponent>();
            }

            if (playerMana == null)
            {
                var combatController = FindFirstObjectByType<PlayerCombatController>();
                if (combatController != null)
                {
                    playerMana = combatController.GetComponent<ManaComponent>();
                }
            }

            if (playerMana == null)
            {
                playerMana = FindFirstObjectByType<ManaComponent>();
            }
        }

        private void OnGUI()
        {
            if (playerMana == null)
            {
                return;
            }

            var x = Screen.width - margin.x - size.x;
            var bgRect = new Rect(x, margin.y, size.x, size.y);
            DrawRect(bgRect, backgroundColor);

            var ratio = playerMana.MaxMana <= 0f ? 0f : Mathf.Clamp01(playerMana.CurrentMana / playerMana.MaxMana);
            var fillRect = new Rect(x + 2f, margin.y + 2f, (size.x - 4f) * ratio, size.y - 4f);
            DrawRect(fillRect, fillColor);

            var label = $"Mana: {Mathf.CeilToInt(playerMana.CurrentMana)} / {Mathf.CeilToInt(playerMana.MaxMana)}";
            var previousColor = GUI.color;
            GUI.color = textColor;
            GUI.Label(bgRect, label);
            GUI.color = previousColor;
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
