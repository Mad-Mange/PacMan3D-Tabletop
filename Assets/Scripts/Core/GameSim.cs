using System;
using UnityEngine;

namespace PacMan.Core
{
    public enum Phase { Idle, Ready, Playing, GhostEaten, Dying, LevelComplete, GameOver }

    public enum GameEvent
    {
        GameStarted, ReadyStarted, PlayStarted,
        DotEaten, EnergizerEaten, GhostEaten,
        FruitAppeared, FruitEaten, FruitExpired,
        PacCaught, DeathAnimStarted, LevelCleared, LevelStarted,
        ExtraLife, GameOver,
    }

    /// <summary>
    /// The whole game rules, independent of rendering: maze, Pac-Man, the four ghosts and the game flow.
    /// Positions are in tile units on the maze grid (see <see cref="Maze"/>). Drive it with <see cref="Tick"/>
    /// at a small fixed step.
    /// </summary>
    public sealed class GameSim
    {
        public const int StartLives = 3;
        public const int ExtraLifeScore = 10000;

        public const float IntroTime = 4.2f;
        public const float ReadyTime = 2f;
        public const float GhostEatenPauseTime = 1f;
        public const float CaughtFreezeTime = 1f;
        public const float DeathAnimTime = 1.6f;
        public const float ClearFreezeTime = 1f;
        public const float ClearFlashTime = 2f;
        public const float GameOverTime = 3f;
        public const float FruitTime = 9.5f;
        public const float FruitScoreTime = 2f;

        public static readonly Vector2 PacStart = new Vector2(13.5f, 23f);
        public static readonly Vector2 FruitPos = new Vector2(13.5f, 17f);
        /// <summary>The spot just outside the ghost-house door.</summary>
        public static readonly Vector2 DoorOutside = new Vector2(13.5f, 11f);
        public const float HouseCenterY = 14f;
        const float EyesSpeed = 1.6f;
        const float HouseSpeed = 0.45f;

        public readonly Maze Maze = new Maze();
        public readonly PacManActor Pac = new PacManActor();
        public readonly Ghost[] Ghosts =
        {
            new Ghost(GhostId.Blinky, new Vector2(13.5f, 11f), Dir.Left, 13.5f, new Vector2Int(25, -3)),
            new Ghost(GhostId.Pinky, new Vector2(13.5f, 14f), Dir.Down, 13.5f, new Vector2Int(2, -3)),
            new Ghost(GhostId.Inky, new Vector2(11.5f, 14f), Dir.Up, 11.5f, new Vector2Int(27, 31)),
            new Ghost(GhostId.Clyde, new Vector2(15.5f, 14f), Dir.Up, 15.5f, new Vector2Int(0, 31)),
        };

        public LevelSpec Spec { get; private set; } = LevelSpec.For(1);
        public int Level { get; private set; } = 1;
        public int Score { get; private set; }
        public int HighScore { get; set; }
        public int Lives { get; private set; }
        public bool Demo { get; private set; }

        public Phase Phase { get; private set; } = Phase.Idle;
        /// <summary>Seconds since the current phase began.</summary>
        public float PhaseTime { get; private set; }
        /// <summary>True during the Ready phase that opens a new game (the longer intro).</summary>
        public bool IsIntro { get; private set; }

        public int DotsEaten { get; private set; }
        public float FrightTimer { get; private set; }
        public bool FruitActive { get; private set; }
        public float FruitTimer { get; private set; }
        public float FruitScoreTimer { get; private set; }
        public int LastFruitPoints { get; private set; }
        public int LastGhostPoints { get; private set; }
        public Ghost LastEatenGhost { get; private set; }

        /// <summary>Index into <see cref="LevelSpec.Waves"/>: even means scatter, odd means chase.</summary>
        public int WaveIndex { get; private set; }

        public event Action<GameEvent> Events;

        System.Random rng;
        float waveTimer;
        float phaseLength;
        float idleTimer;
        int ghostsEatenThisFright;
        bool globalCounterActive;
        int globalDots;
        bool elroySuspended;
        bool extraLifeAwarded;
        bool deathAnimStarted;

        public GameSim(int seed = 1234) => rng = new System.Random(seed);

