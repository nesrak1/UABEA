using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace MeshPlugin.ResourceClass
{
    public class ResourceReader
    {
        public MemoryStream resourceStream;
        private long offset;
        private long size;
        private BinaryReader reader;

        private string path;
        private long offset2;
        private long size2;
        private AssetsFileInstance AFinst;

        public ResourceReader(MemoryStream resourceStream, long offset, long size)
        {
            this.resourceStream = resourceStream;
            this.offset = offset;
            this.size = size;
        }
        public ResourceReader(string path, long offset, long size, AssetsFileInstance AFinst)
        {
            this.path = path;
            this.offset2 = offset;
            this.size2 = size;
            this.AFinst = AFinst;
        }
        public byte[] GetResourceBytes()
        {
            string searchPath = path;
            if (searchPath.StartsWith("archive:/"))
                searchPath = searchPath.Substring(9);

            searchPath = Path.GetFileName(searchPath);

            AssetBundleFile bundle = AFinst.parentBundle.file;

            AssetsFileReader reader = bundle.Reader;
            AssetBundleDirectoryInfo[] dirInf = bundle.BlockAndDirInfo.DirectoryInfos;
            for (int i = 0; i < dirInf.Length; i++)
            {
                AssetBundleDirectoryInfo info = dirInf[i];
                if (info.Name == searchPath)
                {
                    reader.Position = bundle.Header.GetFileDataOffset() + info.Offset + (long)offset2;
                    byte[] bytes = reader.ReadBytes((int)size2);
                    return bytes;
                }
            }
            return null;
        }
        public byte[] GetResourceBytesFromPath()
        {
            string searchPath = path;
            if (searchPath.StartsWith("archive:/"))
                searchPath = searchPath.Substring(9);

            var completePath = AFinst.path + "/" + searchPath;
            if (File.Exists(completePath))
            {
                byte[] rawBytes = File.ReadAllBytes(completePath);
                return rawBytes;
            }
            else
            {
                ShowResourceError();
            }

            return null;
        }
        public byte[] GetData()
        {
            byte[] buffer = new byte[size];
            resourceStream.Position = offset;
            resourceStream.ReadBuffer(buffer, 0, buffer.Length);
            return buffer;
        }
        public byte[] GetDataFromPath()
        {
            this.resourceStream = new MemoryStream(GetResourceBytesFromPath());

            byte[] buffer = new byte[size];
            resourceStream.Position = offset;
            resourceStream.ReadBuffer(buffer, 0, buffer.Length);
            return buffer;
        }

        public void WriteData(string path)
        {
            var binaryReader = reader;
            binaryReader.BaseStream.Position = offset;
            using (var writer = File.OpenWrite(path))
            {
                binaryReader.BaseStream.CopyTo(writer, (int)size);
            }
        }
        private async void ShowResourceError()
        {
            ResourceLoader loader = new ResourceLoader();
            bool saved = await loader.ShowDialog<bool>(loader);
        }
    }
}
