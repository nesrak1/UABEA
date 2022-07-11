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

        //codeflow needs work but should be fine for now
        public static void GetUABENameFast(AssetWorkspace workspace, AssetContainer cont, bool usePrefix, out string assetName, out string typeName)
        {
            assetName = "Unnamed asset";
            typeName = "Unknown type";

            try
            {
                ClassDatabaseFile cldb = workspace.am.classDatabase;
                AssetsFile file = cont.FileInstance.file;
                AssetsFileReader reader = cont.FileReader;
                long filePosition = cont.FilePosition;
                int classId = cont.ClassId;
                ushort monoId = cont.MonoId;

                ClassDatabaseType type = AssetHelper.FindAssetClassByID(cldb, classId);

                if (file.Metadata.TypeTreeEnabled)
                {
                    TypeTreeType ttType;
                    if (classId == 0x72)
                        ttType = AssetHelper.FindTypeTreeTypeByScriptIndex(file.Metadata, monoId);
                    else
                        ttType = AssetHelper.FindTypeTreeTypeByID(file.Metadata, classId);

                    if (ttType != null && ttType.Nodes.Count > 0)
                    {
                        typeName = ttType.Nodes[0].GetTypeString(ttType.StringBuffer);
                        if (ttType.Nodes.Count > 1 && ttType.Nodes[1].GetNameString(ttType.StringBuffer) == "m_Name")
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
                            int componentSize = file.Header.Version > 0x10 ? 0x0c : 0x10;
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
                            {
                                assetName = GetMonoBehaviourNameFast(workspace, cont);
                                if (assetName == "")
                                    assetName = "Unnamed asset";
                            }
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

                typeName = cldb.GetString(type.Name);
                List<ClassDatabaseTypeNode> cldbNodes = type.GetPreferredNode(false).Children;

                if (cldbNodes.Count == 0)
                {
                    assetName = "Unnamed asset";
                    return;
                }

                if (cldbNodes.Count > 1 && cldb.GetString(cldbNodes[1].FieldName) == "m_Name")
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
                    int componentSize = file.Header.Version > 0x10 ? 0x0c : 0x10;
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
                    {
                        assetName = GetMonoBehaviourNameFast(workspace, cont);
                        if (assetName == "")
                            assetName = "Unnamed asset";
                    }
                    return;
                }
                assetName = "Unnamed asset";
            }
            catch
            {
            }
        }

        //not very fast but w/e at least it's stable
        public static string GetMonoBehaviourNameFast(AssetWorkspace workspace, AssetContainer cont)
        {
            try
            {
                if (cont.ClassId != (uint)AssetClassID.MonoBehaviour)
                    return string.Empty;

                AssetTypeValueField monoBf;
                if (cont.HasValueField)
                {
                    monoBf = cont.BaseValueField;
                }
                else
                {
                    AssetTypeTemplateField monoTemp = workspace.GetTemplateField(cont, false);
                    monoBf = monoTemp.MakeValue(cont.FileReader, cont.FilePosition);
                }

                AssetContainer monoScriptCont = workspace.GetAssetContainer(cont.FileInstance, monoBf["m_Script"], false);
                if (monoScriptCont == null)
                    return string.Empty;

                AssetTypeValueField scriptBaseField = monoScriptCont.BaseValueField;
                string scriptClassName = scriptBaseField["m_ClassName"].AsString;

                return scriptClassName;
            }
            catch
            {
                return string.Empty;
            }
        }

        //https://stackoverflow.com/a/23182807
        public static string ReplaceInvalidPathChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        private static string[] byteSizeSuffixes = new string[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        public static string GetFormattedByteSize(long size)
        {
            int log = (int)Math.Log(size, 1024);
            double div = log == 0 ? 1 : Math.Pow(1024, log);
            double num = size / div;
            return $"{num:f2}{byteSizeSuffixes[log]}";
        }

        public static List<string> GetFilesInDirectory(string path, List<string> extensions)
        {
            List<string> files = new List<string>();
            foreach (string extension in extensions)
            {
                files.AddRange(Directory.GetFiles(path, "*." + extension));
            }
            return files;
        }
    }
}
