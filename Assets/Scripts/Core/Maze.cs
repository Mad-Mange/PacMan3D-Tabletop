using UnityEngine;

namespace PacMan.Core
{
    public enum Tile : byte { Empty, Wall, Dot, Energizer, Door }

    /// <summary>
    /// The 28x31 arcade maze. The grid origin is the top-left tile, rows grow downward,
    /// and tile (x, y) has its centre at the point (x, y).
    /// </summary>
    public sealed class Maze
    {
        public const int Width = 28;
        public const int Height = 31;
        public const int TunnelRow = 14;

        public static readonly string[] Layout =
        {
            "############################",
            "#............##............#",
            "#.####.#####.##.#####.####.#",
            "#o####.#####.##.#####.####o#",
            "#.####.#####.##.#####.####.#",
            "#..........................#",
            "#.####.##.########.##.####.#",
            "#.####.##.########.##.####.#",
            "#......##....##....##......#",
            "######.##### ## #####.######",
            "     #.##### ## #####.#     ",
            "     #.##          ##.#     ",
            "     #.## ###--### ##.#     ",
            "######.## #      # ##.######",
            "      .   #      #   .      ",
            "######.## #      # ##.######",
            "     #.## ######## ##.#     ",
            "     #.##          ##.#     ",
            "     #.## ######## ##.#     ",
            "######.## ######## ##.######",
            "#............##............#",
            "#.####.#####.##.#####.####.#",
            "#.####.#####.##.#####.####.#",
            "#o..##.......  .......##..o#",
            "###.##.##.########.##.##.###",
            "###.##.##.########.##.##.###",
            "#......##....##....##......#",
            "#.##########.##.##########.#",
            "#.##########.##.##########.#",
            "#..........................#",
            "############################",
        };

        readonly Tile[] tiles = new Tile[Width * Height];

        public int PelletsLeft { get; private set; }
        public int PelletTotal { get; private set; }

        public Maze() => Reset();

        /// <summary>Restores every dot and energizer.</summary>
        public void Reset()
        {
            PelletsLeft = 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var t = LayoutTile(x, y);
                tiles[y * Width + x] = t;
                if (t == Tile.Dot || t == Tile.Energizer) PelletsLeft++;
            }
            PelletTotal = PelletsLeft;
        }

        public static bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        /// <summary>The tile as authored, ignoring which pellets have been eaten.</summary>
        public static Tile LayoutTile(int x, int y)
        {
            if (!InBounds(x, y)) return y == TunnelRow ? Tile.Empty : Tile.Wall;
            switch (Layout[y][x])
            {
                case '#': return Tile.Wall;
                case '.': return Tile.Dot;
                case 'o': return Tile.Energizer;
                case '-': return Tile.Door;
                default: return Tile.Empty;
            }
        }

        /// <summary>Off-grid tiles are open along the tunnel row and solid everywhere else.</summary>
        public Tile Get(int x, int y)
        {
            if (InBounds(x, y)) return tiles[y * Width + x];
            return y == TunnelRow ? Tile.Empty : Tile.Wall;
        }

        public Tile Get(Vector2Int t) => Get(t.x, t.y);

        /// <summary>True for tiles Pac-Man and roaming ghosts may enter (walls and the door are closed).</summary>
        public bool IsWalkable(Vector2Int t)
        {
            var k = Get(t);
            return k != Tile.Wall && k != Tile.Door;
        }

        /// <summary>Clears a pellet and returns what was there, or Empty.</summary>
        public Tile Eat(Vector2Int t)
        {
            if (!InBounds(t.x, t.y)) return Tile.Empty;
            int i = t.y * Width + t.x;
            var k = tiles[i];
            if (k != Tile.Dot && k != Tile.Energizer) return Tile.Empty;
            tiles[i] = Tile.Empty;
            PelletsLeft--;
            return k;
        }

        /// <summary>The side tunnels, where ghosts slow down.</summary>
        public static bool IsTunnel(Vector2Int t) => t.y == TunnelRow && (t.x <= 5 || t.x >= 22);

        /// <summary>The four tiles above the ghost house and above Pac-Man's start where ghosts may not turn up.</summary>
        public static bool IsNoUpTurnZone(Vector2Int t) => (t.y == 11 || t.y == 23) && t.x >= 12 && t.x <= 15;
    }
}
