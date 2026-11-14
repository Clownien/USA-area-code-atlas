using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MountainRemover
{
    /// <summary>
    /// The mountain: a grid of blocks split into 16x16x16 chunks. Only faces that touch air are meshed,
    /// and only chunks touched by a change are rebuilt, so the mountain can be reshaped while you play.
    /// </summary>
    public class VoxelWorld
    {
        public MountainDef M;
        public int W, H, D, ox, oz;
        public byte[] vox;
        public short[] colTop;      // height of the highest block in each column (0 = empty)
        public int total, remaining;
        public float metresPerBlock;

        public const int CS = 16;
        const int SHADES = 3;
        int CX, CY, CZ;
        readonly Transform root;
        MeshFilter[] chunks = new MeshFilter[0];
        readonly HashSet<int> dirty = new HashSet<int>();
        readonly List<int> dirtyTmp = new List<int>();
        Material[] mats;

        public VoxelWorld(Transform parent)
        {
            root = new GameObject("Mountain").transform;
            root.SetParent(parent, false);
            mats = new Material[Data.Mats.Length * SHADES];
            for (int m = 0; m < Data.Mats.Length; m++)
                for (int s = 0; s < SHADES; s++)
                    { var c = Data.Mats[m].color; float k = 0.86f + 0.11f * s; mats[m * SHADES + s] = Mats.Get(new Color(c.r * k, c.g * k, c.b * k, 1f)); }
        }

        public int Idx(int x, int y, int z) => (y * D + z) * W + x;

        public byte Get(int x, int y, int z)
        {
            if (y < 0) return 255; // bedrock / ground
            if (x < 0 || z < 0 || x >= W || z >= D || y >= H) return 0;
            return vox[(y * D + z) * W + x];
        }

        public Vector3 CellCenter(int x, int y, int z) => new Vector3(x + ox + 0.5f, y + 0.5f, z + oz + 0.5f);

        // ---------------------------------------------------------------- generation
        int HeightAt(int x, int z)
        {
            float nx = (x + 0.5f) / M.W * 2 - 1, nz = (z + 0.5f) / M.D * 2 - 1;
            float n = Noise.Fbm(x * 0.11f, z * 0.11f, M.seed);
            float r = Mathf.Sqrt(nx * nx + nz * nz);
            float h = 0;
            switch (M.shape)
            {
                case "hill":
                    h = M.peak * Mathf.Pow(Mathf.Max(0, 1 - r), 1.3f) * (0.8f + 0.45f * n);
                    break;
                case "rocky":
                {
                    float rr = Mathf.Sqrt(nx * 1.08f * nx * 1.08f + nz * 0.94f * nz * 0.94f);
                    float ridge = 1 - Mathf.Abs(Noise.Value(x * 0.09f, z * 0.09f, M.seed + 3) * 2 - 1);
                    h = M.peak * Mathf.Pow(Mathf.Max(0, 1 - rr), 1.1f) * (0.6f + 0.45f * n + 0.2f * ridge);
                    break;
                }
                case "horn":
                {
                    float px = nx - 0.1f, pz = nz + 0.08f;
                    float a = (px + pz) * 0.7071f, b = (px - pz) * 0.7071f;
                    float d = Mathf.Max(Mathf.Abs(a) * 1.18f, Mathf.Abs(b));
                    h = M.peak * Mathf.Pow(Mathf.Max(0, 1 - d * 1.08f), 1.7f) * (0.88f + 0.24f * n)
                      + M.peak * 0.22f * Mathf.Max(0, 1 - r * 1.05f) * (0.6f + 0.8f * n);
                    break;
                }
                default: // everest: a massif with several sub-peaks
                {
                    float[,] P = { { 0, 0, 1 }, { 0.36f, 0.2f, 0.74f }, { -0.32f, 0.34f, 0.62f }, { 0.12f, -0.42f, 0.56f }, { -0.4f, -0.25f, 0.48f } };
                    for (int i = 0; i < 5; i++)
                    {
                        float dx = nx - P[i, 0], dz = nz - P[i, 1];
                        h = Mathf.Max(h, P[i, 2] * M.peak * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Sqrt(dx * dx + dz * dz) / 0.72f), 1.25f));
                    }
                    h = h * (0.8f + 0.32f * n) + M.peak * 0.18f * Mathf.Max(0, 1 - r) * (0.5f + n);
                    break;
                }
            }
            float edge = Mathf.Min(1, (1 - Mathf.Max(Mathf.Abs(nx), Mathf.Abs(nz))) * 7);
            return Mathf.Clamp(Mathf.FloorToInt(h * Mathf.Max(0, edge)), 0, M.H);
        }

        byte MatAt(int x, int y, int z, int h)
        {
            int depth = h - 1 - y;
            bool high = y > M.peak * 0.6f;
            if (depth == 0) return y >= M.snow ? Data.SNOW : (high ? Data.STONE : Data.GRASS);
            if (depth < 3) return y >= M.snow ? Data.SNOW : (high ? Data.STONE : Data.DIRT);
            byte mat = Data.STONE;
            if (M.granite > 0 && depth > 3 && Noise.Hash3(x >> 2, y >> 2, z >> 2, M.seed + 5) < M.granite) mat = Data.GRANITE;
            if (Noise.Hash3(x, y, z, M.seed + 2) < 0.8f)
            {
                float r = Noise.Hash3(x >> 1, y >> 1, z >> 1, M.seed + 9), deep = 1 - (float)y / M.peak, o = M.ore;
                float p7 = 0.003f * o * (0.3f + deep), p6 = p7 + 0.012f * o * (0.4f + deep), p5 = p6 + 0.035f * o, p4 = p5 + 0.05f;
                if (r < p7) mat = Data.GEM; else if (r < p6) mat = Data.GOLD; else if (r < p5) mat = Data.IRON; else if (r < p4) mat = Data.COAL;
            }
            return mat;
        }

        public void Generate(int mi)
        {
            M = Data.Mountains[mi];
            W = M.W; D = M.D; H = M.H; ox = -W / 2; oz = -D / 2;
            vox = new byte[W * H * D];
            colTop = new short[W * D];
            total = 0;
            for (int z = 0; z < D; z++)
                for (int x = 0; x < W; x++)
                {
                    int h = HeightAt(x, z);
                    colTop[z * W + x] = (short)h;
                    total += h;
                    for (int y = 0; y < h; y++) vox[Idx(x, y, z)] = MatAt(x, y, z, h);
                }
            remaining = total;
            int mx = 1;
            for (int i = 0; i < colTop.Length; i++) if (colTop[i] > mx) mx = colTop[i];
            metresPerBlock = M.metres / mx;
            CX = (W + CS - 1) / CS; CY = (H + CS - 1) / CS; CZ = (D + CS - 1) / CS;
        }

        public void RecountColumns()
        {
            remaining = 0;
            for (int z = 0; z < D; z++)
                for (int x = 0; x < W; x++)
                {
                    int t = 0;
                    for (int y = H - 1; y >= 0; y--)
                    {
                        if (vox[Idx(x, y, z)] != 0) { if (t == 0) t = y + 1; remaining++; }
                    }
                    colTop[z * W + x] = (short)t;
                }
        }

        public int Highest(out int column)
        {
            int best = -1, bh = 0;
            for (int i = 0; i < colTop.Length; i++) if (colTop[i] > bh) { bh = colTop[i]; best = i; }
            column = best;
            return bh;
        }

        // ---------------------------------------------------------------- meshing
        static readonly Vector3Int[] FN =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };
        // Corner order chosen so Cross(v1-v0, v2-v0) points out of the block (Unity's front-face rule).
        static readonly Vector3[][] FV =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) },
            new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) },
            new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
            new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) },
            new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) },
            new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) },
        };

        static readonly List<Vector3> V = new List<Vector3>();
        static readonly List<Vector3> N = new List<Vector3>();
        static List<int>[] T;
        static readonly List<Material> usedMats = new List<Material>();

        void BuildChunk(int cx, int cy, int cz)
        {
            if (T == null || T.Length != mats.Length) { T = new List<int>[mats.Length]; for (int i = 0; i < T.Length; i++) T[i] = new List<int>(); }
            int key = (cy * CZ + cz) * CX + cx;
            V.Clear(); N.Clear();
            for (int i = 0; i < T.Length; i++) T[i].Clear();

            int x0 = cx * CS, y0 = cy * CS, z0 = cz * CS;
            int x1 = Mathf.Min(x0 + CS, W), y1 = Mathf.Min(y0 + CS, H), z1 = Mathf.Min(z0 + CS, D);
            for (int y = y0; y < y1; y++)
                for (int z = z0; z < z1; z++)
                    for (int x = x0; x < x1; x++)
                    {
                        byte m = vox[(y * D + z) * W + x];
                        if (m == 0) continue;
                        int shade = Mathf.Min(SHADES - 1, (int)(Noise.Hash3(x, y, z, 3) * SHADES));
                        var tl = T[m * SHADES + shade];
                        var p = new Vector3(x, y, z);
                        for (int f = 0; f < 6; f++)
                        {
                            var n = FN[f];
                            if (Get(x + n.x, y + n.y, z + n.z) != 0) continue;
                            int b = V.Count;
                            var fv = FV[f];
                            V.Add(p + fv[0]); V.Add(p + fv[1]); V.Add(p + fv[2]); V.Add(p + fv[3]);
                            Vector3 nf = n;
                            N.Add(nf); N.Add(nf); N.Add(nf); N.Add(nf);
                            tl.Add(b); tl.Add(b + 1); tl.Add(b + 2);
                            tl.Add(b); tl.Add(b + 2); tl.Add(b + 3);
                        }
                    }

            var mf = chunks[key];
            if (V.Count == 0)
            {
                if (mf != null) mf.gameObject.SetActive(false);
                return;
            }
            if (mf == null)
            {
                var go = new GameObject("Chunk " + cx + "," + cy + "," + cz);
                go.transform.SetParent(root, false);
                mf = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                mf.sharedMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                chunks[key] = mf;
            }
            mf.gameObject.SetActive(true);
            var mesh = mf.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            usedMats.Clear();
            int used = 0;
            for (int i = 0; i < T.Length; i++) if (T[i].Count > 0) used++;
            mesh.subMeshCount = used;
            int sm = 0;
            for (int i = 0; i < T.Length; i++)
            {
                if (T[i].Count == 0) continue;
                mesh.SetTriangles(T[i], sm++, false);
                usedMats.Add(mats[i]);
            }
            mesh.RecalculateBounds();
            mf.GetComponent<MeshRenderer>().sharedMaterials = usedMats.ToArray();
        }

        public void BuildAll()
        {
            foreach (var c in chunks) if (c != null) { Object.Destroy(c.sharedMesh); Object.Destroy(c.gameObject); }
            chunks = new MeshFilter[CX * CY * CZ];
            dirty.Clear();
            root.localPosition = new Vector3(ox, 0, oz);
            for (int cy = 0; cy < CY; cy++)
                for (int cz = 0; cz < CZ; cz++)
                    for (int cx = 0; cx < CX; cx++)
                        BuildChunk(cx, cy, cz);
        }

        void MarkDirty(int x, int y, int z)
        {
            int cx = x / CS, cy = y / CS, cz = z / CS;
            Add(cx, cy, cz);
            if (x % CS == 0) Add(cx - 1, cy, cz); if (x % CS == CS - 1) Add(cx + 1, cy, cz);
            if (y % CS == 0) Add(cx, cy - 1, cz); if (y % CS == CS - 1) Add(cx, cy + 1, cz);
            if (z % CS == 0) Add(cx, cy, cz - 1); if (z % CS == CS - 1) Add(cx, cy, cz + 1);
        }

        void Add(int a, int b, int c)
        {
            if (a >= 0 && b >= 0 && c >= 0 && a < CX && b < CY && c < CZ) dirty.Add((b * CZ + c) * CX + a);
        }

        public void Flush(int maxChunks)
        {
            if (dirty.Count == 0) return;
            dirtyTmp.Clear();
            foreach (var k in dirty) { dirtyTmp.Add(k); if (dirtyTmp.Count >= maxChunks) break; }
            foreach (var key in dirtyTmp)
            {
                dirty.Remove(key);
                int cx = key % CX, cz = (key / CX) % CZ, cy = key / (CX * CZ);
                BuildChunk(cx, cy, cz);
            }
        }

        /// <summary>Removes one block and returns its material (0 if it was already air).</summary>
        public byte Remove(int x, int y, int z)
        {
            int i = Idx(x, y, z);
            byte m = vox[i];
            if (m == 0) return 0;
            vox[i] = 0;
            remaining--;
            int ci = z * W + x;
            if (y == colTop[ci] - 1)
            {
                int t = y;
                while (t > 0 && vox[Idx(x, t - 1, z)] == 0) t--;
                colTop[ci] = (short)t;
            }
            MarkDirty(x, y, z);
            return m;
        }

        public void ClearAll()
        {
            System.Array.Clear(vox, 0, vox.Length);
            System.Array.Clear(colTop, 0, colTop.Length);
            remaining = 0;
            BuildAll();
        }

        // ---------------------------------------------------------------- queries
        public bool SolidAt(float wx, float wy, float wz)
        {
            int gy = Mathf.FloorToInt(wy);
            if (gy < 0) return true;
            int gx = Mathf.FloorToInt(wx) - ox, gz = Mathf.FloorToInt(wz) - oz;
            if (gx < 0 || gz < 0 || gx >= W || gz >= D || gy >= H) return false;
            return vox[Idx(gx, gy, gz)] != 0;
        }

        /// <summary>Grid walk (Amanatides-Woo) from the camera to the first block.</summary>
        public bool Raycast(Vector3 o, Vector3 d, float maxD, out Vector3Int cell, out byte mat)
        {
            float px = o.x - ox, py = o.y, pz = o.z - oz;
            int x = Mathf.FloorToInt(px), y = Mathf.FloorToInt(py), z = Mathf.FloorToInt(pz);
            int sx = d.x > 0 ? 1 : (d.x < 0 ? -1 : 0), sy = d.y > 0 ? 1 : (d.y < 0 ? -1 : 0), sz = d.z > 0 ? 1 : (d.z < 0 ? -1 : 0);
            float inf = float.PositiveInfinity;
            float tdx = d.x != 0 ? Mathf.Abs(1f / d.x) : inf, tdy = d.y != 0 ? Mathf.Abs(1f / d.y) : inf, tdz = d.z != 0 ? Mathf.Abs(1f / d.z) : inf;
            float tmx = d.x != 0 ? (d.x > 0 ? (x + 1 - px) : (px - x)) * tdx : inf;
            float tmy = d.y != 0 ? (d.y > 0 ? (y + 1 - py) : (py - y)) * tdy : inf;
            float tmz = d.z != 0 ? (d.z > 0 ? (z + 1 - pz) : (pz - z)) * tdz : inf;
            float t = 0;
            for (int i = 0; i < 256; i++)
            {
                if (y < 0) break;
                byte v = Get(x, y, z);
                if (v != 0 && v != 255) { cell = new Vector3Int(x, y, z); mat = v; return true; }
                if (tmx < tmy && tmx < tmz) { x += sx; t = tmx; tmx += tdx; }
                else if (tmy < tmz) { y += sy; t = tmy; tmy += tdy; }
                else { z += sz; t = tmz; tmz += tdz; }
                if (t > maxD) break;
            }
            cell = default; mat = 0;
            return false;
        }

        public int NearestColumn(float px, float pz)
        {
            int best = -1; float bd = float.MaxValue;
            float gx = px - ox, gz = pz - oz;
            for (int z = 0; z < D; z++)
            {
                float dz = z + 0.5f - gz;
                for (int x = 0; x < W; x++)
                {
                    if (colTop[z * W + x] == 0) continue;
                    float dx = x + 0.5f - gx, dd = dx * dx + dz * dz;
                    if (dd < bd) { bd = dd; best = z * W + x; }
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- save mask (run-length: air, solid, air, ...)
        public int[] MaskRLE()
        {
            var runs = new List<int>();
            bool cur = false; int n = 0;
            for (int i = 0; i < vox.Length; i++)
            {
                bool b = vox[i] != 0;
                if (b == cur) n++;
                else { runs.Add(n); cur = b; n = 1; }
            }
            runs.Add(n);
            return runs.ToArray();
        }

        public void ApplyMask(int[] runs)
        {
            int i = 0; bool solid = false;
            foreach (int n in runs)
            {
                if (!solid) for (int k = 0; k < n && i + k < vox.Length; k++) vox[i + k] = 0;
                i += n; solid = !solid;
            }
        }
    }
}
