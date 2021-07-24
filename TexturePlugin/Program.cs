using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using MessageBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace TexturePlugin
{
    public static class TextureHelper
    {
        public static AssetTypeInstance GetByteArrayTexture(AssetWorkspace workspace, AssetContainer tex)
        {
            AssetTypeTemplateField textureTemp = workspace.GetTemplateField(tex.FileInstance.file, tex.ClassId, tex.MonoId);
            AssetTypeTemplateField image_data = textureTemp.children.FirstOrDefault(f => f.name == "image data");
            if (image_data == null)
                return null;
            image_data.valueType = EnumValueTypes.ByteArray;
            AssetTypeInstance textureTypeInstance = new AssetTypeInstance(new[] { textureTemp }, tex.FileReader, tex.FilePosition);
            return textureTypeInstance;
        }

        public static byte[] GetRawTextureBytes(TextureFile texFile, AssetsFileInstance inst)
        {
            string rootPath = Path.GetDirectoryName(inst.path);
            if (texFile.m_StreamData.size != 0 && texFile.m_StreamData.path != string.Empty)
            {
                string fixedStreamPath = texFile.m_StreamData.path;
                if (!Path.IsPathRooted(fixedStreamPath) && rootPath != null)
                {
                    fixedStreamPath = Path.Combine(rootPath, fixedStreamPath);
                }
                if (File.Exists(fixedStreamPath))
                {
                    Stream stream = File.OpenRead(fixedStreamPath);
                    stream.Position = texFile.m_StreamData.offset;
                    texFile.pictureData = new byte[texFile.m_StreamData.size];
                    stream.Read(texFile.pictureData, 0, (int)texFile.m_StreamData.size);
                }
                else
                {
                    return null;
                }
            }
            return texFile.pictureData;
        }
    }

    public class ImportTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Batch import .png";

            if (action != UABEAPluginAction.Import)
                return false;

            if (selection.Count <= 1)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "Texture2D").classId;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != classId)
                    return false;
            }
            return true;
        }

        private async Task<bool> ImportTextures(Window win, List<ImportBatchInfo> batchInfos)
        {
            foreach (ImportBatchInfo batchInfo in batchInfos)
            {
                string selectedFilePath = batchInfo.importFile;

                if (!batchInfo.cont.HasInstance)
                    continue;

                AssetTypeValueField baseField = batchInfo.cont.TypeInstance.GetBaseField();
                TextureFormat fmt = (TextureFormat)baseField.Get("m_TextureFormat").GetValue().AsInt();

                byte[] encImageBytes = TextureImportExport.ImportPng(selectedFilePath, fmt, out int width, out int height);

                if (encImageBytes == null)
                {
                    await MessageBoxUtil.ShowDialog(win, "Error", $"Failed to encode texture format {fmt}");
                    return false;
                }

                AssetTypeValueField m_StreamData = baseField.Get("m_StreamData");
                m_StreamData.Get("offset").GetValue().Set(0);
                m_StreamData.Get("size").GetValue().Set(0);
                m_StreamData.Get("path").GetValue().Set("");

                baseField.Get("m_TextureFormat").GetValue().Set((int)fmt);

                baseField.Get("m_Width").GetValue().Set(width);
                baseField.Get("m_Height").GetValue().Set(height);

                AssetTypeValueField image_data = baseField.Get("image data");
                image_data.GetValue().type = EnumValueTypes.ByteArray;
                image_data.templateField.valueType = EnumValueTypes.ByteArray;
                AssetTypeByteArray byteArray = new AssetTypeByteArray()
                {
                    size = (uint)encImageBytes.Length,
                    data = encImageBytes
                };
                image_data.GetValue().Set(byteArray);
            }

            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            for (int i = 0; i < selection.Count; i++)
            {
                selection[i] = new AssetContainer(selection[i], TextureHelper.GetByteArrayTexture(workspace, selection[i]));
            }

            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select import directory";

            string dir = await ofd.ShowAsync(win);

            if (dir != null && dir != string.Empty)
            {
                ImportBatch dialog = new ImportBatch(workspace, selection, dir, ".png");
                List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(win);
                bool success = await ImportTextures(win, batchInfos);
                if (success)
                {
                    //some of the assets may not get modified, but
                    //uabe still makes replacers for those anyway
                    foreach (AssetContainer cont in selection)
                    {
                        byte[] savedAsset = cont.TypeInstance.WriteToByteArray();

                        var replacer = new AssetsReplacerFromMemory(
                            0, cont.PathId, (int)cont.ClassId, cont.MonoId, savedAsset);

                        workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
    }

    public class ExportTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            if (selection.Count > 1)
                name = "Batch export .png";
            else
                name = "Export .png";

            if (action != UABEAPluginAction.Export)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "Texture2D").classId;

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

                AssetsFileReader reader = bundle.reader;
                AssetBundleDirectoryInfo06[] dirInf = bundle.bundleInf6.dirInf;
                for (int i = 0; i < dirInf.Length; i++)
                {
                    AssetBundleDirectoryInfo06 info = dirInf[i];
                    if (info.name == searchPath)
                    {
                        reader.Position = bundle.bundleHeader6.GetFileDataOffset() + info.offset + streamInfo.offset;
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

            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(win);

            if (dir != null && dir != string.Empty)
            {
                StringBuilder errorBuilder = new StringBuilder();

                foreach (AssetContainer cont in selection)
                {
                    string errorAssetName = $"{Path.GetFileName(cont.FileInstance.path)}/{cont.PathId}";

                    AssetTypeValueField texBaseField = cont.TypeInstance.GetBaseField();
                    TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

                    //0x0 texture, usually called like Font Texture or smth
                    if (texFile.m_Width == 0 && texFile.m_Height == 0)
                        continue;

                    string file = Path.Combine(dir, $"{texFile.m_Name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.png");

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

                    bool success = await TextureImportExport.ExportPng(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat);
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
        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, cont).GetBaseField();
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
            SaveFileDialog sfd = new SaveFileDialog();

            sfd.Title = "Save texture";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "PNG file", Extensions = new List<string>() { "png" } }
            };
            sfd.InitialFileName = $"{texFile.m_Name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.png";

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

                bool success = await TextureImportExport.ExportPng(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat);
                if (!success)
                {
                    string texFormat = ((TextureFormat)texFile.m_TextureFormat).ToString();
                    await MessageBoxUtil.ShowDialog(win, "Error", $"Failed to decode texture format {texFormat}");
                }
                return success;
            }
            return false;
        }
    }

    public class EditTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Edit texture";

            if (action != UABEAPluginAction.Import)
                return false;

            if (selection.Count != 1)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "Texture2D").classId;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != classId)
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, cont).GetBaseField();
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
            EditDialog dialog = new EditDialog(texFile.m_Name, texFile, texBaseField);
            bool saved = await dialog.ShowDialog<bool>(win);
            if (saved)
            {
                byte[] savedAsset = texBaseField.WriteToByteArray();

                var replacer = new AssetsReplacerFromMemory(
                    0, cont.PathId, (int)cont.ClassId, cont.MonoId, savedAsset);

                workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                return true;
            }
            return false;
        }
    }

    public class TexturePlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo();
            info.name = "Texture Import/Export";

            info.options = new List<UABEAPluginOption>();
            info.options.Add(new ImportTextureOption());
            info.options.Add(new ExportTextureOption());
            info.options.Add(new EditTextureOption());
            return info;
        }
    }
}
