using System;
using System.Collections.Generic;
using PacMan.Core;
using UnityEngine;

namespace PacMan.Game
{
    /// <summary>
    /// Pixel art shared by the HUD and the wall decals: a bold 5x7 font, the ghost and Pac-Man icons,
    /// and the fruit sprites from Resources/PixelArt/fruits.json (which the Blender script also reads).
    /// </summary>
    public static class PixelArt
    {
        public static readonly Color PacYellow = new Color32(255, 222, 30, 255);
        public static readonly Color[] GhostColors =
        {
            new Color32(240, 30, 35, 255),   // Blinky
            new Color32(255, 150, 215, 255), // Pinky
            new Color32(60, 230, 255, 255),  // Inky
            new Color32(255, 160, 50, 255),  // Clyde
        };
        public static readonly Color FrightBlue = new Color32(40, 60, 255, 255);

        // ------------------------------------------------------------------ font

        public const string FontChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!-\"./:'?, ";
        const int Cell = 8;
        const int AtlasColumns = 16;

        static readonly string[][] Glyphs =
        {
            new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" }, // A
            new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." }, // B
            new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." }, // C
            new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." }, // D
            new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" }, // E
            new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." }, // F
            new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" }, // G
            new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" }, // H
            new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." }, // I
            new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." }, // J
            new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" }, // K
            new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" }, // L
            new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" }, // M
            new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" }, // N
            new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." }, // O
            new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." }, // P
            new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" }, // Q
            new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" }, // R
            new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." }, // S
            new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." }, // T
            new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." }, // U
            new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." }, // V
            new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." }, // W
            new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" }, // X
            new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." }, // Y
            new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" }, // Z
            new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." }, // 0
            new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." }, // 1
            new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" }, // 2
            new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." }, // 3
            new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." }, // 4
            new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." }, // 5
            new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." }, // 6
            new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." }, // 7
            new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." }, // 8
            new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." }, // 9
            new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." }, // !
            new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." }, // -
            new[] { ".#.#.", ".#.#.", ".....", ".....", ".....", ".....", "....." }, // quote
            new[] { ".....", ".....", ".....", ".....", ".....", ".....", "..#.." }, // .
            new[] { "....#", "...#.", "...#.", "..#..", ".#...", ".#...", "#...." }, // /
            new[] { ".....", "..#..", ".....", ".....", ".....", "..#..", "....." }, // :
            new[] { "..#..", "..#..", ".....", ".....", ".....", ".....", "....." }, // apostrophe
            new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." }, // ?
            new[] { ".....", ".....", ".....", ".....", ".....", "..#..", ".#..." }, // ,
            new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." }, // space
        };

        static Texture2D fontAtlas;
        static Dictionary<char, Rect> glyphUv;

        public static Texture2D FontAtlas
        {
            get
            {
                if (fontAtlas == null) BuildFont();
                return fontAtlas;
            }
        }

        /// <summary>UV rect of a character in <see cref="FontAtlas"/>, or false for unknown characters.</summary>
        public static bool TryGlyph(char c, out Rect uv)
        {
            if (glyphUv == null) BuildFont();
            return glyphUv.TryGetValue(char.ToUpperInvariant(c), out uv);
        }

        static void BuildFont()
        {
            int rows = (FontChars.Length + AtlasColumns - 1) / AtlasColumns;
            int w = AtlasColumns * Cell, h = rows * Cell;
            var px = new Color32[w * h];
            glyphUv = new Dictionary<char, Rect>();
            for (int i = 0; i < FontChars.Length; i++)
            {
                var g = Glyphs[i];
                int cx = (i % AtlasColumns) * Cell;
                int cy = (i / AtlasColumns) * Cell;
                for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    if (g[gy][gx] != '#') continue;
                    // Bold: every pixel is smeared one to the right, like the chunky arcade font.
                    for (int b = 0; b < 2; b++)
                    {
                        int x = cx + gx + b;
                        int y = h - 1 - (cy + gy);
                        px[y * w + x] = new Color32(255, 255, 255, 255);
                    }
                }
                glyphUv[FontChars[i]] = new Rect((float)cx / w, 1f - (float)(cy + Cell) / h, (float)Cell / w, (float)Cell / h);
            }
            fontAtlas = MakeTexture("PixelFont", w, h, px);
        }

