using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PacMan.Game
{
    /// <summary>Collects flat-shaded quads into a mesh with several submeshes (one per material).</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] tris;

        public MeshBuilder(int submeshes)
        {
            tris = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
        }

        /// <summary>A quad given clockwise as seen from the side its normal points to.</summary>
        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int k = 0; k < 4; k++) normals.Add(normal);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            var t = tris[sub];
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        /// <summary>An upward-facing rectangle at height y, UV-mapped in world units.</summary>
        public void TopRect(int sub, float x0, float x1, float z0, float z1, float y)
        {
            Quad(sub, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up,
                new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));
        }

        /// <summary>
        /// An axis-aligned box without a bottom. Side UVs run along the face in world units with v = height,
        /// so a 1x1 texture lines up on every wall; the top is mapped in world x/z.
        /// </summary>
        public void Box(Vector3 min, Vector3 max, int sideSub, int topSub, bool top = true)
        {
            float x0 = min.x, x1 = max.x, y0 = min.y, y1 = max.y, z0 = min.z, z1 = max.z;
            // South (-z)
            Quad(sideSub, new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back,
                new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0));
            // North (+z)
            Quad(sideSub, new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.forward,
                new Vector2(-x1, y0), new Vector2(-x1, y1), new Vector2(-x0, y1), new Vector2(-x0, y0));
            // East (+x)
            Quad(sideSub, new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right,
                new Vector2(z0, y0), new Vector2(z0, y1), new Vector2(z1, y1), new Vector2(z1, y0));
            // West (-x)
            Quad(sideSub, new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left,
                new Vector2(-z1, y0), new Vector2(-z1, y1), new Vector2(-z0, y1), new Vector2(-z0, y0));
            if (top) TopRect(topSub, x0, x1, z0, z1, y1);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
