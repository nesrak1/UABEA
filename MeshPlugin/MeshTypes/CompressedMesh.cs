using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MeshPlugin.MeshClass;

namespace MeshPlugin.MeshTypes
{
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
}
