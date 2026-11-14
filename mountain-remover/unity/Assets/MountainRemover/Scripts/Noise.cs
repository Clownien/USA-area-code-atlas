using UnityEngine;

namespace MountainRemover
{
    /// <summary>Deterministic hash and value noise, so every mountain generates the same way each time.</summary>
    public static class Noise
    {
        public static float Hash2(int x, int z, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263 + s * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h >> 8) / 16777216f;
            }
        }

        public static float Hash3(int x, int y, int z, int s)
        {
            unchecked { return Hash2(x * 31 + y * 7919, z * 17 + y * 104729, s); }
        }

        public static float Value(float x, float z, int s)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float xf = x - xi, zf = z - zi;
            float u = xf * xf * (3 - 2 * xf), v = zf * zf * (3 - 2 * zf);
            float a = Hash2(xi, zi, s), b = Hash2(xi + 1, zi, s), c = Hash2(xi, zi + 1, s), d = Hash2(xi + 1, zi + 1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        public static float Fbm(float x, float z, int s)
        {
            float t = 0, a = 0.5f, f = 1, n = 0;
            for (int i = 0; i < 4; i++) { t += a * Value(x * f, z * f, s + i * 13); n += a; a *= 0.5f; f *= 2; }
            return t / n;
        }
    }
}
