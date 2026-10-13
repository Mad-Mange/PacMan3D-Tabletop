using System.Collections.Generic;
using PacMan.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PacMan.Game
{
    /// <summary>
    /// The 3D tabletop maze: wall blocks built from the layout, the floor with its pellet sockets,
    /// the pellets themselves, the ghost-house door, pixel-art decals and the wooden table.
    /// Grid tile (x, y) sits at world (x, 0, -y), so the maze runs north (+z) from the bottom row.
    /// </summary>
    public sealed class Board
    {
        public const float WallHeight = 0.9f;
        /// <summary>How far a wall face sits inside its tile, so corridors look wider than one tile.</summary>
        const float Inset = 0.32f;
        const float HollowWall = 0.26f;
        const float RimOutset = 0.05f;
        const float RimInner = 0.05f;
        const float RimBottom = WallHeight - 0.13f;
        const float RimTop = WallHeight + 0.015f;
        public const float BoardMargin = 1.6f;

        public readonly Transform Root;
        public readonly Bounds PlayBounds;

        readonly Renderer[] pellets = new Renderer[Maze.Width * Maze.Height];
        readonly Tile[] shown = new Tile[Maze.Width * Maze.Height];
        readonly List<Transform> energizers = new List<Transform>();
        readonly List<Light> energizerLights = new List<Light>();
        readonly Material rimMaterial;
        readonly Color rimColor, rimEmission;
        readonly Renderer door;

        public static Vector3 ToWorld(Vector2 tile, float height = 0f) => new Vector3(tile.x, height, -tile.y);

        public Board(Transform parent)
        {
            Root = new GameObject("Board").transform;
            Root.SetParent(parent, false);

            rimMaterial = new Material(Art.Mat("WallRim")) { name = "WallRim (flash)" };
            rimColor = rimMaterial.GetColor("_BaseColor");
            rimEmission = rimMaterial.GetColor("_EmissionColor");

            BuildTable();
            BuildFloor();
            BuildWalls();
            BuildPellets();
            door = BuildDoor();

            PlayBounds = new Bounds(new Vector3(13.5f, WallHeight * 0.5f, -15f),
                new Vector3(Maze.Width + 0.6f, WallHeight, Maze.Height + 0.6f));
        }

        // ------------------------------------------------------------------ runtime

        /// <summary>Shows exactly the pellets still in the maze.</summary>
        public void SyncPellets(Maze maze)
        {
            for (int y = 0; y < Maze.Height; y++)
            for (int x = 0; x < Maze.Width; x++)
            {
                int i = y * Maze.Width + x;
                var r = pellets[i];
                if (r == null) continue;
                var t = maze.Get(x, y);
                if (t == shown[i]) continue;
                shown[i] = t;
                r.enabled = t == Tile.Dot || t == Tile.Energizer;
                var light = r.GetComponentInChildren<Light>();
                if (light != null) light.enabled = r.enabled;
            }
        }

        /// <summary>Energizers bob and pulse; when frozen (e.g. READY!) they hold still.</summary>
        public void AnimateEnergizers(float time, bool pulsing)
        {
            float s = pulsing ? 0.82f + 0.18f * Mathf.Sin(time * 9f) : 1f;
            for (int i = 0; i < energizers.Count; i++)
            {
                var t = energizers[i];
                t.localScale = Vector3.one * 0.56f * s;
                t.localPosition = new Vector3(t.localPosition.x, 0.32f + (pulsing ? 0.06f * Mathf.Sin(time * 3f + i) : 0f), t.localPosition.z);
                energizerLights[i].intensity = 1.6f * s;
            }
        }

        /// <summary>The end-of-level flash: the blue rims blink white.</summary>
        public void SetFlash(bool white)
        {
            rimMaterial.SetColor("_BaseColor", white ? Color.white : rimColor);
            rimMaterial.SetColor("_EmissionColor", white ? new Color(2.2f, 2.2f, 2.4f) : rimEmission);
        }

        public void SetDoorVisible(bool visible) => door.enabled = visible;

        // ------------------------------------------------------------------ table and floor

        void BuildTable()
        {
            var table = Art.Primitive(PrimitiveType.Quad, "Table", Root, Art.Mat("Table"), false);
            table.transform.localPosition = new Vector3(13.5f, -0.4f, -15f);
            table.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            table.transform.localScale = new Vector3(240f, 240f, 1f);
            table.GetComponent<MeshRenderer>().receiveShadows = true;

            // The black board the maze is mounted on.
            var mb = new MeshBuilder(1);
            var min = new Vector3(-0.5f - BoardMargin, -0.4f, -(Maze.Height - 0.5f) - BoardMargin);
            var max = new Vector3(Maze.Width - 0.5f + BoardMargin, -0.002f, 0.5f + BoardMargin);
            mb.Box(min, max, 0, 0);
            MakeRenderer("BoardBase", mb.ToMesh("BoardBase"), Art.Mat("BoardBase"));
        }

        void BuildFloor()
        {
            var mb = new MeshBuilder(1);
            float x0 = -0.5f, x1 = Maze.Width - 0.5f;
            float z0 = -(Maze.Height - 0.5f), z1 = 0.5f;
            // One floor texture tile covers 2x2 maze tiles, with a socket in the middle of each.
            mb.Quad(0,
                new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0),
                Vector3.up,
                new Vector2(0f, 0f), new Vector2(0f, Maze.Height / 2f), new Vector2(Maze.Width / 2f, Maze.Height / 2f), new Vector2(Maze.Width / 2f, 0f));
            var r = MakeRenderer("Floor", mb.ToMesh("Floor"), Art.Mat("Floor"));
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ walls

        struct Block
        {
            public RectInt Tiles;
            public float MinX, MaxX, MinZ, MaxZ;
            public bool Hollow;
        }

        /// <summary>
        /// Wall tiles plus the unreachable pockets outside the playfield, so the maze sits in a solid frame.
        /// </summary>
        static bool[,] SolidMap()
        {
            var reach = new bool[Maze.Width, Maze.Height];
            var queue = new Queue<Vector2Int>();
            var start = new Vector2Int(13, 23);
            reach[start.x, start.y] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in DirExtensions.Priority)
                {
                    var n = c + d.Vec();
                    if (n.y == Maze.TunnelRow) n.x = (n.x + Maze.Width) % Maze.Width;
                    if (!Maze.InBounds(n.x, n.y) || reach[n.x, n.y] || Maze.LayoutTile(n.x, n.y) == Tile.Wall) continue;
                    reach[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }

            var solid = new bool[Maze.Width, Maze.Height];
            for (int y = 0; y < Maze.Height; y++)
            for (int x = 0; x < Maze.Width; x++)
                solid[x, y] = !reach[x, y];
            return solid;
        }

        /// <summary>Greedy split of the solid tiles into rectangles, each turned into one wall block.</summary>
        static List<Block> Blocks()
        {
            var solid = SolidMap();
            var used = new bool[Maze.Width, Maze.Height];
            var blocks = new List<Block>();

            bool Solid(int x, int y) => Maze.InBounds(x, y) && solid[x, y];

            for (int y = 0; y < Maze.Height; y++)
            for (int x = 0; x < Maze.Width; x++)
            {
                if (!solid[x, y] || used[x, y]) continue;
                int w = 1;
                while (x + w < Maze.Width && solid[x + w, y] && !used[x + w, y]) w++;
                int h = 1;
                while (y + h < Maze.Height && RowFree(x, y + h, w)) h++;
                for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    used[xx, yy] = true;

                // A side facing any open tile is inset; a side buried in other wall reaches into its neighbour.
                bool openW = false, openE = false, openN = false, openS = false;
                for (int yy = y; yy < y + h; yy++)
                {
                    openW |= !Solid(x - 1, yy);
                    openE |= !Solid(x + w, yy);
                }
                for (int xx = x; xx < x + w; xx++)
                {
                    openN |= !Solid(xx, y - 1);
                    openS |= !Solid(xx, y + h);
                }
                const float reach = Inset + 0.02f;
                var b = new Block
                {
                    Tiles = new RectInt(x, y, w, h),
                    MinX = x - 0.5f + (openW ? Inset : -reach),
                    MaxX = x + w - 0.5f - (openE ? Inset : -reach),
                    // Rows grow toward -z: the north side (row y) is the larger z.
                    MaxZ = -(y - 0.5f) - (openN ? Inset : -reach),
                    MinZ = -(y + h - 0.5f) + (openS ? Inset : -reach),
                    Hollow = w >= 3 && h >= 3,
                };
                blocks.Add(b);
            }
            return blocks;

            bool RowFree(int x0, int yy, int w)
            {
                for (int xx = x0; xx < x0 + w; xx++)
                    if (!solid[xx, yy] || used[xx, yy]) return false;
                return true;
            }
        }

        void BuildWalls()
        {
            // Submeshes: 0 sides (glitter + LED strip), 1 tops, 2 blue rims.
            var mb = new MeshBuilder(3);
            var blocks = Blocks();
            foreach (var b in blocks)
            {
                if (b.Hollow) HollowBlock(mb, b);
                else SolidBlock(mb, b);
            }
            var walls = MakeRenderer("Walls", mb.ToMesh("Walls"), Art.Mat("WallSide"), Art.Mat("WallTop"), rimMaterial);
            walls.shadowCastingMode = ShadowCastingMode.On;

            BuildDecals(blocks);
        }

        static void SolidBlock(MeshBuilder mb, Block b)
        {
            mb.Box(new Vector3(b.MinX, 0f, b.MinZ), new Vector3(b.MaxX, WallHeight, b.MaxZ), 0, 1);
            Rim(mb, b.MinX, b.MaxX, b.MinZ, b.MaxZ);
        }

        static void HollowBlock(MeshBuilder mb, Block b)
        {
            float t = HollowWall;
            // Four walls around an open-topped box, like the planters in the reference photo.
            mb.Box(new Vector3(b.MinX, 0f, b.MaxZ - t), new Vector3(b.MaxX, WallHeight, b.MaxZ), 0, 2);
            mb.Box(new Vector3(b.MinX, 0f, b.MinZ), new Vector3(b.MaxX, WallHeight, b.MinZ + t), 0, 2);
            mb.Box(new Vector3(b.MinX, 0f, b.MinZ + t), new Vector3(b.MinX + t, WallHeight, b.MaxZ - t), 0, 2);
            mb.Box(new Vector3(b.MaxX - t, 0f, b.MinZ + t), new Vector3(b.MaxX, WallHeight, b.MaxZ - t), 0, 2);
            Rim(mb, b.MinX, b.MaxX, b.MinZ, b.MaxZ);
            // A dark floor inside the box hides the sockets.
            mb.Box(new Vector3(b.MinX + t, 0f, b.MinZ + t), new Vector3(b.MaxX - t, 0.06f, b.MaxZ - t), 1, 1);
        }

        static void Rim(MeshBuilder mb, float minX, float maxX, float minZ, float maxZ)
        {
            mb.Box(new Vector3(minX - RimOutset, RimBottom, minZ - RimOutset),
                new Vector3(maxX + RimOutset, RimTop, maxZ + RimOutset), 2, 2, false);
            // A blue frame on top, so from above every block is outlined like the arcade maze.
            float o = RimOutset, i = RimInner;
            mb.TopRect(2, minX - o, maxX + o, minZ - o, minZ + i, RimTop);
            mb.TopRect(2, minX - o, maxX + o, maxZ - i, maxZ + o, RimTop);
            mb.TopRect(2, minX - o, minX + i, minZ + i, maxZ - i, RimTop);
            mb.TopRect(2, maxX - i, maxX + o, minZ + i, maxZ - i, RimTop);
        }

        // ------------------------------------------------------------------ decals

        /// <summary>Pixel-art Pac-Man and ghost plaques on some of the long walls, as on the printed maze.</summary>
        void BuildDecals(List<Block> blocks)
        {
            var mb = new MeshBuilder(1);
            var rng = new System.Random(80);
            const int icons = 6;
            const float size = 0.44f, y = 0.39f, lift = 0.004f;
            int placed = 0;
            foreach (var b in blocks)
            {
                if (b.Hollow && rng.NextDouble() < 0.5) continue;
                float lenX = b.MaxX - b.MinX, lenZ = b.MaxZ - b.MinZ;
                // South faces look at the camera, so they get most of the plaques.
                if (lenX >= 2.4f && rng.NextDouble() < 0.75)
                {
                    int count = Mathf.Max(1, Mathf.FloorToInt(lenX / 2.6f));
                    for (int i = 0; i < count; i++)
                    {
                        float cx = Mathf.Lerp(b.MinX, b.MaxX, (i + 0.5f) / count);
                        DecalQuad(mb, new Vector3(cx, y, b.MinZ - lift), Vector3.right, Vector3.back, size, rng.Next(icons), icons);
                        placed++;
                    }
                }
                if (lenZ >= 3.4f && rng.NextDouble() < 0.5)
                {
                    float cz = (b.MinZ + b.MaxZ) * 0.5f;
                    bool east = b.MaxX < 13.5f;
                    var pos = new Vector3(east ? b.MaxX + lift : b.MinX - lift, y, cz);
                    DecalQuad(mb, pos, east ? Vector3.forward : Vector3.back, east ? Vector3.right : Vector3.left, size, rng.Next(icons), icons);
                    placed++;
                }
            }
            if (placed == 0) return;
            var r = MakeRenderer("Decals", mb.ToMesh("Decals"), Art.Mat("Decal"));
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void DecalQuad(MeshBuilder mb, Vector3 center, Vector3 right, Vector3 normal, float size, int icon, int icons)
        {
            var up = Vector3.up * size * 0.5f;
            var r = right * size * 0.5f;
            float u0 = (float)icon / icons, u1 = (float)(icon + 1) / icons;
            mb.Quad(0, center - r - up, center - r + up, center + r + up, center + r - up, normal,
                new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f));
        }

        // ------------------------------------------------------------------ pellets and door

        void BuildPellets()
        {
            var parent = new GameObject("Pellets").transform;
            parent.SetParent(Root, false);
            var dotMat = Art.Mat("Dot");
            var energizerMat = Art.Mat("Energizer");
            for (int y = 0; y < Maze.Height; y++)
            for (int x = 0; x < Maze.Width; x++)
            {
                var t = Maze.LayoutTile(x, y);
                if (t != Tile.Dot && t != Tile.Energizer) continue;
                GameObject go;
                if (t == Tile.Dot)
                {
                    go = Art.Primitive(PrimitiveType.Cube, $"Dot {x},{y}", parent, dotMat, false);
                    go.transform.localPosition = ToWorld(new Vector2(x, y), 0.03f);
                    go.transform.localScale = new Vector3(0.2f, 0.04f, 0.2f);
                }
                else
                {
                    go = Art.Primitive(PrimitiveType.Sphere, $"Energizer {x},{y}", parent, energizerMat, false);
                    go.transform.localPosition = ToWorld(new Vector2(x, y), 0.32f);
                    go.transform.localScale = Vector3.one * 0.5f;
                    energizers.Add(go.transform);
                    var light = new GameObject("Glow").AddComponent<Light>();
                    light.transform.SetParent(go.transform, false);
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.85f, 0.3f);
                    light.range = 2.2f;
                    light.intensity = 1.6f;
                    light.shadows = LightShadows.None;
                    energizerLights.Add(light);
                }
                int i = y * Maze.Width + x;
                pellets[i] = go.GetComponent<Renderer>();
                shown[i] = t;
            }
        }

        Renderer BuildDoor()
        {
            var go = Art.Primitive(PrimitiveType.Cube, "GhostDoor", Root, Art.Mat("Door"), false);
            go.transform.localPosition = new Vector3(13.5f, 0.3f, -12f);
            go.transform.localScale = new Vector3(2f * (1f + Inset), 0.12f, 0.1f);
            return go.GetComponent<Renderer>();
        }

        MeshRenderer MakeRenderer(string name, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            return r;
        }
    }
}
