using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.IO;

namespace UABEAvalonia
{
    //improvement over AssetExternal to handle dynamic changes
    //not to be confused with containers which are just
    //uabe's way of showing what export an asset is connected to
    public class AssetContainer
    {
        public long PathId { get; }
        public uint ClassId { get; }
        public ushort MonoId { get; }
        public uint Size { get; }
        public AssetsFileInstance FileInstance { get; }
        public AssetTypeInstance? TypeInstance { get; }

        public long FilePosition { get; }
        public AssetsFileReader FileReader { get; }
        public AssetID AssetId
        {
            get => new AssetID(FileInstance.path, PathId);
        }
        public bool HasInstance
        {
            get => TypeInstance != null;
        }

        //existing assets
        public AssetContainer(AssetFileInfoEx info, AssetsFileInstance fileInst, AssetTypeInstance? typeInst = null)
        {
            FilePosition = info.absoluteFilePos;
            FileReader = fileInst.file.reader;

            PathId = info.index;
            ClassId = info.curFileType;
            MonoId = AssetHelper.GetScriptIndex(fileInst.file, info);
            Size = info.curFileSize;
            FileInstance = fileInst;
            TypeInstance = typeInst;
        }

        //newly created assets
        public AssetContainer(AssetsFileReader fileReader, long assetPosition, long pathId, uint classId, ushort monoId, uint size,
                              AssetsFileInstance fileInst, AssetTypeInstance? typeInst = null)
        {
            FilePosition = assetPosition;
            FileReader = fileReader;

            PathId = pathId;
            ClassId = classId;
            MonoId = monoId;
            Size = size;
            FileInstance = fileInst;
            TypeInstance = typeInst;
        }

        //modified assets
        public AssetContainer(AssetContainer container, AssetsFileReader fileReader, long assetPosition, uint size)
        {
            FilePosition = assetPosition;
            FileReader = fileReader;

            PathId = container.PathId;
            ClassId = container.ClassId;
            MonoId = container.MonoId;
            Size = size;
            FileInstance = container.FileInstance;
            TypeInstance = container.TypeInstance;
        }

        public AssetContainer(AssetContainer container, AssetTypeInstance typeInst)
        {
            FilePosition = container.FilePosition;
            FileReader = container.FileReader;

            PathId = container.PathId;
            ClassId = container.ClassId;
            MonoId = container.MonoId;
            Size = container.Size;
            FileInstance = container.FileInstance;
            TypeInstance = typeInst;
        }
    }
}
