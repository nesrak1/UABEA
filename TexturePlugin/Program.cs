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
    public class ImportTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetExternal> selection, out string name)
        {
            name = "Import .png";

            if (action != UABEAPluginAction.Import)
                return false;

            //temporary
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
            await MessageBoxUtil.ShowDialog(win, "Not Implemented", "Import png option not supported,\nbut you can still import an image\nwith Edit. Use that instead.");
            return false;
        }
    }

    public class ExportTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetExternal> selection, out string name)
        {
            name = "Export .png";

            if (action != UABEAPluginAction.Export)
                return false;

            //temporary
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

        public AssetTypeValueField GetByteArrayTexture(AssetWorkspace workspace, AssetExternal tex)
        {
            ClassDatabaseType textureType = AssetHelper.FindAssetClassByID(workspace.am.classFile, tex.info.curFileType);
            AssetTypeTemplateField textureTemp = new AssetTypeTemplateField();
            textureTemp.FromClassDatabase(workspace.am.classFile, textureType, 0);
            AssetTypeTemplateField image_data = textureTemp.children.FirstOrDefault(f => f.name == "image data");
            if (image_data == null)
                return null;
            image_data.valueType = EnumValueTypes.ByteArray;
            AssetTypeInstance textureTypeInstance = new AssetTypeInstance(new[] { textureTemp }, tex.file.file.reader, tex.info.absoluteFilePos);
            AssetTypeValueField textureBase = textureTypeInstance.GetBaseField();
            return textureBase;
        }

        public byte[] GetRawTextureBytes(TextureFile texFile, AssetsFileInstance inst)
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

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetExternal> selection)
        {
            AssetExternal tex = selection[0];

            AssetTypeValueField texBaseField = GetByteArrayTexture(workspace, tex);
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
            SaveFileDialog sfd = new SaveFileDialog();

            sfd.Title = "Save texture";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "PNG file", Extensions = new List<string>() { "png" } }
            };

            string file = await sfd.ShowAsync(win);

            if (file != null && file != string.Empty)
            {
                //bundle resS
                TextureFile.StreamingInfo streamInfo = texFile.m_StreamData;
                if (streamInfo.path != null && streamInfo.path != "" && tex.file.parentBundle != null)
                {
                    //some versions apparently don't use archive:/
                    string searchPath = streamInfo.path;
                    if (searchPath.StartsWith("archive:/"))
                        searchPath = searchPath.Substring(9);

                    searchPath = Path.GetFileName(searchPath);

                    AssetBundleFile bundle = tex.file.parentBundle.file;

                    AssetsFileReader reader = bundle.reader;
                    AssetBundleDirectoryInfo06[] dirInf = bundle.bundleInf6.dirInf;
                    bool foundFile = false;
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
                            foundFile = true;
                            break;
                        }
                    }
                    if (!foundFile)
                    {
                        await MessageBoxUtil.ShowDialog(win, "Error", "resS was detected but no file was found in bundle");
                        return false;
                    }
                }

                byte[] data = GetRawTextureBytes(texFile, tex.file);

                bool success = await TextureImportExport.ExportPng(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat);
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

        public AssetTypeValueField GetByteArrayTexture(AssetWorkspace workspace, AssetExternal tex)
        {
            ClassDatabaseType textureType = AssetHelper.FindAssetClassByID(workspace.am.classFile, tex.info.curFileType);
            AssetTypeTemplateField textureTemp = new AssetTypeTemplateField();
            textureTemp.FromClassDatabase(workspace.am.classFile, textureType, 0);
            AssetTypeTemplateField image_data = textureTemp.children.FirstOrDefault(f => f.name == "image data");
            if (image_data == null)
                return null;
            image_data.valueType = EnumValueTypes.ByteArray;
            AssetTypeInstance textureTypeInstance = new AssetTypeInstance(new[] { textureTemp }, tex.file.file.reader, tex.info.absoluteFilePos);
            AssetTypeValueField textureBase = textureTypeInstance.GetBaseField();
            return textureBase;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetExternal> selection)
        {
            AssetExternal tex = selection[0];

            AssetTypeValueField texBaseField = GetByteArrayTexture(workspace, tex);
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
