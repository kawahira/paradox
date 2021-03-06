﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.IO;
using System.Linq;
using SiliconStudio.Paradox.Effects.Data;
using SiliconStudio.Paradox.Graphics;
using SiliconStudio.Core;
using SiliconStudio.Core.Mathematics;
using SiliconStudio.Core.Serialization;
using SiliconStudio.Core.Serialization.Serializers;
using SiliconStudio.Paradox.Graphics.Data;

namespace SiliconStudio.Paradox.Extensions
{
    public static class TNBExtensions
    {
        /// <summary>
        /// Generates the tangents and binormals for this mesh data.
        /// Tangents and bitangents will be encoded as float4:
        /// float3 for tangent and an additional float for handedness (1 or -1),
        /// so that bitangent can be reconstructed.
        /// More info at http://www.terathon.com/code/tangent.html
        /// </summary>
        /// <param name="meshData">The mesh data.</param>
        public static unsafe void GenerateTangentBinormal(this MeshDrawData meshData)
        {
            if (!meshData.IsSimple())
                throw new ArgumentException("meshData is not simple.");

            if (meshData.PrimitiveType != PrimitiveType.TriangleList
                && meshData.PrimitiveType != PrimitiveType.TriangleListWithAdjacency)
                throw new NotImplementedException();

            var vertexBufferBinding = meshData.VertexBuffers[0];
            var indexBufferBinding = meshData.IndexBuffer;
            var indexData = indexBufferBinding != null ? indexBufferBinding.Buffer.Value.Content : null;

            var oldVertexStride = vertexBufferBinding.Declaration.VertexStride;
            var bufferData = vertexBufferBinding.Buffer.Value.Content;

            // TODO: Usage index in key
            var offsetMapping = vertexBufferBinding.Declaration
                .EnumerateWithOffsets()
                .ToDictionary(x => x.VertexElement.SemanticAsText, x => x.Offset);

            var positionOffset = offsetMapping["POSITION"];
            var uvOffset = offsetMapping[VertexElementUsage.TextureCoordinate];
            var normalOffset = offsetMapping[VertexElementUsage.Normal];

            // Add tangent to vertex declaration
            var vertexElements = vertexBufferBinding.Declaration.VertexElements.ToList();
            if (!offsetMapping.ContainsKey(VertexElementUsage.Tangent))
                vertexElements.Add(VertexElement.Tangent<Vector4>());
            vertexBufferBinding.Declaration = new VertexDeclaration(vertexElements.ToArray());
            var newVertexStride = vertexBufferBinding.Declaration.VertexStride;

            // Update mapping
            offsetMapping = vertexBufferBinding.Declaration
                .EnumerateWithOffsets()
                .ToDictionary(x => x.VertexElement.SemanticAsText, x => x.Offset);

            var tangentOffset = offsetMapping[VertexElementUsage.Tangent];

            var newBufferData = new byte[vertexBufferBinding.Count * newVertexStride];

            var tangents = new Vector3[vertexBufferBinding.Count];
            var bitangents = new Vector3[vertexBufferBinding.Count];

            fixed (byte* indexBufferStart = indexData)
            fixed (byte* oldBuffer = &bufferData[vertexBufferBinding.Offset])
            fixed (byte* newBuffer = &newBufferData[0])
            {
                var indexBuffer32 = indexBufferBinding != null && indexBufferBinding.Is32Bit ? (int*)indexBufferStart : null;
                var indexBuffer16 = indexBufferBinding != null && !indexBufferBinding.Is32Bit ? (short*)indexBufferStart : null;

                var indexCount = indexBufferBinding != null ? indexBufferBinding.Count : vertexBufferBinding.Count;

                for (int i = 0; i < indexCount; i += 3)
                {
                    // Get indices
                    int index1 = i + 0;
                    int index2 = i + 1;
                    int index3 = i + 2;

                    if (indexBuffer32 != null)
                    {
                        index1 = indexBuffer32[index1];
                        index2 = indexBuffer32[index2];
                        index3 = indexBuffer32[index3];
                    }
                    else if (indexBuffer16 != null)
                    {
                        index1 = indexBuffer16[index1];
                        index2 = indexBuffer16[index2];
                        index3 = indexBuffer16[index3];
                    }

                    int vertexOffset1 = index1 * oldVertexStride;
                    int vertexOffset2 = index2 * oldVertexStride;
                    int vertexOffset3 = index3 * oldVertexStride;

                    // Get positions
                    var position1 = (Vector3*)&oldBuffer[vertexOffset1 + positionOffset];
                    var position2 = (Vector3*)&oldBuffer[vertexOffset2 + positionOffset];
                    var position3 = (Vector3*)&oldBuffer[vertexOffset3 + positionOffset];

                    // Get texture coordinates
                    var uv1 = (Vector3*)&oldBuffer[vertexOffset1 + uvOffset];
                    var uv2 = (Vector3*)&oldBuffer[vertexOffset2 + uvOffset];
                    var uv3 = (Vector3*)&oldBuffer[vertexOffset3 + uvOffset];

                    // Calculate position and UV vectors from vertex 1 to vertex 2 and 3
                    var edge1 = *position2 - *position1;
                    var edge2 = *position3 - *position1;
                    var uvEdge1 = *uv2 - *uv1;
                    var uvEdge2 = *uv3 - *uv1;

                    var t = Vector3.Normalize(uvEdge2.Y * edge1 - uvEdge1.Y * edge2);
                    var b = Vector3.Normalize(uvEdge1.X * edge2 - uvEdge2.X * edge1);

                    // Contribute to every vertex
                    tangents[index1] += t;
                    tangents[index2] += t;
                    tangents[index3] += t;

                    bitangents[index1] += b;
                    bitangents[index2] += b;
                    bitangents[index3] += b;
                }

                var oldVertexOffset = 0;
                var newVertexOffset = 0;
                for (int i = 0; i < vertexBufferBinding.Count; ++i)
                {
                    Utilities.CopyMemory(new IntPtr(&newBuffer[newVertexOffset]), new IntPtr(&oldBuffer[oldVertexOffset]), oldVertexStride);

                    var normal = *(Vector3*)&oldBuffer[oldVertexOffset + normalOffset];
                    var target = ((float*)(&newBuffer[newVertexOffset + tangentOffset]));

                    var tangent = -tangents[i];
                    var bitangent = bitangents[i];

                    // Gram-Schmidt orthogonalize
                    *((Vector3*)target) = Vector3.Normalize(tangent - normal * Vector3.Dot(normal, tangent));

                    // Calculate handedness
                    target[3] = Vector3.Dot(Vector3.Cross(normal, tangent), bitangent) < 0.0f ? -1.0f : 1.0f;

                    oldVertexOffset += oldVertexStride;
                    newVertexOffset += newVertexStride;
                }
            }

            vertexBufferBinding.Offset = 0;
            vertexBufferBinding.Buffer = new BufferData(BufferFlags.VertexBuffer, newBufferData);
        }
    }
}