        /// <summary>
        /// Writes text into a pixel buffer (row 0 at the bottom) in the bold HUD font, each font pixel
        /// <paramref name="scale"/> pixels big. <paramref name="top"/> is the y of the text's top edge.
        /// </summary>
        public static void StampText(Color32[] px, int w, int h, string s, int x, int top, int scale, Color32 color)
        {
            for (int i = 0; i < s.Length; i++)
            {
                int index = FontChars.IndexOf(char.ToUpperInvariant(s[i]));
                if (index < 0) continue;
                var g = Glyphs[index];
                for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    if (g[gy][gx] != '#') continue;
                    for (int b = 0; b < 2; b++)
                        Fill(px, w, h, x + (i * 8 + gx + b) * scale, top - (gy + 1) * scale, scale, scale, color);
                }
            }
        }

        /// <summary>Copies the opaque pixels of an icon into a buffer, scaled up by whole pixels.</summary>
        public static void StampIcon(Color32[] px, int w, int h, Color32[] icon, int iconSize, int x, int y, int scale, Color32? tint = null)
        {
            for (int iy = 0; iy < iconSize; iy++)
            for (int ix = 0; ix < iconSize; ix++)
            {
                var c = icon[iy * iconSize + ix];
                if (c.a < 128) continue;
                Fill(px, w, h, x + ix * scale, y + iy * scale, scale, scale, tint ?? c);
            }
        }

        static void Fill(Color32[] px, int w, int h, int x0, int y0, int fw, int fh, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y0 + fh); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x0 + fw); x++)
            {
                var d = px[y * w + x];
                float a = c.a / 255f;
                px[y * w + x] = new Color32(
                    (byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), 255);
            }
        }

        // ------------------------------------------------------------------ icons

        static readonly string[] GhostIcon =
        {
            "....######....",
            "..##########..",
            ".############.",
            ".##WW####WW##.",
            "##WWWW##WWWW##",
            "##WWPP##WWPP##",
            "##WWPP##WWPP##",
            "###WW####WW###",
            "##############",
            "##############",
            "##############",
            "##############",
            "##.###..###.##",
            "#...##..##...#",
        };

        static readonly string[] FrightIcon =
        {
            "....######....",
            "..##########..",
            ".############.",
            ".############.",
            "####FF##FF####",
            "####FF##FF####",
            "##############",
            "##############",
            "##F##F##F##F##",
            "#F#FF#FF#FF#F#",
            "##############",
            "##############",
            "##.###..###.##",
            "#...##..##...#",
        };

        /// <summary>A 14x14 ghost in the given body colour.</summary>
        public static Color32[] Ghost(Color body) =>
            Paint(GhostIcon, c => c == '#' ? body : c == 'W' ? new Color(0.96f, 0.96f, 1f) : c == 'P' ? new Color(0.12f, 0.2f, 0.9f) : Color.clear);

        public static Color32[] FrightenedGhost() =>
            Paint(FrightIcon, c => c == '#' ? FrightBlue : c == 'F' ? new Color(1f, 0.78f, 0.65f) : Color.clear);

        /// <summary>A 14x14 Pac-Man facing right (or left), mouth slightly open.</summary>
        public static Color32[] PacMan(bool facingLeft)
        {
            var px = new Color32[14 * 14];
            for (int y = 0; y < 14; y++)
            for (int x = 0; x < 14; x++)
            {
                float dx = x - 6.5f, dy = y - 6.5f;
                if (dx * dx + dy * dy > 6.6f * 6.6f) continue;
                float ax = facingLeft ? -dx : dx;
                if (ax > 0f && Mathf.Abs(dy) < ax * 0.75f) continue;
                px[y * 14 + x] = PacYellow;
            }
            return px;
        }

        static Color32[] Paint(string[] rows, Func<char, Color> color)
        {
            int h = rows.Length, w = rows[0].Length;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = color(rows[y][x]);
            return px;
        }

        public static Texture2D MakeTexture(string name, int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ fruit

        [Serializable] class FruitFile { public PaletteEntry[] palette; public FruitEntry[] fruits; }
        [Serializable] class PaletteEntry { public string key; public string name; public int[] rgb; }
        [Serializable] class FruitEntry { public string name; public string[] rows; }

        static Dictionary<Fruit, Texture2D> fruitIcons;

        public static Texture2D FruitIcon(Fruit fruit)
        {
            if (fruitIcons == null) LoadFruit();
            return fruitIcons.TryGetValue(fruit, out var t) ? t : null;
        }

        static void LoadFruit()
        {
            fruitIcons = new Dictionary<Fruit, Texture2D>();
            var json = Resources.Load<TextAsset>("PixelArt/fruits");
            if (json == null) return;
            var file = JsonUtility.FromJson<FruitFile>(json.text);
            var palette = new Dictionary<char, Color32>();
            foreach (var p in file.palette) palette[p.key[0]] = new Color32((byte)p.rgb[0], (byte)p.rgb[1], (byte)p.rgb[2], 255);
            foreach (var f in file.fruits)
            {
                if (!Enum.TryParse(f.name, out Fruit fruit)) continue;
                var px = Paint(f.rows, c => palette.TryGetValue(c, out var col) ? (Color)col : Color.clear);
                fruitIcons[fruit] = MakeTexture("Fruit_" + f.name, f.rows[0].Length, f.rows.Length, px);
            }
        }
    }
}
