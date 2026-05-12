using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MeshPlugin.TypeStructs;
using System.Threading.Tasks;

namespace MeshPlugin.MeshTypes
{
    public class BlendShapeVertex
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Vector3 tangent;
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
}
