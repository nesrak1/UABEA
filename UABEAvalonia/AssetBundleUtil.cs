using AssetsTools.NET;
using AssetsTools.NET.Extra.Decompressors.LZ4;
using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class AssetBundleUtil
    {
        public static bool IsBundleDataCompressed(AssetBundleFile bundle)
        {
            return bundle.BlockAndDirInfo.BlockInfos.Any(inf => inf.GetCompressionType() != 0);
        }
    }
}
