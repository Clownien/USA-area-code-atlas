using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace MountainRemover
{
    /// <summary>
    /// Builds the whole game at runtime: press Play in any scene and this sets up the camera, light,
    /// mountain, machines and UI. Split across GameManager.cs, Scenery.cs and GameUI.cs.
    /// </summary>
    public partial class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public GameState S = new GameState();
        public VoxelWorld world;
        public Player player;
        public Fx fx;
        public Camera cam;

        // UI / flow state
        bool started, menuOpen = true, shopOpen, clearedOpen, winOpen, confirmNew, hasSave;
        int tool;
        float nextSwing, tGame, saveT, flagT, lastFullToast = -10;

        // targeting
        bool hasTarget; Vector3Int target; byte targetMat;
        int dmgKey = -1; float dmg;

        class Fuse { public GameObject go; public Vector3Int cell; public float t; }
        readonly List<Fuse> fuses = new List<Fuse>();

        public int summitH;

        string SavePath => Path.Combine(Application.persistentDataPath, "mountain-remover-save.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            new GameObject("Mountain Remover").AddComponent<GameManager>();
        }

        void Awake() { I = this; }

        void Start()
        {
            Application.targetFrameRate = 120;
            SetupScene();
            world = new VoxelWorld(transform);
            fx = new Fx(transform, cam);
            player = new Player(this, cam);
            BuildBackdrop();
            BuildHighlight();
            BuildFlag();

            var save = ReadSave();
            if (save != null && save.S != null)
            {
                S = save.S;
                if (S.bag == null) S.bag = new Inv();
                if (S.silo == null) S.silo = new Inv();
                if (S.st == null) S.st = new Stats();
                S.mtn = Mathf.Clamp(S.mtn, 0, Data.Mountains.Length - 1);
                LoadMountain(save.mask);
                player.pos = new Vector3(save.px, save.py, save.pz);
                player.yaw = save.yaw;
                if (player.Collides(player.pos)) player.Spawn();
                hasSave = true;
            }
            else LoadMountain(null);
        }

        void SetupScene()
        {
            // Replace whatever camera/lights the open scene has with our own.
            foreach (var c in Camera.allCameras) Destroy(c.gameObject);
            foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                if (l.gameObject.scene.IsValid()) Destroy(l.gameObject);

            var sky = Data.Hex(0xa9d3ea);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1600f;
            cam.fieldOfView = 72f;
            camGo.AddComponent<AudioListener>();

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Data.Hex(0xfff0d4);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sunGo.transform.rotation = Quaternion.Euler(48, -35, 0);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Data.Hex(0xc7dcec);
            RenderSettings.ambientEquatorColor = Data.Hex(0x9aa596);
            RenderSettings.ambientGroundColor = Data.Hex(0x5f6b3c);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 140;
            RenderSettings.fogEndDistance = 900;
        }

        // ------------------------------------------------------------------ economy helpers
        public int BagCap => Data.Packs[S.pack].cap;
        public int SiloCap => Data.SiloCap(S.siloLv);
        public float CrMult => Data.CrusherMult(S.crusher);
        public float SmMult => Data.SmelterMult(S.smelter);
        public double SaleValue(Inv inv) => inv.rock * CrMult + inv.ore * CrMult * SmMult;
        public float RawValue(byte m) => Data.Mats[m].value * world.M.mult;
        public float HardOf(byte m) => Mathf.Ceil(Data.Mats[m].hard * world.M.hard);

        public bool AddTo(Inv inv, byte m, int cap)
        {
            if (inv.n >= cap) return false;
            double v = RawValue(m);
            inv.n += 1;
            if (Data.Mats[m].ore) inv.ore += v; else inv.rock += v;
            return true;
        }

        void Earn(double v) { S.money += v; S.st.earned += v; }

        bool Paused => !started || menuOpen || shopOpen || clearedOpen || winOpen;

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            tGame += dt;
            HandleKeys();

            if (!started)
            {
                float a = tGame * 0.08f, R = Mathf.Max(world.W, world.D) * 0.9f + 12;
                cam.transform.position = new Vector3(Mathf.Sin(a) * R, world.M.peak * 0.7f + 8, Mathf.Cos(a) * R);
                cam.transform.LookAt(new Vector3(0, world.M.peak * 0.35f, 0));
            }
            else if (!Paused)
            {
                S.st.time += dt;
                player.Look();
                player.Tick(dt);
                player.ApplyCamera(tGame);
                UpdateTarget();
                if (In.MouseHeld && tGame >= nextSwing && Cursor.lockState == CursorLockMode.Locked)
                {
                    if (tool == 0) Swing(); else ThrowDynamite();
                }
                UpdateRigs(dt);
                UpdateBelt(dt);
                UpdateFuses(dt);
                CheckCleared();
                saveT += dt;
                if (saveT > 15) { saveT = 0; Save(); }
            }

            UpdateAmbient(dt);
            fx.Tick(dt);
            player.UpdateViewModel(dt, tGame, tool, started);
            flagT += dt;
            if (flagT > 0.5f) { flagT = 0; PlaceFlag(); }
            world.Flush(Paused ? 2 : 8);
            UpdateCursor();
        }

        void HandleKeys()
        {
            if (!started) return;
            if (In.Down(K.Escape))
            {
                if (shopOpen) CloseShop();
                else if (!Paused) menuOpen = true;
            }
            if (In.Down(K.B) && !menuOpen && !clearedOpen && !winOpen) { if (shopOpen) CloseShop(); else OpenShop(); }
            if (Paused) return;
            if (In.Down(K.E)) Use();
            if (In.Down(K.One)) tool = 0;
            if (In.Down(K.Two)) tool = 1;
            if (In.Down(K.Q) || Mathf.Abs(In.Scroll) > 0.01f) tool = 1 - tool;
            if (In.Down(K.M)) { fx.muted = !fx.muted; Toast(fx.muted ? "Sound off" : "Sound on"); }
        }

        void UpdateCursor()
        {
            bool wantLock = started && !Paused;
            var mode = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            Cursor.visible = !wantLock;
        }

        // ------------------------------------------------------------------ mining
        void UpdateTarget()
        {
            hasTarget = world.Raycast(cam.transform.position, cam.transform.forward, 5f, out target, out targetMat);
            bool show = hasTarget && tool == 0;
            highlight.gameObject.SetActive(show);
            if (show)
            {
                highlight.position = world.CellCenter(target.x, target.y, target.z);
                float d = world.Idx(target.x, target.y, target.z) == dmgKey ? dmg / HardOf(targetMat) : 0;
                var m = Mats.Unlit(Color.Lerp(Color.white, Data.Hex(0xd0602f), Mathf.Round(d * 4) / 4));
                foreach (var r in highlightR) if (r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }

        void Swing()
        {
            var pk = Data.Picks[S.pick];
            nextSwing = tGame + pk.cooldown;
            player.swingT = 1;
            if (!hasTarget) return;
            if (S.bag.n >= BagCap)
            {
                if (tGame - lastFullToast > 2.5f) { Toast("Backpack full. Sell it at the trading post.", -1); lastFullToast = tGame; fx.Play("no"); }
                return;
            }
            int key = world.Idx(target.x, target.y, target.z);
            if (key != dmgKey) { dmgKey = key; dmg = 0; }
            dmg += pk.damage;
            fx.Play("hit");
            var center = world.CellCenter(target.x, target.y, target.z);
            fx.Burst(center, targetMat, 2);
            if (dmg >= HardOf(targetMat))
            {
                byte m = world.Remove(target.x, target.y, target.z);
                AddTo(S.bag, m, BagCap);
                S.st.hand++;
                fx.Burst(center, m, 7);
                fx.Play("break");
                if (Data.Mats[m].ore && Data.Mats[m].value >= 12) Toast(Data.Mats[m].name + " · " + Money(RawValue(m)), 1);
                dmgKey = -1; dmg = 0;
                world.Flush(3);
            }
        }

        void ThrowDynamite()
        {
            nextSwing = tGame + 0.7f;
            player.swingT = 1;
            if (S.dyn <= 0) { Toast("No dynamite left. Buy more in the shop (B).", -1); fx.Play("no"); return; }
            if (!world.Raycast(cam.transform.position, cam.transform.forward, 16f, out var cell, out _)) { Toast("Aim at rock within 16 m.", -1); return; }
            S.dyn--;
            var c = world.CellCenter(cell.x, cell.y, cell.z) + Vector3.up * 0.6f;
            var stick = Prim.Cyl(transform, c, 0.1f, 0.5f, Data.Hex(0xc8352b));
            fuses.Add(new Fuse { go = stick, cell = cell, t = 1f });
        }

        void UpdateFuses(float dt)
        {
            for (int i = fuses.Count - 1; i >= 0; i--)
            {
                var f = fuses[i];
                f.t -= dt;
                f.go.transform.Rotate(0, dt * 460, 0);
                f.go.transform.localScale = new Vector3(0.2f, 0.25f, 0.2f) * (1 + Mathf.Sin(tGame * 30) * 0.08f);
                if (f.t <= 0) { fuses.RemoveAt(i); Explode(f); }
            }
        }

        void Explode(Fuse f)
        {
            Destroy(f.go);
            float r = Data.BlastRadius(S.blastLv);
            int R = Mathf.CeilToInt(r), cap = SiloCap, got = 0, lost = 0;
            var c = f.cell;
            for (int y = c.y - R; y <= c.y + R; y++)
                for (int z = c.z - R; z <= c.z + R; z++)
                    for (int x = c.x - R; x <= c.x + R; x++)
                    {
                        if (y < 0 || x < 0 || z < 0 || x >= world.W || z >= world.D || y >= world.H) continue;
                        if (Vector3.Distance(new Vector3(x, y, z), c) > r) continue;
                        byte m = world.Remove(x, y, z);
                        if (m == 0) continue;
                        if (AddTo(S.silo, m, cap)) got++; else lost++;
                        S.st.blast++;
                        if (Random.value < 0.25f) fx.Burst(world.CellCenter(x, y, z), m, 2);
                    }
            var at = world.CellCenter(c.x, c.y, c.z);
            fx.Explosion(at, r);
            float dist = Vector3.Distance(cam.transform.position, at);
            player.shake = Mathf.Max(player.shake, Mathf.Max(0, 0.6f - dist / 60f));
            fx.Play("boom");
            Toast(lost > 0 ? $"Blast: {got} blocks to the silo, {lost} lost (silo full)" : $"Blast: {got} blocks sent to the silo", lost > 0 ? -1 : 1);
        }

        // ------------------------------------------------------------------ selling
        bool NearPost => Flat(player.pos, postPos + new Vector3(0, 0, 2.2f)) < 4.2f;
        bool NearSilo => Flat(player.pos, siloPos) < 4f;
        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        void Use()
        {
            if (NearPost)
            {
                if (S.bag.n < 1) { Toast("Your backpack is empty."); return; }
                double v = SaleValue(S.bag);
                Earn(v);
                Toast($"Sold {S.bag.n:0} blocks for {Money(v)}", 1);
                S.bag.Clear(); fx.Play("cash"); Save();
            }
            else if (NearSilo)
            {
                if (S.silo.n < 1) { Toast("The silo is empty."); return; }
                double v = SaleValue(S.silo);
                Earn(v);
                Toast($"Shipped {S.silo.n:0} blocks from the silo for {Money(v)}", 1);
                S.silo.Clear(); fx.Play("cash"); Save();
            }
        }

        void UpdateBelt(float dt)
        {
            if (S.belt == 0 || S.silo.n <= 0) return;
            double f = System.Math.Min(1.0, Data.BeltRate(S.belt) * dt / S.silo.n);
            double rock = S.silo.rock * f, ore = S.silo.ore * f;
            Earn(rock * CrMult + ore * CrMult * SmMult);
            S.silo.rock -= rock; S.silo.ore -= ore; S.silo.n -= S.silo.n * f;
            if (S.silo.n < 1e-6) S.silo.Clear();
        }

        // ------------------------------------------------------------------ drill rigs
        public class DrillRig
        {
            public GameObject root;
            public Transform arm, bit;
            public int target = -1;
            public float acc, spin;
            public bool stalled;
        }
        readonly List<DrillRig> rigs = new List<DrillRig>();
        const float RigReach = 4.5f;

        void SyncRigs(bool reset)
        {
            if (reset)
            {
                foreach (var r in rigs) { Destroy(r.root); Destroy(r.arm.gameObject); Destroy(r.bit.gameObject); }
                rigs.Clear();
            }
            while (rigs.Count < S.rigs) rigs.Add(MakeRig(rigs.Count));
        }

        void UpdateRigs(float dt)
        {
            float rate = Data.RigRate(S.rigLv);
            int cap = SiloCap;
            foreach (var r in rigs)
            {
                var p = r.root.transform.position;
                if (r.target < 0 || world.colTop[r.target] == 0) r.target = world.NearestColumn(p.x, p.z);
                if (r.target < 0) { r.arm.gameObject.SetActive(false); r.bit.gameObject.SetActive(false); continue; }
                float tx = r.target % world.W + 0.5f + world.ox, tz = r.target / world.W + 0.5f + world.oz;
                Vector3 to = new Vector3(tx - p.x, 0, tz - p.z);
                float dist = to.magnitude;
                if (dist > RigReach)
                {
                    float step = Mathf.Min(dist - RigReach, 4 * dt);
                    r.root.transform.position = p + to / dist * step;
                    r.root.transform.rotation = Quaternion.LookRotation(to);
                }
                else
                {
                    r.stalled = S.silo.n >= cap;
                    if (!r.stalled)
                    {
                        r.acc += rate * dt;
                        int guard = 0;
                        while (r.acc >= 1 && guard++ < 400)
                        {
                            if (S.silo.n >= cap) { r.stalled = true; break; }
                            if (r.target < 0 || world.colTop[r.target] == 0)
                            {
                                r.target = world.NearestColumn(p.x, p.z);
                                if (r.target < 0) break;
                                float nx = r.target % world.W + 0.5f + world.ox, nz = r.target / world.W + 0.5f + world.oz;
                                if (Flat(new Vector3(nx, 0, nz), p) > RigReach + 1.5f) break;
                            }
                            int x = r.target % world.W, z = r.target / world.W, y = world.colTop[r.target] - 1;
                            byte m = world.Remove(x, y, z);
                            r.acc -= 1;
                            if (m != 0)
                            {
                                AddTo(S.silo, m, cap);
                                S.st.rig++;
                                if (Random.value < 0.35f) fx.Burst(world.CellCenter(x, y, z), m, 2);
                            }
                        }
                        if (r.acc > 2) r.acc = 2;
                    }
                }

                // Boom arm from the tower top to the block being drilled.
                if (r.target < 0) { r.arm.gameObject.SetActive(false); r.bit.gameObject.SetActive(false); continue; }
                int top = world.colTop[r.target];
                Vector3 end = new Vector3(r.target % world.W + 0.5f + world.ox, top + 0.9f, r.target / world.W + 0.5f + world.oz);
                Vector3 start = r.root.transform.TransformPoint(new Vector3(-0.5f, 4.4f, -0.5f));
                r.arm.gameObject.SetActive(true); r.bit.gameObject.SetActive(true);
                r.arm.position = (start + end) / 2;
                var dir = end - start;
                if (dir.sqrMagnitude > 0.0001f) r.arm.rotation = Quaternion.LookRotation(dir);
                r.arm.localScale = new Vector3(0.2f, 0.2f, Mathf.Max(0.01f, dir.magnitude));
                if (!r.stalled && dist <= RigReach) r.spin += dt * 1000;
                r.bit.position = end;
                r.bit.rotation = Quaternion.Euler(0, r.spin, 0) * Quaternion.Euler(180, 0, 0);
            }
        }

        // ------------------------------------------------------------------ progression
        void CheckCleared()
        {
            if (S.cleared || world.remaining > world.total * 0.02f) return;
            S.cleared = true;
            double v = 0;
            for (int i = 0; i < world.vox.Length; i++)
            {
                byte m = world.vox[i];
                if (m != 0) v += RawValue(m);
            }
            world.ClearAll();
            v *= CrMult;
            Earn(v);
            Save();
            if (S.mtn == Data.Mountains.Length - 1) { S.won = true; winOpen = true; return; }
            clearedBonus = v;
            clearedOpen = true;
        }

        double clearedBonus;

        void Travel()
        {
            if (S.mtn >= Data.Mountains.Length - 1) return;
            if (S.silo.n > 0) { Earn(SaleValue(S.silo)); S.silo.Clear(); }
            S.mtn++;
            S.cleared = false;
            LoadMountain(null);
            Banner($"STAGE {S.mtn + 1} OF {Data.Mountains.Length}", world.M.name);
            Save();
        }

        void LoadMountain(int[] mask)
        {
            world.Generate(S.mtn);
            if (mask != null && mask.Length > 0) { world.ApplyMask(mask); world.RecountColumns(); }
            world.BuildAll();
            BuildFacilities();
            BuildTrees();
            SyncRigs(true);
            player.Spawn();
            PlaceFlag();
            hasTarget = false; dmgKey = -1;
        }

        void NewGame()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
            S = new GameState();
            LoadMountain(null);
            hasSave = false;
            confirmNew = false;
        }

        // ------------------------------------------------------------------ saving
        public void Save()
        {
            if (world == null || world.vox == null) return;
            try
            {
                var d = new SaveData { S = S, mask = world.MaskRLE(), px = player.pos.x, py = player.pos.y, pz = player.pos.z, yaw = player.yaw };
                File.WriteAllText(SavePath, JsonUtility.ToJson(d));
            }
            catch (System.Exception e) { Debug.LogWarning("Mountain Remover: could not save. " + e.Message); }
        }

        SaveData ReadSave()
        {
            try { return File.Exists(SavePath) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) : null; }
            catch { return null; }
        }

        void OnApplicationQuit() { if (started) Save(); }
        void OnApplicationPause(bool p) { if (p && started) Save(); }
    }
}
