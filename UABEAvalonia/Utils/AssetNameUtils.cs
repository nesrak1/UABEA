using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class AssetNameUtils
    {
        // codeflow needs work but should be fine for now
        public static void GetDisplayNameFast(AssetWorkspace workspace, AssetContainer cont, bool usePrefix, out string assetName, out string typeName)
        {
            assetName = "Unnamed asset";
            typeName = "Unknown type";

            try
            {
                ClassDatabaseFile cldb = workspace.am.ClassDatabase;
                AssetsFile file = cont.FileInstance.file;
                AssetsFileReader reader = cont.FileReader;
                long filePosition = cont.FilePosition;
                int classId = cont.ClassId;
                ushort monoId = cont.MonoId;

                ClassDatabaseType type = cldb.FindAssetClassByID(classId);

                if (file.Metadata.TypeTreeEnabled)
                {
                    TypeTreeType ttType;
                    if (classId == 0x72 || classId < 0)
                        ttType = file.Metadata.FindTypeTreeTypeByScriptIndex(monoId);
                    else
                        ttType = file.Metadata.FindTypeTreeTypeByID(classId);

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

                if (cldbNodes.Count > 1 && cldb.GetString(cldbNodes[0].FieldName) == "m_Name")
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

        // not very fast but w/e at least it's stable
        public static string GetMonoBehaviourNameFast(AssetWorkspace workspace, AssetContainer cont)
        {
            try
            {
                if (cont.ClassId != (uint)AssetClassID.MonoBehaviour && cont.ClassId >= 0)
                    return string.Empty;

                AssetTypeValueField monoBf;
                if (cont.HasValueField)
                {
                    monoBf = cont.BaseValueField;
                }
                else
                {
                    // this is a bad idea. this directly calls am.GetTemplateField
                    // which won't look for new MonoScripts from UABEA.
                    // hasTypeTree is set to false to ignore type tree (to prevent
                    // reading the entire MonoBehaviour if type trees are provided)

                    // it might be a better idea to just temporarily remove the extra
                    // fields from a single MonoBehaviour so we don't have to read
                    // from the cldb (especially so for stripped versions of bundles)

                    bool wasUsingCache = workspace.am.UseTemplateFieldCache;
                    workspace.am.UseTemplateFieldCache = false;
                    AssetTypeTemplateField monoTemp = workspace.GetTemplateField(cont, true, true);
                    workspace.am.UseTemplateFieldCache = wasUsingCache;

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
    }
}
