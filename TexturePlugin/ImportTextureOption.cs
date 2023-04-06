using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using AssetsTools.NET;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEAvalonia.Plugins;
using UABEAvalonia;

namespace TexturePlugin
{
    public class ImportTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Batch import textures";

            if (action != UABEAPluginAction.Import)
                return false;

            if (selection.Count <= 1)
                return false;

            int classId = am.ClassDatabase.FindAssetClassByName("Texture2D").ClassId;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != classId)
                    return false;
            }
            return true;
        }

        private async Task<bool> ImportTextures(Window win, List<ImportBatchInfo> batchInfos)
        {
            StringBuilder errorBuilder = new StringBuilder();

            foreach (ImportBatchInfo batchInfo in batchInfos)
            {
                AssetContainer cont = batchInfo.cont;

                string errorAssetName = $"{Path.GetFileName(cont.FileInstance.path)}/{cont.PathId}";
                string selectedFilePath = batchInfo.importFile;

                if (!cont.HasValueField)
                    continue;

                AssetTypeValueField baseField = cont.BaseValueField;
                TextureFormat fmt = (TextureFormat)baseField["m_TextureFormat"].AsInt;

                byte[] platformBlob = TextureHelper.GetPlatformBlob(baseField);
                uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

                int mips = 1;
                if (!baseField["m_MipCount"].IsDummy)
                    mips = baseField["m_MipCount"].AsInt;

                byte[] encImageBytes = TextureImportExport.Import(selectedFilePath, fmt, out int width, out int height, ref mips, platform, platformBlob);

                if (encImageBytes == null)
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: Failed to encode texture format {fmt}");
                    continue;
                }

                AssetTypeValueField m_StreamData = baseField["m_StreamData"];
                m_StreamData["offset"].AsInt = 0;
                m_StreamData["size"].AsInt = 0;
                m_StreamData["path"].AsString = "";

                if (!baseField["m_MipCount"].IsDummy)
                    baseField["m_MipCount"].AsInt = mips;

                baseField["m_TextureFormat"].AsInt = (int)fmt;
                // todo: size for multi image textures
                baseField["m_CompleteImageSize"].AsInt = encImageBytes.Length;

                baseField["m_Width"].AsInt = width;
                baseField["m_Height"].AsInt = height;

                AssetTypeValueField image_data = baseField["image data"];
                image_data.Value.ValueType = AssetValueType.ByteArray;
                image_data.TemplateField.ValueType = AssetValueType.ByteArray;
                image_data.AsByteArray = encImageBytes;
            }

            if (errorBuilder.Length > 0)
            {
                string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
                string firstLinesStr = string.Join('\n', firstLines);
                await MessageBoxUtil.ShowDialog(win, "Some errors occurred while exporting", firstLinesStr);
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
                List<string> extensions = new List<string>() { "png", "tga" };

                ImportBatch dialog = new ImportBatch(workspace, selection, dir, extensions);
                List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(win);
                if (batchInfos == null)
                {
                    return false;
                }

                bool success = await ImportTextures(win, batchInfos);
                if (success)
                {
                    foreach (AssetContainer cont in selection)
                    {
                        if (batchInfos.Where(x => x.pathId == cont.PathId).Count() == 0)
                        {
                            continue;
                        }
                        byte[] savedAsset = cont.BaseValueField.WriteToByteArray();

                        var replacer = new AssetsReplacerFromMemory(
                            cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

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
}
