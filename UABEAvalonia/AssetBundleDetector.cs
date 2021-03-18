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
            string possibleBundleHeader;
            int possibleFormat;
            string emptyVersion;

            using (FileStream fs = File.OpenRead(filePath))
            using (AssetsFileReader r = new AssetsFileReader(fs))
            {
                if (fs.Length < 0x20)
                {
                    return DetectedFileType.Unknown;
                }
                possibleBundleHeader = r.ReadStringLength(7);
                r.Position = 0x08;
                possibleFormat = r.ReadInt32();
                r.Position = 0x14;

                string possibleVersion = "";
                char curChar;
                while (r.Position < r.BaseStream.Length && (curChar = (char)r.ReadByte()) != 0x00)
                {
                    possibleVersion += curChar;
                    if (possibleVersion.Length < 0xFF)
                    {
                        break;
                    }
                }
                emptyVersion = Regex.Replace(possibleVersion, "[a-zA-Z0-9\\.]", "");
            }
            if (possibleBundleHeader == "UnityFS")
            {
                return DetectedFileType.BundleFile;
            }
            else if (possibleFormat < 0xFF && emptyVersion == "")
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
