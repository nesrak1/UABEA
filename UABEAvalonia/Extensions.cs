using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Extra.Decompressors.LZ4;
using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class Extensions
    {
        //cheap * search check
        public static bool WildcardMatches(string test, string pattern, bool caseSensitive = true)
        {
            RegexOptions options = 0;
            if (!caseSensitive)
                options |= RegexOptions.IgnoreCase;

            return Regex.IsMatch(test, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", options);
        }

        public static void GetUABENameFast(AssetContainer cont, ClassDatabaseFile cldb, out string assetName, out string typeName)
        {
            GetUABENameFast(cont.FileInstance.file, cldb, cont.FileReader, cont.FilePosition, cont.ClassId, cont.MonoId, out assetName, out typeName);
        }

        //codeflow needs work but should be fine for now
        public static void GetUABENameFast(AssetsFile file, ClassDatabaseFile cldb, AssetsFileReader reader, long filePosition, uint classId, ushort monoId,
                                           out string assetName, out string typeName)
        {
            ClassDatabaseType type = AssetHelper.FindAssetClassByID(cldb, classId);

            if (file.typeTree.hasTypeTree)
            {
                Type_0D ttType;
                if (classId == 0x72)
                    ttType = AssetHelper.FindTypeTreeTypeByScriptIndex(file.typeTree, monoId);
                else
                    ttType = AssetHelper.FindTypeTreeTypeByID(file.typeTree, classId);

                if (ttType != null && ttType.typeFieldsEx.Length != 0)
                {
                    typeName = ttType.typeFieldsEx[0].GetTypeString(ttType.stringTable);
                    if (ttType.typeFieldsEx.Length > 1 && ttType.typeFieldsEx[1].GetNameString(ttType.stringTable) == "m_Name")
                    {
                        reader.Position = filePosition;
                        assetName = reader.ReadCountStringInt32();
                        if (assetName == "")
                            assetName = "Unnamed asset";
                        return;
                    }
                    else if (typeName == "GameObject")
                    {
                        reader.Position = filePosition;
                        int size = reader.ReadInt32();
                        int componentSize = file.header.format > 0x10 ? 0x0c : 0x10;
                        reader.Position += size * componentSize;
                        reader.Position += 0x04;
                        assetName = reader.ReadCountStringInt32();
                        assetName = $"GameObject {assetName}";
                        return;
                    }
                    else if (typeName == "MonoBehaviour")
                    {
                        reader.Position = filePosition;
                        reader.Position += 0x1c;
                        assetName = reader.ReadCountStringInt32();
                        if (assetName == "")
                            assetName = "Unnamed asset";
                        return;
                    }
                    assetName = "Unnamed asset";
                    return;
                }
            }

            if (type == null)
            {
                typeName = $"0x{classId:X8}";
                assetName = "Unnamed asset";
                return;
            }

            typeName = type.name.GetString(cldb);

            if (type.fields.Count == 0)
            {
                assetName = "Unnamed asset";
                return;
            }

            if (type.fields.Count > 1 && type.fields[1].fieldName.GetString(cldb) == "m_Name")
            {
                reader.Position = filePosition;
                assetName = reader.ReadCountStringInt32();
                if (assetName == "")
                    assetName = "Unnamed asset";
                return;
            }
            else if (typeName == "GameObject")
            {
                reader.Position = filePosition;
                int size = reader.ReadInt32();
                int componentSize = file.header.format > 0x10 ? 0x0c : 0x10;
                reader.Position += size * componentSize;
                reader.Position += 0x04;
                assetName = reader.ReadCountStringInt32();
                assetName = $"GameObject {assetName}";
                return;
            }
            else if (typeName == "MonoBehaviour")
            {
                reader.Position = filePosition;
                reader.Position += 0x1c;
                assetName = reader.ReadCountStringInt32();
                if (assetName == "")
                    assetName = "Unnamed asset";
                return;
            }
            assetName = "Unnamed asset";
            return;
        }

        #region REMOVE WHEN ASSETS TOOLS NUGET UPDATED
        public static AssetExternal GetExtAssetFixed(this AssetsManager _this, AssetsFileInstance relativeTo, int fileId, long pathId,
                                                     bool onlyGetInfo = false, bool forceFromCldb = false)
        {
            AssetExternal ext = new AssetExternal
            {
                info = null,
                instance = null,
                file = null
            };

            if (fileId == 0 && pathId == 0)
            {
                return ext;
            }
            else if (fileId != 0)
            {
                AssetsFileInstance dep = relativeTo.GetDependency(_this, fileId - 1);

                if (dep == null)
                    return ext;

                ext.file = dep;
                ext.info = dep.table.GetAssetInfo(pathId);

                if (ext.info == null)
                    return ext;

                if (!onlyGetInfo)
                    ext.instance = _this.GetTypeInstance(dep.file, ext.info, forceFromCldb);
                else
                    ext.instance = null;

                return ext;
            }
            else
            {
                ext.file = relativeTo;
                ext.info = relativeTo.table.GetAssetInfo(pathId);

                if (ext.info == null)
                    return ext;

                if (!onlyGetInfo)
                    ext.instance = _this.GetTypeInstance(relativeTo.file, ext.info, forceFromCldb);
                else
                    ext.instance = null;

                return ext;
            }
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

        public static Type_0D FindTypeTreeTypeByID(TypeTree typeTree, uint id, ushort scriptIndex)
        {
            foreach (Type_0D type in typeTree.unity5Types)
            {
                if (type.classId == id && type.scriptIndex == scriptIndex)
                    return type;
            }
            return null;
        }
        #endregion
    }
}
