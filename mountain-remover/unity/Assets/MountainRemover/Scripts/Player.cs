using UnityEngine;

namespace MountainRemover
{
    /// <summary>First-person walker with its own block collision (no physics engine needed).</summary>
    public class Player
    {
        public Vector3 pos;
        public float velY, yaw, pitch; // degrees; positive pitch looks down
        public bool onGround;
        public float eye = 1.62f;
        public float shake;
        public float swingT;

        const float HalfWidth = 0.3f, Height = 1.75f;
        public float mouseSensitivity = 0.12f;

        readonly GameManager g;
        readonly Transform cam;
        readonly Transform vm, pickRoot, dynRoot;
        readonly Renderer headR, tipR;

        public Player(GameManager gm, Camera camera)
        {
            g = gm;
            cam = camera.transform;

            // First-person view model, parented to the camera.
            vm = new GameObject("ViewModel").transform;
            vm.SetParent(cam, false);
            vm.localPosition = new Vector3(0.28f, -0.28f, 0.48f);
            vm.localScale = Vector3.one * 0.62f;
            pickRoot = new GameObject("Pickaxe").transform; pickRoot.SetParent(vm, false);
            Prim.Box(pickRoot, new Vector3(0, 0.1f, 0), new Vector3(0.06f, 0.62f, 0.06f), Data.Hex(0x7a5233));
            headR = Prim.Box(pickRoot, new Vector3(0, 0.42f, 0), new Vector3(0.46f, 0.08f, 0.08f), Data.Picks[0].color).GetComponent<Renderer>();
            var tip = Prim.Cone(pickRoot, new Vector3(-0.23f, 0.42f, 0), 0.045f, 0.16f, 4, Data.Picks[0].color);
            tip.transform.localRotation = Quaternion.Euler(0, 0, 90);
            tipR = tip.GetComponent<Renderer>();
            dynRoot = new GameObject("Dynamite").transform; dynRoot.SetParent(vm, false);
            Prim.Cyl(dynRoot, Vector3.zero, 0.06f, 0.36f, Data.Hex(0xc8352b));
            Prim.Box(dynRoot, new Vector3(0, 0.24f, 0), new Vector3(0.015f, 0.12f, 0.015f), Data.Hex(0xe9e2cf));
            foreach (var r in vm.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Spawn()
        {
            var w = g.world;
            pos = new Vector3(-2, 0, -(w.D / 2f + 6));
            velY = 0; yaw = 0; pitch = 5; eye = 1.62f;
        }

        public bool Collides(Vector3 p)
        {
            var w = g.world;
            for (int x = Mathf.FloorToInt(p.x - HalfWidth); x <= Mathf.FloorToInt(p.x + HalfWidth); x++)
                for (int y = Mathf.FloorToInt(p.y); y <= Mathf.FloorToInt(p.y + Height); y++)
                    for (int z = Mathf.FloorToInt(p.z - HalfWidth); z <= Mathf.FloorToInt(p.z + HalfWidth); z++)
                        if (w.SolidAt(x + 0.5f, y + 0.5f, z + 0.5f)) return true;
            return false;
        }

        void MoveH(int axis, float d)
        {
            if (d == 0) return;
            pos[axis] += d;
            if (!Collides(pos)) return;
            if (onGround)
            {
                // Step up one block automatically.
                pos.y += 1.02f;
                if (!Collides(pos)) { eye -= 1.02f; return; }
                pos.y -= 1.02f;
            }
            pos[axis] -= d;
        }

        public void Look()
        {
            Vector2 md = In.MouseDelta;
            if (Mathf.Abs(md.x) > 400 || Mathf.Abs(md.y) > 400) return; // ignore jumps when the cursor locks
            yaw += md.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - md.y * mouseSensitivity, -89f, 89f);
        }

        public void Tick(float dt)
        {
            float f = (In.Held(K.W) || In.Held(K.Up) ? 1 : 0) - (In.Held(K.S) || In.Held(K.Down) ? 1 : 0);
            float s = (In.Held(K.D) || In.Held(K.Right) ? 1 : 0) - (In.Held(K.A) || In.Held(K.Left) ? 1 : 0);
            float l = Mathf.Sqrt(f * f + s * s);
            if (l > 1) { f /= l; s /= l; }
            float sp = In.Held(K.Shift) ? 8.5f : 5.4f;
            float yr = yaw * Mathf.Deg2Rad, sn = Mathf.Sin(yr), cs = Mathf.Cos(yr);
            float dx = (sn * f + cs * s) * sp * dt, dz = (cs * f - sn * s) * sp * dt;

            int steps = dt > 0.025f ? 2 : 1;
            for (int i = 0; i < steps; i++)
            {
                float h = dt / steps;
                MoveH(0, dx / steps);
                MoveH(2, dz / steps);
                if (In.Held(K.Space) && onGround) { velY = 8; onGround = false; }
                velY = Mathf.Max(velY - 24 * h, -40);
                float dy = velY * h;
                pos.y += dy;
                if (Collides(pos))
                {
                    pos.y -= dy;
                    if (velY < 0) { onGround = true; pos.y = Mathf.Floor(pos.y + 0.01f); }
                    velY = 0;
                }
                else onGround = false;
                if (pos.y < 0) { pos.y = 0; velY = 0; onGround = true; }
            }

            float lim = Mathf.Max(g.world.W, g.world.D) / 2f + 140;
            pos.x = Mathf.Clamp(pos.x, -lim, lim);
            pos.z = Mathf.Clamp(pos.z, -lim, lim);
            eye += (1.62f - eye) * Mathf.Min(1, dt * 14);
            moving = l > 0.01f;
        }

        bool moving;

        public void ApplyCamera(float t)
        {
            cam.position = pos + Vector3.up * eye;
            cam.rotation = Quaternion.Euler(pitch, yaw, 0);
            if (shake > 0)
            {
                cam.position += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0) * shake;
            }
        }

        public void UpdateViewModel(float dt, float t, int tool, bool visible)
        {
            vm.gameObject.SetActive(visible);
            pickRoot.gameObject.SetActive(tool == 0);
            dynRoot.gameObject.SetActive(tool == 1);
            swingT = Mathf.Max(0, swingT - dt * 5);
            shake = Mathf.Max(0, shake - dt * 2.5f);
            float sw = Mathf.Sin(swingT * Mathf.PI);
            float bob = moving ? Mathf.Sin(t * 9) * 0.012f : 0;
            vm.localRotation = Quaternion.Euler(12 + sw * 63, -14, -6);
            vm.localPosition = new Vector3(0.28f, -0.28f + bob, 0.48f + sw * 0.05f);
            var c = Data.Picks[g.S.pick].color;
            var mat = Mats.Get(c);
            if (headR.sharedMaterial != mat) { headR.sharedMaterial = mat; tipR.sharedMaterial = mat; }
        }
    }
}
