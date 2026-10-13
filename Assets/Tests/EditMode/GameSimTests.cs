using System.Collections.Generic;
using NUnit.Framework;
using PacMan.Core;
using UnityEngine;

namespace PacMan.Tests
{
    public class GameSimTests
    {
        const float Step = 1f / 120f;

        static GameSim StartPlaying()
        {
            var sim = new GameSim();
            sim.StartGame();
            Run(sim, GameSim.IntroTime + Step);
            Assert.AreEqual(Phase.Playing, sim.Phase);
            return sim;
        }

        static void Run(GameSim sim, float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) sim.Tick(Step);
        }

        /// <summary>Keeps every ghost except <paramref name="keep"/> harmlessly inside the house.</summary>
        static void PinOthersInHouse(GameSim sim, Ghost keep)
        {
            foreach (var g in sim.Ghosts)
            {
                if (g == keep) continue;
                g.State = GhostState.InHouse;
                g.Pos = new Vector2(g.HouseX, GameSim.HouseCenterY);
            }
        }

        static void Park(Ghost g, Vector2 pos)
        {
            g.State = GhostState.Active;
            g.Pos = pos;
            g.Dir = Dir.Left;
        }

        [Test]
        public void MazeHas240DotsAnd4Energizers()
        {
            int dots = 0, energizers = 0;
            for (int y = 0; y < Maze.Height; y++)
            {
                Assert.AreEqual(Maze.Width, Maze.Layout[y].Length, $"row {y}");
                for (int x = 0; x < Maze.Width; x++)
                {
                    var t = Maze.LayoutTile(x, y);
                    if (t == Tile.Dot) dots++;
                    if (t == Tile.Energizer) energizers++;
                }
            }
            Assert.AreEqual(240, dots);
            Assert.AreEqual(4, energizers);
            Assert.AreEqual(244, new Maze().PelletsLeft);
        }

        [Test]
        public void EveryPelletIsReachableFromPacManStart()
        {
            var maze = new Maze();
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var start = new Vector2Int(13, 23);
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in DirExtensions.Priority)
                {
                    var n = c + d.Vec();
                    if (n.y == Maze.TunnelRow) n.x = (n.x + Maze.Width) % Maze.Width;
                    if (!maze.IsWalkable(n) || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }

            for (int y = 0; y < Maze.Height; y++)
            for (int x = 0; x < Maze.Width; x++)
            {
                var t = Maze.LayoutTile(x, y);
                if (t == Tile.Dot || t == Tile.Energizer)
                    Assert.IsTrue(seen.Contains(new Vector2Int(x, y)), $"pellet at {x},{y} unreachable");
            }
        }

        [Test]
        public void PacManStartsMovingLeftAndEatsDots()
        {
            var sim = StartPlaying();
            Run(sim, 1f);
            Assert.Less(sim.Pac.Pos.x, GameSim.PacStart.x);
            Assert.AreEqual(GameSim.PacStart.y, sim.Pac.Pos.y);
            Assert.Greater(sim.Score, 0);
            Assert.AreEqual(sim.Score, sim.DotsEaten * 10);
        }

        [Test]
        public void PacManStopsAtWall()
        {
            var sim = StartPlaying();
            // Heading down column 1, the wall at (1, 30) stops Pac-Man on the centre of (1, 29).
            sim.Pac.Pos = new Vector2(1f, 28.5f);
            sim.Pac.Dir = Dir.Down;
            Run(sim, 0.5f);
            Assert.AreEqual(new Vector2(1f, 29f), sim.Pac.Pos);
            Assert.AreEqual(Dir.None, sim.Pac.Dir);
        }

        [Test]
        public void BufferedTurnIsTakenAtNextOpening()
        {
            var sim = StartPlaying();
            sim.Pac.Wanted = Dir.Up;
            Run(sim, 2f);
            // Moving left from (13.5, 23), the first opening upward is at column 12.
            Assert.AreEqual(Dir.Up, sim.Pac.Facing);
            Assert.AreEqual(12f, sim.Pac.Pos.x);
            Assert.Less(sim.Pac.Pos.y, 23f);
        }

