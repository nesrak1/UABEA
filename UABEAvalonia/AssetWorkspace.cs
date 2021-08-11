using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class AssetWorkspace
    {
        public AssetsManager am { get; }
        public bool fromBundle { get; }

        public List<AssetsFileInstance> LoadedFiles { get; }
        public Dictionary<AssetID, AssetContainer> LoadedAssets { get; }

        public Dictionary<string, AssetsFileInstance> LoadedFileLookup { get; }
        public Dictionary<string, AssemblyDefinition> LoadedAssemblies { get; }

        public Dictionary<AssetID, AssetsReplacer> NewAssets { get; }
        public Dictionary<AssetID, Stream> NewAssetDatas { get; } //for preview in info window
        public HashSet<AssetID> RemovedAssets { get; }

        public bool Modified { get; set; }

        public delegate void AssetWorkspaceItemUpdateEvent(AssetsFileInstance file, AssetID assetId);
        public event AssetWorkspaceItemUpdateEvent? ItemUpdated;

        public AssetWorkspace(AssetsManager am, bool fromBundle)
        {
            this.am = am;
            this.fromBundle = fromBundle;

            LoadedFiles = new List<AssetsFileInstance>();
            LoadedAssets = new Dictionary<AssetID, AssetContainer>();

            LoadedFileLookup = new Dictionary<string, AssetsFileInstance>();
            LoadedAssemblies = new Dictionary<string, AssemblyDefinition>();

            NewAssets = new Dictionary<AssetID, AssetsReplacer>();
            NewAssetDatas = new Dictionary<AssetID, Stream>();
            RemovedAssets = new HashSet<AssetID>();

            Modified = false;
        }

        public void AddReplacer(AssetsFileInstance forFile, AssetsReplacer replacer, Stream? previewStream = null)
        {
            AssetsFile assetsFile = forFile.file;
            AssetID assetId = new AssetID(forFile.path, replacer.GetPathID());

            if (NewAssets.ContainsKey(assetId))
                RemoveReplacer(forFile, NewAssets[assetId], true);

            NewAssets[assetId] = replacer;

            //make stream to use as a replacement to the one from file
            if (previewStream == null)
            {
                MemoryStream newStream = new MemoryStream();
                AssetsFileWriter newWriter = new AssetsFileWriter(newStream);
                replacer.Write(newWriter);
                newStream.Position = 0;
                previewStream = newStream;
            }
            NewAssetDatas[assetId] = previewStream;

            if (!(replacer is AssetsRemover))
            {
                AssetsFileReader reader = new AssetsFileReader(previewStream);
                AssetContainer cont = new AssetContainer(
                    reader, 0, replacer.GetPathID(), (uint)replacer.GetClassID(),
                    replacer.GetMonoScriptID(), (uint)previewStream.Length, forFile);

                LoadedAssets[assetId] = cont;
            }
            else
            {
                LoadedAssets.Remove(assetId);
            }

            if (ItemUpdated != null)
                ItemUpdated(forFile, assetId);

            Modified = true;
        }

        public void RemoveReplacer(AssetsFileInstance forFile, AssetsReplacer replacer, bool closePreviewStream = true)
        {
            AssetID assetId = new AssetID(forFile.path, replacer.GetPathID());

            if (NewAssets.ContainsKey(assetId))
            {
                NewAssets.Remove(assetId);
            }
            if (NewAssetDatas.ContainsKey(assetId))
            {
                if (closePreviewStream)
                    NewAssetDatas[assetId].Close();
                NewAssetDatas.Remove(assetId);
            }
            if (replacer is AssetsRemover && RemovedAssets.Contains(assetId))
                RemovedAssets.Remove(assetId);

            if (ItemUpdated != null)
                ItemUpdated(forFile, assetId);

            if (NewAssets.Count == 0)
                Modified = false;
        }

        public void GenerateAssetsFileLookup()
        {
            foreach (AssetsFileInstance inst in LoadedFiles)
            {
                LoadedFileLookup[inst.path.ToLower()] = inst;
            }
        }

        public AssetTypeTemplateField GetTemplateField(AssetsFile file, uint type, ushort scriptIndex)
        {
            uint fixedId = AssetHelper.FixAudioID(type);
            bool hasTypeTree = file.typeTree.hasTypeTree;

            AssetTypeTemplateField baseField = new AssetTypeTemplateField();
            if (hasTypeTree)
            {
                Type_0D type0d = AssetHelper.FindTypeTreeTypeByID(file.typeTree, fixedId, scriptIndex);

                if (type0d != null && type0d.typeFieldsExCount > 0)
                    baseField.From0D(type0d, 0);
                else //fallback to cldb
                    baseField.FromClassDatabase(am.classFile, AssetHelper.FindAssetClassByID(am.classFile, fixedId), 0);
            }
            else
            {
                baseField.FromClassDatabase(am.classFile, AssetHelper.FindAssetClassByID(am.classFile, fixedId), 0);
            }

            return baseField;
        }

        public AssetContainer GetAssetContainer(AssetsFileInstance fileInst, int fileId, long pathId, bool onlyInfo = true)
        {
            if (fileId != 0)
            {
                fileInst = fileInst.GetDependency(am, fileId - 1);
            }

            if (fileInst != null)
            {
                AssetID assetId = new AssetID(fileInst.path, pathId);
                if (LoadedAssets.TryGetValue(assetId, out AssetContainer? cont))
                {
                    if (!onlyInfo && !cont.HasInstance)
                    {
                        AssetTypeTemplateField tempField = GetTemplateField(cont.FileInstance.file, cont.ClassId, cont.MonoId);
                        AssetTypeInstance typeInst = new AssetTypeInstance(tempField, cont.FileReader, cont.FilePosition);
                        cont = new AssetContainer(cont, typeInst);
                    }
                    return cont;
                }
            }
            return null;
        }

        public AssetContainer GetAssetContainer(AssetsFileInstance fileInst, AssetTypeValueField pptrField, bool onlyInfo = true)
        {
            int fileId = pptrField.Get("m_FileID").GetValue().AsInt();
            long pathId = pptrField.Get("m_PathID").GetValue().AsInt64();
            return GetAssetContainer(fileInst, fileId, pathId, onlyInfo);
        }

        public AssetTypeValueField GetBaseField(AssetContainer cont)
        {
            AssetsFileInstance fileInst = cont.FileInstance;
            if (cont.ClassId == 0x72)
            {
                TypeTree tt = cont.FileInstance.file.typeTree;
                //check if typetree data exists already
                if (!tt.hasTypeTree || AssetHelper.FindTypeTreeTypeByScriptIndex(tt, cont.MonoId) == null)
                {
                    //deserialize from dll (todo: ask user if dll isn't in normal location)
                    string filePath;
                    if (fileInst.parentBundle != null)
                        filePath = Path.GetDirectoryName(fileInst.parentBundle.path);
                    else
                        filePath = Path.GetDirectoryName(fileInst.path);

                    string managedPath = Path.Combine(filePath, "Managed");
                    if (Directory.Exists(managedPath))
                    {
                        AssetTypeValueField monoBaseField = GetConcatMonoBaseField(cont, managedPath);
                        if (monoBaseField != null)
                            return monoBaseField;
                    }
                    //fallback to no mono deserialization for now
                }
            }

            cont = GetAssetContainer(cont.FileInstance, 0, cont.PathId, false);
            if (cont != null)
                return cont.TypeInstance.GetBaseField();
            else
                return null;
        }

        public AssetTypeValueField GetBaseField(AssetsFileInstance fileInst, int fileId, long pathId)
        {
            AssetContainer? cont = GetAssetContainer(fileInst, fileId, pathId, false);
            if (cont != null)
                return GetBaseField(cont);
            else
                return null;
        }

        public AssetTypeValueField GetConcatMonoBaseField(AssetContainer cont, string managedPath)
        {
            AssetsFile file = cont.FileInstance.file;
            AssetTypeTemplateField baseTemp = new AssetTypeTemplateField();
            baseTemp.FromClassDatabase(am.classFile, AssetHelper.FindAssetClassByID(am.classFile, cont.ClassId), 0);
            AssetTypeInstance mainAti = new AssetTypeInstance(baseTemp, cont.FileReader, cont.FilePosition);
            ushort scriptIndex = cont.MonoId;
            if (scriptIndex != 0xFFFF)
            {
                AssetContainer monoScriptCont = GetAssetContainer(cont.FileInstance, mainAti.GetBaseField().Get("m_Script"), false);
                if (monoScriptCont == null)
                    return null;

                AssetTypeValueField scriptBaseField = monoScriptCont.TypeInstance.GetBaseField();
                string scriptName = scriptBaseField.Get("m_Name").GetValue().AsString();
                string scriptNamespace = scriptBaseField.Get("m_Namespace").GetValue().AsString();
                string assemblyName = scriptBaseField.Get("m_AssemblyName").GetValue().AsString();
                string assemblyPath = Path.Combine(managedPath, assemblyName);

                if (scriptNamespace != string.Empty)
                    scriptName = scriptNamespace + "." + scriptName;

                if (File.Exists(assemblyPath))
                {
                    AssemblyDefinition asmDef;

                    if (!LoadedAssemblies.ContainsKey(assemblyName))
                    {
                        LoadedAssemblies.Add(assemblyName, MonoDeserializer.GetAssemblyWithDependencies(assemblyPath));
                    }
                    asmDef = LoadedAssemblies[assemblyName];

                    MonoDeserializer mc = new MonoDeserializer();
                    mc.Read(scriptName, asmDef, file.header.format);
                    List<AssetTypeTemplateField> monoTemplateFields = mc.children;

                    AssetTypeTemplateField[] templateField = baseTemp.children.Concat(monoTemplateFields).ToArray();
                    baseTemp.children = templateField;
                    baseTemp.childrenCount = baseTemp.children.Length;

                    mainAti = new AssetTypeInstance(baseTemp, cont.FileReader, cont.FilePosition);
                }
            }
            return mainAti.GetBaseField();
        }
    }
}
