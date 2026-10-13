using UnityEngine;

namespace PacMan.Core
{
    /// <summary>
    /// A small steering AI for the attract-mode demo: breadth-first search to the most attractive goal
    /// (pellet, frightened ghost or fruit) while treating tiles near dangerous ghosts as blocked.
    /// </summary>
    public sealed class Autopilot
    {
        const int Size = Maze.Width * Maze.Height;
        const int DangerRadius = 4;

        readonly int[] dist = new int[Size];
        readonly Dir[] firstStep = new Dir[Size];
        readonly bool[] danger = new bool[Size];
        readonly int[] queue = new int[Size];
        readonly Dir[] order = new Dir[4];

        public Dir Choose(GameSim sim)
        {
            var pac = sim.Pac;
            var start = pac.Tile;
            if (!Maze.InBounds(start.x, start.y)) return pac.Dir != Dir.None ? pac.Dir : pac.Facing;

            MarkDanger(sim);
            Search(sim, start, pac.Facing);

            int bestScore = int.MaxValue;
            Dir best = Dir.None;
            for (int i = 0; i < Size; i++)
            {
                if (dist[i] < 0 || firstStep[i] == Dir.None) continue;
                int score = GoalScore(sim, i % Maze.Width, i / Maze.Width, dist[i]);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = firstStep[i];
                }
            }
            return best != Dir.None ? best : Escape(sim, start);
        }

        int GoalScore(GameSim sim, int x, int y, int d)
        {
            int score = int.MaxValue;
            var t = sim.Maze.Get(x, y);
            if (t == Tile.Dot) score = d;
            // Save energizers for when a ghost is close enough to be worth eating.
            else if (t == Tile.Energizer) score = d + (NearestDangerDistance(sim, x, y) < 8 ? -4 : 12);

            if (sim.FruitActive && y == 17 && (x == 13 || x == 14)) score = Mathf.Min(score, d - 6);

            if (sim.IsFrightened && !sim.FrightFlashing)
                foreach (var g in sim.Ghosts)
                    if (g.Frightened && g.State == GhostState.Active && g.Tile.x == x && g.Tile.y == y)
                        score = Mathf.Min(score, d - 10);
            return score;
        }

        void MarkDanger(GameSim sim)
        {
            System.Array.Clear(danger, 0, Size);
            foreach (var g in sim.Ghosts)
            {
                if (!g.IsDangerous) continue;
                var t = g.Tile;
                if (!Maze.InBounds(t.x, t.y)) continue;
                for (int i = 0; i < Size; i++) dist[i] = -1;
                int head = 0, tail = 0;
                int s = t.y * Maze.Width + t.x;
                dist[s] = 0;
                queue[tail++] = s;
                while (head < tail)
                {
                    int c = queue[head++];
                    danger[c] = true;
                    if (dist[c] >= DangerRadius) continue;
                    foreach (var d in DirExtensions.Priority)
                    {
                        int n = Neighbor(sim, c, d);
                        if (n < 0 || dist[n] >= 0) continue;
                        dist[n] = dist[c] + 1;
                        queue[tail++] = n;
                    }
                }
            }
        }

        void Search(GameSim sim, Vector2Int start, Dir facing)
        {
            for (int i = 0; i < Size; i++)
            {
                dist[i] = -1;
                firstStep[i] = Dir.None;
            }

            // Prefer to keep going the way we face when paths tie, so the demo doesn't jitter.
            order[0] = facing == Dir.None ? Dir.Left : facing;
            int k = 1;
            foreach (var d in DirExtensions.Priority)
                if (d != order[0]) order[k++] = d;

            int s = start.y * Maze.Width + start.x;
            int head = 0, tail = 0;
            dist[s] = 0;
            queue[tail++] = s;
            while (head < tail)
            {
                int c = queue[head++];
                foreach (var d in order)
                {
                    int n = Neighbor(sim, c, d);
                    if (n < 0 || dist[n] >= 0 || danger[n]) continue;
                    dist[n] = dist[c] + 1;
                    firstStep[n] = c == s ? d : firstStep[c];
                    queue[tail++] = n;
                }
            }
        }

        Dir Escape(GameSim sim, Vector2Int start)
        {
            Dir best = Dir.None;
            int bestDist = -1;
            int s = start.y * Maze.Width + start.x;
            foreach (var d in DirExtensions.Priority)
            {
                int n = Neighbor(sim, s, d);
                if (n < 0) continue;
                int far = NearestDangerDistance(sim, n % Maze.Width, n / Maze.Width);
                if (far > bestDist)
                {
                    bestDist = far;
                    best = d;
                }
            }
            return best;
        }

        static int NearestDangerDistance(GameSim sim, int x, int y)
        {
            int best = int.MaxValue;
            foreach (var g in sim.Ghosts)
            {
                if (!g.IsDangerous) continue;
                var t = g.Tile;
                best = Mathf.Min(best, Mathf.Abs(t.x - x) + Mathf.Abs(t.y - y));
            }
            return best;
        }

        /// <summary>Index of the walkable neighbour in direction d (wrapping through the tunnel), or -1.</summary>
        static int Neighbor(GameSim sim, int index, Dir d)
        {
            var v = d.Vec();
            int x = index % Maze.Width + v.x;
            int y = index / Maze.Width + v.y;
            if (y == Maze.TunnelRow) x = (x + Maze.Width) % Maze.Width;
            if (!Maze.InBounds(x, y) || !sim.Maze.IsWalkable(new Vector2Int(x, y))) return -1;
            return y * Maze.Width + x;
        }
    }
}
