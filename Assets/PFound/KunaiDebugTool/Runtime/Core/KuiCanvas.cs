using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kunai
{
    internal class KuiCanvas : IDisposable
    {
        Mesh _mesh;
        Material _material;
        // Keep originals so UploadAndDraw can re-create _mesh / _material if
        // Unity destroys them mid-session (domain reload, play-mode toggles,
        // some scene transitions). Without these we'd silently lose the
        // overlay until KUI.Initialize is called again.
        Shader _shader;
        Texture2D _fontAtlas;
        NativeArray<KuiVertex> _vertices;
        NativeArray<uint> _indices;
        int _vertexCapacity;
        int _indexCapacity;
        CommandBuffer _cmdBuffer;

        static readonly VertexAttributeDescriptor[] VertexDescriptors =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        public KuiCanvas(Shader shader, Texture2D fontAtlas, int initialVertexCapacity = 4096)
        {
            _shader = shader;
            _fontAtlas = fontAtlas;

            _mesh = new Mesh { name = "KunaiDebugMesh" };
            _mesh.MarkDynamic();

            _material = new Material(shader);
            if (fontAtlas != null)
                _material.mainTexture = fontAtlas;

            _vertexCapacity = initialVertexCapacity;
            _indexCapacity = (initialVertexCapacity / 4) * 6;
            _vertices = new NativeArray<KuiVertex>(_vertexCapacity, Allocator.Persistent);
            _indices = new NativeArray<uint>(_indexCapacity, Allocator.Persistent);

            _cmdBuffer = new CommandBuffer { name = "KunaiDebugOverlay" };

            RebuildIndices(_indexCapacity);
        }

        public void GenerateAndFlush(ref KuiCommandBuffer commandBuffer,
            NativeArray<KuiGlyph> asciiCache,
            NativeParallelHashMap<int, KuiGlyph> extendedCache,
            float fontScale, float lineHeight, float screenWidth, float screenHeight)
        {
            int cmdCount = commandBuffer.Commands.Length;
            if (cmdCount == 0) return;

            var commands = new NativeArray<KuiDrawCommand>(cmdCount, Allocator.TempJob);
            NativeArray<KuiDrawCommand>.Copy(commandBuffer.Commands.AsArray(), commands, cmdCount);

            var vertexCounts = new NativeArray<int>(cmdCount, Allocator.TempJob);
            var vertexOffsets = new NativeArray<int>(cmdCount, Allocator.TempJob);
            var totalVertexCount = new NativeArray<int>(1, Allocator.TempJob);

            var textChars = new NativeArray<char>(commandBuffer.TextChars.Length, Allocator.TempJob);
            if (commandBuffer.TextChars.Length > 0)
                NativeArray<char>.Copy(commandBuffer.TextChars.AsArray(), textChars, commandBuffer.TextChars.Length);

            var sortJob = new LayerSortJob { Commands = commands };
            var sortHandle = sortJob.Schedule();

            var countJob = new VertexCountJob
            {
                Commands = commands,
                VertexCounts = vertexCounts
            };
            var countHandle = countJob.Schedule(cmdCount, 32, sortHandle);

            var prefixJob = new PrefixSumJob
            {
                VertexCounts = vertexCounts,
                VertexOffsets = vertexOffsets,
                TotalVertexCount = totalVertexCount
            };
            var prefixHandle = prefixJob.Schedule(countHandle);
            prefixHandle.Complete();

            int totalVerts = totalVertexCount[0];
            if (totalVerts == 0)
            {
                commands.Dispose();
                vertexCounts.Dispose();
                vertexOffsets.Dispose();
                totalVertexCount.Dispose();
                textChars.Dispose();
                return;
            }

            EnsureCapacity(totalVerts);

            var writeJob = new VertexWriteJob
            {
                Commands = commands,
                VertexOffsets = vertexOffsets,
                VertexCounts = vertexCounts,
                TextChars = textChars,
                AsciiCache = asciiCache,
                ExtendedCache = extendedCache,
                FontScale = fontScale,
                LineHeight = lineHeight,
                ScreenHeight = screenHeight,
                Vertices = _vertices
            };
            var writeHandle = writeJob.Schedule(cmdCount, 16);
            writeHandle.Complete();

            UploadAndDraw(totalVerts, screenWidth, screenHeight);

            commands.Dispose();
            vertexCounts.Dispose();
            vertexOffsets.Dispose();
            totalVertexCount.Dispose();
            textChars.Dispose();
        }

        void EnsureCapacity(int requiredVertices)
        {
            if (requiredVertices <= _vertexCapacity) return;

            int newCapacity = _vertexCapacity;
            while (newCapacity < requiredVertices)
                newCapacity *= 2;

            _vertices.Dispose();
            _vertices = new NativeArray<KuiVertex>(newCapacity, Allocator.Persistent);
            _vertexCapacity = newCapacity;

            int newIndexCapacity = (newCapacity / 4) * 6;
            _indices.Dispose();
            _indices = new NativeArray<uint>(newIndexCapacity, Allocator.Persistent);
            _indexCapacity = newIndexCapacity;
            RebuildIndices(_indexCapacity);

            Debug.LogWarning($"[Kunai] Vertex buffer resized: {_vertexCapacity / 2} → {_vertexCapacity}");
        }

        void RebuildIndices(int indexCount)
        {
            int quadCount = indexCount / 6;
            for (int i = 0; i < quadCount; i++)
            {
                uint v = (uint)(i * 4);
                int idx = i * 6;
                _indices[idx + 0] = v + 0;
                _indices[idx + 1] = v + 1;
                _indices[idx + 2] = v + 2;
                _indices[idx + 3] = v + 0;
                _indices[idx + 4] = v + 2;
                _indices[idx + 5] = v + 3;
            }
        }

        void UploadAndDraw(int vertexCount, float screenWidth, float screenHeight)
        {
            // Defensive null check: Unity may destroy our Mesh / Material
            // objects out from under us during domain reload, play-mode
            // enter/exit, or scene transitions that recycle render assets.
            // The C# reference becomes a "fake null" Unity object and
            // ApplyAndDisposeWritableMeshData / DrawMesh throw ArgumentNull
            // every frame until we re-create. Re-allocating here keeps the
            // overlay alive across those events without leaking — Dispose()
            // still nulls the references cleanly.
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "KunaiDebugMesh" };
                _mesh.MarkDynamic();
            }
            if (_material == null && _shader != null)
            {
                _material = new Material(_shader);
                if (_fontAtlas != null)
                    _material.mainTexture = _fontAtlas;
            }

            int indexCount = (vertexCount / 4) * 6;

            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArray[0];

            meshData.SetVertexBufferParams(vertexCount, VertexDescriptors);
            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            var vertexData = meshData.GetVertexData<KuiVertex>();
            NativeArray<KuiVertex>.Copy(_vertices, vertexData, vertexCount);

            var indexData = meshData.GetIndexData<uint>();
            NativeArray<uint>.Copy(_indices, indexData, indexCount);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            var flags = MeshUpdateFlags.DontRecalculateBounds
                      | MeshUpdateFlags.DontValidateIndices
                      | MeshUpdateFlags.DontNotifyMeshUsers;

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _mesh, flags);

            _cmdBuffer.Clear();
            _cmdBuffer.SetViewProjectionMatrices(
                Matrix4x4.identity,
                Matrix4x4.Ortho(0, screenWidth, 0, screenHeight, -1, 1)
            );
            _cmdBuffer.DrawMesh(_mesh, Matrix4x4.identity, _material);
        }

        // Pulled by KuiOverlayRunner inside its WaitForEndOfFrame coroutine.
        // The CommandBuffer's explicit ortho ViewProjection draws straight to
        // the back buffer; no camera state required.
        public void ExecuteOnBackBuffer()
        {
            if (_cmdBuffer != null)
                Graphics.ExecuteCommandBuffer(_cmdBuffer);
        }

        public void ClearCommandBuffer()
        {
            if (_cmdBuffer != null) _cmdBuffer.Clear();
        }

        public void Dispose()
        {
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();

            if (_cmdBuffer != null) { _cmdBuffer.Dispose(); _cmdBuffer = null; }
            if (_mesh != null) { UnityEngine.Object.DestroyImmediate(_mesh); _mesh = null; }
            if (_material != null) { UnityEngine.Object.DestroyImmediate(_material); _material = null; }
        }
    }
}
