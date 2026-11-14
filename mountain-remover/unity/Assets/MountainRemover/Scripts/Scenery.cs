using System.Collections.Generic;
using UnityEngine;

namespace MountainRemover
{
    public partial class GameManager
    {
        Transform facRoot, treeRoot, skyRoot;
        Vector3 postPos, siloPos;
        readonly List<Transform> beltItems = new List<Transform>();
        readonly List<float> beltT = new List<float>();
        float beltX0, beltX1;
        readonly List<Transform> clouds = new List<Transform>();
        Transform highlight;
        readonly List<Renderer> highlightR = new List<Renderer>();
        Transform flag, cloth;
        GameObject scar;

        static readonly Color Hazard = Data.Hex(0xf2b705);
        static readonly Color Charcoal = Data.Hex(0x2b2f33);

        // ------------------------------------------------------------------ static world dressing
        void BuildBackdrop()
        {
            skyRoot = new GameObject("Scenery").transform;
            skyRoot.SetParent(transform, false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground";
            ground.transform.SetParent(skyRoot, false);
            ground.transform.localScale = new Vector3(300, 1, 300);
            ground.GetComponent<Renderer>().sharedMaterial = Mats.Get(Data.Hex(0x5c9440));

            scar = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(scar.GetComponent<Collider>());
            scar.name = "Dig Site";
            scar.transform.SetParent(skyRoot, false);
            scar.transform.localPosition = new Vector3(0, 0.01f, 0);
            scar.GetComponent<Renderer>().sharedMaterial = Mats.Get(Data.Hex(0xa88a62));

            // Ring of distant snowy peaks.
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2 + Noise.Hash2(i, 1, 9) * 0.2f, R = 520 + Noise.Hash2(i, 2, 9) * 260;
                float h = 90 + Noise.Hash2(i, 3, 9) * 220, rad = 70 + Noise.Hash2(i, 4, 9) * 110;
                int sides = 5 + i % 3;
                var pos = new Vector3(Mathf.Cos(a) * R, -2, Mathf.Sin(a) * R);
                var rot = Quaternion.Euler(0, Noise.Hash2(i, 5, 9) * 180, 0);
                var m = Prim.Cone(skyRoot, pos, rad, h, sides, Data.Hex(i % 2 == 1 ? 0x8796a8u : 0x7d8c9cu));
                m.transform.localRotation = rot;
                var cap = Prim.Cone(skyRoot, pos + Vector3.up * h * 0.72f, rad * 0.28f, h * 0.28f, sides, Data.Hex(0xf3f6fa));
                cap.transform.localRotation = rot;
            }

            for (int i = 0; i < 14; i++)
            {
                var c = new GameObject("Cloud").transform;
                c.SetParent(skyRoot, false);
                for (int k = 0; k < 3; k++)
                {
                    var b = Prim.Box(c, new Vector3(k * 7 - 7, (k % 2) * 2, (k - 1) * 3), new Vector3(18 + k * 6, 4, 10 + k * 3), Color.white);
                    b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                c.localPosition = new Vector3((Noise.Hash2(i, 7, 3) - 0.5f) * 900, 120 + Noise.Hash2(i, 8, 3) * 60, (Noise.Hash2(i, 9, 3) - 0.5f) * 900);
                clouds.Add(c);
            }
        }

        void BuildHighlight()
        {
            highlight = new GameObject("Target Outline").transform;
            highlight.SetParent(transform, false);
            const float s = 1.02f, t = 0.03f;
            for (int axis = 0; axis < 3; axis++)
                for (int i = 0; i < 4; i++)
                {
                    float a = (i & 1) == 0 ? -s / 2 : s / 2, b = (i & 2) == 0 ? -s / 2 : s / 2;
                    Vector3 p, size;
                    if (axis == 0) { p = new Vector3(0, a, b); size = new Vector3(s, t, t); }
                    else if (axis == 1) { p = new Vector3(a, 0, b); size = new Vector3(t, s, t); }
                    else { p = new Vector3(a, b, 0); size = new Vector3(t, t, s); }
                    var e = Prim.UnlitBox(highlight, p, size, Color.white);
                    var r = e.GetComponent<Renderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    highlightR.Add(r);
                }
            highlight.gameObject.SetActive(false);
        }

        void BuildFlag()
        {
            flag = new GameObject("Summit Flag").transform;
            flag.SetParent(transform, false);
            Prim.Box(flag, new Vector3(0, 0.8f, 0), new Vector3(0.06f, 1.6f, 0.06f), Data.Hex(0xe9e2cf));
            var pivot = new GameObject("Cloth").transform;
            pivot.SetParent(flag, false);
            pivot.localPosition = new Vector3(0, 1.38f, 0);
            Prim.Box(pivot, new Vector3(0.36f, 0, 0), new Vector3(0.7f, 0.42f, 0.02f), Data.Hex(0xd0602f));
            cloth = pivot;
        }

        void PlaceFlag()
        {
            int h = world.Highest(out int col);
            summitH = h;
            if (col < 0) { flag.gameObject.SetActive(false); return; }
            flag.gameObject.SetActive(true);
            flag.position = new Vector3(col % world.W + 0.5f + world.ox, h, col / world.W + 0.5f + world.oz);
        }

        void UpdateAmbient(float dt)
        {
            foreach (var c in clouds)
            {
                var p = c.localPosition;
                p.x += dt * 2.2f;
                if (p.x > 500) p.x = -500;
                c.localPosition = p;
            }
            float speed = S.silo.n > 0 ? 0.35f : 0;
            for (int i = 0; i < beltItems.Count; i++)
            {
                beltT[i] = (beltT[i] + dt * speed) % 1f;
                var p = beltItems[i].localPosition;
                p.x = Mathf.Lerp(beltX0, beltX1, beltT[i]);
                beltItems[i].localPosition = p;
            }
            if (cloth != null) cloth.localRotation = Quaternion.Euler(0, Mathf.Sin(tGame * 3) * 15, 0);
        }

        // ------------------------------------------------------------------ per-mountain buildings
        void BuildFacilities()
        {
            if (facRoot != null) Destroy(facRoot.gameObject);
            facRoot = new GameObject("Facilities").transform;
            facRoot.SetParent(transform, false);
            beltItems.Clear(); beltT.Clear();

            scar.transform.localScale = new Vector3((world.W + 2) / 10f, 1, (world.D + 2) / 10f);

            float zF = -(world.D / 2f + 10);
            postPos = new Vector3(-7, 0, zF);
            siloPos = new Vector3(3, 0, zF);

            // Trading post (front faces +Z, towards the mountain)
            var post = new GameObject("Trading Post").transform;
            post.SetParent(facRoot, false);
            post.localPosition = postPos;
            Prim.Box(post, new Vector3(0, 1.5f, 0), new Vector3(5, 3, 3.6f), Data.Hex(0xa0673d));
            var roof = Prim.Cone(post, new Vector3(0, 3.0f, 0), 4.2f, 1.8f, 4, Data.Hex(0xc8552b));
            roof.transform.localRotation = Quaternion.Euler(0, 45, 0);
            roof.transform.localScale = new Vector3(1, 1, 0.8f);
            Prim.Box(post, new Vector3(0, 1.05f, 2.2f), new Vector3(4.4f, 0.25f, 1.2f), Data.Hex(0x6b4429));
            Prim.Sign(post, "TRADING POST", new Vector3(0, 2.45f, 1.84f), 4.2f, 1.05f);

            // Silo
            var silo = new GameObject("Silo").transform;
            silo.SetParent(facRoot, false);
            silo.localPosition = siloPos;
            Prim.Cyl(silo, new Vector3(0, 3.3f, 0), 1.5f, 5.4f, Data.Hex(0xb8bdc3));
            Prim.Cone(silo, new Vector3(0, 6.0f, 0), 1.6f, 1.2f, 10, Data.Hex(0x8f959c));
            foreach (var l in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Prim.Box(silo, new Vector3(l.x, 0.45f, l.y), new Vector3(0.2f, 0.9f, 0.2f), Hazard);
            Prim.Sign(silo, "SILO", new Vector3(0, 2.2f, 1.56f), 2.2f, 0.6f);

            if (S.belt > 0)
            {
                beltX0 = siloPos.x - 1.6f; beltX1 = postPos.x + 2.6f;
                float len = beltX0 - beltX1, cx = (beltX0 + beltX1) / 2, bz = zF + 1.2f;
                Prim.Box(facRoot, new Vector3(cx, 0.75f, bz), new Vector3(len, 0.25f, 1), Charcoal);
                for (int i = 0; i < 4; i++) Prim.Box(facRoot, new Vector3(beltX1 + len * (i + 0.5f) / 4, 0.37f, bz), new Vector3(0.16f, 0.75f, 0.9f), Hazard);
                for (int i = 0; i < 6; i++)
                {
                    beltItems.Add(Prim.Box(facRoot, new Vector3(cx, 1.07f, bz), Vector3.one * 0.4f, Data.Mats[3].color).transform);
                    beltT.Add(i / 6f);
                }
            }

            if (S.crusher > 0)
            {
                var c = new GameObject("Crusher").transform;
                c.SetParent(facRoot, false);
                c.localPosition = new Vector3(8, 0, zF);
                Prim.Box(c, new Vector3(0, 1, 0), new Vector3(2.4f, 2, 2.4f), Data.Hex(0x3f6f8f));
                var hop = Prim.Cone(c, new Vector3(0, 2.0f, 0), 0.8f, 1.4f, 4, Hazard, 1.6f);
                hop.transform.localRotation = Quaternion.Euler(0, 45, 0);
                Prim.Sign(c, "CRUSHER", new Vector3(0, 1.2f, 1.24f), 2.2f, 0.55f);
            }

            if (S.smelter > 0)
            {
                var s = new GameObject("Smelter").transform;
                s.SetParent(facRoot, false);
                s.localPosition = new Vector3(13, 0, zF);
                Prim.Box(s, new Vector3(0, 1.2f, 0), new Vector3(2.6f, 2.4f, 2.6f), Data.Hex(0x5a4a44));
                Prim.Box(s, new Vector3(0.7f, 3.6f, -0.6f), new Vector3(0.7f, 3, 0.7f), Data.Hex(0x3d3330));
                Prim.UnlitBox(s, new Vector3(0, 0.9f, 1.31f), new Vector3(1.2f, 0.8f, 0.02f), Data.Hex(0xff8a2a));
                Prim.Sign(s, "SMELTER", new Vector3(0, 2.05f, 1.34f), 2.2f, 0.55f);
            }
        }

        void BuildTrees()
        {
            if (treeRoot != null) Destroy(treeRoot.gameObject);
            treeRoot = new GameObject("Trees").transform;
            treeRoot.SetParent(transform, false);
            float R0 = Mathf.Max(world.W, world.D) / 2f + 16;
            int seed = world.M.seed;
            for (int i = 0; i < 140; i++)
            {
                float a = Noise.Hash2(i, 1, seed) * Mathf.PI * 2, r = R0 + Noise.Hash2(i, 2, seed) * 110;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                if (z < -world.D / 2f && z > -world.D / 2f - 20 && Mathf.Abs(x) < 22) continue;
                var t = new GameObject("Tree").transform;
                t.SetParent(treeRoot, false);
                t.localPosition = new Vector3(x, 0, z);
                t.localScale = Vector3.one * (0.8f + Noise.Hash2(i, 3, seed) * 0.9f);
                Prim.Cyl(t, new Vector3(0, 0.6f, 0), 0.22f, 1.2f, Data.Hex(0x6b4a2f));
                Prim.Cone(t, new Vector3(0, 1.1f, 0), 1.2f, 3f, 6, Data.Hex(i % 2 == 1 ? 0x3f8a46u : 0x2f7340u));
            }
        }

        DrillRig MakeRig(int i)
        {
            var root = new GameObject("Drill Rig " + (i + 1));
            root.transform.SetParent(transform, false);
            var t = root.transform;
            Prim.Box(t, new Vector3(0, 0.75f, 0), new Vector3(2.2f, 0.7f, 2.2f), Hazard);
            Prim.Box(t, new Vector3(-0.9f, 0.22f, 0), new Vector3(0.5f, 0.45f, 2.4f), Charcoal);
            Prim.Box(t, new Vector3(0.9f, 0.22f, 0), new Vector3(0.5f, 0.45f, 2.4f), Charcoal);
            Prim.Box(t, new Vector3(0.45f, 1.5f, 0.45f), new Vector3(0.9f, 0.8f, 0.9f), Charcoal);
            Prim.Box(t, new Vector3(-0.5f, 2.8f, -0.5f), new Vector3(0.35f, 3.4f, 0.35f), Hazard);
            var arm = Prim.Box(transform, Vector3.zero, Vector3.one, Data.Hex(0x5b6066)).transform;
            var bit = Prim.Cone(transform, Vector3.zero, 0.32f, 0.9f, 6, Data.Hex(0xa4aab1)).transform;
            float a = i * 2.39996f + 0.4f, R = Mathf.Max(world.W, world.D) / 2f + 4;
            t.position = new Vector3(Mathf.Cos(a) * R, 0, Mathf.Sin(a) * R);
            return new DrillRig { root = root, arm = arm, bit = bit };
        }
    }
}
