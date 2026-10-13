using PacMan.Game;
using UnityEngine;

namespace PacMan.EditorTools
{
    /// <summary>
    /// Procedural textures for the tabletop look in the reference photo: glittery teal 3D-print walls
    /// with LED strips, the black floor with pellet sockets, the wooden table and the pixel-art plaques.
    /// Every texture tiles.
    /// </summary>
    static partial class TextureGen
    {
        public struct Set
        {
            public Color[] Albedo, Mask, Normal, Emission;
        }

        // ------------------------------------------------------------------ noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
            }
        }

        /// <summary>Smooth value noise with period p (in lattice cells) so it tiles.</summary>
        static float Noise(float x, float y, int p, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = Mod(x0, p), bx = Mod(x0 + 1, p), ay = Mod(y0, p), by = Mod(y0 + 1, p);
            float a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
            float b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float u, float v, int basePeriod, int seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int p = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u * p, v * p, p, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                p *= 2;
            }
            return sum / norm;
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static Color EncodeNormal(Vector3 n)
        {
            n.Normalize();
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        static Set NewSet(int size, bool emission)
        {
            int n = size * size;
            return new Set
            {
                Albedo = new Color[n],
                Mask = new Color[n],
                Normal = new Color[n],
                Emission = emission ? new Color[n] : null,
            };
        }

        // ------------------------------------------------------------------ walls

        /// <summary>
        /// Wall sides, mapped 1 texture per world unit with v = height: glitter, faint print layers and two
        /// LED strips (at 0.2 and 0.58 units up) of little yellow lights.
        /// </summary>
        public static Set WallSide(int size)
        {
            var s = Glitter(size, new Color(0.03f, 0.10f, 0.095f), new Color(0.07f, 0.19f, 0.17f), 11, true);
            float[] strips = { 0.20f, 0.58f };
            const int dotsPerUnit = 14;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float v = (y + 0.5f) / size, u = (x + 0.5f) / size;
                s.Emission[i] = Color.black;
                foreach (float strip in strips)
                {
                    float dv = Mathf.Abs(v - strip);
                    if (dv < 0.022f)
                    {
                        // Recessed groove the lights sit in.
                        s.Albedo[i] *= 0.45f;
                        s.Mask[i] = new Color(0f, 0f, 0f, 0.3f);
                        float du = Mathf.Abs(Mathf.Repeat(u * dotsPerUnit, 1f) - 0.5f) / dotsPerUnit;
                        if (du < 0.011f && dv < 0.011f)
                        {
                            s.Albedo[i] = new Color(1f, 0.82f, 0.25f);
                            s.Emission[i] = new Color(1f, 0.72f, 0.12f);
                            s.Mask[i] = new Color(0f, 0f, 0f, 0.8f);
                        }
                        float edge = dv > 0.016f ? Mathf.Sign(v - strip) : 0f;
                        s.Normal[i] = EncodeNormal(new Vector3(0f, -edge * 0.8f, 1f));
                    }
                }
            }
            return s;
        }

        /// <summary>Dark speckled cap on top of the walls.</summary>
        public static Set WallTop(int size) =>
            Glitter(size, new Color(0.035f, 0.065f, 0.065f), new Color(0.07f, 0.11f, 0.105f), 23, false);

        /// <summary>Sparkly 3D-print plastic: mottled colour, glitter flakes with their own tilt, optional print layers.</summary>
        static Set Glitter(int size, Color dark, Color light, int seed, bool layers)
        {
            var s = NewSet(size, true);
            var rng = new System.Random(seed);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float u = (float)x / size, v = (float)y / size;
                float n = Fbm(u, v, 6, seed);
                var c = Color.Lerp(dark, light, Mathf.SmoothStep(0.2f, 0.8f, n));
                var normal = new Vector3(0f, 0f, 1f);
                if (layers)
                {
                    // Fine horizontal print layers, 48 per unit.
                    float layer = Mathf.Repeat(v * 48f, 1f);
                    float ridge = Mathf.Sin(layer * Mathf.PI * 2f);
                    normal.y += ridge * 0.18f;
                    c *= 0.92f + 0.08f * (0.5f + 0.5f * ridge);
                }
                var mask = new Color(0.25f, 0f, 0f, 0.42f);

                if (rng.NextDouble() < 0.07)
                {
                    // A glitter flake: bright, metallic and angled its own way so it glints.
                    float tint = (float)rng.NextDouble();
                    c = Color.Lerp(new Color(0.35f, 0.75f, 0.68f), new Color(0.75f, 0.85f, 0.95f), tint) * (0.6f + 0.6f * (float)rng.NextDouble());
                    mask = new Color(1f, 0f, 0f, 0.9f);
                    normal = new Vector3((float)rng.NextDouble() * 1.2f - 0.6f, (float)rng.NextDouble() * 1.2f - 0.6f, 1f);
                }
                s.Albedo[i] = c;
                s.Mask[i] = mask;
                s.Normal[i] = EncodeNormal(normal);
                s.Emission[i] = Color.black;
            }
            return s;
        }

        // ------------------------------------------------------------------ floor

        /// <summary>
        /// One texture covers 2x2 maze tiles: matte black panels with seams on the texture edges and a recessed
        /// square socket under every pellet position.
        /// </summary>
        public static Set Floor(int size)
        {
            var s = NewSet(size, false);
            int tile = size / 2;
            float socketHalf = tile * 0.16f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float n = Fbm((float)x / size, (float)y / size, 8, 41);
                var c = new Color(0.028f, 0.028f, 0.032f) * (0.8f + 0.4f * n);
                var mask = new Color(0f, 0f, 0f, 0.38f + 0.1f * n);
                var normal = new Vector3(0f, 0f, 1f);

                // Panel seams every two tiles.
                int ex = Mathf.Min(x, size - 1 - x), ey = Mathf.Min(y, size - 1 - y);
                if (ex < 2 || ey < 2)
                {
                    c = new Color(0.012f, 0.012f, 0.014f);
                    mask.a = 0.15f;
                }

                // Pellet socket in the middle of each tile.
                float lx = Mathf.Repeat(x + 0.5f, tile) - tile * 0.5f;
                float ly = Mathf.Repeat(y + 0.5f, tile) - tile * 0.5f;
                float d = Mathf.Max(Mathf.Abs(lx), Mathf.Abs(ly));
                if (d < socketHalf + 2.5f)
                {
                    if (d < socketHalf)
                    {
                        c = new Color(0.006f, 0.006f, 0.008f);
                        mask.a = 0.2f;
                    }
                    else
                    {
                        // Bevelled rim, lit from the upper left.
                        bool horizontal = Mathf.Abs(lx) > Mathf.Abs(ly);
                        float sign = horizontal ? Mathf.Sign(lx) : Mathf.Sign(ly);
                        normal = horizontal ? new Vector3(sign * 0.9f, 0f, 1f) : new Vector3(0f, sign * 0.9f, 1f);
                        c = new Color(0.05f, 0.05f, 0.056f);
                    }
                }
                s.Albedo[i] = c;
                s.Mask[i] = mask;
                s.Normal[i] = EncodeNormal(normal);
            }
            return s;
        }

        // ------------------------------------------------------------------ decals

        public const int DecalIcons = 6;

        /// <summary>Pixel-art plaques: Pac-Man, the four ghosts and a frightened ghost, 16 px apart.</summary>
        public static Color32[] Decals(out int width, out int height)
        {
            width = DecalIcons * 16;
            height = 16;
            var atlas = new Color32[width * height];
            var icons = new[]
            {
                PixelArt.PacMan(false),
                PixelArt.Ghost(PixelArt.GhostColors[0]),
                PixelArt.Ghost(PixelArt.GhostColors[1]),
                PixelArt.Ghost(PixelArt.GhostColors[2]),
                PixelArt.Ghost(PixelArt.GhostColors[3]),
                PixelArt.FrightenedGhost(),
            };
            for (int k = 0; k < icons.Length; k++)
            for (int y = 0; y < 14; y++)
            for (int x = 0; x < 14; x++)
                atlas[(y + 1) * width + k * 16 + x + 1] = icons[k][y * 14 + x];
            return atlas;
        }
    }
}
