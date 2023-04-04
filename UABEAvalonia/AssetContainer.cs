using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.IO;

namespace UABEAvalonia
{
    // improvement over AssetExternal to handle dynamic changes
    // not to be confused with unity containers which are just
    // unity's way of showing what export an asset is connected to
    public class AssetContainer
    {
        public long PathId { get; }
        public int ClassId { get; }
        public ushort MonoId { get; }
        public uint Size { get; }
        public string Container { get; set; } // should be a list later
        public AssetsFileInstance FileInstance { get; }
        public AssetTypeValueField? BaseValueField { get; }

        public long FilePosition { get; }
        public AssetsFileReader FileReader { get; }
        // deprecated
        public AssetID AssetId
        {
            get => new AssetID(FileInstance.path, PathId);
        }
        public AssetPPtr AssetPPtr
        {
            get => new AssetPPtr(FileInstance.path, 0, PathId);
        }
        public bool HasValueField
        {
            get => BaseValueField != null;
        }

        // existing assets
        public AssetContainer(AssetFileInfo info, AssetsFileInstance fileInst, AssetTypeValueField? baseField = null)
        {
            FilePosition = info.AbsoluteByteStart;
            FileReader = fileInst.file.Reader;

            PathId = info.PathId;
            ClassId = info.TypeId;
            MonoId = fileInst.file.GetScriptIndex(info);
            Size = info.ByteSize;
            Container = string.Empty;
            FileInstance = fileInst;
            BaseValueField = baseField;
        }

        // newly created assets
        public AssetContainer(AssetsFileReader fileReader, long assetPosition, long pathId, int classId, ushort monoId, uint size,
                              AssetsFileInstance fileInst, AssetTypeValueField? baseField = null)
        {
            FilePosition = assetPosition;
            FileReader = fileReader;

            PathId = pathId;
            ClassId = classId;
            MonoId = monoId;
            Size = size;
            Container = string.Empty;
            FileInstance = fileInst;
            BaseValueField = baseField;
        }

        // modified assets
        public AssetContainer(AssetContainer container, AssetsFileReader fileReader, long assetPosition, uint size)
        {
            FilePosition = assetPosition;
            FileReader = fileReader;

            PathId = container.PathId;
            ClassId = container.ClassId;
            MonoId = container.MonoId;
            Size = size;
            Container = string.Empty;
            FileInstance = container.FileInstance;
            BaseValueField = container.BaseValueField;
        }

        public AssetContainer(AssetContainer container, AssetTypeValueField baseField)
        {
            FilePosition = container.FilePosition;
            FileReader = container.FileReader;

            PathId = container.PathId;
            ClassId = container.ClassId;
            MonoId = container.MonoId;
            Size = container.Size;
            Container = string.Empty;
            FileInstance = container.FileInstance;
            BaseValueField = baseField;
        }
    }
}
