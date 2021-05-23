using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace TexturePlugin
{
    public static class TextureHelper
    {
        public static AssetTypeInstance GetByteArrayTexture(AssetWorkspace workspace, AssetExternal tex)
        {
            ClassDatabaseType textureType = AssetHelper.FindAssetClassByID(workspace.am.classFile, tex.info.curFileType);
            AssetTypeTemplateField textureTemp = new AssetTypeTemplateField();
            textureTemp.FromClassDatabase(workspace.am.classFile, textureType, 0);
            AssetTypeTemplateField image_data = textureTemp.children.FirstOrDefault(f => f.name == "image data");
            if (image_data == null)
                return null;
            image_data.valueType = EnumValueTypes.ByteArray;
            AssetTypeInstance textureTypeInstance = new AssetTypeInstance(new[] { textureTemp }, tex.file.file.reader, tex.info.absoluteFilePos);
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
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetExternal> selection, out string name)
        {
            name = "Batch import .png";

            if (action != UABEAPluginAction.Import)
                return false;

            if (selection.Count <= 1)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "Texture2D").classId;

            foreach (AssetExternal ext in selection)
            {
                if (ext.info.curFileType != classId)
                    return false;
            }
            return true;
        }

        private async Task<bool> ImportTextures(Window win, List<ImportBatchInfo> batchInfos)
        {
            foreach (ImportBatchInfo batchInfo in batchInfos)
            {
                string selectedFilePath = batchInfo.importFile;

                AssetTypeValueField baseField = batchInfo.ext.instance.GetBaseField();
                TextureFormat fmt = (TextureFormat)baseField.Get("m_TextureFormat").GetValue().AsInt();

                byte[] encImageBytes = TextureImportExport.ImportPng(selectedFilePath, fmt, out int width, out int height);

                if (encImageBytes == null)
                {
                    await MessageBoxUtil.ShowDialog(win, "Error", $"Failed to decode texture\n({fmt})");
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

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetExternal> selection)
        {
            for (int i = 0; i < selection.Count; i++)
            {
                AssetExternal ext = selection[i];
                AssetExternal newExt = new AssetExternal()
                {
                    file = ext.file,
                    info = ext.info,
                    instance = TextureHelper.GetByteArrayTexture(workspace, ext)
                };
                selection[i] = newExt;
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
                    foreach (AssetExternal ext in selection)
                    {
                        byte[] savedAsset = ext.instance.WriteToByteArray();

                        var replacer = new AssetsReplacerFromMemory(
                            0, ext.info.index, (int)ext.info.curFileType,
                            AssetHelper.GetScriptIndex(ext.file.file, ext.info), savedAsset);

                        workspace.AddReplacer(ext.file, replacer, new MemoryStream(savedAsset));
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
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetExternal> selection, out string name)
        {
            if (selection.Count > 1)
                name = "Batch export .png";
            else
                name = "Export .png";

            if (action != UABEAPluginAction.Export)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "Texture2D").classId;

            foreach (AssetExternal ext in selection)
            {
                if (ext.info.curFileType != classId)
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetExternal> selection)
        {
            if (selection.Count > 1)
                return await BatchExport(win, workspace, selection);
            else
                return await SingleExport(win, workspace, selection);
        }

        private bool GetResSTexture(TextureFile texFile, AssetExternal ext)
        {
            TextureFile.StreamingInfo streamInfo = texFile.m_StreamData;
            if (streamInfo.path != null && streamInfo.path != "" && ext.file.parentBundle != null)
            {
                //some versions apparently don't use archive:/
                string searchPath = streamInfo.path;
                if (searchPath.StartsWith("archive:/"))
                    searchPath = searchPath.Substring(9);

                searchPath = Path.GetFileName(searchPath);

                AssetBundleFile bundle = ext.file.parentBundle.file;

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

        public async Task<bool> BatchExport(Window win, AssetWorkspace workspace, List<AssetExternal> selection)
        {
            for (int i = 0; i < selection.Count; i++)
            {
                AssetExternal ext = selection[i];
                AssetExternal newExt = new AssetExternal()
                {
                    file = ext.file,
                    info = ext.info,
                    instance = TextureHelper.GetByteArrayTexture(workspace, ext)
                };
                selection[i] = newExt;
            }

            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(win);

            if (dir != null && dir != string.Empty)
            {
                foreach (AssetExternal ext in selection)
                {
                    AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, ext).GetBaseField();
                    TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

                    string file = Path.Combine(dir, $"{texFile.m_Name}-{Path.GetFileName(ext.file.path)}-{ext.info.index}.png");

                    //bundle resS
                    if (!GetResSTexture(texFile, ext))
                    {
                        await MessageBoxUtil.ShowDialog(win, "Error", "resS was detected but no file was found in bundle");
                        return false;
                    }

                    byte[] data = TextureHelper.GetRawTextureBytes(texFile, ext.file);

                    bool success = await TextureImportExport.ExportPng(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat);
                    if (!success)
                    {
                        await MessageBoxUtil.ShowDialog(win, "Error", $"Failed to decode texture\n({(TextureFormat)texFile.m_TextureFormat})");
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetExternal> selection)
        {
            AssetExternal ext = selection[0];

            AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, ext).GetBaseField();
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
            SaveFileDialog sfd = new SaveFileDialog();

            sfd.Title = "Save texture";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "PNG file", Extensions = new List<string>() { "png" } }
            };
            sfd.InitialFileName = $"{texFile.m_Name}-{Path.GetFileName(ext.file.path)}-{ext.info.index}.png";

            string file = await sfd.ShowAsync(win);

            if (file != null && file != string.Empty)
            {
                //bundle resS
                if (!GetResSTexture(texFile, ext))
                {
                    await MessageBoxUtil.ShowDialog(win, "Error", "resS was detected but no file was found in bundle");
                    return false;
                }

                byte[] data = TextureHelper.GetRawTextureBytes(texFile, ext.file);

                bool success = await TextureImportExport.ExportPng(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat);
                if (!success)
                {
                    await MessageBoxUtil.ShowDialog(win, "Error", $"Failed to decode texture\n({(TextureFormat)texFile.m_TextureFormat})");
                }
                return success;
            }
            return false;
        }
    }

    public class EditTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetExternal> selection, out string name)
        {
            name = "Edit texture";

            if (action != UABEAPluginAction.Import)
                return false;

            if (selection.Count != 1)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "Texture2D").classId;

            foreach (AssetExternal ext in selection)
            {
                if (ext.info.curFileType != classId)
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetExternal> selection)
        {
            AssetExternal tex = selection[0];

            AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, tex).GetBaseField();
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
            EditDialog dialog = new EditDialog(texFile.m_Name, texFile, texBaseField);
            bool saved = await dialog.ShowDialog<bool>(win);
            if (saved)
            {
                byte[] savedAsset = texBaseField.WriteToByteArray();

                var replacer = new AssetsReplacerFromMemory(
                    0, tex.info.index, (int)tex.info.curFileType,
                    AssetHelper.GetScriptIndex(tex.file.file, tex.info), savedAsset);

                workspace.AddReplacer(tex.file, replacer, new MemoryStream(savedAsset));
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
