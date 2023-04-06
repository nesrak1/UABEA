using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class UnityContainer
    {
        public List<AssetPPtr> PreloadTable { get; } = new();
        // normally this map is string -> AssetInfo, but we only do path id -> string lookups so this isn't useful
        public Dictionary<UnityContainerAssetInfo, string> AssetMap { get; } = new();

        public void FromAssetBundle(AssetsManager am, AssetsFileInstance fromFile, AssetTypeValueField assetBundleBf)
        {
            AssetTypeValueField m_PreloadTable = assetBundleBf["m_PreloadTable.Array"];
            foreach (AssetTypeValueField ptr in m_PreloadTable)
            {
                AssetPPtr assetPPtr = AssetPPtr.FromField(ptr);
                assetPPtr.SetFilePathFromFile(am, fromFile);
                PreloadTable.Add(assetPPtr);
            }

            AssetTypeValueField m_Container = assetBundleBf["m_Container.Array"];
            foreach (AssetTypeValueField container in m_Container)
            {
                string key = container["first"].AsString;
                AssetTypeValueField value = container["second"];

                UnityContainerAssetInfo assetInfo = UnityContainerAssetInfo.FromField(value);
                assetInfo.asset.SetFilePathFromFile(am, fromFile);
                if (assetInfo.asset.PathId != 0)
                {
                    AssetMap[assetInfo] = key;
                }
            }
        }

        public void FromResourceManager(AssetsManager am, AssetsFileInstance fromFile, AssetTypeValueField rsrcManBf)
        {
            AssetTypeValueField m_Container = rsrcManBf["m_Container.Array"];
            foreach (AssetTypeValueField container in m_Container)
            {
                string key = container["first"].AsString;
                AssetTypeValueField value = container["second"];

                AssetPPtr assetPPtr = AssetPPtr.FromField(value);
                assetPPtr.SetFilePathFromFile(am, fromFile);

                UnityContainerAssetInfo assetInfo = new UnityContainerAssetInfo(assetPPtr);
                if (assetPPtr.PathId != 0)
                {
                    AssetMap[assetInfo] = key;
                }
            }
        }

        public string? GetContainerPath(AssetsFileInstance fileInst, long pathId)
        {
            return GetContainerPath(new AssetPPtr(fileInst.path, 0, pathId));
        }

        public string? GetContainerPath(AssetPPtr assetPPtr)
        {
            UnityContainerAssetInfo search = new UnityContainerAssetInfo(assetPPtr);
            if (AssetMap.TryGetValue(search, out string? path))
            {
                return path;
            }

            return null;
        }

        public UnityContainerAssetInfo GetContainerInfo(string path)
        {
            return AssetMap.FirstOrDefault(i => i.Value == path.ToLower()).Key;
        }

        // if an assets file, file can be any opened file. if a bundle file, it should be _that_ bundle file.
        public static bool TryGetBundleContainerBaseField(
            AssetWorkspace workspace, AssetsFileInstance file,
            [MaybeNullWhen(false)] out AssetsFileInstance actualFile,
            [MaybeNullWhen(false)] out AssetTypeValueField baseField
        )
        {
            actualFile = null;
            baseField = null;

            List<AssetFileInfo> assetBundleInfos = file.file.GetAssetsOfType(AssetClassID.AssetBundle);
            if (assetBundleInfos.Count == 0)
            {
                return false;
            }

            AssetContainer? bundleCont = workspace.GetAssetContainer(file, 0, assetBundleInfos[0].PathId, false);
            if (bundleCont == null || bundleCont.BaseValueField == null)
            {
                return false;
            }

            actualFile = file;
            baseField = bundleCont.BaseValueField;
            return true;
        }

        public static bool TryGetRsrcManContainerBaseField(
            AssetWorkspace workspace, AssetsFileInstance file,
            [MaybeNullWhen(false)] out AssetsFileInstance actualFile,
            [MaybeNullWhen(false)] out AssetTypeValueField baseField
        )
        {
            actualFile = null;
            baseField = null;

            string gameDir = Extensions.GetAssetsFileDirectory(file);
            if (gameDir == null)
            {
                return false;
            }

            // todo: what about mainData?
            string ggmPath = Path.Combine(gameDir, "globalgamemanagers");
            if (!File.Exists(ggmPath))
            {
                return false;
            }

            AssetsFileInstance ggmInst;
            int ggmIndex = workspace.LoadedFiles.FindIndex(f => f.path == ggmPath);
            if (ggmIndex != -1)
            {
                ggmInst = workspace.LoadedFiles[ggmIndex];
            }
            else
            {
                ggmInst = workspace.am.LoadAssetsFile(ggmPath, true);
            }

            List<AssetFileInfo> resourceManagerInfos = ggmInst.file.GetAssetsOfType(AssetClassID.ResourceManager);
            if (resourceManagerInfos.Count == 0)
            {
                return false;
            }
                
            AssetContainer? rsrcManCont = workspace.GetAssetContainer(ggmInst, 0, resourceManagerInfos[0].PathId, false);
            if (rsrcManCont != null && rsrcManCont.BaseValueField != null)
            {
                actualFile = rsrcManCont.FileInstance;
                baseField = rsrcManCont.BaseValueField;
                return true;
            }
            else
            {
                // if we haven't loaded ggm into LoadedFiles yet, load it manually through AssetsManager
                // we don't load it into LoadedFiles now to prevent clutter
                actualFile = ggmInst;
                baseField = workspace.am.GetBaseField(ggmInst, resourceManagerInfos[0]);
                return true;
            }
        }
    }

    public class UnityContainerAssetInfo
    {
        public int preloadIndex;
        public int preloadSize;
        public AssetPPtr asset;

        public UnityContainerAssetInfo(AssetPPtr asset)
        {
            preloadIndex = -1;
            preloadSize = -1;
            this.asset = asset;
        }

        public UnityContainerAssetInfo(int preloadIndex, int preloadSize, AssetPPtr asset)
        {
            this.preloadIndex = preloadIndex;
            this.preloadSize = preloadSize;
            this.asset = asset;
        }

        public static UnityContainerAssetInfo FromField(AssetTypeValueField field)
        {
            int preloadIndex = field["preloadIndex"].AsInt;
            int preloadSize = field["preloadSize"].AsInt;
            AssetPPtr asset = AssetPPtr.FromField(field["asset"]);
            return new UnityContainerAssetInfo(preloadIndex, preloadSize, asset);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not UnityContainerAssetInfo)
            {
                return false;
            }

            UnityContainerAssetInfo assetInfo = (UnityContainerAssetInfo)obj;
            return assetInfo.asset.Equals(asset);
        }

        public override int GetHashCode()
        {
            return asset.GetHashCode();
        }
    }
}
