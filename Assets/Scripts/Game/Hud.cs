using UnityEngine;

namespace PacMan.Game
{
    /// <summary>
    /// Draws pixel-font text and icons with Graphics.DrawTexture, in top-left-origin pixel coordinates.
    /// Works both from OnGUI (repaint) and onto a render texture after GL.LoadPixelMatrix.
    /// </summary>
    public sealed class Hud
    {
        public enum Align { Left, Center, Right }

        readonly Texture2D white;

        public float Width { get; private set; }
        public float Height { get; private set; }
        /// <summary>Size of one font pixel on screen; a character cell is 8 of them.</summary>
        public float Px { get; private set; }

        public Hud()
        {
            white = PixelArt.MakeTexture("White", 1, 1, new[] { new Color32(255, 255, 255, 255) });
        }

        public void Begin(float width, float height)
        {
            Width = width;
            Height = height;
            // One font pixel per 1/230 of the height, but narrow (portrait) screens shrink it to fit the score bar.
            Px = Mathf.Max(1f, Mathf.Floor(Mathf.Min(height / 230f, width / 190f)));
        }

        public float TextWidth(string s, float scale = 1f) => s.Length * 8f * Px * scale - 2f * Px * scale;

        public void Text(string s, float x, float y, Color color, Align align = Align.Left, float scale = 1f, bool shadow = true)
        {
            if (string.IsNullOrEmpty(s)) return;
            float px = Px * scale;
            float w = TextWidth(s, scale);
            float x0 = align == Align.Left ? x : align == Align.Center ? x - w * 0.5f : x - w;
            x0 = Mathf.Round(x0);
            y = Mathf.Round(y);
            if (shadow) Glyphs(s, x0 + px, y + px, px, new Color(0f, 0f, 0f, color.a * 0.85f));
            Glyphs(s, x0, y, px, color);
        }

        void Glyphs(string s, float x, float y, float px, Color color)
        {
            var atlas = PixelArt.FontAtlas;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == ' ' || !PixelArt.TryGlyph(s[i], out var uv)) continue;
                Draw(new Rect(x + i * 8f * px, y, 8f * px, 8f * px), atlas, uv, color);
            }
        }

        public void Image(Texture tex, Rect r, Color color) => Draw(r, tex, new Rect(0f, 0f, 1f, 1f), color);

        public void Panel(Rect r, Color color) => Draw(r, white, new Rect(0f, 0f, 1f, 1f), color);

        static void Draw(Rect r, Texture tex, Rect uv, Color color)
        {
            // DrawTexture's neutral tint is 0.5 grey.
            Graphics.DrawTexture(r, tex, uv, 0, 0, 0, 0, color * 0.5f);
        }
    }
}