        public Ghost Blinky => Ghosts[0];
        public bool IsChase => WaveIndex >= Spec.Waves.Length || (WaveIndex & 1) == 1;
        public bool IsFrightened => FrightTimer > 0f;

        /// <summary>True during the last <see cref="LevelSpec.FrightFlashes"/> flashes of the fright timer.</summary>
        public bool FrightFlashing => FrightTimer > 0f && FrightTimer < Spec.FrightFlashes * FlashPeriod;
        public const float FlashPeriod = 0.4f;

        public void StartGame(bool demo = false)
        {
            Demo = demo;
            Score = 0;
            Lives = StartLives;
            Level = 1;
            extraLifeAwarded = false;
            StartLevel(1);
            IsIntro = !demo;
            Enter(Phase.Ready, demo ? ReadyTime : IntroTime);
            Raise(GameEvent.GameStarted);
            Raise(GameEvent.ReadyStarted);
        }

        /// <summary>Back to a fresh, idle level 1 (used behind the title screen).</summary>
        public void Reset()
        {
            Demo = false;
            Score = 0;
            Lives = StartLives;
            IsIntro = false;
            StartLevel(1);
            Enter(Phase.Idle);
        }

        public void Tick(float dt)
        {
            PhaseTime += dt;
            if (FruitScoreTimer > 0f) FruitScoreTimer -= dt;

            switch (Phase)
            {
                case Phase.Ready:
                    if (PhaseTime >= phaseLength)
                    {
                        IsIntro = false;
                        Enter(Phase.Playing);
                        Raise(GameEvent.PlayStarted);
                    }
                    break;

                case Phase.Playing:
                    TickPlaying(dt);
                    break;

                case Phase.GhostEaten:
                    if (PhaseTime >= GhostEatenPauseTime)
                    {
                        LastEatenGhost = null;
                        Enter(Phase.Playing);
                    }
                    break;

                case Phase.Dying:
                    if (!deathAnimStarted && PhaseTime >= CaughtFreezeTime)
                    {
                        deathAnimStarted = true;
                        Raise(GameEvent.DeathAnimStarted);
                    }
                    if (PhaseTime >= CaughtFreezeTime + DeathAnimTime) LoseLife();
                    break;

                case Phase.LevelComplete:
                    if (PhaseTime >= ClearFreezeTime + ClearFlashTime)
                    {
                        Level++;
                        StartLevel(Level);
                        Enter(Phase.Ready, ReadyTime);
                        Raise(GameEvent.LevelStarted);
                        Raise(GameEvent.ReadyStarted);
                    }
                    break;

                case Phase.GameOver:
                    if (PhaseTime >= GameOverTime) Enter(Phase.Idle);
                    break;
            }
        }

        // ---------------------------------------------------------------- flow

        void StartLevel(int level)
        {
            Level = level;
            Spec = LevelSpec.For(level);
            Maze.Reset();
            DotsEaten = 0;
            globalCounterActive = false;
            elroySuspended = false;
            foreach (var g in Ghosts) g.DotCounter = 0;
            ResetActors();
        }

        void ResetActors()
        {
            Pac.Reset(PacStart);
            foreach (var g in Ghosts)
            {
                g.Pos = g.StartPos;
                g.Dir = g.StartDir;
                g.Frightened = false;
                g.State = g.Id == GhostId.Blinky ? GhostState.Active : GhostState.InHouse;
            }
            FrightTimer = 0f;
            WaveIndex = 0;
            waveTimer = 0f;
            idleTimer = 0f;
            FruitActive = false;
            LastEatenGhost = null;
        }

        void LoseLife()
        {
            Lives--;
            if (Lives <= 0)
            {
                Enter(Phase.GameOver);
                Raise(GameEvent.GameOver);
                return;
            }
            // After a death the house switches to one shared dot counter, and Elroy waits for Clyde to leave.
            globalCounterActive = true;
            globalDots = 0;
            elroySuspended = true;
            ResetActors();
            Enter(Phase.Ready, ReadyTime);
            Raise(GameEvent.ReadyStarted);
        }

