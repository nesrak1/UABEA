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
            AssetsFileReader reader = bundle.reader;
            reader.Position = bundle.bundleHeader6.GetBundleInfoOffset();
            MemoryStream blocksInfoStream;
            AssetsFileReader memReader;
            int compressedSize = (int)bundle.bundleHeader6.compressedSize;
            byte[] uncompressedBytes;
            switch (bundle.bundleHeader6.GetCompressionType())
            {
                case 1:
                    uncompressedBytes = new byte[bundle.bundleHeader6.decompressedSize];
                    using (MemoryStream mstream = new MemoryStream(reader.ReadBytes(compressedSize)))
                    {
                        MemoryStream decoder = SevenZipHelper.StreamDecompress(mstream, compressedSize);
                        decoder.Read(uncompressedBytes, 0, (int)bundle.bundleHeader6.decompressedSize);
                        decoder.Dispose();
                    }
                    blocksInfoStream = new MemoryStream(uncompressedBytes);
                    break;
                case 2:
                case 3:
                    uncompressedBytes = new byte[bundle.bundleHeader6.decompressedSize];
                    using (MemoryStream mstream = new MemoryStream(reader.ReadBytes(compressedSize)))
                    {
                        var decoder = new Lz4DecoderStream(mstream);
                        decoder.Read(uncompressedBytes, 0, (int)bundle.bundleHeader6.decompressedSize);
                        decoder.Dispose();
                    }
                    blocksInfoStream = new MemoryStream(uncompressedBytes);
                    break;
                default:
                    blocksInfoStream = null;
                    break;
            }

            var uncompressedInf = bundle.bundleInf6;
            if (bundle.bundleHeader6.GetCompressionType() != 0)
            {
                using (memReader = new AssetsFileReader(blocksInfoStream))
                {
                    memReader.Position = 0;
                    uncompressedInf = new AssetBundleBlockAndDirectoryList06();
                    uncompressedInf.Read(0, memReader);
                }
            }

            return uncompressedInf.blockInf.Any(inf => (inf.flags & 0x3f) != 0);
        }
    }
}
