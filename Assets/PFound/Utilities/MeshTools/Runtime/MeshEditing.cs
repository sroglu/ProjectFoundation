using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.MeshTools
{
    /// <summary>
    /// In-place and extractive edits over an existing <see cref="Mesh"/>: rescaling the
    /// vertex cloud and lifting a single submesh out into a standalone mesh.
    /// </summary>
    public static class MeshEditing
    {
        /// <summary>
        /// Multiplies every vertex of <paramref name="mesh"/> by <paramref name="scale"/>
        /// component-wise (baking a scale into the geometry rather than the transform) and
        /// refreshes the bounds. Returns the same mesh for chaining.
        /// </summary>
        public static Mesh ScaleVertices(this Mesh mesh, Vector3 scale)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                vertices[i] = new Vector3(p.x * scale.x, p.y * scale.y, p.z * scale.z);
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Uniform overload of <see cref="ScaleVertices(Mesh,Vector3)"/>.</summary>
        public static Mesh ScaleVertices(this Mesh mesh, float scale)
        {
            return ScaleVertices(mesh, new Vector3(scale, scale, scale));
        }

        /// <summary>
        /// Builds a new single-submesh <see cref="Mesh"/> containing only the geometry of
        /// submesh <paramref name="submeshIndex"/> from <paramref name="source"/>. Only the
        /// vertices actually referenced by that submesh are carried over (with matching
        /// normals and UVs when present) and re-indexed from zero.
        /// </summary>
        public static Mesh ExtractSubmesh(this Mesh source, int submeshIndex)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (submeshIndex < 0 || submeshIndex >= source.subMeshCount)
                throw new ArgumentOutOfRangeException(nameof(submeshIndex));

            int[] sourceTriangles = source.triangles;
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector2[] sourceUVs = source.uv;

            bool hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
            bool hasUVs = sourceUVs != null && sourceUVs.Length == sourceVertices.Length;

            UnityEngine.Rendering.SubMeshDescriptor descriptor = source.GetSubMesh(submeshIndex);
            int start = descriptor.indexStart;
            int count = descriptor.indexCount;

            var remap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = hasNormals ? new List<Vector3>() : null;
            var newUVs = hasUVs ? new List<Vector2>() : null;
            var newTriangles = new int[count];

            for (int i = 0; i < count; i++)
            {
                int originalIndex = sourceTriangles[start + i] + descriptor.baseVertex;

                if (!remap.TryGetValue(originalIndex, out int mapped))
                {
                    mapped = newVertices.Count;
                    remap.Add(originalIndex, mapped);

                    newVertices.Add(sourceVertices[originalIndex]);
                    if (hasNormals) newNormals.Add(sourceNormals[originalIndex]);
                    if (hasUVs) newUVs.Add(sourceUVs[originalIndex]);
                }

                newTriangles[i] = mapped;
            }

            var mesh = new Mesh { name = source.name + "_Sub" + submeshIndex };
            mesh.SetVertices(newVertices);
            if (hasNormals) mesh.SetNormals(newNormals);
            if (hasUVs) mesh.SetUVs(0, newUVs);
            mesh.SetTriangles(newTriangles, 0);

            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