        void Enter(Phase phase, float length = 0f)
        {
            Phase = phase;
            PhaseTime = 0f;
            phaseLength = length;
        }

        void Raise(GameEvent e) => Events?.Invoke(e);

        void AddScore(int points)
        {
            Score += points;
            if (!extraLifeAwarded && Score >= ExtraLifeScore)
            {
                extraLifeAwarded = true;
                Lives++;
                Raise(GameEvent.ExtraLife);
            }
            if (!Demo && Score > HighScore) HighScore = Score;
        }

        // ---------------------------------------------------------------- playing

        void TickPlaying(float dt)
        {
            if (FrightTimer > 0f)
            {
                FrightTimer -= dt;
                if (FrightTimer <= 0f) EndFright();
            }
            else
            {
                UpdateWaves(dt);
            }

            if (FruitActive)
            {
                FruitTimer -= dt;
                if (FruitTimer <= 0f)
                {
                    FruitActive = false;
                    Raise(GameEvent.FruitExpired);
                }
            }

            idleTimer += dt;
            UpdateHouseRelease();
            if (elroySuspended && Ghosts[3].State == GhostState.Active) elroySuspended = false;

            MovePac(dt);
            if (Phase != Phase.Playing) return;
            if (CheckCollisions()) return;

            foreach (var g in Ghosts) MoveGhost(g, dt);
            CheckCollisions();
        }

        void UpdateWaves(float dt)
        {
            if (WaveIndex >= Spec.Waves.Length) return;
            waveTimer += dt;
            if (waveTimer < Spec.Waves[WaveIndex]) return;
            waveTimer -= Spec.Waves[WaveIndex];
            WaveIndex++;
            foreach (var g in Ghosts)
                if (g.State == GhostState.Active) g.Dir = g.Dir.Opposite();
        }

        void StartFright()
        {
            ghostsEatenThisFright = 0;
            bool fright = Spec.FrightTime > 0f;
            foreach (var g in Ghosts)
            {
                if (g.IsEyes) continue;
                if (g.State == GhostState.Active) g.Dir = g.Dir.Opposite();
                g.Frightened = fright;
            }
            FrightTimer = Spec.FrightTime;
        }

        void EndFright()
        {
            FrightTimer = 0f;
            foreach (var g in Ghosts) g.Frightened = false;
        }

        bool CheckCollisions()
        {
            foreach (var g in Ghosts)
            {
                if (g.State != GhostState.Active) continue;
                if ((g.Pos - Pac.Pos).sqrMagnitude > 0.36f) continue;
                if (g.Frightened) EatGhost(g);
                else Caught();
                return true;
            }

            if (FruitActive && (Pac.Pos - FruitPos).sqrMagnitude < 0.36f)
            {
                FruitActive = false;
                LastFruitPoints = Spec.FruitPoints;
                FruitScoreTimer = FruitScoreTime;
                AddScore(LastFruitPoints);
                Raise(GameEvent.FruitEaten);
            }
            return false;
        }

        void EatGhost(Ghost g)
        {
            g.State = GhostState.Eaten;
            g.Frightened = false;
            LastGhostPoints = 200 << Mathf.Min(ghostsEatenThisFright, 3);
            ghostsEatenThisFright++;
            AddScore(LastGhostPoints);
            LastEatenGhost = g;
            Enter(Phase.GhostEaten);
            Raise(GameEvent.GhostEaten);
        }

        void Caught()
        {
            FruitActive = false;
            FrightTimer = 0f;
            deathAnimStarted = false;
            Enter(Phase.Dying);
            Raise(GameEvent.PacCaught);
        }

        // ---------------------------------------------------------------- Pac-Man

