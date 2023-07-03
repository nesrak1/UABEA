using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshPlugin.MeshTypes
{
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
}
