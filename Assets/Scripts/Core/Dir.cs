using UnityEngine;

namespace PacMan.Core
{
    public enum Dir { None = -1, Up = 0, Left = 1, Down = 2, Right = 3 }

    public static class DirExtensions
    {
        /// <summary>The arcade's tie-break order when a ghost has equally good options.</summary>
        public static readonly Dir[] Priority = { Dir.Up, Dir.Left, Dir.Down, Dir.Right };

        /// <summary>Grid step for a direction. Rows grow downward, so Up is -y.</summary>
        public static Vector2Int Vec(this Dir d)
        {
            switch (d)
            {
                case Dir.Up: return new Vector2Int(0, -1);
                case Dir.Down: return new Vector2Int(0, 1);
                case Dir.Left: return new Vector2Int(-1, 0);
                case Dir.Right: return new Vector2Int(1, 0);
                default: return Vector2Int.zero;
            }
        }

        public static Dir Opposite(this Dir d)
        {
            switch (d)
            {
                case Dir.Up: return Dir.Down;
                case Dir.Down: return Dir.Up;
                case Dir.Left: return Dir.Right;
                case Dir.Right: return Dir.Left;
                default: return Dir.None;
            }
        }

        public static bool IsHorizontal(this Dir d) => d == Dir.Left || d == Dir.Right;
    }
}
