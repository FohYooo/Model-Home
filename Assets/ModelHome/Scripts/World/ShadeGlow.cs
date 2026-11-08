using UnityEngine;

namespace ModelHome
{
    /// <summary>Turns a lamp's shade glow on/off per renderer (M_Lampshade emits by default; a property
    /// block blacks it out when the lamp is off), so lamps sharing the material switch independently.</summary>
    public static class ShadeGlow
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public static void Set(Renderer r, bool on)
        {
            if (r == null) return;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].name.StartsWith("M_Lampshade")) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block, i);
                block.SetColor(EmissionColor, on ? mats[i].GetColor(EmissionColor) : Color.black);
                r.SetPropertyBlock(block, i);
            }
        }
    }
}
