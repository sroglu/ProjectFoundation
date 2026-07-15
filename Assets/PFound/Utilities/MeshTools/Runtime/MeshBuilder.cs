using System;
using UnityEngine;

namespace PFound.Utilities.MeshTools
{
    /// <summary>
    /// Procedural mesh generators. Every builder returns a fresh, fully-populated
    /// <see cref="Mesh"/> (positions, normals, UVs, triangles) with its bounds recalculated,
    /// ready to drop into a <see cref="MeshFilter"/>.
    /// </summary>
    public static class MeshBuilder
    {
        /// <summary>
        /// Builds a flat rectangle lying in the XZ plane, centred on the origin with its
        /// normal pointing up (+Y). <paramref name="columns"/> and <paramref name="rows"/>
        /// control tessellation along X and Z respectively (minimum one each).
        /// </summary>
        public static Mesh CreateXZPlane(float width, float depth, int columns = 1, int rows = 1)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            int vertsX = columns + 1;
            int vertsZ = rows + 1;
            int vertexCount = vertsX * vertsZ;

            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[columns * rows * 6];

            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;

            int v = 0;
            for (int z = 0; z < vertsZ; z++)
            {
                float tz = (float)z / rows;
                for (int x = 0; x < vertsX; x++)
                {
                    float tx = (float)x / columns;
                    positions[v] = new Vector3(-halfW + tx * width, 0f, -halfD + tz * depth);
                    normals[v] = Vector3.up;
                    uvs[v] = new Vector2(tx, tz);
                    v++;
                }
            }

            int t = 0;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int bottomLeft = z * vertsX + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + vertsX;
                    int topRight = topLeft + 1;

                    triangles[t++] = bottomLeft;
                    triangles[t++] = topLeft;
                    triangles[t++] = topRight;

                    triangles[t++] = bottomLeft;
                    triangles[t++] = topRight;
                    triangles[t++] = bottomRight;
                }
            }

            return Assemble("XZ Plane", positions, normals, uvs, triangles);
        }

        /// <summary>
        /// Builds an upright cylinder centred on the origin, its axis along Y, spanning
        /// <paramref name="height"/> from bottom to top. <paramref name="radialSegments"/>
        /// slices the circumference (minimum three); when <paramref name="capped"/> is true a
        /// fan of triangles seals each end.
        /// </summary>
        public static Mesh CreateCylinder(float radius, float height, int radialSegments = 16, bool capped = true)
        {
            radialSegments = Mathf.Max(3, radialSegments);

            float halfH = height * 0.5f;
            int ring = radialSegments + 1; // duplicate seam vertex for clean UVs

            // Side wall uses a doubled ring (top + bottom); caps add their own rings + centres.
            int sideVertexCount = ring * 2;
            int capVertexCount = capped ? (ring * 2 + 2) : 0;
            int vertexCount = sideVertexCount + capVertexCount;

            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            int sideTriangleCount = radialSegments * 6;
            int capTriangleCount = capped ? radialSegments * 6 : 0;
            var triangles = new int[sideTriangleCount + capTriangleCount];

            // ---- Side wall ----
            for (int i = 0; i < ring; i++)
            {
                float angle = (float)i / radialSegments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                var outward = new Vector3(cos, 0f, sin);
                float u = (float)i / radialSegments;

                positions[i] = new Vector3(cos * radius, -halfH, sin * radius);
                positions[ring + i] = new Vector3(cos * radius, halfH, sin * radius);
                normals[i] = outward;
                normals[ring + i] = outward;
                uvs[i] = new Vector2(u, 0f);
                uvs[ring + i] = new Vector2(u, 1f);
            }

            int tri = 0;
            for (int i = 0; i < radialSegments; i++)
            {
                int bottom = i;
                int bottomNext = i + 1;
                int top = ring + i;
                int topNext = ring + i + 1;

                triangles[tri++] = bottom;
                triangles[tri++] = top;
                triangles[tri++] = topNext;

                triangles[tri++] = bottom;
                triangles[tri++] = topNext;
                triangles[tri++] = bottomNext;
            }

            if (capped)
            {
                BuildCap(positions, normals, uvs, triangles, radius, radialSegments, ring,
                    sideVertexCount, ref tri, -halfH, Vector3.down);
                BuildCap(positions, normals, uvs, triangles, radius, radialSegments, ring,
                    sideVertexCount + ring + 1, ref tri, halfH, Vector3.up);
            }

            return Assemble("Cylinder", positions, normals, uvs, triangles);
        }

        private static void BuildCap(
            Vector3[] positions, Vector3[] normals, Vector2[] uvs, int[] triangles,
            float radius, int radialSegments, int ring, int baseIndex, ref int tri,
            float y, Vector3 normal)
        {
            int centre = baseIndex;
            positions[centre] = new Vector3(0f, y, 0f);
            normals[centre] = normal;
            uvs[centre] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < ring; i++)
            {
                float angle = (float)i / radialSegments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int index = centre + 1 + i;
                positions[index] = new Vector3(cos * radius, y, sin * radius);
                normals[index] = normal;
                uvs[index] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
            }

            bool facingUp = normal == Vector3.up;
            for (int i = 0; i < radialSegments; i++)
            {
                int a = centre + 1 + i;
                int b = centre + 1 + i + 1;

                triangles[tri++] = centre;
                triangles[tri++] = facingUp ? a : b;
                triangles[tri++] = facingUp ? b : a;
            }
        }

        private static Mesh Assemble(string name, Vector3[] positions, Vector3[] normals, Vector2[] uvs, int[] triangles)
        {
            if (positions.Length > 65535)
                throw new ArgumentException("Mesh exceeds the 16-bit index limit; raise index format if this is intentional.");

            var mesh = new Mesh { name = name };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
