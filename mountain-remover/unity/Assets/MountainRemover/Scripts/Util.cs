using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MountainRemover
{
    /// <summary>Creates and caches flat-colour materials that work in both the Built-in pipeline and URP.</summary>
    public static class Mats
    {
        static readonly Dictionary<int, Material> lit = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> unlit = new Dictionary<int, Material>();
        static Shader litShader, unlitShader;

        static bool URP => GraphicsSettings.currentRenderPipeline != null;

        static int Key(Color c)
        {
            Color32 k = c;
            return (k.r << 24) | (k.g << 16) | (k.b << 8) | k.a;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static Material Get(Color c)
        {
            int key = Key(c);
            if (lit.TryGetValue(key, out var m)) return m;
            if (litShader == null)
            {
                litShader = URP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
                if (litShader == null) litShader = Shader.Find("Standard");
                if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Legacy Shaders/Diffuse");
            }
            m = new Material(litShader);
            SetColor(m, c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.06f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.06f);
            lit[key] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            int key = Key(c);
            if (unlit.TryGetValue(key, out var m)) return m;
            if (unlitShader == null)
            {
                unlitShader = URP ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
                if (unlitShader == null) unlitShader = litShader;
            }
            m = new Material(unlitShader);
            SetColor(m, c);
            unlit[key] = m;
            return m;
        }
    }

    /// <summary>Small helpers for building low-poly props out of primitives.</summary>
    public static class Prim
    {
        static Font font;
        static readonly Dictionary<string, Mesh> coneCache = new Dictionary<string, Mesh>();

        static GameObject Make(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return g;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Color c) => Make(PrimitiveType.Cube, parent, pos, size, Mats.Get(c));
        public static GameObject UnlitBox(Transform parent, Vector3 pos, Vector3 size, Color c) => Make(PrimitiveType.Cube, parent, pos, size, Mats.Unlit(c));
        public static GameObject Sphere(Transform parent, Vector3 pos, float d, Color c) => Make(PrimitiveType.Sphere, parent, pos, Vector3.one * d, Mats.Unlit(c));

        /// <summary>Cylinder centred on pos.</summary>
        public static GameObject Cyl(Transform parent, Vector3 pos, float radius, float height, Color c)
            => Make(PrimitiveType.Cylinder, parent, pos, new Vector3(radius * 2, height / 2, radius * 2), Mats.Get(c));

        /// <summary>Flat-shaded cone (or frustum) with its base at pos.</summary>
        public static GameObject Cone(Transform parent, Vector3 pos, float r, float h, int sides, Color c, float rTop = 0)
        {
            string key = r + ":" + h + ":" + sides + ":" + rTop;
            if (!coneCache.TryGetValue(key, out var mesh)) { mesh = MeshUtil.Cone(r, rTop, h, sides); coneCache[key] = mesh; }
            var g = new GameObject("Cone");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(c);
            return g;
        }

        public static Font GetFont()
        {
            if (font != null) return font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
            if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { font = null; } }
            return font;
        }

        /// <summary>A dark sign board with hazard stripes and text, readable from the +Z side.</summary>
        public static GameObject Sign(Transform parent, string text, Vector3 pos, float w, float h)
        {
            var root = new GameObject("Sign");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            Box(root.transform, Vector3.zero, new Vector3(w, h, 0.06f), Data.Hex(0x1e2023));
            Box(root.transform, new Vector3(0, h * 0.44f, 0.01f), new Vector3(w, h * 0.09f, 0.07f), Data.Hex(0xf2b705));
            Box(root.transform, new Vector3(0, -h * 0.44f, 0.01f), new Vector3(w, h * 0.09f, 0.07f), Data.Hex(0xf2b705));
            var f = GetFont();
            if (f != null)
            {
                var tgo = new GameObject("Text");
                tgo.transform.SetParent(root.transform, false);
                tgo.transform.localPosition = new Vector3(0, 0, 0.05f);
                tgo.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var tm = tgo.AddComponent<TextMesh>();
                tm.font = f;
                tm.text = text;
                tm.fontSize = 64;
                tm.fontStyle = FontStyle.Bold;
                tm.characterSize = h * 0.075f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Data.Hex(0xf3efe6);
                tgo.GetComponent<MeshRenderer>().sharedMaterial = f.material;
                // Shrink to fit the board width.
                var b = tgo.GetComponent<MeshRenderer>().bounds.size.x;
                if (b > w * 0.9f && b > 0) tm.characterSize *= w * 0.9f / b;
            }
            return root;
        }
    }

    public static class MeshUtil
    {
        static void Tri(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nrm, outward) < 0) { var tmp = b; b = c; c = tmp; nrm = -nrm; }
            nrm.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nrm); n.Add(nrm); n.Add(nrm);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public static Mesh Cone(float r, float rTop, float h, int sides)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                Vector3 mid = new Vector3(Mathf.Cos((a0 + a1) / 2), 0, Mathf.Sin((a0 + a1) / 2));
                Vector3 outward = (mid + Vector3.up * (r - rTop) / Mathf.Max(h, 0.001f)).normalized;
                if (rTop <= 0)
                {
                    Tri(v, n, t, b0, b1, new Vector3(0, h, 0), outward);
                }
                else
                {
                    Vector3 t0 = new Vector3(Mathf.Cos(a0) * rTop, h, Mathf.Sin(a0) * rTop);
                    Vector3 t1 = new Vector3(Mathf.Cos(a1) * rTop, h, Mathf.Sin(a1) * rTop);
                    Tri(v, n, t, b0, b1, t1, outward);
                    Tri(v, n, t, b0, t1, t0, outward);
                    Tri(v, n, t, new Vector3(0, h, 0), t0, t1, Vector3.up);
                }
                Tri(v, n, t, Vector3.zero, b0, b1, Vector3.down);
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
