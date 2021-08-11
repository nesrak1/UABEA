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

        public static void GetUABENameFast(AssetContainer cont, ClassDatabaseFile cldb, bool usePrefix, out string assetName, out string typeName)
        {
            GetUABENameFast(cont.FileInstance.file, cldb, cont.FileReader, cont.FilePosition, cont.ClassId, cont.MonoId, usePrefix, out assetName, out typeName);
        }

        //codeflow needs work but should be fine for now
        public static void GetUABENameFast(AssetsFile file, ClassDatabaseFile cldb, AssetsFileReader reader, long filePosition, uint classId, ushort monoId,
                                           bool usePrefix, out string assetName, out string typeName)
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
                        if (usePrefix)
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
                if (usePrefix)
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

        //https://stackoverflow.com/a/23182807
        public static string ReplaceInvalidPathChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
