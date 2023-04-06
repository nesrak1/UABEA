using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using AssetsTools.NET;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEAvalonia.Plugins;
using UABEAvalonia;
using System.IO;

namespace TexturePlugin
{
    public class ExportTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            if (selection.Count > 1)
                name = "Batch export textures";
            else
                name = "Export texture";

            if (action != UABEAPluginAction.Export)
                return false;

            int classId = am.ClassDatabase.FindAssetClassByName("Texture2D").ClassId;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != classId)
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            if (selection.Count > 1)
                return await BatchExport(win, workspace, selection);
            else
                return await SingleExport(win, workspace, selection);
        }

        private bool GetResSTexture(TextureFile texFile, AssetContainer cont)
        {
            TextureFile.StreamingInfo streamInfo = texFile.m_StreamData;
            if (streamInfo.path != null && streamInfo.path != "" && cont.FileInstance.parentBundle != null)
            {
                //some versions apparently don't use archive:/
                string searchPath = streamInfo.path;
                if (searchPath.StartsWith("archive:/"))
                    searchPath = searchPath.Substring(9);

                searchPath = Path.GetFileName(searchPath);

                AssetBundleFile bundle = cont.FileInstance.parentBundle.file;

                AssetsFileReader reader = bundle.DataReader;
                AssetBundleDirectoryInfo[] dirInf = bundle.BlockAndDirInfo.DirectoryInfos;
                for (int i = 0; i < dirInf.Length; i++)
                {
                    AssetBundleDirectoryInfo info = dirInf[i];
                    if (info.Name == searchPath)
                    {
                        reader.Position = info.Offset + (long)streamInfo.offset;
                        texFile.pictureData = reader.ReadBytes((int)streamInfo.size);
                        texFile.m_StreamData.offset = 0;
                        texFile.m_StreamData.size = 0;
                        texFile.m_StreamData.path = "";
                        return true;
                    }
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        public async Task<bool> BatchExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            for (int i = 0; i < selection.Count; i++)
            {
                selection[i] = new AssetContainer(selection[i], TextureHelper.GetByteArrayTexture(workspace, selection[i]));
            }

            ExportBatchChooseType dialog = new ExportBatchChooseType();
            string fileType = await dialog.ShowDialog<string>(win);

            if (fileType != null && fileType != string.Empty)
            {
                OpenFolderDialog ofd = new OpenFolderDialog();
                ofd.Title = "Select export directory";

                string dir = await ofd.ShowAsync(win);

                if (dir != null && dir != string.Empty)
                {
                    StringBuilder errorBuilder = new StringBuilder();

                    foreach (AssetContainer cont in selection)
                    {
                        string errorAssetName = $"{Path.GetFileName(cont.FileInstance.path)}/{cont.PathId}";

                        AssetTypeValueField texBaseField = cont.BaseValueField;
                        TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

                        //0x0 texture, usually called like Font Texture or smth
                        if (texFile.m_Width == 0 && texFile.m_Height == 0)
                            continue;

                        string assetName = Extensions.ReplaceInvalidPathChars(texFile.m_Name);
                        string file = Path.Combine(dir, $"{assetName}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.{fileType.ToLower()}");

                        //bundle resS
                        if (!GetResSTexture(texFile, cont))
                        {
                            string resSName = Path.GetFileName(texFile.m_StreamData.path);
                            errorBuilder.AppendLine($"[{errorAssetName}]: resS was detected but {resSName} was not found in bundle");
                            continue;
                        }

                        byte[] data = TextureHelper.GetRawTextureBytes(texFile, cont.FileInstance);

                        if (data == null)
                        {
                            string resSName = Path.GetFileName(texFile.m_StreamData.path);
                            errorBuilder.AppendLine($"[{errorAssetName}]: resS was detected but {resSName} was not found on disk");
                            continue;
                        }

                        byte[] platformBlob = TextureHelper.GetPlatformBlob(texBaseField);
                        uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

                        bool success = TextureImportExport.Export(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat, platform, platformBlob);
                        if (!success)
                        {
                            string texFormat = ((TextureFormat)texFile.m_TextureFormat).ToString();
                            errorBuilder.AppendLine($"[{errorAssetName}]: Failed to decode texture format {texFormat}");
                            continue;
                        }
                    }

                    if (errorBuilder.Length > 0)
                    {
                        string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
                        string firstLinesStr = string.Join('\n', firstLines);
                        await MessageBoxUtil.ShowDialog(win, "Some errors occurred while exporting", firstLinesStr);
                    }

                    return true;
                }
                return false;
            }
            return false;
        }

        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, cont);
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

            // 0x0 texture, usually called like Font Texture or smth
            if (texFile.m_Width == 0 && texFile.m_Height == 0)
            {
                await MessageBoxUtil.ShowDialog(win, "Error", $"Texture size is 0x0. Texture cannot be exported.");
                return false;
            }

            string assetName = Extensions.ReplaceInvalidPathChars(texFile.m_Name);

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save texture";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "PNG file", Extensions = new List<string>() { "png" } },
                new FileDialogFilter() { Name = "TGA file", Extensions = new List<string>() { "tga" } }
            };
            sfd.InitialFileName = $"{assetName}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.png";

            string file = await sfd.ShowAsync(win);

            if (file != null && file != string.Empty)
            {
                string errorAssetName = $"{Path.GetFileName(cont.FileInstance.path)}/{cont.PathId}";

                //bundle resS
                if (!GetResSTexture(texFile, cont))
                {
                    string resSName = Path.GetFileName(texFile.m_StreamData.path);
                    await MessageBoxUtil.ShowDialog(win, "Error", $"[{errorAssetName}]: resS was detected but {resSName} was not found in bundle");
                    return false;
                }

                byte[] data = TextureHelper.GetRawTextureBytes(texFile, cont.FileInstance);

                if (data == null)
                {
                    string resSName = Path.GetFileName(texFile.m_StreamData.path);
                    await MessageBoxUtil.ShowDialog(win, "Error", $"[{errorAssetName}]: resS was detected but {resSName} was not found on disk");
                    return false;
                }

                byte[] platformBlob = TextureHelper.GetPlatformBlob(texBaseField);
                uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

                bool success = TextureImportExport.Export(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat, platform, platformBlob);
                if (!success)
                {
                    string texFormat = ((TextureFormat)texFile.m_TextureFormat).ToString();
                    await MessageBoxUtil.ShowDialog(win, "Error", $"[{errorAssetName}]: Failed to decode texture format {texFormat}");
                }
                return success;
            }
            return false;
        }
    }
}