        [Test]
        public void CorneringTakesTheTurnBeforeTheTileCentre()
        {
            var sim = StartPlaying();
            sim.Pac.Wanted = Dir.Up;
            float turnedAt = float.NaN;
            for (int i = 0; i < 120 && float.IsNaN(turnedAt); i++)
            {
                sim.Tick(Step);
                if (sim.Pac.Dir == Dir.Up) turnedAt = sim.Pac.Pos.x;
            }
            Assert.Greater(turnedAt, 12f, "turned early, before reaching the centre of column 12");
            Assert.LessOrEqual(turnedAt, 12f + GameSim.CornerWindow + 0.07f);
            Run(sim, 0.3f);
            Assert.AreEqual(12f, sim.Pac.Pos.x, "glided back onto the lane");
        }

        [Test]
        public void TunnelWrapsAround()
        {
            var sim = StartPlaying();
            sim.Pac.Pos = new Vector2(0f, Maze.TunnelRow);
            sim.Pac.Dir = Dir.Left;
            for (float t = 0f; t < 0.6f; t += Step)
            {
                PinOthersInHouse(sim, null);
                sim.Tick(Step);
            }
            Assert.Greater(sim.Pac.Pos.x, 20f);
            Assert.AreEqual(Dir.Left, sim.Pac.Dir);
        }

        [Test]
        public void PinkyAimsFourTilesAheadWithUpOverflowBug()
        {
            var sim = StartPlaying();
            sim.Pac.Pos = new Vector2(10f, 20f);
            sim.Pac.Facing = Dir.Right;
            Assert.AreEqual(new Vector2Int(14, 20), sim.ChaseTarget(sim.Ghosts[1]));
            sim.Pac.Facing = Dir.Up;
            Assert.AreEqual(new Vector2Int(6, 16), sim.ChaseTarget(sim.Ghosts[1]));
        }

        [Test]
        public void InkyDoublesTheVectorFromBlinky()
        {
            var sim = StartPlaying();
            sim.Pac.Pos = new Vector2(10f, 20f);
            sim.Pac.Facing = Dir.Left;
            sim.Ghosts[0].Pos = new Vector2(12f, 26f);
            // Pivot (8, 20); Blinky (12, 26) -> target (4, 14).
            Assert.AreEqual(new Vector2Int(4, 14), sim.ChaseTarget(sim.Ghosts[2]));
        }

        [Test]
        public void ClydeRetreatsWhenClose()
        {
            var sim = StartPlaying();
            var clyde = sim.Ghosts[3];
            sim.Pac.Pos = new Vector2(6f, 20f);
            clyde.Pos = new Vector2(6f, 26f);
            Assert.AreEqual(clyde.ScatterTile, sim.ChaseTarget(clyde));
            clyde.Pos = new Vector2(26f, 1f);
            Assert.AreEqual(new Vector2Int(6, 20), sim.ChaseTarget(clyde));
        }

        [Test]
        public void GhostsLeaveTheHouse()
        {
            var sim = StartPlaying();
            Run(sim, 3f);
            Assert.AreEqual(GhostState.Active, sim.Ghosts[1].State, "Pinky leaves at once");
            Assert.Less(sim.Ghosts[1].Pos.y, 11.01f);
        }

        [Test]
        public void EnergizerLetsPacManEatGhostsForDoublingPoints()
        {
            var sim = StartPlaying();
            // Eat the dot at (2, 23) and the energizer at (1, 23), then stop against the wall.
            sim.Pac.Pos = new Vector2(2f, 23f);
            sim.Pac.Dir = Dir.Left;
            Run(sim, 0.4f);
            Assert.IsTrue(sim.IsFrightened);
            Assert.AreEqual(new Vector2(1f, 23f), sim.Pac.Pos);
            int before = sim.Score;

            Park(sim.Ghosts[0], sim.Pac.Pos);
            sim.Ghosts[0].Frightened = true;
            Run(sim, Step);
            Assert.AreEqual(Phase.GhostEaten, sim.Phase);
            Assert.AreEqual(200, sim.LastGhostPoints);
            Assert.AreEqual(GhostState.Eaten, sim.Ghosts[0].State);
            Run(sim, GameSim.GhostEatenPauseTime + Step);

            Park(sim.Ghosts[1], sim.Pac.Pos);
            sim.Ghosts[1].Frightened = true;
            Run(sim, Step);
            Assert.AreEqual(400, sim.LastGhostPoints);
            Assert.AreEqual(before + 600, sim.Score);
        }

