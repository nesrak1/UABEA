using AssetsTools.NET;
using AssetsTools.NET.Extra;
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
    public static class Extensions
    {
        public static AssetExternal GetExtAssetNewData(this AssetsManager _this, AssetsFileInstance relativeTo, int fileId, long pathId,
                                                       Stream data, bool onlyGetInfo = false, bool forceFromCldb = false)
        {
            AssetExternal ext = new AssetExternal();
            if (fileId == 0 && pathId == 0)
            {
                ext.info = null;
                ext.instance = null;
                ext.file = null;
            }
            else if (fileId != 0)
            {
                AssetsFileInstance dep = relativeTo.GetDependency(_this, fileId - 1);
                ext.info = dep.table.GetAssetInfo(pathId);
                if (!onlyGetInfo)
                    ext.instance = _this.GetTypeInstanceNewData(dep.file, ext.info, data, forceFromCldb);
                else
                    ext.instance = null;
                ext.file = dep;
            }
            else
            {
                ext.info = relativeTo.table.GetAssetInfo(pathId);
                if (!onlyGetInfo)
                    ext.instance = _this.GetTypeInstanceNewData(relativeTo.file, ext.info, data, forceFromCldb);
                else
                    ext.instance = null;
                ext.file = relativeTo;
            }
            return ext;
        }

        public static AssetTypeInstance GetTypeInstanceNewData(this AssetsManager _this, AssetsFile file, AssetFileInfoEx info,
                                                               Stream data, bool forceFromCldb = false)
        {
            return new AssetTypeInstance(_this.GetTemplateBaseField(file, info, forceFromCldb), new AssetsFileReader(data), 0);
        }

        public static void UnpackInfoOnly(this AssetBundleFile bundle)
        {
            AssetsFileReader reader = bundle.reader;

            reader.Position = 0;
            if (bundle.Read(reader, true))
            {
                reader.Position = bundle.bundleHeader6.GetBundleInfoOffset();
                MemoryStream blocksInfoStream;
                AssetsFileReader memReader;
                int compressedSize = (int)bundle.bundleHeader6.compressedSize;
                switch (bundle.bundleHeader6.GetCompressionType())
                {
                    case 1:
                        using (MemoryStream mstream = new MemoryStream(reader.ReadBytes(compressedSize)))
                        {
                            blocksInfoStream = SevenZipHelper.StreamDecompress(mstream);
                        }
                        break;
                    case 2:
                    case 3:
                        byte[] uncompressedBytes = new byte[bundle.bundleHeader6.decompressedSize];
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
                if (bundle.bundleHeader6.GetCompressionType() != 0)
                {
                    using (memReader = new AssetsFileReader(blocksInfoStream))
                    {
                        memReader.Position = 0;
                        bundle.bundleInf6.Read(0, memReader);
                    }
                }
            }
        }
    }
}
