using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Shared IMGUI helpers for ability queue orb HUD so new visuals stay consistent.
    /// </summary>
    public static class AbilityQueueHudOrbDrawing
    {
        /// <summary>
        /// Bottom-right inside the icon: small backing + countdown text for passive activation.
        /// </summary>
        public static void DrawPassiveCountdownBadge(
            Rect iconRect,
            AbilityQueuePassiveHudState passive,
            Color backingColor,
            Color textColor,
            GUIStyle badgeLabelStyle,
            Vector2 badgeMaxSize,
            float paddingFromEdges)
        {
            if (!passive.HasCountdown || badgeLabelStyle == null)
            {
                return;
            }

            var text = passive.GetCountdownLabelText();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var content = new GUIContent(text);
            var textSize = badgeLabelStyle.CalcSize(content);
            var bw = Mathf.Min(badgeMaxSize.x, textSize.x + 6f);
            var bh = Mathf.Min(badgeMaxSize.y, textSize.y + 2f);
            var badgeRect = new Rect(
                iconRect.xMax - bw - paddingFromEdges,
                iconRect.yMax - bh - paddingFromEdges,
                bw,
                bh);

            var prev = GUI.color;
            GUI.color = backingColor;
            GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);
            GUI.color = textColor;
            GUI.Label(badgeRect, text, badgeLabelStyle);
            GUI.color = prev;
        }
    }
}
