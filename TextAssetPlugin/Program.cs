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
    public class ImportTextAssetOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Import .txt";

            if (action != UABEAPluginAction.Import)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "TextAsset").classId;

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
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select import directory";

            string dir = await ofd.ShowAsync(win);

            if (dir != null && dir != string.Empty)
            {
                List<string> extensions = new List<string>() { "txt" };
                ImportBatch dialog = new ImportBatch(workspace, selection, dir, extensions);
                List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(win);
                foreach (ImportBatchInfo batchInfo in batchInfos)
                {
                    AssetContainer cont = batchInfo.cont;

                    AssetTypeValueField baseField = workspace.GetBaseField(cont);

                    string file = batchInfo.importFile;

                    byte[] byteData = File.ReadAllBytes(file);
                    baseField.Get("m_Script").GetValue().Set(byteData);

                    byte[] savedAsset = baseField.WriteToByteArray();

                    var replacer = new AssetsReplacerFromMemory(
                        0, cont.PathId, (int)cont.ClassId, cont.MonoId, savedAsset);

                    workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                }
                return true;
            }
            return false;
        }
        public async Task<bool> SingleImport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            OpenFileDialog ofd = new OpenFileDialog();

            AssetTypeValueField baseField = workspace.GetBaseField(cont);

            ofd.Title = "Open text file";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "TXT file", Extensions = new List<string>() { "txt" } }
            };

            string[] fileList = await ofd.ShowAsync(win);
            if (fileList.Length == 0)
                return false;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                byte[] byteData = File.ReadAllBytes(file);
                baseField.Get("m_Script").GetValue().Set(byteData);

                byte[] savedAsset = baseField.WriteToByteArray();

                var replacer = new AssetsReplacerFromMemory(
                    0, cont.PathId, (int)cont.ClassId, cont.MonoId, savedAsset);

                workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
            }
            return false;
        }
    }
    
    public class ExportTextAssetOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Export .txt";

            if (action != UABEAPluginAction.Export)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "TextAsset").classId;

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
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(win);

            if (dir != null && dir != string.Empty)
            {
                foreach (AssetContainer cont in selection)
                {
                    AssetTypeValueField baseField = workspace.GetBaseField(cont);

                    string name = baseField.Get("m_Name").GetValue().AsString();
                    byte[] byteData = baseField.Get("m_Script").GetValue().AsStringBytes();

                    name = Extensions.ReplaceInvalidPathChars(name);
                    string file = Path.Combine(dir, $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.txt");

                    File.WriteAllBytes(file, byteData);
                }
                return true;
            }
            return false;
        }
        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            SaveFileDialog sfd = new SaveFileDialog();

            AssetTypeValueField baseField = workspace.GetBaseField(cont);
            string name = baseField.Get("m_Name").GetValue().AsString();
            name = Extensions.ReplaceInvalidPathChars(name);

            sfd.Title = "Save text file";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "TXT file", Extensions = new List<string>() { "txt" } }
            };
            sfd.InitialFileName = $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.txt";

            string file = await sfd.ShowAsync(win);

            if (file != null && file != string.Empty)
            {
                byte[] byteData = baseField.Get("m_Script").GetValue().AsStringBytes();
                File.WriteAllBytes(file, byteData);

                return true;
            }
            return false;
        }
    }

    public class TextAssetPlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo();
            info.name = "TextAsset Import/Export";

            info.options = new List<UABEAPluginOption>();
            info.options.Add(new ImportTextAssetOption());
            info.options.Add(new ExportTextAssetOption());
            return info;
        }
    }
}
