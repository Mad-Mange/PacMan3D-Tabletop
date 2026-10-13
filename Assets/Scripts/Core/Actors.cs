using UnityEngine;

namespace PacMan.Core
{
    public enum GhostId { Blinky, Pinky, Inky, Clyde }

    /// <summary>
    /// InHouse: bobbing inside the ghost house. Leaving: on the way out of the door.
    /// Active: roaming the maze. Eaten: eyes heading back to the door. Entering: eyes sinking into the house.
    /// </summary>
    public enum GhostState { InHouse, Leaving, Active, Eaten, Entering }

    public static class Grid
    {
        /// <summary>The tile whose centre is nearest to a point (halves round up, consistently).</summary>
        public static Vector2Int TileOf(Vector2 p) =>
            new Vector2Int(Mathf.FloorToInt(p.x + 0.5f), Mathf.FloorToInt(p.y + 0.5f));

        public static bool AtCenter(Vector2 p) =>
            Mathf.Abs(p.x - Mathf.Round(p.x)) < 1e-4f && Mathf.Abs(p.y - Mathf.Round(p.y)) < 1e-4f;
    }

    public sealed class PacManActor
    {
        public Vector2 Pos;
        public Dir Dir;
        /// <summary>The last direction moved in. Stays put while Pac-Man is stopped against a wall.</summary>
        public Dir Facing;
        /// <summary>Buffered input: taken at the next tile centre where it is open.</summary>
        public Dir Wanted;
        /// <summary>Remaining pause after eating (the arcade loses a frame per dot, three per energizer).</summary>
        public float Stall;
        /// <summary>Total distance travelled, drives the chomp animation.</summary>
        public float Distance;

        public Vector2Int Tile => Grid.TileOf(Pos);
        public bool IsMoving => Dir != Dir.None && Stall <= 0f;

        public void Reset(Vector2 start)
        {
            Pos = start;
            Dir = Dir.Left;
            Facing = Dir.Left;
            Wanted = Dir.None;
            Stall = 0f;
        }
    }

    public sealed class Ghost
    {
        public readonly GhostId Id;
        public readonly Vector2 StartPos;
        public readonly Dir StartDir;
        /// <summary>Column the ghost rests at inside the house.</summary>
        public readonly float HouseX;
        public readonly Vector2Int ScatterTile;

        public Vector2 Pos;
        public Dir Dir;
        public GhostState State;
        public bool Frightened;
        public int DotCounter;
        public Vector2Int Target;

        public Ghost(GhostId id, Vector2 startPos, Dir startDir, float houseX, Vector2Int scatterTile)
        {
            Id = id;
            StartPos = startPos;
            StartDir = startDir;
            HouseX = houseX;
            ScatterTile = scatterTile;
        }

        public Vector2Int Tile => Grid.TileOf(Pos);

        /// <summary>Touching this ghost kills Pac-Man.</summary>
        public bool IsDangerous => State == GhostState.Active && !Frightened;

        /// <summary>Only the eyes are drawn.</summary>
        public bool IsEyes => State == GhostState.Eaten || State == GhostState.Entering;
    }
}
