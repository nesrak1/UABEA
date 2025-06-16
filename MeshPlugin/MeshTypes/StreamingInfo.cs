using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshPlugin.MeshTypes
{
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
}
