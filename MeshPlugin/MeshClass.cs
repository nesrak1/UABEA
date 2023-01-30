using System.Collections;
using System.Collections.Generic;
using AssetsTools;
using AssetsTools.NET;
using System.Drawing.Printing;
using SevenZip.Compression.LZMA;
using System.Linq;
using System;
using AssetsTools.NET.Extra;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using System.Resources;
using Avalonia.Media;
using Avalonia.Controls;

namespace MeshPlugin
{
    public class MeshClass
    {
        public class BoneWeights4
        {
            public float[] weight;
            public int[] boneIndex;

            public BoneWeights4()
            {
                weight = new float[4];
                boneIndex = new int[4];
            }

            public BoneWeights4(AssetsFileReader reader)
            {
                weight = reader.ReadSingleArray(4);
                boneIndex = reader.ReadInt32Array(4);
            }
        }
        public class Mesh
        {
            //Fields in type-tree
            public string m_Name;
            private bool m_Use16BitIndices = true;
            public SubMesh[] m_SubMeshes;
            public BlendShapeData m_Shapes;
            private m_BindPose[] m_BindPoses;
            public AssetsTools.Matrix4x4[] m_BindPose;
            public uint[] m_BoneNameHashes;
            public uint m_RootBoneNameHash;
            public uint m_MeshCompression;
            public bool m_IsReadable;
            public bool m_KeepVertices;
            public bool m_KeepIndices;
            public int m_IndexFormat;
            public uint[] m_IndexBuffer;
            public VertexData m_VertexData;
            public CompressedMesh m_CompressedMesh;
            public int m_MeshUsageFlags;
            public byte[] m_BakedConvexCollisionMesh;
            public byte[] m_BakedTriangleCollisionMesh;
            public float[] m_MeshMetrics;
            public StreamingInfo m_StreamData;

            public float[] m_Vertices;
            public BoneWeights4[] m_Skin;
            public float[] m_Normals;
            public float[] m_Colors;
            public float[] m_UV0;
            public float[] m_UV1;
            public float[] m_UV2;
            public float[] m_UV3;
            public float[] m_UV4;
            public float[] m_UV5;
            public float[] m_UV6;
            public float[] m_UV7;
            public float[] m_Tangents;

            public List<uint> m_Indices = new List<uint>();

