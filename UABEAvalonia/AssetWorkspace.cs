using AssetsTools.NET;
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
        public AssetsFileInstance mainFile { get; }
        public bool fromBundle { get; }

        public List<AssetsFileInstance> LoadedFiles { get; }
        public List<AssetExternal> LoadedAssets { get; }
        
        public Dictionary<AssetID, AssetsReplacer> NewAssets { get; }
        public Dictionary<AssetID, Stream> NewAssetDatas { get; } //for preview in info window

        public bool Modified { get; set; }
        public string AssetsFileName { get; }

        public delegate void AssetWorkspaceItemUpdateEvent(AssetID updatedAssetId);
        public event AssetWorkspaceItemUpdateEvent? ItemUpdated;

        public AssetWorkspace(AssetsManager am, AssetsFileInstance assetsFile, bool fromBundle, string assetsFileName)
        {
            this.am = am;
            this.mainFile = assetsFile;
            this.fromBundle = fromBundle;

            LoadedFiles = new List<AssetsFileInstance>();
            LoadedAssets = new List<AssetExternal>();

            NewAssets = new Dictionary<AssetID, AssetsReplacer>();
            NewAssetDatas = new Dictionary<AssetID, Stream>();

            Modified = false;

            AssetsFileName = assetsFileName;
        }

        public void AddReplacer(AssetsFileInstance forFile, AssetsReplacer replacer, Stream? previewStream = null)
        {
            AssetID assetId = new AssetID(forFile.path, replacer.GetPathID());

            NewAssets[assetId] = replacer;
            if (previewStream == null)
            {
                MemoryStream newStream = new MemoryStream();
                AssetsFileWriter newWriter = new AssetsFileWriter(newStream);
                replacer.Write(newWriter);
                newStream.Position = 0;
                NewAssetDatas[assetId] = newStream;
            }
            else
            {
                NewAssetDatas[assetId] = previewStream;
            }

            if (ItemUpdated != null)
                ItemUpdated(assetId);

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

            if (ItemUpdated != null)
                ItemUpdated(assetId);

            if (NewAssets.Count == 0)
                Modified = false;
        }

        public AssetExternal GetExtAssetReplaced(AssetsFileInstance fromFile, int fileId, long pathId, bool onlyInfo = false)
        {
            AssetExternal infoExt = am.GetExtAsset(fromFile, fileId, pathId, true);
            AssetFileInfoEx info = infoExt.info;
            AssetsFileInstance fileInst = infoExt.file;
            AssetsFile file = fileInst.file;

            if (!onlyInfo)
            {
                AssetID assetId = new AssetID(infoExt.file.path, pathId);

                if (NewAssetDatas.ContainsKey(assetId))
                {
                    return am.GetExtAssetNewData(fileInst, fileId, pathId, NewAssetDatas[assetId]);
                }
                else if (info.curFileType == 0x72)
                {
                    if (file.typeTree.hasTypeTree && AssetHelper.FindTypeTreeTypeByScriptIndex(file.typeTree, info.scriptIndex) != null)
                    {
                        //typetree data exists already, use that instead (automatically used by getextasset already)
                        return am.GetExtAsset(fileInst, fileId, pathId);
                    }
                    else
                    {
                        //deserialize from dll (todo: ask user if dll isn't in normal location)
                        string managedPath = Path.Combine(Path.GetDirectoryName(fileInst.path), "Managed");
                        if (Directory.Exists(managedPath))
                        {
                            //assetexternal uses ati but assetsmanager's monobehaviour
                            //deserializer only returns a basefield lol what dumb design
                            //I will be coming back to this soon
                            AssetTypeInstance fakeAti = new AssetTypeInstance(new AssetTypeTemplateField[0], file.reader, 0);
                            fakeAti.baseFields = new AssetTypeValueField[] { am.GetMonoBaseFieldCached(fileInst, info, managedPath) };
                            fakeAti.baseFieldCount = 1;

                            AssetExternal monoExt = new AssetExternal()
                            {
                                file = infoExt.file,
                                info = infoExt.info,
                                instance = fakeAti
                            };
                            return monoExt;
                        }
                        else
                        {
                            //fallback to no deserialization for now
                            return am.GetExtAsset(fileInst, fileId, pathId);
                        }
                    }
                }
                else
                {
                    return am.GetExtAsset(fileInst, fileId, pathId);
                }
            }
            else
            {
                return infoExt;
            }
        }
    }
}
