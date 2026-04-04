using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Renders the queue at the top center: slots left (back) to right (front), names below icons.
    /// </summary>
    public sealed class AbilityQueueHud : MonoBehaviour
    {
        [SerializeField] private AbilityQueueComponent queue;
        [SerializeField] private float topMargin = 16f;
        [SerializeField] private Vector2 iconSize = new(48f, 48f);
        [SerializeField] private float slotSpacing = 8f;
        [SerializeField] private float nameHeight = 22f;
        [SerializeField] private Color slotBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color frontHighlightColor = new(0.3f, 0.6f, 0.9f, 0.35f);
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private Font labelFont;

        private void Awake()
        {
            if (queue == null)
            {
                queue = FindFirstObjectByType<AbilityQueueComponent>();
            }
        }

        private void OnGUI()
        {
            if (queue == null)
            {
                return;
            }

            var count = queue.Count;
            if (count == 0)
            {
                return;
            }

            var slotW = iconSize.x + slotSpacing;
            var totalW = count * iconSize.x + Mathf.Max(0, count - 1) * slotSpacing;
            var startX = (Screen.width - totalW) * 0.5f;
            var y = topMargin;

            var prev = GUI.color;
            for (var i = 0; i < count; i++)
            {
                var item = queue.GetSlot(i);
                if (item == null)
                {
                    continue;
                }

                var x = startX + i * slotW;
                var iconRect = new Rect(x, y, iconSize.x, iconSize.y);
                var isFront = i == count - 1;

                var bg = iconRect;
                GUI.color = isFront ? frontHighlightColor : slotBackgroundColor;
                GUI.DrawTexture(bg, Texture2D.whiteTexture);

                var sprite = item.QueueSprite;
                if (sprite != null)
                {
                    DrawSprite(iconRect, sprite);
                }

                var nameRect = new Rect(x, y + iconSize.y + 2f, iconSize.x, nameHeight);
                GUI.color = labelColor;
                var font = labelFont != null ? labelFont : GUI.skin.label.font;
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    font = font,
                    fontSize = 11
                };
                GUI.Label(nameRect, item.DisplayName ?? "", style);
            }

            GUI.color = prev;
        }

        private static void DrawSprite(Rect screenRect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var tw = tex.width;
            var th = tex.height;
            var uv = new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
            GUI.DrawTextureWithTexCoords(screenRect, tex, uv);
        }
    }
}
