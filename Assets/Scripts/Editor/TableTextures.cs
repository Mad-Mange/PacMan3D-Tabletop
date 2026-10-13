using System.Collections.Generic;
using PacMan.Core;
using PacMan.Game;
using UnityEngine;

namespace PacMan.EditorTools
{
    /// <summary>Textures for the table and the props around the board.</summary>
    static partial class TextureGen
    {
        /// <summary>
        /// Oak table top: four planks across the texture (each about 9 maze tiles wide), staggered butt joints,
        /// knots, lacquer that is glossier in some places than others, and recessed seams in the normal map.
        /// </summary>
        public static Set TableWood(int size)
        {
            var s = NewSet(size, false);
            const int planks = 4;
            var light = new Color(0.78f, 0.58f, 0.38f);
            var dark = new Color(0.46f, 0.29f, 0.16f);
            var knots = new List<Vector3>();
            var rng = new System.Random(77);
            // A few small knots (radius in v; they stretch about three times as long along the grain).
            for (int p = 0; p < planks; p++)
            for (int k = 0; k < 2; k++)
                knots.Add(new Vector3((float)rng.NextDouble(), (p + 0.25f + 0.5f * (float)rng.NextDouble()) / planks,
                    0.0025f + 0.003f * (float)rng.NextDouble()));

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                int plank = Mathf.Min(planks - 1, (int)(v * planks));
                float pv = v * planks - plank;
                float tone = Hash(plank, 3, 97);
                float joint = Hash(plank, 11, 5);
                float hue = Hash(plank, 17, 23) - 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    int i = y * size + x;

                    // Grain runs along u; knots bend it into rings.
                    float warp = Fbm(u * 0.5f, v * 2f, 3, 61 + plank, 3) * 3f;
                    float knot = 0f;
                    foreach (var kn in knots)
                    {
                        float dx = Mathf.Abs(u - kn.x);
                        dx = Mathf.Min(dx, 1f - dx) * 0.35f;
                        float dy = v - kn.y;
                        knot += Mathf.Exp(-(dx * dx + dy * dy) / (kn.z * kn.z * 6f)) * 3f;
                    }
                    float rings = Mathf.Sin((v * 260f + warp * 6f + knot * 4f) * Mathf.PI);
                    float fine = Fbm(u * 8f, v * 0.5f, 4, 31 + plank, 4);
                    float t = 0.35f + 0.35f * tone + 0.18f * rings * (0.4f + fine) + 0.12f * (fine - 0.5f);
                    var c = Color.Lerp(dark, light, Mathf.Clamp01(t));
                    c.r += hue * 0.06f;
                    c.b -= hue * 0.04f;
                    if (knot > 1.5f) c *= Mathf.Lerp(1f, 0.62f, Mathf.Clamp01(knot - 1.5f));

                    float gloss = 0.32f + 0.25f * Fbm(u * 2f, v * 2f, 3, 9, 3);
                    var normal = new Vector3(0f, rings * 0.06f, 1f);

                    // Seams between planks, and one butt joint along each plank.
                    float seam = Mathf.Min(pv, 1f - pv) * size / planks;
                    float ju = Mathf.Abs(u - joint);
                    ju = Mathf.Min(ju, 1f - ju) * size;
                    if (seam < 2.5f || ju < 1.5f)
                    {
                        c *= 0.3f;
                        gloss = 0.1f;
                    }
                    else if (seam < 4.5f)
                    {
                        normal.y += (pv < 0.5f ? 1f : -1f) * 0.7f;
                    }
                    else if (ju < 3.5f)
                    {
                        normal.x += (Mathf.Repeat(u - joint + 0.5f, 1f) > 0.5f ? 1f : -1f) * 0.7f;
                    }

                    s.Albedo[i] = c;
                    s.Mask[i] = new Color(0f, 0f, 0f, gloss);
                    s.Normal[i] = EncodeNormal(normal);
                }
            }
            return s;
        }

        /// <summary>Grid paper with a pencil sketch of the maze, a couple of doodles and a scribbled high score.</summary>
        public static Color32[] NotebookPage(int w, int h)
        {
            var px = new Color32[w * h];
            var rng = new System.Random(4);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Fbm((float)x / w, (float)y / h, 6, 3, 3);
                var c = new Color(0.97f, 0.955f, 0.9f) * (0.96f + 0.06f * n);
                if (x % 17 == 0 || y % 17 == 0) c = Color.Lerp(c, new Color(0.62f, 0.78f, 0.93f), 0.45f);
                if (x >= 56 && x <= 57) c = Color.Lerp(c, new Color(0.9f, 0.35f, 0.35f), 0.7f);
                px[y * w + x] = c;
            }

            var pencil = new Color32(70, 70, 80, 200);
            const int tile = 13;
            int ox = 78, top = h - 110;
            bool Wall(int x, int y) => Maze.InBounds(x, y) && Maze.LayoutTile(x, y) == Tile.Wall;
            for (int ty = 0; ty < Maze.Height; ty++)
            for (int tx = 0; tx < Maze.Width; tx++)
            {
                if (!Wall(tx, ty)) continue;
                int x0 = ox + tx * tile, y1 = top - ty * tile, y0 = y1 - tile;
                if (!Wall(tx, ty - 1)) Line(px, w, h, x0, y1, x0 + tile, y1, pencil, rng);
                if (!Wall(tx, ty + 1)) Line(px, w, h, x0, y0, x0 + tile, y0, pencil, rng);
                if (!Wall(tx - 1, ty)) Line(px, w, h, x0, y0, x0, y1, pencil, rng);
                if (!Wall(tx + 1, ty)) Line(px, w, h, x0 + tile, y0, x0 + tile, y1, pencil, rng);
            }

            PixelArt.StampText(px, w, h, "MAZE V2", 80, h - 40, 3, pencil);
            PixelArt.StampText(px, w, h, "HI 24310", 300, h - 46, 2, new Color32(200, 40, 40, 200));
            PixelArt.StampIcon(px, w, h, PixelArt.PacMan(false), 14, 120, 24, 3, pencil);
            for (int d = 0; d < 4; d++)
                PixelArt.StampIcon(px, w, h, PixelArt.Ghost(Color.white), 14, 200 + d * 58, 24, 3, pencil);
            return px;
        }

        static void Line(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color32 c, System.Random rng)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1;
            float wobble = (float)rng.NextDouble() * 0.8f;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / Mathf.Max(1, steps - 1);
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t) + (y0 == y1 ? 0f : wobble));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t) + (x0 == x1 ? 0f : wobble));
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                float k = c.a / 255f * (0.6f + 0.4f * (float)rng.NextDouble());
                var d = px[y * w + x];
                px[y * w + x] = new Color32((byte)(d.r + (c.r - d.r) * k), (byte)(d.g + (c.g - d.g) * k), (byte)(d.b + (c.b - d.b) * k), 255);
            }
        }

        /// <summary>Box art for the game box on the table: logo, the cast and a starry night.</summary>
        public static Color32[] BoxArt(int size)
        {
            var px = new Color32[size * size];
            var rng = new System.Random(12);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = Color.Lerp(new Color(0.02f, 0.03f, 0.16f), new Color(0.1f, 0.04f, 0.26f), (float)y / size);
            for (int i = 0; i < 160; i++)
            {
                byte b = (byte)rng.Next(120, 255);
                px[rng.Next(size) * size + rng.Next(size)] = new Color32(b, b, b, 255);
            }

            // Double blue border, like the arcade maze.
            var blue = new Color32(40, 90, 255, 255);
            for (int k = 0; k < 2; k++)
            {
                int inset = 14 + k * 12;
                for (int t = 0; t < 5; t++)
                for (int i = inset; i < size - inset; i++)
                {
                    px[(inset + t) * size + i] = blue;
                    px[(size - 1 - inset - t) * size + i] = blue;
                    px[i * size + inset + t] = blue;
                    px[i * size + size - 1 - inset - t] = blue;
                }
            }

            int logoW = 7 * 8 * 7 - 2 * 7;
            PixelArt.StampText(px, size, size, "PAC-MAN", (size - logoW) / 2 + 5, size - 66, 7, new Color32(170, 40, 20, 255));
            PixelArt.StampText(px, size, size, "PAC-MAN", (size - logoW) / 2, size - 60, 7, new Color32(255, 220, 30, 255));
            PixelArt.StampText(px, size, size, "3D", (size - 2 * 8 * 6) / 2, size - 132, 6, new Color32(70, 230, 255, 255));
            PixelArt.StampText(px, size, size, "TABLETOP EDITION", (size - 16 * 8 * 2) / 2, size - 196, 2, new Color32(240, 240, 255, 255));

            int step = 84, x0 = (size - 5 * step) / 2 + 7;
            PixelArt.StampIcon(px, size, size, PixelArt.PacMan(false), 14, x0, 128, 5);
            for (int g = 0; g < 4; g++)
                PixelArt.StampIcon(px, size, size, PixelArt.Ghost(PixelArt.GhostColors[g]), 14, x0 + (g + 1) * step, 128, 5);
            for (int d = 0; d < 9; d++)
            for (int y = 96; y < 104; y++)
            for (int x = 60 + d * 44; x < 68 + d * 44; x++)
                px[y * size + x] = new Color32(255, 190, 150, 255);
            PixelArt.StampText(px, size, size, "1 PLAYER", (size - 8 * 8 * 2) / 2, 74, 2, new Color32(200, 200, 220, 255));
            return px;
        }

        /// <summary>A coffee ring stain, mostly transparent, with a gap where the mug was lifted off.</summary>
        public static Color[] CoffeeStain(int size)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx);
                float radius = 0.36f + 0.012f * Mathf.Sin(a * 3f) + 0.008f * Mathf.Sin(a * 7f + 1f);
                float thickness = 0.012f + 0.01f * (0.5f + 0.5f * Mathf.Sin(a * 2f + 0.5f));
                float ring = Mathf.Exp(-Mathf.Pow((r - radius) / thickness, 2f));
                float fill = r < radius ? 0.07f : 0f;
                float gap = Mathf.SmoothStep(0f, 1f, Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 130f)) / 40f);
                px[y * size + x] = new Color(0.32f, 0.18f, 0.08f, Mathf.Clamp01(ring * 0.55f * gap + fill));
            }
            return px;
        }

        /// <summary>A soft round blob for dust motes and steam.</summary>
        public static Color[] SoftDot(int size)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            return px;
        }

        /// <summary>
        /// Light cookie for the sun: a sunlit window with soft mullion shadows; outside it the table sits in shade.
        /// </summary>
        public static Color[] WindowCookie(int size)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float edge = Mathf.Min(Mathf.Min(u - 0.08f, 0.92f - u), Mathf.Min(v - 0.06f, 0.94f - v));
                float inside = Mathf.SmoothStep(0f, 1f, edge / 0.05f);
                float bar = Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Min(Mathf.Abs(v - 0.36f), Mathf.Abs(v - 0.66f)));
                float mullion = Mathf.SmoothStep(0f, 1f, (bar - 0.008f) / 0.02f);
                float value = Mathf.Lerp(0.38f, 1f, inside * Mathf.Lerp(0.55f, 1f, mullion));
                px[y * size + x] = new Color(value, value, value, value);
            }
            return px;
        }
    }
}