        [Test]
        public void EatenGhostReturnsHomeAndComesBack()
        {
            var sim = StartPlaying();
            var blinky = sim.Ghosts[0];
            blinky.State = GhostState.Eaten;
            blinky.Pos = new Vector2(1f, 29f);
            blinky.Dir = Dir.Right;
            bool entered = false;
            for (int i = 0; i < 120 * 15 && blinky.State != GhostState.Active; i++)
            {
                PinOthersInHouse(sim, blinky);
                sim.Tick(Step);
                entered |= blinky.State == GhostState.Entering;
            }
            Assert.IsTrue(entered);
            Assert.AreEqual(GhostState.Active, blinky.State);
            Assert.IsFalse(blinky.Frightened);
        }

        [Test]
        public void TouchingADangerousGhostCostsALife()
        {
            var sim = StartPlaying();
            Park(sim.Ghosts[0], sim.Pac.Pos);
            Run(sim, Step);
            Assert.AreEqual(Phase.Dying, sim.Phase);
            Run(sim, GameSim.CaughtFreezeTime + GameSim.DeathAnimTime + Step);
            Assert.AreEqual(GameSim.StartLives - 1, sim.Lives);
            Assert.AreEqual(Phase.Ready, sim.Phase);
            Assert.AreEqual(GameSim.PacStart, sim.Pac.Pos);
        }

        [Test]
        public void LosingAllLivesEndsTheGame()
        {
            var sim = StartPlaying();
            for (int i = 0; i < GameSim.StartLives; i++)
            {
                Run(sim, GameSim.ReadyTime + Step);
                Park(sim.Ghosts[0], sim.Pac.Pos);
                Run(sim, Step);
                Run(sim, GameSim.CaughtFreezeTime + GameSim.DeathAnimTime + Step);
            }
            Assert.AreEqual(Phase.GameOver, sim.Phase);
            Run(sim, GameSim.GameOverTime + Step);
            Assert.AreEqual(Phase.Idle, sim.Phase);
        }

        [Test]
        public void ScatterChaseWavesFollowTheTable()
        {
            var sim = StartPlaying();
            Assert.IsFalse(sim.IsChase);
            for (float t = 0f; t < 7.1f; t += Step)
            {
                PinOthersInHouse(sim, null);
                sim.Tick(Step);
            }
            Assert.IsTrue(sim.IsChase);
        }

        [Test]
        public void AutopilotMakesSteadyProgress()
        {
            var sim = new GameSim(7);
            var pilot = new Autopilot();
            sim.StartGame(demo: true);
            int maxDots = 0;
            for (int i = 0; i < 120 * 60 && sim.Phase != Phase.GameOver && sim.Level == 1; i++)
            {
                if (sim.Phase == Phase.Playing) sim.Pac.Wanted = pilot.Choose(sim);
                sim.Tick(Step);
                maxDots = Mathf.Max(maxDots, sim.DotsEaten);
            }
            Assert.Greater(maxDots, 120, "the demo AI should clear a good part of the maze");
            Assert.AreEqual(0, sim.HighScore, "demo games never set a high score");
        }

        [Test]
        public void LevelSpecsGetHarder()
        {
            var l1 = LevelSpec.For(1);
            var l5 = LevelSpec.For(5);
            Assert.AreEqual(Fruit.Cherry, l1.Fruit);
            Assert.AreEqual(100, l1.FruitPoints);
            Assert.Greater(l5.GhostSpeed, l1.GhostSpeed);
            Assert.Less(l5.FrightTime, l1.FrightTime);
            Assert.AreEqual(Fruit.Key, LevelSpec.For(30).Fruit);
            Assert.AreEqual(0f, LevelSpec.For(30).FrightTime);
        }
    }
}
