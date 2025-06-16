using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MeshPlugin.MeshClass;

namespace MeshPlugin.MeshTypes
{
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
}