            public Mesh(AssetTypeValueField baseField, AssetsFileInstance AFinst)
            {
                //Required for the streams part which is essential for calculations
                string unityVersion = AFinst.file.Metadata.UnityVersion;
                UnityVersion AssetUnityVersion = new UnityVersion(unityVersion);
                string[] versionArray = unityVersion.Split('.');
                List<float> version = new List<float>();
                for (int i = 0; i < versionArray.Length; i++)
                {
                    if (i == versionArray.Length - 1)
                    {
                        string final = versionArray[i];
                        string digital = string.Concat(final.Where(Char.IsDigit));
                        version.Add(float.Parse(digital));
                    }
                    else
                    {
                        version.Add(float.Parse(versionArray[i]));
                    }
                }



                m_Name = baseField["m_Name"].AsString;

                var sub = baseField["m_SubMeshes.Array"].AsArray;
                var index = sub.size;

                m_SubMeshes = new SubMesh[index];

                for (int i = 0; i < index; i++)
                {
                    m_SubMeshes[i] = new SubMesh(baseField, i);
                }
                if (!baseField["m_Shapes"].IsDummy)
                {
                    var m_Shapes_Field = baseField["m_Shapes"];
                    m_Shapes = new BlendShapeData(m_Shapes_Field);
                }
                if (!baseField["m_BindPose"].IsDummy)
                {
                    var m_BindPose = baseField["m_BindPose"];
                    var count = m_BindPose["Array"].AsArray.size;
                    this.m_BindPose = new AssetsTools.Matrix4x4[count];
                    m_BindPoses = new m_BindPose[count];
                    for (int i = 0; i < count; i++)
                    {
                        m_BindPoses[i] = new m_BindPose(m_BindPose["Array"][i]);
                        this.m_BindPose[i] = new AssetsTools.Matrix4x4(m_BindPoses[i].e.ToArray());
                    }
                }
                if (!baseField["m_BoneNameHashes"].IsDummy)
                {
                    var m_BoneNameHashes = baseField["m_BoneNameHashes"];
                    var count = m_BoneNameHashes["Array"].AsArray.size;
                    this.m_BoneNameHashes = new uint[count];
                    for (int i = 0; i < count; i++)
                    {
                        this.m_BoneNameHashes[i] = m_BoneNameHashes["Array"][i].AsUInt;
                    }
                }
                if (!baseField["m_RootBoneNameHash"].IsDummy)
                {
                    this.m_RootBoneNameHash = baseField["m_RootBoneNameHash"].AsUInt;
                }
                if (!baseField["m_MeshCompression"].IsDummy)
                {
                    this.m_MeshCompression = baseField["m_MeshCompression"].AsByte;
                }
                if (!baseField["m_IsReadable"].IsDummy)
                {
                    this.m_IsReadable = baseField["m_IsReadable"].AsBool;
                }
                if (!baseField["m_KeepVertices;"].IsDummy)
                {
                    this.m_KeepVertices = baseField["m_KeepVertices"].AsBool;
                }
                if (!baseField["m_KeepIndices"].IsDummy)
                {
                    this.m_KeepIndices = baseField["m_KeepIndices"].AsBool;
                }
                if (!baseField["m_IndexFormat"].IsDummy)
                {
                    this.m_IndexFormat = baseField["m_IndexFormat"].AsInt;
                    m_Use16BitIndices = this.m_IndexFormat == 0;
                }
                if (!baseField["m_IndexBuffer"].IsDummy)
                {
                    if (m_Use16BitIndices)
                    {
                        var m_IndexBuffer = baseField["m_IndexBuffer"];
                        var count = m_IndexBuffer["Array"].AsArray.size;
                        var bytes = new byte[count];
                        for (int i = 0; i < count; i++)
                        {
                            bytes[i] = m_IndexBuffer["Array"][i].AsByte;
                        }
                        MemoryStream ms = new MemoryStream(bytes);
                        AssetsFileReader reader = new AssetsFileReader(ms);
                        {
                            reader.BigEndian = AFinst.file.Reader.BigEndian;
                            this.m_IndexBuffer = new uint[count / 2];
                            for (int i = 0; i < this.m_IndexBuffer.Length; i++)
                            {
                                this.m_IndexBuffer[i] = reader.ReadUInt16();
                            }
                        }
                    }
                    else
                    {
                        var m_IndexBuffer = baseField["m_IndexBuffer"];
                        var count = m_IndexBuffer["Array"].AsArray.size;
                        var bytes = new byte[count];
                        for (int i = 0; i < count; i++)
                        {
                            bytes[i] = m_IndexBuffer["Array"][i].AsByte;
                        }
                        MemoryStream ms = new MemoryStream(bytes);
                        AssetsFileReader reader = new AssetsFileReader(ms);
                        {
                            reader.BigEndian = AFinst.file.Reader.BigEndian;
                            this.m_IndexBuffer = reader.ReadUInt32Array();
                        }
                    }
                }
                if (!baseField["m_VertexData"].IsDummy)
                {
                    this.m_VertexData = new VertexData(baseField["m_VertexData"], version);
                }
                if (!baseField["m_CompressedMesh"].IsDummy)
                {
                    m_CompressedMesh = new CompressedMesh(baseField["m_CompressedMesh"]);
                }

                //Skip m_localAABB as it is of no use
                //m_localAABB = new AABB(reader);

                if (!baseField["m_MeshUsageFlags"].IsDummy)
                {
                    m_MeshUsageFlags = baseField["m_MeshUsageFlags"].AsInt;
                }
                if (!baseField["m_BakedConvexCollisionMesh"].IsDummy)
                {
                    var count = baseField["m_BakedConvexCollisionMesh.Array"].AsArray.size;
                    m_BakedConvexCollisionMesh = new byte[count];
                    for (int i = 0; i < count; i++)
                    {
                        m_BakedConvexCollisionMesh[i] = baseField["m_BakedConvexCollisionMesh.Array"][i].AsByte;
                    }
                }
                if (!baseField["m_BakedTriangleCollisionMesh"].IsDummy)
                {
                    var count = baseField["m_BakedTriangleCollisionMesh.Array"].AsArray.size;
                    m_BakedTriangleCollisionMesh = new byte[count];
                    for (int i = 0; i < count; i++)
                    {
                        m_BakedTriangleCollisionMesh[i] = baseField["m_BakedTriangleCollisionMesh.Array"][i].AsByte;
                    }
                }
                if (!baseField["m_MeshMetrics[0]"].IsDummy && !baseField["m_MeshMetrics[1]"].IsDummy)
                {
                    m_MeshMetrics = new float[2];
                    m_MeshMetrics[0] = baseField["m_MeshMetrics[0]"].AsFloat;
                    m_MeshMetrics[1] = baseField["m_MeshMetrics[1]"].AsFloat;
                }
                if (!baseField["m_StreamData"].IsDummy)
                {
                    m_StreamData = new StreamingInfo(baseField["m_StreamData"], version);
                }
                if (AFinst.parentBundle != null)
                {
                    ProcessData(AFinst);
                }
                else
                {
                    if (!string.IsNullOrEmpty(m_StreamData?.path))
                    {

                        string searchPath = m_StreamData.path;
                        if (searchPath.StartsWith("archive:/"))
                            searchPath = searchPath.Substring(9);

                        var completePath = AFinst.path + "/" + searchPath;
                        if(File.Exists(completePath))
                        {
                            byte[] rawBytes = File.ReadAllBytes(completePath);
                            MemoryStream ms = new MemoryStream(rawBytes);
                            ProcessDataBytes(AFinst, ms);
                        }
                        else
                        {
                            ShowResourceError();
                        }
                    }
                    else
                    {
                        ProcessData(AFinst);
                    }
                }
            }
            private async void ShowResourceError()
            {
                ResourceLoader loader = new ResourceLoader();
                bool saved = await loader.ShowDialog<bool>(loader);
            }
            private void ProcessDataBytes(AssetsFileInstance AFinst, MemoryStream ms)
            {
                string unityVersion = AFinst.file.Metadata.UnityVersion;
                string[] versionArray = unityVersion.Split('.');
                var version = 1.0f;
                if (versionArray[0] != versionArray[1])
                {
                    version = float.Parse(versionArray[0] + "." + versionArray[1]);
                }
                else
                {
                    version = float.Parse(versionArray[0]);
                }

                if (!string.IsNullOrEmpty(m_StreamData?.path))
                {
                    if (m_VertexData.m_VertexCount > 0)
                    {
                        var resourceReader = new MeshPlugin.ResourceClass.ResourceReader(ms, m_StreamData.offset, m_StreamData.size);
                        m_VertexData.m_DataSize = resourceReader.GetData();
                    }
                }
                if (version >= 3.5) //3.5 and up
                {
                    ReadVertexData(AFinst);
                }

                if (version >= 2.6) //2.6.0 and later
                {
                    DecompressCompressedMesh(AFinst);
                }

                GetTriangles(AFinst);
            }
            private void ProcessData(AssetsFileInstance AFinst)
            {
                string unityVersion = AFinst.file.Metadata.UnityVersion;
                string[] versionArray = unityVersion.Split('.');
                var version = 1.0f;
                if (versionArray[0] != versionArray[1])
                {
                    version = float.Parse(versionArray[0] + "." + versionArray[1]);
                }
                else
                {
                    version = float.Parse(versionArray[0]);
                }

                if (!string.IsNullOrEmpty(m_StreamData?.path))
                {
                    if (m_VertexData.m_VertexCount > 0)
                    {
                        var resourceReader = new MeshPlugin.ResourceClass.ResourceReader(m_StreamData.path, m_StreamData.offset, m_StreamData.size, AFinst);
                        m_VertexData.m_DataSize = resourceReader.GetResourceBytes();
                    }
                }
                if (version >= 3.5) //3.5 and up
                {
                    ReadVertexData(AFinst);
                }

                if (version >= 2.6) //2.6.0 and later
                {
                    DecompressCompressedMesh(AFinst);
                }

                GetTriangles(AFinst);
            }
            private void ReadVertexData(AssetsFileInstance AFinst)
            {
                string unityVersion = AFinst.file.Metadata.UnityVersion;
                UnityVersion AssetUnityVersion = new UnityVersion(unityVersion);
                string[] versionArray = unityVersion.Split('.');
                List<float> version = new List<float>();
                for (int i = 0; i < versionArray.Length; i++)
                {
                    if (i == versionArray.Length - 1)
                    {
                        string final = versionArray[i];
                        string digital = string.Concat(final.Where(Char.IsDigit));
                        version.Add(float.Parse(digital));
                    }
                    else
                    {
                        version.Add(float.Parse(versionArray[i]));
                    }
                }
                for (var chn = 0; chn < m_VertexData.m_Channels.Length; chn++)
                {
                    var m_Channel = m_VertexData.m_Channels[chn];
                    if (m_Channel.dimension > 0)
                    {
                        var m_Stream = m_VertexData.m_Streams[m_Channel.stream];
                        var channelMask = new BitArray(new[] { (int)m_Stream.channelMask });
                        if (channelMask.Get(chn))
                        {
                            if (version[0] < 2018 && chn == 2 && m_Channel.format == 2) //kShaderChannelColor && kChannelFormatColor
                            {
                                m_Channel.dimension = 4;
                            }

                            var vertexFormat = MeshHelper.ToVertexFormat(m_Channel.format, version);
                            var componentByteSize = (int)MeshHelper.GetFormatSize(vertexFormat);
                            var componentBytes = new byte[m_VertexData.m_VertexCount * m_Channel.dimension * componentByteSize];
                            for (int v = 0; v < m_VertexData.m_VertexCount; v++)
                            {
                                var vertexOffset = (int)m_Stream.offset + m_Channel.offset + (int)m_Stream.stride * v;
                                for (int d = 0; d < m_Channel.dimension; d++)
                                {
                                    var componentOffset = vertexOffset + componentByteSize * d;
                                    Buffer.BlockCopy(m_VertexData.m_DataSize, componentOffset, componentBytes, componentByteSize * (v * m_Channel.dimension + d), componentByteSize);
                                }
                            }

                            if (AFinst.file.Reader.BigEndian != false && componentByteSize > 1) //swap bytes
                            {
                                for (var i = 0; i < componentBytes.Length / componentByteSize; i++)
                                {
                                    var buff = new byte[componentByteSize];
                                    Buffer.BlockCopy(componentBytes, i * componentByteSize, buff, 0, componentByteSize);
                                    buff = buff.Reverse().ToArray();
                                    Buffer.BlockCopy(buff, 0, componentBytes, i * componentByteSize, componentByteSize);
                                }
                            }

                            int[] componentsIntArray = null;
                            float[] componentsFloatArray = null;
                            if (MeshHelper.IsIntFormat(vertexFormat))
                                componentsIntArray = MeshHelper.BytesToIntArray(componentBytes, vertexFormat);
                            else
                                componentsFloatArray = MeshHelper.BytesToFloatArray(componentBytes, vertexFormat);

                            if (version[0] >= 2018)
                            {
                                switch (chn)
                                {
                                    case 0: //kShaderChannelVertex
                                        m_Vertices = componentsFloatArray;
                                        break;
                                    case 1: //kShaderChannelNormal
                                        m_Normals = componentsFloatArray;
                                        break;
                                    case 2: //kShaderChannelTangent
                                        m_Tangents = componentsFloatArray;
                                        break;
                                    case 3: //kShaderChannelColor
                                        m_Colors = componentsFloatArray;
                                        break;
                                    case 4: //kShaderChannelTexCoord0
                                        m_UV0 = componentsFloatArray;
                                        break;
                                    case 5: //kShaderChannelTexCoord1
                                        m_UV1 = componentsFloatArray;
                                        break;
                                    case 6: //kShaderChannelTexCoord2
                                        m_UV2 = componentsFloatArray;
                                        break;
                                    case 7: //kShaderChannelTexCoord3
                                        m_UV3 = componentsFloatArray;
                                        break;
                                    case 8: //kShaderChannelTexCoord4
                                        m_UV4 = componentsFloatArray;
                                        break;
                                    case 9: //kShaderChannelTexCoord5
                                        m_UV5 = componentsFloatArray;
                                        break;
                                    case 10: //kShaderChannelTexCoord6
                                        m_UV6 = componentsFloatArray;
                                        break;
                                    case 11: //kShaderChannelTexCoord7
                                        m_UV7 = componentsFloatArray;
                                        break;
                                    //2018.2 and up
                                    case 12: //kShaderChannelBlendWeight
                                        if (m_Skin == null)
                                        {
                                            InitMSkin();
                                        }
                                        for (int i = 0; i < m_VertexData.m_VertexCount; i++)
                                        {
                                            for (int j = 0; j < m_Channel.dimension; j++)
                                            {
                                                m_Skin[i].weight[j] = componentsFloatArray[i * m_Channel.dimension + j];
                                            }
                                        }
                                        break;
                                    case 13: //kShaderChannelBlendIndices
                                        if (m_Skin == null)
                                        {
                                            InitMSkin();
                                        }
                                        for (int i = 0; i < m_VertexData.m_VertexCount; i++)
                                        {
                                            for (int j = 0; j < m_Channel.dimension; j++)
                                            {
                                                m_Skin[i].boneIndex[j] = componentsIntArray[i * m_Channel.dimension + j];
                                            }
                                        }
                                        break;
                                }
                            }
                            else
                            {
                                switch (chn)
                                {
                                    case 0: //kShaderChannelVertex
                                        m_Vertices = componentsFloatArray;
                                        break;
                                    case 1: //kShaderChannelNormal
                                        m_Normals = componentsFloatArray;
                                        break;
                                    case 2: //kShaderChannelColor
                                        m_Colors = componentsFloatArray;
                                        break;
                                    case 3: //kShaderChannelTexCoord0
                                        m_UV0 = componentsFloatArray;
                                        break;
                                    case 4: //kShaderChannelTexCoord1
                                        m_UV1 = componentsFloatArray;
                                        break;
                                    case 5:
                                        if (version[0] >= 5) //kShaderChannelTexCoord2
                                        {
                                            m_UV2 = componentsFloatArray;
                                        }
                                        else //kShaderChannelTangent
                                        {
                                            m_Tangents = componentsFloatArray;
                                        }
                                        break;
                                    case 6: //kShaderChannelTexCoord3
                                        m_UV3 = componentsFloatArray;
                                        break;
                                    case 7: //kShaderChannelTangent
                                        m_Tangents = componentsFloatArray;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            private void InitMSkin()
            {
                m_Skin = new BoneWeights4[m_VertexData.m_VertexCount];
                for (int i = 0; i < m_VertexData.m_VertexCount; i++)
                {
                    m_Skin[i] = new BoneWeights4();
                }
            }
            private void SetUV(int uv, float[] m_UV)
            {
                switch (uv)
                {
                    case 0:
                        m_UV0 = m_UV;
                        break;
                    case 1:
                        m_UV1 = m_UV;
                        break;
                    case 2:
                        m_UV2 = m_UV;
                        break;
                    case 3:
                        m_UV3 = m_UV;
                        break;
                    case 4:
                        m_UV4 = m_UV;
                        break;
                    case 5:
                        m_UV5 = m_UV;
                        break;
                    case 6:
                        m_UV6 = m_UV;
                        break;
                    case 7:
                        m_UV7 = m_UV;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public float[] GetUV(int uv)
            {
                switch (uv)
                {
                    case 0:
                        return m_UV0;
                    case 1:
                        return m_UV1;
                    case 2:
                        return m_UV2;
                    case 3:
                        return m_UV3;
                    case 4:
                        return m_UV4;
                    case 5:
                        return m_UV5;
                    case 6:
                        return m_UV6;
                    case 7:
                        return m_UV7;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            private void DecompressCompressedMesh(AssetsFileInstance AFinst)
            {
                string unityVersion = AFinst.file.Metadata.UnityVersion;
                string[] versionArray = unityVersion.Split('.');
                var version = 1.0f;
                if (versionArray[0] != versionArray[1])
                {
                    version = float.Parse(versionArray[0] + "." + versionArray[1]);
                }
                else
                {
                    version = float.Parse(versionArray[0]);
                }

                //Vertex
                if (m_CompressedMesh.m_Vertices.m_NumItems > 0)
                {
                    m_VertexData.m_VertexCount = m_CompressedMesh.m_Vertices.m_NumItems / 3;
                    m_Vertices = m_CompressedMesh.m_Vertices.UnpackFloats(3, 3 * 4);
                }
                //UV
                if (m_CompressedMesh.m_UV.m_NumItems > 0)
                {
                    var m_UVInfo = m_CompressedMesh.m_UVInfo;
                    if (m_UVInfo != 0)
                    {
                        const int kInfoBitsPerUV = 4;
                        const int kUVDimensionMask = 3;
                        const int kUVChannelExists = 4;
                        const int kMaxTexCoordShaderChannels = 8;

                        int uvSrcOffset = 0;
                        for (int uv = 0; uv < kMaxTexCoordShaderChannels; uv++)
                        {
                            var texCoordBits = m_UVInfo >> (uv * kInfoBitsPerUV);
                            texCoordBits &= (1u << kInfoBitsPerUV) - 1u;
                            if ((texCoordBits & kUVChannelExists) != 0)
                            {
                                var uvDim = 1 + (int)(texCoordBits & kUVDimensionMask);
                                var m_UV = m_CompressedMesh.m_UV.UnpackFloats(uvDim, uvDim * 4, uvSrcOffset, (int)m_VertexData.m_VertexCount);
                                SetUV(uv, m_UV);
                                uvSrcOffset += uvDim * (int)m_VertexData.m_VertexCount;
                            }
                        }
                    }
                    else
                    {
                        m_UV0 = m_CompressedMesh.m_UV.UnpackFloats(2, 2 * 4, 0, (int)m_VertexData.m_VertexCount);
                        if (m_CompressedMesh.m_UV.m_NumItems >= m_VertexData.m_VertexCount * 4)
                        {
                            m_UV1 = m_CompressedMesh.m_UV.UnpackFloats(2, 2 * 4, (int)m_VertexData.m_VertexCount * 2, (int)m_VertexData.m_VertexCount);
                        }
                    }
                }
                //BindPose
                if (version < 5)
                {
                    if (m_CompressedMesh.m_BindPoses.m_NumItems > 0)
                    {
                        m_BindPose = new AssetsTools.Matrix4x4[m_CompressedMesh.m_BindPoses.m_NumItems / 16];
                        var m_BindPoses_Unpacked = m_CompressedMesh.m_BindPoses.UnpackFloats(16, 4 * 16);
                        var buffer = new float[16];
                        for (int i = 0; i < m_BindPose.Length; i++)
                        {
                            Array.Copy(m_BindPoses_Unpacked, i * 16, buffer, 0, 16);
                            m_BindPose[i] = new AssetsTools.Matrix4x4(buffer);
                        }
                    }
                }
                //Normal
                if (m_CompressedMesh.m_Normals.m_NumItems > 0)
                {
                    var normalData = m_CompressedMesh.m_Normals.UnpackFloats(2, 4 * 2);
                    var signs = m_CompressedMesh.m_NormalSigns.UnpackInts();
                    m_Normals = new float[m_CompressedMesh.m_Normals.m_NumItems / 2 * 3];
                    for (int i = 0; i < m_CompressedMesh.m_Normals.m_NumItems / 2; ++i)
                    {
                        var x = normalData[i * 2 + 0];
                        var y = normalData[i * 2 + 1];
                        var zsqr = 1 - x * x - y * y;
                        float z;
                        if (zsqr >= 0f)
                            z = (float)Math.Sqrt(zsqr);
                        else
                        {
                            z = 0;
                            var normal = new AssetsTools.Vector3(x, y, z);
                            normal.Normalize();
                            x = normal.X;
                            y = normal.Y;
                            z = normal.Z;
                        }
                        if (signs[i] == 0)
                            z = -z;
                        m_Normals[i * 3] = x;
                        m_Normals[i * 3 + 1] = y;
                        m_Normals[i * 3 + 2] = z;
                    }
                }
                //Tangent
                if (m_CompressedMesh.m_Tangents.m_NumItems > 0)
                {
                    var tangentData = m_CompressedMesh.m_Tangents.UnpackFloats(2, 4 * 2);
                    var signs = m_CompressedMesh.m_TangentSigns.UnpackInts();
                    m_Tangents = new float[m_CompressedMesh.m_Tangents.m_NumItems / 2 * 4];
                    for (int i = 0; i < m_CompressedMesh.m_Tangents.m_NumItems / 2; ++i)
                    {
                        var x = tangentData[i * 2 + 0];
                        var y = tangentData[i * 2 + 1];
                        var zsqr = 1 - x * x - y * y;
                        float z;
                        if (zsqr >= 0f)
                            z = (float)Math.Sqrt(zsqr);
                        else
                        {
                            z = 0;
                            var vector3f = new AssetsTools.Vector3(x, y, z);
                            vector3f.Normalize();
                            x = vector3f.X;
                            y = vector3f.Y;
                            z = vector3f.Z;
                        }
                        if (signs[i * 2 + 0] == 0)
                            z = -z;
                        var w = signs[i * 2 + 1] > 0 ? 1.0f : -1.0f;
                        m_Tangents[i * 4] = x;
                        m_Tangents[i * 4 + 1] = y;
                        m_Tangents[i * 4 + 2] = z;
                        m_Tangents[i * 4 + 3] = w;
                    }
                }
                //FloatColor
                if (version >= 5)
                {
                    if (m_CompressedMesh.m_FloatColors.m_NumItems > 0)
                    {
                        m_Colors = m_CompressedMesh.m_FloatColors.UnpackFloats(1, 4);
                    }
                }
                //Skin
                if (m_CompressedMesh.m_Weights.m_NumItems > 0)
                {
                    var weights = m_CompressedMesh.m_Weights.UnpackInts();
                    var boneIndices = m_CompressedMesh.m_BoneIndices.UnpackInts();

                    InitMSkin();

                    int bonePos = 0;
                    int boneIndexPos = 0;
                    int j = 0;
                    int sum = 0;

                    for (int i = 0; i < m_CompressedMesh.m_Weights.m_NumItems; i++)
                    {
                        //read bone index and weight.
                        m_Skin[bonePos].weight[j] = weights[i] / 31.0f;
                        m_Skin[bonePos].boneIndex[j] = boneIndices[boneIndexPos++];
                        j++;
                        sum += weights[i];

                        //the weights add up to one. fill the rest for this vertex with zero, and continue with next one.
                        if (sum >= 31)
                        {
                            for (; j < 4; j++)
                            {
                                m_Skin[bonePos].weight[j] = 0;
                                m_Skin[bonePos].boneIndex[j] = 0;
                            }
                            bonePos++;
                            j = 0;
                            sum = 0;
                        }
                        //we read three weights, but they don't add up to one. calculate the fourth one, and read
                        //missing bone index. continue with next vertex.
                        else if (j == 3)
                        {
                            m_Skin[bonePos].weight[j] = (31 - sum) / 31.0f;
                            m_Skin[bonePos].boneIndex[j] = boneIndices[boneIndexPos++];
                            bonePos++;
                            j = 0;
                            sum = 0;
                        }
                    }
                }
                //IndexBuffer
                if (m_CompressedMesh.m_Triangles.m_NumItems > 0)
                {
                    m_IndexBuffer = Array.ConvertAll(m_CompressedMesh.m_Triangles.UnpackInts(), x => (uint)x);
                }
                //Color
                if (m_CompressedMesh.m_Colors?.m_NumItems > 0)
                {
                    m_CompressedMesh.m_Colors.m_NumItems *= 4;
                    m_CompressedMesh.m_Colors.m_BitSize /= 4;
                    var tempColors = m_CompressedMesh.m_Colors.UnpackInts();
                    m_Colors = new float[m_CompressedMesh.m_Colors.m_NumItems];
                    for (int v = 0; v < m_CompressedMesh.m_Colors.m_NumItems; v++)
                    {
                        m_Colors[v] = tempColors[v] / 255f;
                    }
                }
            }

            private void GetTriangles(AssetsFileInstance AFinst)
            {
                string unityVersion = AFinst.file.Metadata.UnityVersion;
                string[] versionArray = unityVersion.Split('.');
                var version = 1.0f;
                if (versionArray[0] != versionArray[1])
                {
                    version = float.Parse(versionArray[0] + "." + versionArray[1]);
                }
                else
                {
                    version = float.Parse(versionArray[0]);
                }

                foreach (var m_SubMesh in m_SubMeshes)
                {
                    var firstIndex = m_SubMesh.firstByte / 2;
                    if (!m_Use16BitIndices)
                    {
                        firstIndex /= 2;
                    }
                    var indexCount = m_SubMesh.indexCount;
                    var topology = m_SubMesh.topology;
                    if (topology == GfxPrimitiveType.kPrimitiveTriangles)
                    {
                        for (int i = 0; i < indexCount; i += 3)
                        {
                            m_Indices.Add(m_IndexBuffer[firstIndex + i]);
                            m_Indices.Add(m_IndexBuffer[firstIndex + i + 1]);
                            m_Indices.Add(m_IndexBuffer[firstIndex + i + 2]);
                        }
                    }
                    else if (version < 4 || topology == GfxPrimitiveType.kPrimitiveTriangleStrip)
                    {
                        // de-stripify :
                        uint triIndex = 0;
                        for (int i = 0; i < indexCount - 2; i++)
                        {
                            var a = m_IndexBuffer[firstIndex + i];
                            var b = m_IndexBuffer[firstIndex + i + 1];
                            var c = m_IndexBuffer[firstIndex + i + 2];

                            // skip degenerates
                            if (a == b || a == c || b == c)
                                continue;

                            // do the winding flip-flop of strips :
                            if ((i & 1) == 1)
                            {
                                m_Indices.Add(b);
                                m_Indices.Add(a);
                            }
                            else
                            {
                                m_Indices.Add(a);
                                m_Indices.Add(b);
                            }
                            m_Indices.Add(c);
                            triIndex += 3;
                        }
                        //fix indexCount
                        m_SubMesh.indexCount = triIndex;
                    }
                    else if (topology == GfxPrimitiveType.kPrimitiveQuads)
                    {
                        for (int q = 0; q < indexCount; q += 4)
                        {
                            m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                            m_Indices.Add(m_IndexBuffer[firstIndex + q + 1]);
                            m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                            m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                            m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                            m_Indices.Add(m_IndexBuffer[firstIndex + q + 3]);
                        }
                        //fix indexCount
                        m_SubMesh.indexCount = indexCount / 2 * 3;
                    }
                    else
                    {
                        throw new NotSupportedException("Failed getting triangles. Submesh topology is lines or points.");
                    }
                }
            }
        }
        public class StreamingInfo
        {
            public long offset; //ulong
            public uint size;
            public string path;

            public StreamingInfo(AssetTypeValueField m_StreamData, List<float> version)
            {
                if (version[0] >= 2020 || (version[0] == 2020 && version[1] >= 1))
                {
                    offset = m_StreamData["offset"].AsLong;
                }
                else
                {
                    offset = m_StreamData["offset"].AsUInt;
                }
                size = m_StreamData["size"].AsUInt;
                path = m_StreamData["path"].AsString;
            }
        }
        public class CompressedMesh
        {
            public PackedFloatVector m_Vertices;
            public PackedFloatVector m_UV;
            public PackedFloatVector m_BindPoses;
            public PackedFloatVector m_Normals;
            public PackedFloatVector m_Tangents;
            public PackedIntVector m_Weights;
            public PackedIntVector m_NormalSigns;
            public PackedIntVector m_TangentSigns;
            public PackedFloatVector m_FloatColors;
            public PackedIntVector m_BoneIndices;
            public PackedIntVector m_Triangles;
            public PackedIntVector m_Colors;
            public uint m_UVInfo;

            public CompressedMesh(AssetTypeValueField m_CompressedMesh)
            {

                m_Vertices = new PackedFloatVector(m_CompressedMesh["m_Vertices"]);
                m_UV = new PackedFloatVector(m_CompressedMesh["m_UV"]);
                m_Normals = new PackedFloatVector(m_CompressedMesh["m_Normals"]);
                m_Tangents = new PackedFloatVector(m_CompressedMesh["m_Tangents"]);
                m_Weights = new PackedIntVector(m_CompressedMesh["m_Weights"]);
                m_NormalSigns = new PackedIntVector(m_CompressedMesh["m_NormalSigns"]);
                m_TangentSigns = new PackedIntVector(m_CompressedMesh["m_TangentSigns"]);
                m_FloatColors = new PackedFloatVector(m_CompressedMesh["m_FloatColors"]);
                m_BoneIndices = new PackedIntVector(m_CompressedMesh["m_BoneIndices"]);
                m_Triangles = new PackedIntVector(m_CompressedMesh["m_Triangles"]);
                m_UVInfo = m_CompressedMesh["m_UVInfo"].AsUInt;
            }
        }
        public class PackedFloatVector
        {
            public uint m_NumItems;
            public float m_Range;
            public float m_Start;
            public byte[] m_Data;
            public byte m_BitSize;

            public PackedFloatVector(AssetTypeValueField m_PackedFloatVector)
            {
                m_NumItems = m_PackedFloatVector["m_NumItems"].AsUInt;
                m_Range = m_PackedFloatVector["m_Range"].AsFloat;
                m_Start = m_PackedFloatVector["m_Start"].AsFloat;

                var count = m_PackedFloatVector["m_Data.Array"].AsArray.size;
                m_Data = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    m_Data[i] = m_PackedFloatVector["m_Data.Array"][i].AsByte;
                }
                m_BitSize = m_PackedFloatVector["m_BitSize"].AsByte;
            }

            public float[] UnpackFloats(int itemCountInChunk, int chunkStride, int start = 0, int numChunks = -1)
            {
                int bitPos = m_BitSize * start;
                int indexPos = bitPos / 8;
                bitPos %= 8;

                float scale = 1.0f / m_Range;
                if (numChunks == -1)
                    numChunks = (int)m_NumItems / itemCountInChunk;
                var end = chunkStride * numChunks / 4;
                var data = new List<float>();
                for (var index = 0; index != end; index += chunkStride / 4)
                {
                    for (int i = 0; i < itemCountInChunk; ++i)
                    {
                        uint x = 0;

                        int bits = 0;
                        while (bits < m_BitSize)
                        {
                            x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                            int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                            bitPos += num;
                            bits += num;
                            if (bitPos == 8)
                            {
                                indexPos++;
                                bitPos = 0;
                            }
                        }
                        x &= (uint)(1 << m_BitSize) - 1u;
                        data.Add(x / (scale * ((1 << m_BitSize) - 1)) + m_Start);
                    }
                }

                return data.ToArray();
            }
        }

        public class PackedIntVector
        {
            public uint m_NumItems;
            public byte[] m_Data;
            public byte m_BitSize;

            public PackedIntVector(AssetTypeValueField m_PackedIntVector)
            {
                m_NumItems = m_PackedIntVector["m_NumItems"].AsUInt;

                var count = m_PackedIntVector["m_Data.Array"].AsArray.size;
                m_Data = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    m_Data[i] = m_PackedIntVector["m_Data.Array"][i].AsByte;
                }

                m_BitSize = m_PackedIntVector["m_BitSize"].AsByte;
            }

            public int[] UnpackInts()
            {
                var data = new int[m_NumItems];
                int indexPos = 0;
                int bitPos = 0;
                for (int i = 0; i < m_NumItems; i++)
                {
                    int bits = 0;
                    data[i] = 0;
                    while (bits < m_BitSize)
                    {
                        data[i] |= (m_Data[indexPos] >> bitPos) << bits;
                        int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                        bitPos += num;
                        bits += num;
                        if (bitPos == 8)
                        {
                            indexPos++;
                            bitPos = 0;
                        }
                    }
                    data[i] &= (1 << m_BitSize) - 1;
                }
                return data;
            }
        }
        public class m_BindPose
        {
            public List<float> e = new List<float>();
            public m_BindPose(AssetTypeValueField data)
            {
                e.Add(data["e00"].AsFloat);
                e.Add(data["e01"].AsFloat);
                e.Add(data["e02"].AsFloat);
                e.Add(data["e03"].AsFloat);
                e.Add(data["e10"].AsFloat);
                e.Add(data["e11"].AsFloat);
                e.Add(data["e12"].AsFloat);
                e.Add(data["e13"].AsFloat);
                e.Add(data["e20"].AsFloat);
                e.Add(data["e21"].AsFloat);
                e.Add(data["e22"].AsFloat);
                e.Add(data["e23"].AsFloat);
                e.Add(data["e30"].AsFloat);
                e.Add(data["e31"].AsFloat);
                e.Add(data["e32"].AsFloat);
                e.Add(data["e33"].AsFloat);
            }
        }
        public class SubMesh
        {
            public uint firstByte;
            public uint indexCount;
            public GfxPrimitiveType topology;
            public uint triangleCount;
            public uint baseVertex;
            public uint firstVertex;
            public uint vertexCount;
            public AABB localAABB;

            public SubMesh(AssetTypeValueField baseField, int index)
            {
                var data = baseField["m_SubMeshes.Array"][index];

                firstByte = data["firstByte"].AsUInt;
                indexCount = data["indexCount"].AsUInt;

                if (!data["topology"].IsDummy)
                {
                    topology = (GfxPrimitiveType)data["topology"].AsInt;
                }

                if (!data["triangleCount"].IsDummy)
                {
                    triangleCount = data["triangleCount"].AsUInt;
                }

                if (!data["baseVertex"].IsDummy)
                {
                    baseVertex = data["baseVertex"].AsUInt;
                }

                if (!data["firstVertex"].IsDummy)
                {
                    firstVertex = data["firstVertex"].AsUInt;
                }

                if (!data["vertexCount"].IsDummy)
                {
                    vertexCount = data["vertexCount"].AsUInt;
                }
                if (!data["localAABB"].IsDummy)
                {
                    var AABB_Field = data["localAABB"];
                    localAABB = new AABB(AABB_Field);
                }
            }
        }
        public enum GfxPrimitiveType : int
        {
            kPrimitiveTriangles = 0,
            kPrimitiveTriangleStrip = 1,
            kPrimitiveQuads = 2,
            kPrimitiveLines = 3,
            kPrimitiveLineStrip = 4,
            kPrimitivePoints = 5,
        };
        public class AABB
        {
            public AssetsTools.Vector3 m_Center;
            public AssetsTools.Vector3 m_Extent;

            public AABB(AssetTypeValueField AABB)
            {
                var m_Center = AABB["m_Center"];
                this.m_Center.X = m_Center["x"].AsFloat;
                this.m_Center.Y = m_Center["y"].AsFloat;
                this.m_Center.Z = m_Center["z"].AsFloat;

                var m_Extent = AABB["m_Extent"];
                this.m_Extent.X = m_Extent["x"].AsFloat;
                this.m_Extent.Y = m_Extent["y"].AsFloat;
                this.m_Extent.Z = m_Extent["z"].AsFloat;
            }
        }
        public class BlendShapeData
        {
            public BlendShapeVertex[] vertices;
            public MeshBlendShape[] shapes;
            public MeshBlendShapeChannel[] channels;
            public float[] fullWeights;

            public BlendShapeData(AssetTypeValueField m_Shapes)
            {
                //Section-- vector vertices
                int numVerts = m_Shapes["vertices.Array"].AsArray.size;

                vertices = new BlendShapeVertex[numVerts];
                for (int i = 0; i < numVerts; i++)
                {
                    var data = m_Shapes["vertices.Array"][i];
                    vertices[i] = new BlendShapeVertex(data);
                }

                //Section-- vector shapes
                int numShapes = m_Shapes["shapes.Array"].AsArray.size;
                shapes = new MeshBlendShape[numShapes];
                for (int i = 0; i < numShapes; i++)
                {
                    var data = m_Shapes["shapes.Array"][i];
                    shapes[i] = new MeshBlendShape(data);
                }

                //Section-- vector channels
                int numChannels = m_Shapes["channels.Array"].AsArray.size;
                channels = new MeshBlendShapeChannel[numChannels];
                for (int i = 0; i < numChannels; i++)
                {
                    var data = m_Shapes["channels.Array"][i];
                    channels[i] = new MeshBlendShapeChannel(data);
                }

                int floatCount = m_Shapes["fullWeights.Array"].AsArray.size;
                fullWeights = new float[floatCount];
                for (int i = 0; i < floatCount; i++)
                {
                    fullWeights[i] = m_Shapes["fullWeights.Array"][i].AsFloat;
                }
            }
        }
        public class BlendShapeVertex
        {
            public AssetsTools.Vector3 vertex;
            public AssetsTools.Vector3 normal;
            public AssetsTools.Vector3 tangent;
            public uint index;

            public BlendShapeVertex(AssetTypeValueField data)
            {
                var vertex_Field = data["vertex"];
                vertex.X = vertex_Field["x"].AsFloat;
                vertex.Y = vertex_Field["y"].AsFloat;
                vertex.Z = vertex_Field["z"].AsFloat;

                var normal_Field = data["normal"];
                normal.X = normal_Field["x"].AsFloat;
                normal.Y = normal_Field["y"].AsFloat;
                normal.Z = normal_Field["z"].AsFloat;

                var tangent_Field = data["tangent"];
                tangent.X = tangent_Field["x"].AsFloat;
                tangent.Y = tangent_Field["y"].AsFloat;
                tangent.Z = tangent_Field["z"].AsFloat;

                index = data["index"].AsUInt;
            }
        }
        public class MeshBlendShape
        {
            public uint firstVertex;
            public uint vertexCount;
            public bool hasNormals;
            public bool hasTangents;

            public MeshBlendShape(AssetTypeValueField data)
            {

                firstVertex = data["firstVertex"].AsUInt;
                vertexCount = data["vertexCount"].AsUInt;

                hasNormals = data["hasNormals"].AsBool;
                hasTangents = data["hasTangents"].AsBool;
            }
        }
        public class MeshBlendShapeChannel
        {
            public string name;
            public uint nameHash;
            public int frameIndex;
            public int frameCount;

            public MeshBlendShapeChannel(AssetTypeValueField data)
            {
                name = data["name"].AsString;
                nameHash = data["nameHash"].AsUInt;
                frameIndex = data["frameIndex"].AsInt;
                frameCount = data["frameCount"].AsInt;
            }
        }
        public class VertexData
        {
            public uint m_VertexCount;
            public ChannelInfo[] m_Channels;
            public StreamInfo[] m_Streams;
            public byte[] m_DataSize;

            public VertexData(AssetTypeValueField m_VertexData, List<float> version)
            {

                m_VertexCount = m_VertexData["m_VertexCount"].AsUInt;

                if (!m_VertexData["m_Channels"].IsDummy)
                {
                    var count = m_VertexData["m_Channels.Array"].AsArray.size;
                    m_Channels = new ChannelInfo[count];
                    for (int i = 0; i < count; i++)
                    {
                        m_Channels[i] = new ChannelInfo(m_VertexData["m_Channels.Array"][i]);
                    }
                }

                GetStreams(version);

                if (!m_VertexData["m_DataSize"].IsDummy)
                {
                    m_DataSize = m_VertexData["m_DataSize"].AsByteArray;
                }
            }


            private void GetStreams(List<float> version)
            {
                var streamCount = m_Channels.Max(x => x.stream) + 1;
                m_Streams = new StreamInfo[streamCount];
                uint offset = 0;
                for (int s = 0; s < streamCount; s++)
                {
                    uint chnMask = 0;
                    uint stride = 0;
                    for (int chn = 0; chn < m_Channels.Length; chn++)
                    {
                        var m_Channel = m_Channels[chn];
                        if (m_Channel.stream == s)
                        {
                            if (m_Channel.dimension > 0)
                            {
                                chnMask |= 1u << chn;
                                stride += m_Channel.dimension * MeshHelper.GetFormatSize(MeshHelper.ToVertexFormat(m_Channel.format, version));
                            }
                        }
                    }
                    m_Streams[s] = new StreamInfo
                    {
                        channelMask = chnMask,
                        offset = offset,
                        stride = stride,
                        dividerOp = 0,
                        frequency = 0
                    };
                    offset += m_VertexCount * stride;
                    //static size_t AlignStreamSize (size_t size) { return (size + (kVertexStreamAlign-1)) & ~(kVertexStreamAlign-1); }
                    offset = (offset + (16u - 1u)) & ~(16u - 1u);
                }
            }

            private void GetChannels(List<float> version)
            {
                m_Channels = new ChannelInfo[6];
                for (int i = 0; i < 6; i++)
                {
                    m_Channels[i] = new ChannelInfo();
                }
                for (var s = 0; s < m_Streams.Length; s++)
                {
                    var m_Stream = m_Streams[s];
                    var channelMask = new BitArray(new[] { (int)m_Stream.channelMask });
                    byte offset = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        if (channelMask.Get(i))
                        {
                            var m_Channel = m_Channels[i];
                            m_Channel.stream = (byte)s;
                            m_Channel.offset = offset;
                            switch (i)
                            {
                                case 0: //kShaderChannelVertex
                                case 1: //kShaderChannelNormal
                                    m_Channel.format = 0; //kChannelFormatFloat
                                    m_Channel.dimension = 3;
                                    break;
                                case 2: //kShaderChannelColor
                                    m_Channel.format = 2; //kChannelFormatColor
                                    m_Channel.dimension = 4;
                                    break;
                                case 3: //kShaderChannelTexCoord0
                                case 4: //kShaderChannelTexCoord1
                                    m_Channel.format = 0; //kChannelFormatFloat
                                    m_Channel.dimension = 2;
                                    break;
                                case 5: //kShaderChannelTangent
                                    m_Channel.format = 0; //kChannelFormatFloat
                                    m_Channel.dimension = 4;
                                    break;
                            }
                            offset += (byte)(m_Channel.dimension * MeshHelper.GetFormatSize(MeshHelper.ToVertexFormat(m_Channel.format, version)));
                        }
                    }
                }
            }
        }
        public class ChannelInfo
        {
            public byte stream;
            public byte offset;
            public byte format;
            public byte dimension;

            public ChannelInfo() { }

            public ChannelInfo(AssetTypeValueField data)
            {
                stream = data["stream"].AsByte;
                offset = data["offset"].AsByte;
                format = data["format"].AsByte;
                dimension = (byte)(data["dimension"].AsByte & 0xF);
            }
        }
        public class StreamInfo
        {
            public uint channelMask;
            public uint offset;
            public uint stride;
            public uint align;
            public byte dividerOp;
            public ushort frequency;

            public StreamInfo() { }

            public StreamInfo(AssetsFileReader reader, AssetsFileInstance AFinst)
            {
                string unityVersion = AFinst.file.Metadata.UnityVersion;
                string[] versionArray = unityVersion.Split('.');
                var version = 1.0f;
                if (versionArray[0] != versionArray[1])
                {
                    version = float.Parse(versionArray[0] + "." + versionArray[1]);
                }
                else
                {
                    version = float.Parse(versionArray[0]);
                }


                channelMask = reader.ReadUInt32();
                offset = reader.ReadUInt32();

                if (version < 4) //4.0 down
                {
                    stride = reader.ReadUInt32();
                    align = reader.ReadUInt32();
                }
                else
                {
                    stride = reader.ReadByte();
                    dividerOp = reader.ReadByte();
                    frequency = reader.ReadUInt16();
                }
            }
        }
        public class MeshHelper
        {
            public enum VertexChannelFormat
            {
                kChannelFormatFloat,
                kChannelFormatFloat16,
                kChannelFormatColor,
                kChannelFormatByte,
                kChannelFormatUInt32
            }

            public enum VertexFormat2017
            {
                kVertexFormatFloat,
                kVertexFormatFloat16,
                kVertexFormatColor,
                kVertexFormatUNorm8,
                kVertexFormatSNorm8,
                kVertexFormatUNorm16,
                kVertexFormatSNorm16,
                kVertexFormatUInt8,
                kVertexFormatSInt8,
                kVertexFormatUInt16,
                kVertexFormatSInt16,
                kVertexFormatUInt32,
                kVertexFormatSInt32
            }

            public enum VertexFormat
            {
                kVertexFormatFloat,
                kVertexFormatFloat16,
                kVertexFormatUNorm8,
                kVertexFormatSNorm8,
                kVertexFormatUNorm16,
                kVertexFormatSNorm16,
                kVertexFormatUInt8,
                kVertexFormatSInt8,
                kVertexFormatUInt16,
                kVertexFormatSInt16,
                kVertexFormatUInt32,
                kVertexFormatSInt32
            }

            public static VertexFormat ToVertexFormat(int format, List<float> version)
            {
                if (version[0] < 2017)
                {
                    switch ((VertexChannelFormat)format)
                    {
                        case VertexChannelFormat.kChannelFormatFloat:
                            return VertexFormat.kVertexFormatFloat;
                        case VertexChannelFormat.kChannelFormatFloat16:
                            return VertexFormat.kVertexFormatFloat16;
                        case VertexChannelFormat.kChannelFormatColor: //in 4.x is size 4
                            return VertexFormat.kVertexFormatUNorm8;
                        case VertexChannelFormat.kChannelFormatByte:
                            return VertexFormat.kVertexFormatUInt8;
                        case VertexChannelFormat.kChannelFormatUInt32: //in 5.x
                            return VertexFormat.kVertexFormatUInt32;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(format), format, null);
                    }
                }
                else if (version[0] < 2019)
                {
                    switch ((VertexFormat2017)format)
                    {
                        case VertexFormat2017.kVertexFormatFloat:
                            return VertexFormat.kVertexFormatFloat;
                        case VertexFormat2017.kVertexFormatFloat16:
                            return VertexFormat.kVertexFormatFloat16;
                        case VertexFormat2017.kVertexFormatColor:
                        case VertexFormat2017.kVertexFormatUNorm8:
                            return VertexFormat.kVertexFormatUNorm8;
                        case VertexFormat2017.kVertexFormatSNorm8:
                            return VertexFormat.kVertexFormatSNorm8;
                        case VertexFormat2017.kVertexFormatUNorm16:
                            return VertexFormat.kVertexFormatUNorm16;
                        case VertexFormat2017.kVertexFormatSNorm16:
                            return VertexFormat.kVertexFormatSNorm16;
                        case VertexFormat2017.kVertexFormatUInt8:
                            return VertexFormat.kVertexFormatUInt8;
                        case VertexFormat2017.kVertexFormatSInt8:
                            return VertexFormat.kVertexFormatSInt8;
                        case VertexFormat2017.kVertexFormatUInt16:
                            return VertexFormat.kVertexFormatUInt16;
                        case VertexFormat2017.kVertexFormatSInt16:
                            return VertexFormat.kVertexFormatSInt16;
                        case VertexFormat2017.kVertexFormatUInt32:
                            return VertexFormat.kVertexFormatUInt32;
                        case VertexFormat2017.kVertexFormatSInt32:
                            return VertexFormat.kVertexFormatSInt32;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(format), format, null);
                    }
                }
                else
                {
                    return (VertexFormat)format;
                }
            }


            public static uint GetFormatSize(VertexFormat format)
            {
                switch (format)
                {
                    case VertexFormat.kVertexFormatFloat:
                    case VertexFormat.kVertexFormatUInt32:
                    case VertexFormat.kVertexFormatSInt32:
                        return 4u;
                    case VertexFormat.kVertexFormatFloat16:
                    case VertexFormat.kVertexFormatUNorm16:
                    case VertexFormat.kVertexFormatSNorm16:
                    case VertexFormat.kVertexFormatUInt16:
                    case VertexFormat.kVertexFormatSInt16:
                        return 2u;
                    case VertexFormat.kVertexFormatUNorm8:
                    case VertexFormat.kVertexFormatSNorm8:
                    case VertexFormat.kVertexFormatUInt8:
                    case VertexFormat.kVertexFormatSInt8:
                        return 1u;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            }

            public static bool IsIntFormat(VertexFormat format)
            {
                return format >= VertexFormat.kVertexFormatUInt8;
            }

            public static float[] BytesToFloatArray(byte[] inputBytes, VertexFormat format)
            {
                var size = GetFormatSize(format);
                var len = inputBytes.Length / size;
                var result = new float[len];
                for (int i = 0; i < len; i++)
                {
                    switch (format)
                    {
                        case VertexFormat.kVertexFormatFloat:
                            result[i] = BitConverter.ToSingle(inputBytes, i * 4);
                            break;
                        case VertexFormat.kVertexFormatFloat16:
                            result[i] = Half.ToHalf(inputBytes, i * 2);
                            break;
                        case VertexFormat.kVertexFormatUNorm8:
                            result[i] = inputBytes[i] / 255f;
                            break;
                        case VertexFormat.kVertexFormatSNorm8:
                            result[i] = Math.Max((sbyte)inputBytes[i] / 127f, -1f);
                            break;
                        case VertexFormat.kVertexFormatUNorm16:
                            result[i] = BitConverter.ToUInt16(inputBytes, i * 2) / 65535f;
                            break;
                        case VertexFormat.kVertexFormatSNorm16:
                            result[i] = Math.Max(BitConverter.ToInt16(inputBytes, i * 2) / 32767f, -1f);
                            break;
                    }
                }
                return result;
            }

            public static int[] BytesToIntArray(byte[] inputBytes, VertexFormat format)
            {
                var size = GetFormatSize(format);
                var len = inputBytes.Length / size;
                var result = new int[len];
                for (int i = 0; i < len; i++)
                {
                    switch (format)
                    {
                        case VertexFormat.kVertexFormatUInt8:
                        case VertexFormat.kVertexFormatSInt8:
                            result[i] = inputBytes[i];
                            break;
                        case VertexFormat.kVertexFormatUInt16:
                        case VertexFormat.kVertexFormatSInt16:
                            result[i] = BitConverter.ToInt16(inputBytes, i * 2);
                            break;
                        case VertexFormat.kVertexFormatUInt32:
                        case VertexFormat.kVertexFormatSInt32:
                            result[i] = BitConverter.ToInt32(inputBytes, i * 4);
                            break;
                    }
                }
                return result;
            }
        }
    }
}
