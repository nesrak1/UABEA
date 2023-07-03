using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshPlugin.MeshTypes
{
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
}