        void MovePac(float dt)
        {
            var p = Pac;
            if (p.Wanted != Dir.None && p.Wanted == p.Dir.Opposite()) p.Dir = p.Wanted;
            if (p.Dir == Dir.None && p.Wanted != Dir.None && Grid.AtCenter(p.Pos) && Maze.IsWalkable(p.Tile + p.Wanted.Vec()))
                p.Dir = p.Wanted;

            if (p.Stall > 0f)
            {
                p.Stall -= dt;
                if (p.Stall > 0f) return;
                dt = -p.Stall;
                p.Stall = 0f;
            }

            TryCorner(p);
            if (p.Dir != Dir.None) p.Facing = p.Dir;
            float speed = (IsFrightened ? Spec.PacFrightSpeed : Spec.PacSpeed) * LevelSpec.MaxSpeed;
            float dist = speed * dt;
            // After an early turn, glide back onto the lane centre while moving: the arcade's diagonal cornering.
            if (p.Dir != Dir.None)
            {
                bool horizontal = p.Dir.IsHorizontal();
                float off = horizontal ? p.Pos.y : p.Pos.x;
                float lane = Mathf.Round(off);
                float fix = Mathf.Min(Mathf.Abs(lane - off), dist);
                off = Mathf.Abs(lane - off) <= dist ? lane : off + Mathf.Sign(lane - off) * fix;
                if (horizontal) p.Pos.y = off;
                else p.Pos.x = off;
            }
            p.Distance += Advance(ref p.Pos, ref p.Dir, dist, DecidePac);
            if (p.Dir != Dir.None) p.Facing = p.Dir;

            EatAt(p.Tile);
        }

        /// <summary>How far from a tile centre (before or after it) Pac-Man may already take a buffered turn.</summary>
        public const float CornerWindow = 0.35f;

        void TryCorner(PacManActor p)
        {
            if (p.Dir == Dir.None || p.Wanted == Dir.None || p.Wanted == p.Dir || p.Wanted == p.Dir.Opposite()) return;
            var tile = p.Tile;
            float along = p.Dir.IsHorizontal() ? p.Pos.x - tile.x : p.Pos.y - tile.y;
            if (Mathf.Abs(along) <= CornerWindow && Maze.IsWalkable(tile + p.Wanted.Vec())) p.Dir = p.Wanted;
        }

        Dir DecidePac(Vector2Int tile, Dir dir)
        {
            if (Pac.Wanted != Dir.None && Maze.IsWalkable(tile + Pac.Wanted.Vec())) return Pac.Wanted;
            return Maze.IsWalkable(tile + dir.Vec()) ? dir : Dir.None;
        }

        void EatAt(Vector2Int tile)
        {
            var eaten = Maze.Eat(tile);
            if (eaten == Tile.Empty) return;

            DotsEaten++;
            idleTimer = 0f;
            CountDotForHouse();

            if (eaten == Tile.Dot)
            {
                AddScore(10);
                Pac.Stall = 1f / 60f;
                Raise(GameEvent.DotEaten);
            }
            else
            {
                AddScore(50);
                Pac.Stall = 3f / 60f;
                StartFright();
                Raise(GameEvent.EnergizerEaten);
            }

            if (DotsEaten == 70 || DotsEaten == 170)
            {
                FruitActive = true;
                FruitTimer = FruitTime;
                Raise(GameEvent.FruitAppeared);
            }

            if (Maze.PelletsLeft == 0)
            {
                EndFright();
                FruitActive = false;
                Enter(Phase.LevelComplete);
                Raise(GameEvent.LevelCleared);
            }
        }

        // ---------------------------------------------------------------- ghost house

        Ghost PreferredInHouse()
        {
            for (int i = 1; i < Ghosts.Length; i++)
                if (Ghosts[i].State == GhostState.InHouse) return Ghosts[i];
            return null;
        }

        int DotLimit(Ghost g)
        {
            switch (g.Id)
            {
                case GhostId.Inky: return Spec.InkyDotLimit;
                case GhostId.Clyde: return Spec.ClydeDotLimit;
                default: return 0;
            }
        }

        void CountDotForHouse()
        {
            if (!globalCounterActive)
            {
                var g = PreferredInHouse();
                if (g != null) g.DotCounter++;
                return;
            }

            globalDots++;
            if (globalDots == 7) Release(Ghosts[1]);
            else if (globalDots == 17) Release(Ghosts[2]);
            else if (globalDots == 32)
            {
                Release(Ghosts[3]);
                globalCounterActive = false;
            }
        }

