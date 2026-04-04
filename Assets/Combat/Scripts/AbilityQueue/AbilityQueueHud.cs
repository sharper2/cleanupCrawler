using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Empty slot outlines; orbs slide on screen. Front (next evoke) is the rightmost filled slot.
    /// New orbs join the back (left) and enter from off-screen left into their slot.
    /// </summary>
    public sealed class AbilityQueueHud : MonoBehaviour
    {
        [SerializeField] private AbilityQueueComponent queue;
        [SerializeField] private float topMargin = 16f;
        [SerializeField] private Vector2 iconSize = new(48f, 48f);
        [SerializeField] private float slotSpacing = 8f;
        [SerializeField] private float nameHeight = 22f;
        [Header("Orb appearance")]
        [SerializeField] private Color orbBackgroundColor = new(0.35f, 0.75f, 1f, 0.55f);
        [SerializeField] private Color emptySlotOutlineColor = new(0.5f, 0.55f, 0.6f, 0.9f);
        [SerializeField] private Color orbLabelColor = Color.white;
        [SerializeField] private Color emptySlotLabelColor = new(0.55f, 0.55f, 0.6f, 0.85f);
        [SerializeField] private Font labelFont;
        [Header("Passive countdown (on orb icon)")]
        [SerializeField] private bool showPassiveCountdown = true;
        [SerializeField, Range(7, 14)] private int passiveCountdownFontSize = 9;
        [SerializeField] private Vector2 passiveBadgeMaxSize = new(34f, 16f);
        [SerializeField, Min(0f)] private float passiveBadgePadding = 2f;
        [SerializeField] private Color passiveBadgeBackingColor = new(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color passiveBadgeTextColor = new(1f, 1f, 1f, 0.95f);
        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float slotSlideSpeed = 12f;
        [SerializeField, Min(0f)] private float newOrbEnterFromLeftOffsetSlots = 2.5f;
        [Header("Evoke feedback")]
        [SerializeField, Min(0f)] private float evokeFlashDuration = 0.28f;
        [SerializeField] private Color evokeIconFlashColor = new(1f, 0.97f, 0.75f, 0.55f);
        [SerializeField, Min(0f)] private float evokedOrbLingerSeconds = 1f;

        private readonly List<TrackedOrb> _tracked = new();
        private readonly HashSet<int> _knownIds = new();
        private readonly List<LingeringEvokedOrb> _linger = new();

        private float _evokeFlashUntil;
        private float _evokeFlashVisualSlot;

        private struct TrackedOrb
        {
            public int InstanceId;
            public IAbilityQueueItem Item;
            public float VisualSlotIndex;
        }

        private struct LingeringEvokedOrb
        {
            public IAbilityQueueItem Item;
            public float VisualSlotIndex;
            public float RemoveAtTime;
        }

        private void Awake()
        {
            if (queue == null)
            {
                queue = FindFirstObjectByType<AbilityQueueComponent>();
            }

            if (queue != null)
            {
                queue.QueueChanged += OnQueueChanged;
                queue.FrontOrbEvoked += OnFrontOrbEvoked;
                WarmStartTrackedOrbs();
            }
        }

        private void OnFrontOrbEvoked(IAbilityQueueItem item)
        {
            if (queue == null || item == null)
            {
                return;
            }

            var maxSlots = queue.MaxSlots;
            // Event runs before the front orb is removed; queue still has full count.
            var countBefore = queue.Count;
            var frontIdx = countBefore - 1;
            var frontVis = VisualSlotFromQueueIndexRuntime(frontIdx, countBefore, maxSlots);

            for (var t = 0; t < _tracked.Count; t++)
            {
                if (_tracked[t].Item != item)
                {
                    continue;
                }

                frontVis = Mathf.Max(frontVis, _tracked[t].VisualSlotIndex);
                break;
            }

            _linger.Add(new LingeringEvokedOrb
            {
                Item = item,
                VisualSlotIndex = frontVis,
                RemoveAtTime = Time.unscaledTime + evokedOrbLingerSeconds
            });
            _evokeFlashVisualSlot = frontVis;
            _evokeFlashUntil = Time.unscaledTime + evokeFlashDuration;
        }

        private void WarmStartTrackedOrbs()
        {
            var count = queue.Count;
            for (var i = 0; i < count; i++)
            {
                var id = queue.GetSlotInstanceId(i);
                if (id == 0)
                {
                    continue;
                }

                _knownIds.Add(id);
                _tracked.Add(new TrackedOrb
                {
                    InstanceId = id,
                    Item = queue.GetSlot(i),
                    VisualSlotIndex = VisualSlotFromQueueIndexRuntime(i, count, queue.MaxSlots)
                });
            }
        }

        private void OnDestroy()
        {
            if (queue != null)
            {
                queue.QueueChanged -= OnQueueChanged;
                queue.FrontOrbEvoked -= OnFrontOrbEvoked;
            }
        }

        private void OnQueueChanged()
        {
            if (queue == null)
            {
                return;
            }

            SyncNewOrbsFromLeft();
            ReconcileTrackedItems();
        }

        /// <summary>Queue index 0 = back (left), Count-1 = front (right). Visual column index for HUD bar.</summary>
        private static float VisualSlotFromQueueIndexRuntime(int queueIndexFromBack, int count, int maxSlots)
        {
            return queueIndexFromBack + Mathf.Max(0, maxSlots - count);
        }

        private void SyncNewOrbsFromLeft()
        {
            var count = queue.Count;
            var maxSlots = queue.MaxSlots;

            for (var i = 0; i < count; i++)
            {
                var id = queue.GetSlotInstanceId(i);
                if (id == 0 || _knownIds.Contains(id))
                {
                    continue;
                }

                _knownIds.Add(id);
                var item = queue.GetSlot(i);
                var targetVis = VisualSlotFromQueueIndexRuntime(i, count, maxSlots);
                var startVis = targetVis - newOrbEnterFromLeftOffsetSlots;

                _tracked.Add(new TrackedOrb
                {
                    InstanceId = id,
                    Item = item,
                    VisualSlotIndex = startVis
                });
            }
        }

        private void LateUpdate()
        {
            if (queue == null)
            {
                return;
            }

            ReconcileTrackedItems();
            StepVisualInterpolation();
            TickLinger();
        }

        private void ReconcileTrackedItems()
        {
            var count = queue.Count;
            var maxSlots = queue.MaxSlots;
            var alive = new HashSet<int>();
            for (var i = 0; i < count; i++)
            {
                var id = queue.GetSlotInstanceId(i);
                if (id == 0)
                {
                    continue;
                }

                alive.Add(id);
                var item = queue.GetSlot(i);

                for (var t = 0; t < _tracked.Count; t++)
                {
                    if (_tracked[t].InstanceId != id)
                    {
                        continue;
                    }

                    var o = _tracked[t];
                    o.Item = item;
                    _tracked[t] = o;
                }
            }

            for (var t = _tracked.Count - 1; t >= 0; t--)
            {
                var id = _tracked[t].InstanceId;
                if (alive.Contains(id))
                {
                    continue;
                }

                _knownIds.Remove(id);
                _tracked.RemoveAt(t);
            }
        }

        private void TickLinger()
        {
            var now = Time.unscaledTime;
            for (var i = _linger.Count - 1; i >= 0; i--)
            {
                if (_linger[i].RemoveAtTime <= now)
                {
                    _linger.RemoveAt(i);
                }
            }
        }

        private void StepVisualInterpolation()
        {
            if (queue == null)
            {
                return;
            }

            var count = queue.Count;
            var maxSlots = queue.MaxSlots;
            var dt = Time.deltaTime;
            var speed = slotSlideSpeed;

            for (var i = 0; i < _tracked.Count; i++)
            {
                var orb = _tracked[i];
                var slotInQueue = FindSlotIndexForInstance(orb.InstanceId, count);
                if (slotInQueue < 0)
                {
                    continue;
                }

                var target = VisualSlotFromQueueIndexRuntime(slotInQueue, count, maxSlots);
                orb.VisualSlotIndex = Mathf.MoveTowards(orb.VisualSlotIndex, target, speed * dt);
                _tracked[i] = orb;
            }
        }

        private int FindSlotIndexForInstance(int instanceId, int count)
        {
            for (var s = 0; s < count; s++)
            {
                if (queue.GetSlotInstanceId(s) == instanceId)
                {
                    return s;
                }
            }

            return -1;
        }

        private void OnGUI()
        {
            if (queue == null)
            {
                return;
            }

            var count = queue.Count;
            var maxSlots = queue.MaxSlots;
            var slotW = iconSize.x + slotSpacing;
            var totalW = maxSlots * iconSize.x + Mathf.Max(0, maxSlots - 1) * slotSpacing;
            var startX = (Screen.width - totalW) * 0.5f;
            var y = topMargin;

            var prev = GUI.color;
            var font = labelFont != null ? labelFont : GUI.skin.label.font;
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                font = font,
                fontSize = 11
            };

            var passiveBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                font = font,
                fontSize = passiveCountdownFontSize,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };

            for (var i = 0; i < maxSlots; i++)
            {
                var x = startX + i * slotW;
                var iconRect = new Rect(x, y, iconSize.x, iconSize.y);
                DrawEmptySlotOutline(iconRect, emptySlotOutlineColor);
                var nameRectEmpty = new Rect(x, y + iconSize.y + 2f, iconSize.x, nameHeight);
                GUI.color = emptySlotLabelColor;
                GUI.Label(nameRectEmpty, "—", labelStyle);
            }

            DrawOrbList(_tracked, count, startX, y, slotW, labelStyle, passiveBadgeStyle);
            DrawOrbListLinger(_linger, startX, y, slotW, labelStyle);

            var flashOn = Time.unscaledTime < _evokeFlashUntil;
            if (flashOn)
            {
                var fx = startX + _evokeFlashVisualSlot * slotW;
                var iconFlashRect = new Rect(fx, y, iconSize.x, iconSize.y);
                GUI.color = evokeIconFlashColor;
                GUI.DrawTexture(iconFlashRect, Texture2D.whiteTexture);
            }

            GUI.color = prev;
        }

        private void DrawOrbList(
            List<TrackedOrb> list,
            int count,
            float startX,
            float y,
            float slotW,
            GUIStyle labelStyle,
            GUIStyle passiveBadgeStyle)
        {
            for (var t = 0; t < list.Count; t++)
            {
                var orb = list[t];
                if (orb.Item == null)
                {
                    continue;
                }

                if (FindSlotIndexForInstance(orb.InstanceId, count) < 0)
                {
                    continue;
                }

                var vx = startX + orb.VisualSlotIndex * slotW;
                var orbRect = new Rect(vx, y, iconSize.x, iconSize.y);

                GUI.color = orbBackgroundColor;
                GUI.DrawTexture(orbRect, Texture2D.whiteTexture);

                var sprite = orb.Item.QueueSprite;
                if (sprite != null)
                {
                    DrawSpriteTinted(orbRect, sprite, 1f);
                }

                if (showPassiveCountdown && queue.TryGetPassiveCountdownForInstanceId(orb.InstanceId, out var passive))
                {
                    AbilityQueueHudOrbDrawing.DrawPassiveCountdownBadge(
                        orbRect,
                        passive,
                        passiveBadgeBackingColor,
                        passiveBadgeTextColor,
                        passiveBadgeStyle,
                        passiveBadgeMaxSize,
                        passiveBadgePadding);
                }

                var nameRect = new Rect(vx, y + iconSize.y + 2f, iconSize.x, nameHeight);
                GUI.color = orbLabelColor;
                GUI.Label(nameRect, orb.Item.DisplayName ?? "", labelStyle);
            }
        }

        private void DrawOrbListLinger(List<LingeringEvokedOrb> list, float startX, float y, float slotW, GUIStyle labelStyle)
        {
            var now = Time.unscaledTime;
            for (var i = 0; i < list.Count; i++)
            {
                var ling = list[i];
                if (ling.Item == null)
                {
                    continue;
                }

                var vx = startX + ling.VisualSlotIndex * slotW;
                var orbRect = new Rect(vx, y, iconSize.x, iconSize.y);
                var fade = Mathf.Clamp01((ling.RemoveAtTime - now) / Mathf.Max(0.01f, evokedOrbLingerSeconds));

                var c = orbBackgroundColor;
                c.a *= 0.35f + 0.4f * fade;
                GUI.color = c;
                GUI.DrawTexture(orbRect, Texture2D.whiteTexture);

                if (ling.Item.QueueSprite != null)
                {
                    DrawSpriteTinted(orbRect, ling.Item.QueueSprite, 0.75f + 0.2f * fade);
                }

                var nameRect = new Rect(vx, y + iconSize.y + 2f, iconSize.x, nameHeight);
                var lc = orbLabelColor;
                lc.a *= fade;
                GUI.color = lc;
                GUI.Label(nameRect, ling.Item.DisplayName ?? "", labelStyle);
            }
        }

        private static void DrawEmptySlotOutline(Rect r, Color outlineColor)
        {
            var prev = GUI.color;
            GUI.color = outlineColor;
            const float t = 2f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.25f);
            GUI.DrawTexture(new Rect(r.x + t, r.y + t, r.width - 2f * t, r.height - 2f * t), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void DrawSprite(Rect screenRect, Sprite sprite)
        {
            DrawSpriteTinted(screenRect, sprite, 1f);
        }

        private static void DrawSpriteTinted(Rect screenRect, Sprite sprite, float brightness)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            var prev = GUI.color;
            GUI.color = new Color(brightness, brightness, brightness, 1f);
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var tw = tex.width;
            var th = tex.height;
            var uv = new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
            GUI.DrawTextureWithTexCoords(screenRect, tex, uv);
            GUI.color = prev;
        }
    }
}
