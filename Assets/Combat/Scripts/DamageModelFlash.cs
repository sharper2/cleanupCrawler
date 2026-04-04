using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Briefly tints mesh / skinned model materials when <see cref="HealthComponent"/> takes damage.
    /// Uses <see cref="MaterialPropertyBlock"/> so shared materials are not duplicated.
    /// </summary>
    public sealed class DamageModelFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private HealthComponent health;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color flashColor = new(1f, 0.2f, 0.2f, 1f);
        [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
        [SerializeField, Min(0f)] private float flashPeak = 1f;
        [SerializeField] private bool useUnscaledTime;

        private readonly List<MaterialSlotCache> _cache = new();
        private MaterialPropertyBlock _block;
        private bool _cached;
        private Coroutine _flashRoutine;

        private struct MaterialSlotCache
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public int ColorPropertyId;
            public Color Original;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (health == null)
            {
                health = GetComponentInParent<HealthComponent>();
            }

            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.DamageTaken += OnDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamageTaken -= OnDamageTaken;
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }

            RestoreOriginalColors();
        }

        private void OnDamageTaken(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            EnsureCache();

            if (_cache.Count == 0)
            {
                return;
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }

            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private void EnsureCache()
        {
            if (_cached)
            {
                return;
            }

            _cache.Clear();

            foreach (var r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null)
                    {
                        continue;
                    }

                    if (!TryResolveColorProperty(m, out var propId, out var original))
                    {
                        continue;
                    }

                    _cache.Add(new MaterialSlotCache
                    {
                        Renderer = r,
                        MaterialIndex = i,
                        ColorPropertyId = propId,
                        Original = original
                    });
                }
            }

            _cached = true;
        }

        private static bool TryResolveColorProperty(Material m, out int propId, out Color original)
        {
            if (m.HasProperty(BaseColorId))
            {
                propId = BaseColorId;
                original = m.GetColor(BaseColorId);
                return true;
            }

            if (m.HasProperty(ColorId))
            {
                propId = ColorId;
                original = m.GetColor(ColorId);
                return true;
            }

            propId = 0;
            original = default;
            return false;
        }

        private IEnumerator FlashRoutine()
        {
            var dur = Mathf.Max(0.01f, flashDuration);
            var t = 0f;

            while (t < dur)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var u = Mathf.Clamp01(t / dur);
                var falloff = 1f - u;
                falloff *= falloff;
                var blend = flashPeak * falloff;
                ApplyBlend(blend);
                yield return null;
            }

            RestoreOriginalColors();
            _flashRoutine = null;
        }

        private void ApplyBlend(float blend)
        {
            blend = Mathf.Clamp01(blend);

            for (var i = 0; i < _cache.Count; i++)
            {
                var slot = _cache[i];
                var r = slot.Renderer;
                if (r == null)
                {
                    continue;
                }

                var c = Color.Lerp(slot.Original, flashColor, blend);
                _block.Clear();
                _block.SetColor(slot.ColorPropertyId, c);
                r.SetPropertyBlock(_block, slot.MaterialIndex);
            }
        }

        private void RestoreOriginalColors()
        {
            for (var i = 0; i < _cache.Count; i++)
            {
                var slot = _cache[i];
                var r = slot.Renderer;
                if (r == null)
                {
                    continue;
                }

                _block.Clear();
                _block.SetColor(slot.ColorPropertyId, slot.Original);
                r.SetPropertyBlock(_block, slot.MaterialIndex);
            }
        }
    }
}
