using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
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
        public HashSet<string> LoadedFileNames { get; }
        // todo: replace assetid -> assetpptr
        public Dictionary<AssetID, AssetContainer> LoadedAssets { get; }

        public Dictionary<string, AssetsFileInstance> LoadedFileLookup { get; }

        public Dictionary<AssetID, AssetsReplacer> NewAssets { get; }
        public Dictionary<AssetID, Stream> NewAssetDatas { get; } //for preview in info window
        public HashSet<AssetID> RemovedAssets { get; }

        // we have to do this because we want to be able to tell
        // if all changes we've made have been removed so that
        // we can know not to save it. for example, if we removed
        // all replacers, we still need to save it if there were
        // changes to dependencies.
        public Dictionary<AssetsFileInstance, AssetsFileChangeTypes> OtherAssetChanges { get; }

        public bool Modified { get; set; }

        public delegate void AssetWorkspaceItemUpdateEvent(AssetsFileInstance file, AssetID assetId);
        public event AssetWorkspaceItemUpdateEvent? ItemUpdated;

        public delegate void MonoTemplateFailureEvent(string path);
        public event MonoTemplateFailureEvent? MonoTemplateLoadFailed;

        private bool setMonoTempGeneratorsYet;

        public AssetWorkspace(AssetsManager am, bool fromBundle)
        {
            this.am = am;
            this.fromBundle = fromBundle;

            LoadedFiles = new List<AssetsFileInstance>();
            LoadedFileNames = new HashSet<string>();
            LoadedAssets = new Dictionary<AssetID, AssetContainer>();

            LoadedFileLookup = new Dictionary<string, AssetsFileInstance>();

            NewAssets = new Dictionary<AssetID, AssetsReplacer>();
            NewAssetDatas = new Dictionary<AssetID, Stream>();
            RemovedAssets = new HashSet<AssetID>();

            OtherAssetChanges = new Dictionary<AssetsFileInstance, AssetsFileChangeTypes>();

            Modified = false;

            setMonoTempGeneratorsYet = false;
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
                    reader, 0, replacer.GetPathID(), replacer.GetClassID(),
                    replacer.GetMonoScriptID(), (uint)previewStream.Length, forFile);

                LoadedAssets[assetId] = cont;
            }
            else
            {
                LoadedAssets.Remove(assetId);
            }

            ItemUpdated?.Invoke(forFile, assetId);

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

            ItemUpdated?.Invoke(forFile, assetId);

            if (NewAssets.Count == 0 && !AnyOtherAssetChanges())
                Modified = false;
        }

        public void LoadAssetsFile(AssetsFileInstance fromFile, bool loadDependencies)
        {
            string fromFilePath = fromFile.path.ToLower();
            if (LoadedFileNames.Contains(fromFilePath))
                return;

            fromFile.file.GenerateQuickLookupTree();

            LoadedFiles.Add(fromFile);
            LoadedFileNames.Add(fromFile.path.ToLower());

            foreach (AssetFileInfo info in fromFile.file.AssetInfos)
            {
                AssetContainer cont = new AssetContainer(info, fromFile);
                LoadedAssets.Add(cont.AssetId, cont);
            }

            if (loadDependencies)
            {
                for (int i = 0; i < fromFile.file.Metadata.Externals.Count; i++)
                {
                    AssetsFileInstance dep = fromFile.GetDependency(am, i);
                    if (dep == null)
                        continue;

                    string depPath = dep.path.ToLower();
                    if (!LoadedFileNames.Contains(depPath))
                    {
                        LoadAssetsFile(dep, true);
                    }
                    else
                    {
                        continue;
                    }
                }
            }
        }

        // todo: unload file

        // todo: not very fast and this loop happens twice since it iterates again during write
        public HashSet<AssetsFileInstance> GetChangedFiles()
        {
            HashSet<AssetsFileInstance> changedFiles = new HashSet<AssetsFileInstance>();
            foreach (var newAsset in NewAssets)
            {
                AssetID assetId = newAsset.Key;
                string fileName = assetId.fileName;

                if (LoadedFileLookup.TryGetValue(fileName.ToLower(), out AssetsFileInstance? file))
                {
                    changedFiles.Add(file);
                }
            }

            foreach (var assetChangePair in OtherAssetChanges)
            {
                if (assetChangePair.Value != 0)
                    changedFiles.Add(assetChangePair.Key);
            }

            return changedFiles;
        }

        public void SetOtherAssetChangeFlag(AssetsFileInstance fileInst, AssetsFileChangeTypes changeTypes)
        {
            if (!OtherAssetChanges.ContainsKey(fileInst))
                OtherAssetChanges[fileInst] = AssetsFileChangeTypes.None;

            OtherAssetChanges[fileInst] |= changeTypes;
        }

        public void UnsetOtherAssetChangeFlag(AssetsFileInstance fileInst, AssetsFileChangeTypes changeTypes)
        {
            if (!OtherAssetChanges.ContainsKey(fileInst))
                OtherAssetChanges[fileInst] = AssetsFileChangeTypes.None;

            OtherAssetChanges[fileInst] &= ~changeTypes;

            if (OtherAssetChanges[fileInst] == AssetsFileChangeTypes.None)
                OtherAssetChanges.Remove(fileInst);
        }

        private bool AnyOtherAssetChanges()
        {
            foreach (var assetChangePair in OtherAssetChanges)
            {
                if (assetChangePair.Value != 0)
                    return true;
            }
            return false;
        }

        public void GenerateAssetsFileLookup()
        {
            foreach (AssetsFileInstance inst in LoadedFiles)
            {
                LoadedFileLookup[inst.path.ToLower()] = inst;
            }
        }

        public AssetTypeTemplateField GetTemplateField(AssetContainer cont, bool forceCldb = false, bool skipMonoBehaviourFields = false)
        {
            AssetReadFlags readFlags = AssetReadFlags.None;
            if (forceCldb)
                readFlags |= AssetReadFlags.ForceFromCldb;
            if (skipMonoBehaviourFields)
                readFlags |= AssetReadFlags.SkipMonoBehaviourFields;

            return am.GetTemplateBaseField(cont.FileInstance, cont.FileReader, cont.FilePosition, cont.ClassId, cont.MonoId, readFlags);
        }

        public AssetContainer? GetAssetContainer(AssetsFileInstance fileInst, int fileId, long pathId, bool onlyInfo = true)
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
                    if (!onlyInfo && !cont.HasValueField)
                    {
                        // only set mono temp generator when we open a MonoBehaviour
                        bool isMonoBehaviour = cont.ClassId == (int)AssetClassID.MonoBehaviour || cont.ClassId < 0;
                        if (isMonoBehaviour && !setMonoTempGeneratorsYet && !fileInst.file.Metadata.TypeTreeEnabled)
                        {
                            string dataDir = Extensions.GetAssetsFileDirectory(fileInst);
                            bool success = SetMonoTempGenerators(dataDir);
                            if (!success)
                            {
                                MonoTemplateLoadFailed?.Invoke(dataDir);
                            }
                        }

                        try
                        {
                            AssetTypeTemplateField tempField = GetTemplateField(cont);

                            RefTypeManager? refMan = null;
                            if (isMonoBehaviour)
                            {
                                refMan = am.GetRefTypeManager(fileInst);
                            }

                            AssetTypeValueField baseField = tempField.MakeValue(cont.FileReader, cont.FilePosition, refMan);
                            cont = new AssetContainer(cont, baseField);
                        }
                        catch
                        {
                            cont = null;
                        }
                    }
                    return cont;
                }
            }
            return null;
        }

        public AssetContainer GetAssetContainer(AssetsFileInstance fileInst, AssetTypeValueField pptrField, bool onlyInfo = true)
        {
            int fileId = pptrField["m_FileID"].AsInt;
            long pathId = pptrField["m_PathID"].AsLong;
            return GetAssetContainer(fileInst, fileId, pathId, onlyInfo);
        }

        public AssetContainer GetAssetContainer(AssetsFileInstance fileInst, AssetPPtr pptr, bool onlyInfo = true)
        {
            int fileId = pptr.FileId;
            long pathId = pptr.PathId;
            return GetAssetContainer(fileInst, fileId, pathId, onlyInfo);
        }

        // todo: just overwrite original container values
        public AssetContainer GetAssetContainer(AssetContainer cont)
        {
            return GetAssetContainer(cont.FileInstance, 0, cont.PathId, false);
        }

        public List<AssetContainer> GetAssetsOfType(int classId)
        {
            List<AssetContainer> filteredAssets = new List<AssetContainer>();

            var allConts = LoadedAssets.Values;
            foreach (AssetContainer cont in allConts)
            {
                if (cont == null)
                    continue;

                if (cont.ClassId == classId)
                {
                    filteredAssets.Add(cont);
                }
            }

            return filteredAssets;
        }

        public List<AssetContainer> GetAssetsOfType(AssetClassID classId)
        {
            return GetAssetsOfType((int)classId);
        }

        public AssetTypeValueField? GetBaseField(AssetContainer cont)
        {
            if (cont.HasValueField)
                return cont.BaseValueField;

            cont = GetAssetContainer(cont.FileInstance, 0, cont.PathId, false);
            if (cont != null)
                return cont.BaseValueField;
            else
                return null;
        }

        public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, int fileId, long pathId)
        {
            AssetContainer? cont = GetAssetContainer(fileInst, fileId, pathId, false);
            if (cont != null)
                return GetBaseField(cont);
            else
                return null;
        }

        public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
        {
            int fileId = pptrField["m_FileID"].AsInt;
            long pathId = pptrField["m_PathID"].AsLong;

            AssetContainer? cont = GetAssetContainer(fileInst, fileId, pathId, false);
            if (cont != null)
                return GetBaseField(cont);
            else
                return null;
        }

        public AssetTypeValueField GetConcatMonoBaseField(AssetContainer cont, string managedPath)
        {
            AssetTypeTemplateField baseTemp = GetConcatMonoTemplateField(cont, managedPath);
            return baseTemp.MakeValue(cont.FileReader, cont.FilePosition);
        }

        public AssetTypeTemplateField GetConcatMonoTemplateField(AssetContainer cont, string managedPath)
        {
            AssetsFile file = cont.FileInstance.file;
            AssetTypeTemplateField baseTemp = GetTemplateField(cont);

            ushort scriptIndex = cont.MonoId;
            if (scriptIndex != 0xFFFF)
            {
                AssetTypeValueField baseField = baseTemp.MakeValue(cont.FileReader, cont.FilePosition);

                AssetContainer monoScriptCont = GetAssetContainer(cont.FileInstance, baseField["m_Script"], false);
                if (monoScriptCont == null)
                    return baseTemp;

                AssetTypeValueField scriptBaseField = monoScriptCont.BaseValueField;
                if (scriptBaseField == null)
                    return baseTemp;

                string scriptClassName = scriptBaseField["m_ClassName"].AsString;
                string scriptNamespace = scriptBaseField["m_Namespace"].AsString;
                string assemblyName = scriptBaseField["m_AssemblyName"].AsString;
                string assemblyPath = Path.Combine(managedPath, assemblyName);

                if (!File.Exists(assemblyPath))
                    return baseTemp;

                MonoCecilTempGenerator mc = new MonoCecilTempGenerator(managedPath);
                baseTemp = mc.GetTemplateField(baseTemp, assemblyName, scriptNamespace, scriptClassName, new UnityVersion(file.Metadata.UnityVersion));
            }
            return baseTemp;
        }

        public bool SetMonoTempGenerators(string fileDir)
        {
            if (!setMonoTempGeneratorsYet)
            {
                setMonoTempGeneratorsYet = true;
                FindCpp2IlFilesResult il2cppFiles = FindCpp2IlFiles.Find(fileDir);
                if (il2cppFiles.success && ConfigurationManager.Settings.UseCpp2Il)
                {
                    am.MonoTempGenerator = new Cpp2IlTempGenerator(il2cppFiles.metaPath, il2cppFiles.asmPath);
                    return true;
                }
                else
                {
                    string managedDir = Path.Combine(fileDir, "Managed");
                    if (Directory.Exists(managedDir))
                    {
                        am.MonoTempGenerator = new MonoCecilTempGenerator(managedDir);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
