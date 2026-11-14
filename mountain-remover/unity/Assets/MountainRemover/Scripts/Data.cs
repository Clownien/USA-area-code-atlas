using UnityEngine;

namespace MountainRemover
{
    public struct MatDef
    {
        public string name;
        public Color color;
        public float hard;
        public float value;
        public bool ore;

        public MatDef(string name, uint hex, float hard, float value, bool ore = false)
        {
            this.name = name;
            color = Data.Hex(hex);
            this.hard = hard;
            this.value = value;
            this.ore = ore;
        }
    }

    public class MountainDef
    {
        public string name;
        public int W, D, H, peak, seed, snow;
        public string shape;
        public float mult, ore, granite, hard, metres;
        public string desc;
    }

    public struct PickDef
    {
        public string name; public float damage; public float cooldown; public Color color; public int cost;
        public PickDef(string n, float d, float cd, uint col, int c) { name = n; damage = d; cooldown = cd; color = Data.Hex(col); cost = c; }
    }

    public struct PackDef
    {
        public string name; public int cap; public int cost;
        public PackDef(string n, int cap, int c) { name = n; this.cap = cap; cost = c; }
    }

    /// <summary>All tuning numbers live here: materials, mountains, upgrade tiers and prices.</summary>
    public static class Data
    {
        public static Color Hex(uint h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);

        public const byte GRASS = 1, DIRT = 2, STONE = 3, COAL = 4, IRON = 5, GOLD = 6, GEM = 7, SNOW = 8, GRANITE = 9;

        public static readonly MatDef[] Mats =
        {
            new MatDef("Air",      0x000000, 0,  0),
            new MatDef("Grass",    0x6fae4b, 1,  0.5f),
            new MatDef("Dirt",     0x8d6442, 1,  0.5f),
            new MatDef("Stone",    0x8e9196, 3,  1),
            new MatDef("Coal",     0x2c2c31, 3,  4,   true),
            new MatDef("Iron ore", 0xc98654, 5,  12,  true),
            new MatDef("Gold ore", 0xf2c218, 7,  45,  true),
            new MatDef("Gemstone", 0x3cd8e2, 10, 160, true),
            new MatDef("Snow",     0xf2f6fb, 1,  0.5f),
            new MatDef("Granite",  0x6d6065, 6,  2),
        };

        public static readonly MountainDef[] Mountains =
        {
            new MountainDef { name = "Small Hill",     W = 20, D = 20, H = 10, peak = 8,  shape = "hill",    seed = 11, snow = 99, mult = 1f,   ore = 1f,   granite = 0f,    hard = 1f,   metres = 16,   desc = "A grassy lump behind the depot. Good for learning the ropes." },
            new MountainDef { name = "Rocky Mountain", W = 36, D = 36, H = 20, peak = 17, shape = "rocky",   seed = 23, snow = 99, mult = 1.5f, ore = 1.3f, granite = 0.15f, hard = 1.3f, metres = 340,  desc = "Real rock and the first seams of iron. Drill rigs start to pay off here." },
            new MountainDef { name = "Large Mountain", W = 52, D = 52, H = 30, peak = 27, shape = "rocky",   seed = 37, snow = 22, mult = 2.5f, ore = 1.6f, granite = 0.25f, hard = 1.7f, metres = 1350, desc = "Too big to dig by hand. You need a proper operation." },
            new MountainDef { name = "The Matterhorn", W = 64, D = 64, H = 48, peak = 45, shape = "horn",    seed = 41, snow = 28, mult = 4f,   ore = 2f,   granite = 0.35f, hard = 2.2f, metres = 4478, desc = "4,478 m of granite pyramid on the Swiss-Italian border." },
            new MountainDef { name = "Mount Everest",  W = 96, D = 96, H = 66, peak = 63, shape = "everest", seed = 59, snow = 36, mult = 7f,   ore = 2.6f, granite = 0.45f, hard = 3f,   metres = 8849, desc = "8,849 m. The last mountain." },
        };

        public static readonly PickDef[] Picks =
        {
            new PickDef("Rusty Pickaxe",    1,  0.42f, 0x8a6a55, 0),
            new PickDef("Iron Pickaxe",     2,  0.38f, 0xa9adb3, 60),
            new PickDef("Steel Pickaxe",    4,  0.33f, 0xd4d9df, 450),
            new PickDef("Diamond-Tip Pick", 8,  0.28f, 0x6fe3ef, 3500),
            new PickDef("Jackhammer",       20, 0.14f, 0xf2b705, 30000),
        };

        public static readonly PackDef[] Packs =
        {
            new PackDef("Canvas Sack", 25,   0),
            new PackDef("Wheelbarrow", 60,   90),
            new PackDef("Mine Cart",   150,  700),
            new PackDef("Ore Hauler",  400,  5000),
            new PackDef("Dump Truck",  1200, 40000),
        };

        public const int MaxRigs = 16, MaxRigLv = 8, MaxSiloLv = 8, MaxBelt = 8, MaxCrusher = 8, MaxSmelter = 6, MaxBlast = 5;
        public const int DynamitePackCost = 60;

        static int R(double v) => (int)System.Math.Round(v);
        public static int RigCost(int n) => R(300 * System.Math.Pow(1.55, n));
        public static float RigRate(int lv) => 0.4f * Mathf.Pow(1.75f, lv);
        public static int RigUpCost(int lv) => R(400 * System.Math.Pow(2.7, lv));
        public static int SiloCap(int lv) => R(150 * System.Math.Pow(2.4, lv));
        public static int SiloCost(int lv) => R(200 * System.Math.Pow(2.6, lv));
        public static float BeltRate(int lv) => lv > 0 ? 3f * Mathf.Pow(2f, lv - 1) : 0f;
        public static int BeltCost(int lv) => R(150 * System.Math.Pow(2.8, lv));
        public static int CrusherCost(int lv) => R(400 * System.Math.Pow(2.6, lv));
        public static int SmelterCost(int lv) => R(1200 * System.Math.Pow(2.8, lv));
        public static float BlastRadius(int lv) => 2.2f + 0.8f * lv;
        public static int BlastCost(int lv) => R(800 * System.Math.Pow(3, lv));
        public static float CrusherMult(int lv) => 1f + 0.3f * lv;
        public static float SmelterMult(int lv) => 1f + 0.6f * lv;
    }

    [System.Serializable]
    public class Inv
    {
        public double n, rock, ore;
        public void Clear() { n = 0; rock = 0; ore = 0; }
    }

    [System.Serializable]
    public class Stats
    {
        public int hand, rig, blast;
        public double earned;
        public float time;
    }

    /// <summary>Everything that gets saved about the player's company.</summary>
    [System.Serializable]
    public class GameState
    {
        public double money = 25;
        public int pick, pack, rigs, rigLv, siloLv, belt, crusher, smelter, dyn, blastLv, mtn;
        public bool cleared, won;
        public Inv bag = new Inv();
        public Inv silo = new Inv();
        public Stats st = new Stats();
    }

    [System.Serializable]
    public class SaveData
    {
        public GameState S;
        public int[] mask;
        public float px, py, pz, yaw;
    }
}
