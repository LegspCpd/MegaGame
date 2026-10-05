using System.Collections.Generic;
using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Builds meshes at runtime from vertex data, following the order the Unity
    /// docs require (vertices, then UVs, then triangles, then RecalculateNormals
    /// / RecalculateBounds). Used to replace the blockout look: buildings,
    /// weapons and props get real silhouettes instead of scaled cubes.
    /// </summary>
    public static class MeshBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _tris = new List<int>();

        public MeshBuilder()
        {
        }

        public int VertexCount => _verts.Count;

        public void Clear()
        {
            _verts.Clear();
            _uvs.Clear();
            _tris.Clear();
        }

        /// <summary>Adds a quad from four corners (counter-clockwise winding).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale = 1f)
        {
            int i = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(uvScale, 0f));
            _uvs.Add(new Vector2(uvScale, uvScale));
            _uvs.Add(new Vector2(0f, uvScale));

            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
        }

        /// <summary>Axis-aligned box; faces pointing away from the box are skipped when hollow.</summary>
        public void AddBox(Vector3 center, Vector3 size, float uvScale = 1f, bool skipTopBottom = false)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);

            // -Z (back)
            AddQuad(p001, p101, p111, p011, uvScale);
            // +Z (front)
            AddQuad(p100, p000, p010, p110, uvScale);
            // -X (left)
            AddQuad(p000, p001, p011, p010, uvScale);
            // +X (right)
            AddQuad(p101, p100, p110, p111, uvScale);

            if (!skipTopBottom)
            {
                AddQuad(p010, p011, p111, p110, uvScale); // +Y
                AddQuad(p000, p100, p101, p001, uvScale); // -Y
            }
        }

        /// <summary>
        /// Tapered tower: a box whose top face is inset and optionally rotated,
        /// which reads as a building with a setback rather than a plain block.
        /// </summary>
        public void AddSetbackTower(Vector3 baseCenter, float width, float depth,
                                    float lowerHeight, float upperHeight,
                                    float topInset, float twistDegrees, float uvScale = 1f)
        {
            float hw = width * 0.5f, hd = depth * 0.5f;
            Vector3 c = baseCenter;

            Vector3 b00 = c + new Vector3(-hw, 0f, -hd);
            Vector3 b10 = c + new Vector3(hw, 0f, -hd);
            Vector3 b11 = c + new Vector3(hw, 0f, hd);
            Vector3 b01 = c + new Vector3(-hw, 0f, hd);

            float ty = lowerHeight;
            Vector3 m00 = b00 + Vector3.up * ty;
            Vector3 m10 = b10 + Vector3.up * ty;
            Vector3 m11 = b11 + Vector3.up * ty;
            Vector3 m01 = b01 + Vector3.up * ty;

            AddQuad(m01, m11, m10, m00, uvScale);   // top of the lower section
            AddQuad(b01, b11, m11, m01, uvScale);  // +Z
            AddQuad(b11, b10, m10, m11, uvScale);  // +X
            AddQuad(b10, b00, m00, m10, uvScale);  // -Z
            AddQuad(b00, b01, m01, m00, uvScale);  // -X

            // Upper section, inset and twisted.
            Quaternion twist = Quaternion.Euler(0f, twistDegrees, 0f);
            Vector3 inset = new Vector3(topInset, 0f, topInset);

            Vector3 u00 = (m00 - c + inset);
            Vector3 u10 = (m10 - c + inset);
            Vector3 u11 = (m11 - c + inset);
            Vector3 u01 = (m01 - c + inset);
            u00 = c + twist * u00 + Vector3.up * ty;
            u10 = c + twist * u10 + Vector3.up * ty;
            u11 = c + twist * u11 + Vector3.up * ty;
            u01 = c + twist * u01 + Vector3.up * ty;

            Vector3 topOffset = Vector3.up * upperHeight;

            AddQuad(u01, u11, u10, u00, uvScale);                       // roof of the lower shaft
            AddQuad(u01, u11, u11 + topOffset, u01 + topOffset, uvScale);
            AddQuad(u11, u10, u10 + topOffset, u11 + topOffset, uvScale);
            AddQuad(u10, u00, u00 + topOffset, u10 + topOffset, uvScale);
            AddQuad(u00, u01, u01 + topOffset, u00 + topOffset, uvScale);
            // Cap of the upper shaft. u00,u01,u11,u10 run counter-clockwise when
            // viewed from above, which is the winding AddQuad needs for +Y.
            AddQuad(u00 + topOffset, u01 + topOffset, u11 + topOffset, u10 + topOffset, uvScale);
        }

        /// <summary>A tapered cylinder (cone frustum), used for posts and towers.</summary>
        public void AddFrustum(Vector3 baseCenter, float bottomRadius, float topRadius,
                               float height, int segments = 8, float uvScale = 1f)
        {
            int start = _verts.Count;
            Vector3 c = baseCenter;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float ang = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);

                _verts.Add(c + new Vector3(cos * bottomRadius, 0f, sin * bottomRadius));
                _verts.Add(c + new Vector3(cos * topRadius, height, sin * topRadius));

                _uvs.Add(new Vector2(t * uvScale, 0f));
                _uvs.Add(new Vector2(t * uvScale, uvScale));
            }

            for (int i = 0; i < segments; i++)
            {
                int b0 = start + i * 2;
                int t0 = b0 + 1;
                int b1 = b0 + 2;
                int t1 = b0 + 3;

                _tris.Add(b0); _tris.Add(t0); _tris.Add(t1);
                _tris.Add(b0); _tris.Add(t1); _tris.Add(b1);
            }

            // Cap
            int capCentre = _verts.Count;
            _verts.Add(c + Vector3.up * height);
            _uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                int t0 = start + i * 2 + 1;
                int t1 = start + (i + 1) * 2 + 1;
                _tris.Add(capCentre); _tris.Add(t1); _tris.Add(t0);
            }
        }

        /// <summary>
        /// Builds the Mesh. Follows the documented order: Clear, vertices, uv,
        /// triangles, then normals and bounds. RecalculateNormals is what keeps
        /// hand-built geometry from rendering black.
        /// </summary>
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.SetVertices(_verts);
            if (_uvs.Count == _verts.Count) mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Procedural geometry is generated once and never edited again.
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>Creates a GameObject with a MeshFilter/MeshRenderer using this mesh.</summary>
        public GameObject ToObject(string name, Material material, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ToMesh(name);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            return go;
        }
    }
}