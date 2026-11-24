using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Models made in Blender by Tools/Blender/models.py (Resources/Models). Their materials are named by role (Paint, Glass,
    /// Shirt...); each is swapped for a game material in the colour Models/materials.txt lists, so everything renders with the
    /// device-safe templates. A tint callback recolours a role on a given renderer (a vehicle's Paint, a passenger's Shirt).
    /// </summary>
    internal static class SliceModels
    {
        private static Dictionary<string, (Color colour, float smoothness, float alpha)> roles;

        public static Transform Spawn(string model, Transform parent, Func<Renderer, string, Color?> tint = null)
        {
            Transform t = Object.Instantiate(Resources.Load<GameObject>("Models/" + model), parent, false).transform;
            t.name = model;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                Material[] ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = Material(ms[i].name, tint?.Invoke(r, ms[i].name));
                r.sharedMaterials = ms;
            }
            return t;
        }

        private static Material Material(string role, Color? tint)
        {
            roles ??= Load();
            if (!roles.TryGetValue(role, out var spec))
            {
                Debug.LogWarning("[Slice] materials.txt has no colour for model material " + role);
                spec = (Color.magenta, 0f, 1f);
            }
            Color c = tint ?? spec.colour;
            return spec.alpha < 1f ? SliceWorld.Glass(new Color(c.r, c.g, c.b, spec.alpha), spec.smoothness) : SliceWorld.Lit(c, spec.smoothness);
        }

        /// <summary>Lines of "role rrggbb roughness alpha"; smoothness is 1 - Blender's roughness.</summary>
        private static Dictionary<string, (Color, float, float)> Load()
        {
            var d = new Dictionary<string, (Color, float, float)>();
            foreach (string line in Resources.Load<TextAsset>("Models/materials").text.Split('\n'))
            {
                string[] f = line.Trim().Split(' ');
                if (f.Length != 4 || f[0].StartsWith("#")) continue;
                ColorUtility.TryParseHtmlString("#" + f[1], out Color c);
                d[f[0]] = (c, 1f - float.Parse(f[2], CultureInfo.InvariantCulture), float.Parse(f[3], CultureInfo.InvariantCulture));
            }
            return d;
        }
    }
}
