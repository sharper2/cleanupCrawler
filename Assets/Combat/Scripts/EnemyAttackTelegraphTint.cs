using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Temporary tint on enemy renderers during attack windup (meshes or <see cref="SpriteRenderer"/>).
    /// </summary>
    public static class EnemyAttackTelegraphTint
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static void Apply(Renderer[] renderers, Color tintColor, MaterialPropertyBlock block)
        {
            if (renderers == null || block == null)
            {
                return;
            }

            foreach (var r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                var mats = r.sharedMaterials;
                for (var mi = 0; mi < mats.Length; mi++)
                {
                    var m = mats[mi];
                    if (m == null || !TryGetColorProperty(m, out var propId))
                    {
                        continue;
                    }

                    block.Clear();
                    r.GetPropertyBlock(block, mi);
                    block.SetColor(propId, tintColor);
                    r.SetPropertyBlock(block, mi);
                }
            }
        }

        public static void Clear(Renderer[] renderers)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                var mats = r.sharedMaterials;
                for (var mi = 0; mi < mats.Length; mi++)
                {
                    if (mats[mi] == null)
                    {
                        continue;
                    }

                    r.SetPropertyBlock(null, mi);
                }
            }
        }

        private static bool TryGetColorProperty(Material m, out int propId)
        {
            if (m.HasProperty(BaseColorId))
            {
                propId = BaseColorId;
                return true;
            }

            if (m.HasProperty(ColorId))
            {
                propId = ColorId;
                return true;
            }

            propId = 0;
            return false;
        }
    }
}