        void UpdateHouseRelease()
        {
            var g = PreferredInHouse();
            if (g == null) return;
            if (!globalCounterActive && g.DotCounter >= DotLimit(g))
            {
                Release(g);
                return;
            }
            if (idleTimer >= Spec.IdleReleaseTime)
            {
                idleTimer = 0f;
                Release(g);
            }
        }

        static void Release(Ghost g)
        {
            if (g.State == GhostState.InHouse) g.State = GhostState.Leaving;
        }

        // ---------------------------------------------------------------- ghosts

        float GhostSpeed(Ghost g)
        {
            float s;
            switch (g.State)
            {
                case GhostState.Eaten:
                case GhostState.Entering:
                    s = EyesSpeed;
                    break;
                case GhostState.InHouse:
                case GhostState.Leaving:
                    s = HouseSpeed;
                    break;
                default:
                    if (Maze.IsTunnel(g.Tile)) s = Spec.GhostTunnelSpeed;
                    else if (g.Frightened) s = Spec.GhostFrightSpeed;
                    else if (IsElroy(g, Spec.Elroy2Dots)) s = Spec.Elroy2Speed;
                    else if (IsElroy(g, Spec.Elroy1Dots)) s = Spec.Elroy1Speed;
                    else s = Spec.GhostSpeed;
                    break;
            }
            return s * LevelSpec.MaxSpeed;
        }

        bool IsElroy(Ghost g, int dots) => g.Id == GhostId.Blinky && !elroySuspended && Maze.PelletsLeft <= dots;

        void MoveGhost(Ghost g, float dt)
        {
            float dist = GhostSpeed(g) * dt;
            switch (g.State)
            {
                case GhostState.InHouse:
                    Bob(g, dist);
                    break;

                case GhostState.Leaving:
                    // Settle on the centre row, slide to the door column, then rise out of the door.
                    bool atDoorColumn = Mathf.Abs(g.Pos.x - DoorOutside.x) < 1e-4f;
                    if ((atDoorColumn || (MoveAxis(g, false, HouseCenterY, ref dist) && MoveAxis(g, true, DoorOutside.x, ref dist)))
                        && MoveAxis(g, false, DoorOutside.y, ref dist))
                    {
                        g.State = GhostState.Active;
                        g.Dir = Dir.Left;
                    }
                    break;

                case GhostState.Entering:
                    if (MoveAxis(g, true, DoorOutside.x, ref dist) && MoveAxis(g, false, HouseCenterY, ref dist)
                        && MoveAxis(g, true, g.HouseX, ref dist))
                    {
                        g.State = GhostState.Leaving;
                        g.Frightened = false;
                    }
                    break;

                default:
                    g.Target = TargetFor(g);
                    Advance(ref g.Pos, ref g.Dir, dist, (tile, dir) => DecideGhost(g, tile, dir));
                    break;
            }
        }

        static void Bob(Ghost g, float dist)
        {
            if (g.Dir != Dir.Up && g.Dir != Dir.Down) g.Dir = Dir.Up;
            for (int i = 0; i < 2 && dist > 0f; i++)
            {
                float target = g.Dir == Dir.Up ? HouseCenterY - 0.5f : HouseCenterY + 0.5f;
                Dir keep = g.Dir;
                if (MoveAxis(g, false, target, ref dist)) g.Dir = keep.Opposite();
                else g.Dir = keep;
            }
        }

        /// <summary>Moves along one axis toward a coordinate. Returns true once it is reached.</summary>
        static bool MoveAxis(Ghost g, bool horizontal, float target, ref float dist)
        {
            float cur = horizontal ? g.Pos.x : g.Pos.y;
            float delta = target - cur;
            if (Mathf.Abs(delta) > 1e-4f)
            {
                g.Dir = horizontal ? (delta > 0f ? Dir.Right : Dir.Left) : (delta > 0f ? Dir.Down : Dir.Up);
                float step = Mathf.Min(Mathf.Abs(delta), dist);
                dist -= step;
                cur += Mathf.Sign(delta) * step;
                if (Mathf.Abs(target - cur) <= 1e-4f) cur = target;
                if (horizontal) g.Pos.x = cur;
                else g.Pos.y = cur;
            }
            return Mathf.Abs(target - cur) <= 1e-4f;
        }

