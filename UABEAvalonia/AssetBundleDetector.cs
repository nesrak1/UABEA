using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class AssetBundleDetector
    {
        public static DetectedFileType DetectFileType(string filePath)
        {
            using (FileStream fs = File.OpenRead(filePath))
            using (AssetsFileReader r = new AssetsFileReader(fs))
            {
                return DetectFileType(r, 0);
            }
        }

        public static DetectedFileType DetectFileType(AssetsFileReader r, long startAddress)
        {
            string possibleBundleHeader;
            int possibleFormat;
            string emptyVersion, fullVersion;

            r.BigEndian = true;

            if (r.BaseStream.Length < 0x20)
            {
                return DetectedFileType.Unknown;
            }
            r.Position = startAddress;
            possibleBundleHeader = r.ReadStringLength(7);
            r.Position = startAddress + 0x08;
            possibleFormat = r.ReadInt32();

            r.Position = startAddress + (possibleFormat >= 0x16 ? 0x30 : 0x14);

            string possibleVersion = "";
            char curChar;
            while (r.Position < r.BaseStream.Length && (curChar = (char)r.ReadByte()) != 0x00)
            {
                possibleVersion += curChar;
                if (possibleVersion.Length > 0xFF)
                {
                    break;
                }
            }
            emptyVersion = Regex.Replace(possibleVersion, "[a-zA-Z0-9\\.\\n]", "");
            fullVersion = Regex.Replace(possibleVersion, "[^a-zA-Z0-9\\.\\n]", "");

            if (possibleBundleHeader == "UnityFS")
            {
                return DetectedFileType.BundleFile;
            }
            else if (possibleFormat < 0xFF && emptyVersion.Length == 0 && fullVersion.Length >= 5)
            {
                return DetectedFileType.AssetsFile;
            }
            return DetectedFileType.Unknown;
        }
    }

    public enum DetectedFileType
    {
        Unknown,
        AssetsFile,
        BundleFile
    }
}
