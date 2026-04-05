using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Empty slot outlines; orbs slide on screen. Front (next evoke) is the rightmost filled slot.
    /// New orbs join the back (left) and enter from off-screen left into their slot, easing in as they arrive.
    /// Evoked orbs linger and slide off to the right of the bar.
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
        [Tooltip("Lower = snappier snap-in; higher = softer deceleration into the slot.")]
        [SerializeField, Min(0.02f)] private float slotSlideSmoothTime = 0.22f;
        [Tooltip("Caps approach speed in slot-columns per second (visual index space).")]
        [SerializeField, Min(0.01f)] private float slotSlideMaxSpeed = 14f;
        [SerializeField, Min(0f)] private float newOrbEnterFromLeftOffsetSlots = 2.5f;
        [Header("Evoke feedback")]
        [SerializeField, Min(0f)] private float evokeFlashDuration = 0.28f;
        [SerializeField] private Color evokeIconFlashColor = new(1f, 0.97f, 0.75f, 0.55f);
        [SerializeField, Min(0f)] private float evokedOrbLingerSeconds = 1f;
        [Tooltip("Linger orb animates to this many slot-columns past the right edge of the bar (index maxSlots).")]
        [SerializeField, Min(0f)] private float evokedOrbLingerExitOffsetSlots = 1.35f;

        private readonly List<TrackedOrb> _tracked = new();
        private readonly HashSet<int> _knownIds = new();
        private readonly List<LingeringEvokedOrb> _linger = new();

        private struct TrackedOrb
        {
            public int InstanceId;
            public IAbilityQueueItem Item;
            public float VisualSlotIndex;
            public float VisualSlotVelocity;
        }

        private struct LingeringEvokedOrb
        {
            public IAbilityQueueItem Item;
            public float VisualSlotIndex;
            public float VisualSlotVelocity;
            public float RemoveAtTime;
            /// <summary>Evoke flash overlay on this linger orb until this time (unscaled).</summary>
            public float FlashUntilTime;
            /// <summary>SmoothDamp target in slot index space (evoke column or off-screen right).</summary>
            public float LingerTargetVisualSlot;
            /// <summary>Used for fade alpha (may differ from default evoke linger).</summary>
            public float LingerDurationSeconds;
        }

        private void Awake()
        {
            if (queue == null)
            {
                var gridPlayer = FindFirstObjectByType<DungeonGridPlayerController>();
                if (gridPlayer != null)
                {
                    queue = gridPlayer.GetComponent<AbilityQueueComponent>()
                            ?? gridPlayer.GetComponentInChildren<AbilityQueueComponent>(true)
                            ?? gridPlayer.GetComponentInParent<AbilityQueueComponent>();
                }

                if (queue == null)
                {
                    queue = FindFirstObjectByType<AbilityQueueComponent>();
                }
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

            var lingerDuration = evokedOrbLingerSeconds;
            var lingerTarget = maxSlots + evokedOrbLingerExitOffsetSlots;
            var lingerStart = frontVis;
            if (item is IAbilityQueueEvokeLingerBehavior lingerBehavior)
            {
                lingerDuration = Mathf.Max(0.01f, lingerBehavior.EvokeLingerDurationSeconds);
                if (lingerBehavior.EvokeLingerStayRightOfHud)
                {
                    // Column index maxSlots is immediately right of the bar (slots are 0..maxSlots-1).
                    lingerTarget = maxSlots;
                    lingerStart = maxSlots;
                }
                else if (lingerBehavior.EvokeLingerStayAtEvokeSlot)
                {
                    lingerTarget = frontVis;
                }
            }

            _linger.Add(new LingeringEvokedOrb
            {
                Item = item,
                VisualSlotIndex = lingerStart,
                VisualSlotVelocity = 0f,
                RemoveAtTime = Time.unscaledTime + lingerDuration,
                FlashUntilTime = Time.unscaledTime + evokeFlashDuration,
                LingerTargetVisualSlot = lingerTarget,
                LingerDurationSeconds = lingerDuration
            });
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
                    VisualSlotIndex = VisualSlotFromQueueIndexRuntime(i, count, queue.MaxSlots),
                    VisualSlotVelocity = 0f
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
                    VisualSlotIndex = startVis,
                    VisualSlotVelocity = 0f
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
            StepLingerVisualInterpolation();
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

            for (var i = 0; i < _tracked.Count; i++)
            {
                var orb = _tracked[i];
                var slotInQueue = FindSlotIndexForInstance(orb.InstanceId, count);
                if (slotInQueue < 0)
                {
                    continue;
                }

                var target = VisualSlotFromQueueIndexRuntime(slotInQueue, count, maxSlots);
                var vel = orb.VisualSlotVelocity;
                orb.VisualSlotIndex = Mathf.SmoothDamp(
                    orb.VisualSlotIndex,
                    target,
                    ref vel,
                    slotSlideSmoothTime,
                    slotSlideMaxSpeed,
                    dt);
                orb.VisualSlotVelocity = vel;
                _tracked[i] = orb;
            }
        }

        private void StepLingerVisualInterpolation()
        {
            if (queue == null || _linger.Count == 0)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;

            for (var i = 0; i < _linger.Count; i++)
            {
                var ling = _linger[i];
                var vel = ling.VisualSlotVelocity;
                ling.VisualSlotIndex = Mathf.SmoothDamp(
                    ling.VisualSlotIndex,
                    ling.LingerTargetVisualSlot,
                    ref vel,
                    slotSlideSmoothTime,
                    slotSlideMaxSpeed,
                    dt);
                ling.VisualSlotVelocity = vel;
                _linger[i] = ling;
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

                var passiveShown = false;
                if (showPassiveCountdown && queue.TryGetPassiveCountdownForInstanceId(orb.InstanceId, out var passive))
                {
                    passiveShown = passive.HasCountdown;
                    AbilityQueueHudOrbDrawing.DrawPassiveCountdownBadge(
                        orbRect,
                        passive,
                        passiveBadgeBackingColor,
                        passiveBadgeTextColor,
                        passiveBadgeStyle,
                        passiveBadgeMaxSize,
                        passiveBadgePadding);
                }

                if (orb.Item is IAbilityQueueAttackStackContributor stackContributor
                    && queue.TryGetAttackStackDamageForInstanceId(orb.InstanceId, out var stackDamage))
                {
                    var totalDamage = stackContributor.GetEvokeDamageTotal(stackDamage);
                    AbilityQueueHudOrbDrawing.DrawEvokeDamageStackBadge(
                        orbRect,
                        totalDamage,
                        passiveShown,
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
                var fade = Mathf.Clamp01((ling.RemoveAtTime - now) / Mathf.Max(0.01f, ling.LingerDurationSeconds));

                var c = orbBackgroundColor;
                c.a *= 0.35f + 0.4f * fade;
                GUI.color = c;
                GUI.DrawTexture(orbRect, Texture2D.whiteTexture);

                if (ling.Item.QueueSprite != null)
                {
                    DrawSpriteTinted(orbRect, ling.Item.QueueSprite, 0.75f + 0.2f * fade);
                }

                if (now < ling.FlashUntilTime)
                {
                    GUI.color = evokeIconFlashColor;
                    GUI.DrawTexture(orbRect, Texture2D.whiteTexture);
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
