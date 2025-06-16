using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshPlugin.MeshTypes
{
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
}