        Vector2Int TargetFor(Ghost g)
        {
            if (g.State == GhostState.Eaten) return new Vector2Int(13, 11);
            if (IsChase || IsElroy(g, Spec.Elroy1Dots)) return ChaseTarget(g);
            return g.ScatterTile;
        }

        /// <summary>The four personalities: Blinky hunts, Pinky ambushes, Inky flanks off Blinky, Clyde keeps his distance.</summary>
        public Vector2Int ChaseTarget(Ghost g)
        {
            var p = Pac.Tile;
            switch (g.Id)
            {
                case GhostId.Pinky:
                    return Ahead(p, Pac.Facing, 4);
                case GhostId.Inky:
                    var pivot = Ahead(p, Pac.Facing, 2);
                    return pivot + (pivot - Blinky.Tile);
                case GhostId.Clyde:
                    return (g.Tile - p).sqrMagnitude > 64 ? p : g.ScatterTile;
                default:
                    return p;
            }
        }

        /// <summary>A point n tiles ahead of Pac-Man, including the arcade's overflow bug when facing up.</summary>
        static Vector2Int Ahead(Vector2Int p, Dir facing, int n)
        {
            var t = p + facing.Vec() * n;
            if (facing == Dir.Up) t.x -= n;
            return t;
        }

        Dir DecideGhost(Ghost g, Vector2Int tile, Dir current)
        {
            if (g.State == GhostState.Eaten && tile.y == 11 && (tile.x == 13 || tile.x == 14))
            {
                g.State = GhostState.Entering;
                return Dir.None;
            }

            Dir reverse = current.Opposite();
            if (g.Frightened)
            {
                int start = rng.Next(4);
                for (int i = 0; i < 4; i++)
                {
                    var d = DirExtensions.Priority[(start + i) & 3];
                    if (d != reverse && GhostCanEnter(tile + d.Vec())) return d;
                }
                return reverse;
            }

            bool noUp = g.State == GhostState.Active && Maze.IsNoUpTurnZone(tile);
            Dir best = Dir.None;
            int bestDist = int.MaxValue;
            foreach (var d in DirExtensions.Priority)
            {
                if (d == reverse || (noUp && d == Dir.Up)) continue;
                var next = tile + d.Vec();
                if (!GhostCanEnter(next)) continue;
                int dist = (next - g.Target).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = d;
                }
            }
            return best != Dir.None ? best : reverse;
        }

        bool GhostCanEnter(Vector2Int t) => Maze.IsWalkable(t);

        // ---------------------------------------------------------------- movement

        /// <summary>
        /// Moves along <paramref name="dir"/>, snapping to each tile centre it reaches and letting
        /// <paramref name="decide"/> pick the next direction there (None stops). Returns the distance moved.
        /// </summary>
        static float Advance(ref Vector2 pos, ref Dir dir, float dist, Func<Vector2Int, Dir, Dir> decide)
        {
            float moved = 0f;
            for (int guard = 0; dist > 1e-6f && dir != Dir.None && guard < 16; guard++)
            {
                bool horizontal = dir.IsHorizontal();
                var v = dir.Vec();
                int sign = horizontal ? v.x : v.y;
                float along = horizontal ? pos.x : pos.y;
                float next = sign > 0 ? Mathf.Floor(along) + 1f : Mathf.Ceil(along) - 1f;
                float toCenter = Mathf.Abs(next - along);
                bool reachesCenter = dist >= toCenter;
                float step = reachesCenter ? toCenter : dist;

                along = reachesCenter ? next : along + sign * step;
                if (horizontal) pos.x = along;
                else pos.y = along;
                moved += step;
                dist -= step;

                if (!reachesCenter) break;
                WrapTunnel(ref pos);
                dir = decide(Grid.TileOf(pos), dir);
            }
            return moved;
        }

        /// <summary>The tunnel loops through two hidden tiles beyond each edge; x = -2 and x = 29 are the same spot.</summary>
        static void WrapTunnel(ref Vector2 pos)
        {
            if (pos.x <= -2f) pos.x += 31f;
            else if (pos.x >= 29f) pos.x -= 31f;
        }
    }
}
