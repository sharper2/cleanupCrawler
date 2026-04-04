using System.Collections;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Optional shared placeholder VFX for any orb ability (rings, flashes, etc.).
    /// Add once on the player; <see cref="AbilityQueueComponent"/> passes it through <see cref="AbilityQueueContext.OrbVisuals"/>.
    /// </summary>
    public sealed class OrbAbilityVisualFeedback : MonoBehaviour
    {
        [SerializeField, Min(8)] private int ringSegments = 56;
        [SerializeField, Min(0.001f)] private float ringWidth = 0.07f;
        [SerializeField] private float groundYOffset = 0.04f;
        [Tooltip("Optional; if unset, a simple Unlit/URP material is created at runtime.")]
        [SerializeField] private Material lineMaterial;

        private LineRenderer _line;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            EnsureLineRenderer();
        }

        private void EnsureLineRenderer()
        {
            if (_line != null)
            {
                return;
            }

            var go = new GameObject("OrbAbilityRing");
            go.transform.SetParent(transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.positionCount = ringSegments + 1;
            _line.startWidth = ringWidth;
            _line.endWidth = ringWidth;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;

            if (lineMaterial != null)
            {
                _line.material = lineMaterial;
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _line.material = new Material(shader);
                }
            }
        }

        /// <summary>Draws a fading ring on the XZ plane around this transform (typically the player).</summary>
        public void PlayRing(float worldRadius, float duration, Color color)
        {
            EnsureLineRenderer();
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }

            _flashRoutine = StartCoroutine(FlashRing(worldRadius, duration, color));
        }

        private IEnumerator FlashRing(float worldRadius, float duration, Color color)
        {
            _line.enabled = true;
            var start = Time.unscaledTime;

            while (Time.unscaledTime < start + duration)
            {
                var t = (Time.unscaledTime - start) / Mathf.Max(0.001f, duration);
                var c = color;
                c.a *= 1f - t * t;
                _line.startColor = c;
                _line.endColor = c;
                BuildRing(worldRadius);
                yield return null;
            }

            _line.enabled = false;
            _flashRoutine = null;
        }

        private void BuildRing(float worldRadius)
        {
            var center = transform.position + Vector3.up * groundYOffset;
            var n = ringSegments + 1;
            _line.positionCount = n;
            var r = Mathf.Max(0.05f, worldRadius);

            for (var i = 0; i < n; i++)
            {
                var ang = (float)i / ringSegments * Mathf.PI * 2f;
                var x = Mathf.Cos(ang) * r;
                var z = Mathf.Sin(ang) * r;
                _line.SetPosition(i, center + new Vector3(x, 0f, z));
            }
        }
    }
}
