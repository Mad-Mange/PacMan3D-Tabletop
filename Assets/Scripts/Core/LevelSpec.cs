using UnityEngine;

namespace PacMan.Core
{
    public enum Fruit { Cherry, Strawberry, Orange, Apple, Melon, Galaxian, Bell, Key }

    /// <summary>
    /// Per-level difficulty, taken from the arcade's tables. Speeds are fractions of
    /// <see cref="MaxSpeed"/>, the arcade's 100% speed.
    /// </summary>
    public sealed class LevelSpec
    {
        /// <summary>The arcade's 100% speed (75.76 px/s at 8 px per tile), in tiles per second.</summary>
        public const float MaxSpeed = 75.75757625f / 8f;

        public int Level;
        public Fruit Fruit;
        public int FruitPoints;

        public float PacSpeed, PacFrightSpeed;
        public float GhostSpeed, GhostTunnelSpeed, GhostFrightSpeed;

        /// <summary>Blinky turns into "Cruise Elroy" (faster, and chasing during scatter) at these dot counts.</summary>
        public int Elroy1Dots, Elroy2Dots;
        public float Elroy1Speed, Elroy2Speed;

        public float FrightTime;
        public int FrightFlashes;

        /// <summary>Alternating scatter/chase durations in seconds, starting with scatter. Chase lasts forever after the last entry.</summary>
        public float[] Waves;

        public int InkyDotLimit, ClydeDotLimit;

        /// <summary>A ghost is pushed out of the house when Pac-Man goes this long without eating.</summary>
        public float IdleReleaseTime;

        static readonly Fruit[] FruitByLevel =
        {
            Fruit.Cherry, Fruit.Strawberry, Fruit.Orange, Fruit.Orange, Fruit.Apple, Fruit.Apple,
            Fruit.Melon, Fruit.Melon, Fruit.Galaxian, Fruit.Galaxian, Fruit.Bell, Fruit.Bell, Fruit.Key,
        };

        static readonly int[] FruitPointTable = { 100, 300, 500, 700, 1000, 2000, 3000, 5000 };
        static readonly float[] FrightSeconds = { 6, 5, 4, 3, 2, 5, 2, 2, 1, 5, 2, 1, 1, 3, 1, 1, 0, 1 };
        static readonly int[] FrightFlashTable = { 5, 5, 5, 5, 5, 5, 5, 5, 3, 5, 5, 3, 3, 5, 3, 3, 0, 3 };
        static readonly int[] Elroy1Table = { 20, 30, 40, 40, 40, 50, 50, 50, 60, 60, 60, 80, 80, 80, 100, 100, 100, 100, 120 };

        public static Fruit FruitFor(int level) => FruitByLevel[Mathf.Clamp(level, 1, FruitByLevel.Length) - 1];

        public static int PointsFor(Fruit fruit) => FruitPointTable[(int)fruit];

        public static LevelSpec For(int level)
        {
            level = Mathf.Max(1, level);
            int band = level == 1 ? 0 : level <= 4 ? 1 : 2;
            var s = new LevelSpec { Level = level, Fruit = FruitFor(level) };
            s.FruitPoints = PointsFor(s.Fruit);

            s.PacSpeed = band == 0 ? 0.80f : band == 1 ? 0.90f : level <= 20 ? 1.00f : 0.90f;
            s.PacFrightSpeed = band == 0 ? 0.90f : band == 1 ? 0.95f : 1.00f;
            s.GhostSpeed = band == 0 ? 0.75f : band == 1 ? 0.85f : 0.95f;
            s.GhostTunnelSpeed = band == 0 ? 0.40f : band == 1 ? 0.45f : 0.50f;
            s.GhostFrightSpeed = band == 0 ? 0.50f : band == 1 ? 0.55f : 0.60f;

            s.Elroy1Dots = Elroy1Table[Mathf.Min(level, Elroy1Table.Length) - 1];
            s.Elroy2Dots = s.Elroy1Dots / 2;
            s.Elroy1Speed = band == 0 ? 0.80f : band == 1 ? 0.90f : 1.00f;
            s.Elroy2Speed = band == 0 ? 0.85f : band == 1 ? 0.95f : 1.05f;

            bool inTable = level <= FrightSeconds.Length;
            s.FrightTime = inTable ? FrightSeconds[level - 1] : 0f;
            s.FrightFlashes = inTable ? FrightFlashTable[level - 1] : 0;

            s.Waves = band == 0
                ? new[] { 7f, 20f, 7f, 20f, 5f, 20f, 5f }
                : band == 1
                    ? new[] { 7f, 20f, 7f, 20f, 5f, 1033f, 1f / 60f }
                    : new[] { 5f, 20f, 5f, 20f, 5f, 1037f, 1f / 60f };

            s.InkyDotLimit = level == 1 ? 30 : 0;
            s.ClydeDotLimit = level == 1 ? 60 : level == 2 ? 50 : 0;
            s.IdleReleaseTime = level <= 4 ? 4f : 3f;
            return s;
        }
    }
}
