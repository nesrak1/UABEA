using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshPlugin.MeshTypes
{
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
}
