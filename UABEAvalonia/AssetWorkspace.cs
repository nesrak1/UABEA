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
    }
}
