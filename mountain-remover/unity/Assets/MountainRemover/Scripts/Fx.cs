using System.Collections.Generic;
using UnityEngine;

namespace MountainRemover
{
    /// <summary>Rock chips, explosions and procedurally generated sound effects (no audio files needed).</summary>
    public class Fx
    {
        class Part { public Transform t; public Renderer r; public Vector3 v; public float life; }
        class Boom { public Transform t; public float time, radius; }

        readonly List<Part> parts = new List<Part>();
        readonly List<Boom> booms = new List<Boom>();
        readonly Transform root;
        int next;

        readonly AudioSource src;
        AudioClip hit, brk, boom, cash, buy, no;
        public bool muted;

        public Fx(Transform parent, Camera cam)
        {
            root = new GameObject("FX").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < 160; i++)
            {
                var g = Prim.Box(root, Vector3.zero, Vector3.one * 0.2f, Data.Mats[3].color);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.SetActive(false);
                parts.Add(new Part { t = g.transform, r = g.GetComponent<Renderer>() });
            }
            src = cam.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0;
            BuildClips();
        }

        public void Burst(Vector3 at, byte mat, int n)
        {
            var m = Mats.Get(Data.Mats[mat].color);
            for (int i = 0; i < n; i++)
            {
                var p = parts[next++ % parts.Count];
                p.r.sharedMaterial = m;
                p.t.gameObject.SetActive(true);
                p.t.position = at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));
                p.t.localScale = Vector3.one * 0.2f * Random.Range(0.6f, 1.8f);
                p.v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 6.5f), Random.Range(-2.5f, 2.5f));
                p.life = Random.Range(0.6f, 1.1f);
            }
        }

        public void Explosion(Vector3 at, float radius)
        {
            var s = Prim.Sphere(root, at, 1, Data.Hex(0xffb347));
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            booms.Add(new Boom { t = s.transform, time = 0, radius = radius });
        }

        public void Tick(float dt)
        {
            foreach (var p in parts)
            {
                if (!p.t.gameObject.activeSelf) continue;
                p.life -= dt;
                if (p.life <= 0) { p.t.gameObject.SetActive(false); continue; }
                p.v.y -= 20 * dt;
                var pos = p.t.position + p.v * dt;
                if (pos.y < 0.1f) { pos.y = 0.1f; p.v.y *= -0.3f; p.v.x *= 0.6f; p.v.z *= 0.6f; }
                p.t.position = pos;
                p.t.Rotate(dt * 340, 0, dt * 290);
            }
            for (int i = booms.Count - 1; i >= 0; i--)
            {
                var b = booms[i];
                b.time += dt;
                float k = b.time / 0.45f;
                float scale = k < 0.6f ? Mathf.Lerp(0.2f, 3f * b.radius, k / 0.6f) : Mathf.Lerp(3f * b.radius, 0f, (k - 0.6f) / 0.4f);
                b.t.localScale = Vector3.one * Mathf.Max(0.01f, scale);
                if (k >= 1) { Object.Destroy(b.t.gameObject); booms.RemoveAt(i); }
            }
        }

        // ------------------------------------------------------------------ audio
        public void Play(string name)
        {
            if (muted) return;
            AudioClip c = null;
            switch (name)
            {
                case "hit": c = hit; src.pitch = Random.Range(0.9f, 1.15f); break;
                case "break": c = brk; src.pitch = Random.Range(0.9f, 1.1f); break;
                case "boom": c = boom; src.pitch = 1; break;
                case "cash": c = cash; src.pitch = 1; break;
                case "buy": c = buy; src.pitch = 1; break;
                case "no": c = no; src.pitch = 1; break;
            }
            if (c != null) src.PlayOneShot(c, name == "hit" ? 0.5f : 0.8f);
        }

        void BuildClips()
        {
            hit = Synth("hit", 0.08f, 2600, 700, 0.9f, 900, 700, 0.15f, 45);
            brk = Synth("break", 0.25f, 900, 0, 1f, 140, 60, 0.4f, 14);
            boom = Synth("boom", 1.2f, 260, 0, 1.4f, 70, 28, 0.8f, 3.2f);
            no = Synth("no", 0.16f, 0, 0, 0, 220, 150, 0.35f, 12, square: true);
            buy = Synth("buy", 0.2f, 0, 0, 0, 440, 990, 0.4f, 9);
            // two-note "cha-ching"
            int sr = 44100, n = (int)(0.28f * sr);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float a = t < 0.09f ? Mathf.Sin(2 * Mathf.PI * 880 * t) * Mathf.Exp(-t * 25) : 0;
                float t2 = t - 0.08f;
                float b = t2 > 0 ? Mathf.Sin(2 * Mathf.PI * 1320 * t2) * Mathf.Exp(-t2 * 14) : 0;
                d[i] = (a + b) * 0.35f;
            }
            cash = AudioClip.Create("cash", n, 1, sr, false);
            cash.SetData(d, 0);
        }

        /// <summary>Filtered noise plus a pitch-swept tone with an exponential decay.</summary>
        static AudioClip Synth(string name, float dur, float lowpass, float highpass, float noiseGain, float f0, float f1, float toneGain, float decay, bool square = false)
        {
            int sr = 44100, n = (int)(dur * sr);
            var d = new float[n];
            var rng = new System.Random(name.Length * 977);
            float lp = 0, hp = 0, phase = 0;
            float aL = lowpass > 0 ? 1 - Mathf.Exp(-2 * Mathf.PI * lowpass / sr) : 0;
            float aH = highpass > 0 ? 1 - Mathf.Exp(-2 * Mathf.PI * highpass / sr) : 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += aL * (noise - lp);
                hp += aH * (lp - hp);
                float filtered = highpass > 0 ? lp - hp : lp;
                float f = f0 * Mathf.Pow(f1 / f0, t / dur);
                phase += 2 * Mathf.PI * f / sr;
                float tone = square ? Mathf.Sign(Mathf.Sin(phase)) * 0.5f : Mathf.Sin(phase);
                d[i] = Mathf.Clamp((filtered * noiseGain * 2 + tone * toneGain) * env, -1, 1);
            }
            var clip = AudioClip.Create(name, n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
