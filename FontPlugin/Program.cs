using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace FontPlugin
{
    public static class FontHelper
    {        
        public static AssetTypeValueField GetByteArrayFont(AssetWorkspace workspace, AssetContainer font)
        {
            AssetTypeTemplateField fontTemp = workspace.GetTemplateField(font);
            AssetTypeTemplateField fontData = fontTemp.Children.FirstOrDefault(f => f.Name == "m_FontData");
            if (fontData == null)
                return null;
            
            // m_FontData.Array
            fontData.Children[0].ValueType = AssetValueType.ByteArray;

            AssetTypeValueField baseField = fontTemp.MakeValue(font.FileReader, font.FilePosition);
            return baseField;
        }

        public static bool IsDataOtf(byte[] byteData)
        {
            return byteData[0] == 0x4f &&
                byteData[1] == 0x54 &&
                byteData[2] == 0x54 &&
                byteData[3] == 0x4f;
        }
    }

    public class ImportFontOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Import .ttf/.otf";

            if (action != UABEAPluginAction.Import)
                return false;

            int classId = am.ClassDatabase.FindAssetClassByName("Font").ClassId;

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
                return await BatchImport(win, workspace, selection);
            else
                return await SingleImport(win, workspace, selection);
        }

        public async Task<bool> BatchImport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            var selectedFolders = await win.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Select import directory"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return false;

            string dir = selectedFolderPaths[0];

            List<string> extensions = new List<string>() { "otf", "ttf" };
            ImportBatch dialog = new ImportBatch(workspace, selection, dir, extensions);
            List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(win);
            foreach (ImportBatchInfo batchInfo in batchInfos)
            {
                AssetContainer cont = batchInfo.cont;

                AssetTypeValueField baseField = FontHelper.GetByteArrayFont(workspace, cont);

                string file = batchInfo.importFile;

                byte[] byteData = File.ReadAllBytes(file);
                baseField["m_FontData.Array"].AsByteArray = byteData;

                byte[] savedAsset = baseField.WriteToByteArray();

                var replacer = new AssetsReplacerFromMemory(
                    cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

                workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
            }
            return true;
        }

        public async Task<bool> SingleImport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField baseField = FontHelper.GetByteArrayFont(workspace, cont);

            var selectedFiles = await win.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open font file",
                FileTypeFilter = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("Font files (*.ttf;*.otf)") { Patterns = new List<string>() { "*.ttf", "*.otf" } },
                    new FilePickerFileType("All types (*.*)") { Patterns = new List<string>() { "*.*" } }
                }
            });

            string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
            if (selectedFilePaths.Length == 0)
                return false;

            string file = selectedFilePaths[0];

            byte[] byteData = File.ReadAllBytes(file);
            baseField["m_FontData.Array"].AsByteArray = byteData;

            byte[] savedAsset = baseField.WriteToByteArray();

            var replacer = new AssetsReplacerFromMemory(
                cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

            workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
            return true;
        }
    }
    
    public class ExportFontOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Export .ttf/.otf";

            if (action != UABEAPluginAction.Export)
                return false;

            int classId = am.ClassDatabase.FindAssetClassByName("Font").ClassId;

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

        public async Task<bool> BatchExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            var selectedFolders = await win.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Select export directory"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return false;

            string dir = selectedFolderPaths[0];

            foreach (AssetContainer cont in selection)
            {
                AssetTypeValueField baseField = FontHelper.GetByteArrayFont(workspace, cont);

                string name = baseField["m_Name"].AsString;
                byte[] byteData = baseField["m_FontData.Array"].AsByteArray;

                if (byteData.Length == 0)
                    continue;

                name = PathUtils.ReplaceInvalidPathChars(name);

                bool isOtf = FontHelper.IsDataOtf(byteData);
                string extension = isOtf ? "otf" : "ttf";

                string file = Path.Combine(dir, $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.{extension}");

                File.WriteAllBytes(file, byteData);
            }
            return true;
        }

        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField baseField = FontHelper.GetByteArrayFont(workspace, cont);
            string name = baseField["m_Name"].AsString;
            name = PathUtils.ReplaceInvalidPathChars(name);

            byte[] byteData = baseField["m_FontData.Array"].AsByteArray;

            if (byteData.Length == 0)
            {
                await MessageBoxUtil.ShowDialog(win,
                    "Empty font", "This font does not use m_FontData. It cannot be exported as a ttf or otf.");

                return false;
            }

            bool isOtf = FontHelper.IsDataOtf(byteData);
            string extension = isOtf ? "otf" : "ttf";

            var selectedFile = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save font file",
                FileTypeChoices = new List<FilePickerFileType>()
                {
                    new FilePickerFileType($"Font file (*.{extension})") { Patterns = new List<string>() { "*." + extension } },
                    new FilePickerFileType($"All types (*.*)") { Patterns = new List<string>() { "*.*" } },
                },
                DefaultExtension = extension,
                SuggestedFileName = $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}"
            });

            string selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
            if (selectedFilePath == null)
                return false;

            File.WriteAllBytes(selectedFilePath, byteData);

            return true;
        }
    }

    public class FontPlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo();
            info.name = "Font Import/Export";

            info.options = new List<UABEAPluginOption>();
            info.options.Add(new ImportFontOption());
            info.options.Add(new ExportFontOption());
            return info;
        }
    }
}
