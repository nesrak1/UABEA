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

namespace TextAssetPlugin
{
    public static class TextAssetHelper
    {
        public static string GetUContainerExtension(AssetContainer item)
        {
            string ucont = item.Container;
            if (Path.GetFileName(ucont) != Path.GetFileNameWithoutExtension(ucont))
            {
                return Path.GetExtension(ucont);
            }

            return string.Empty;
        }
    }

    public class ImportTextAssetOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Import .txt";

            if (action != UABEAPluginAction.Import)
                return false;

            int classId = am.ClassDatabase.FindAssetClassByName("TextAsset").ClassId;

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
                List<string> extensions = new List<string>() { "*" };
                ImportBatch dialog = new ImportBatch(workspace, selection, dir, extensions);
                List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(win);
                foreach (ImportBatchInfo batchInfo in batchInfos)
                {
                    AssetContainer cont = batchInfo.cont;

                    AssetTypeValueField baseField = workspace.GetBaseField(cont);

                    string file = batchInfo.importFile;

                    byte[] byteData = File.ReadAllBytes(file);
                    baseField["m_Script"].AsByteArray = byteData;

                    byte[] savedAsset = baseField.WriteToByteArray();

                    var replacer = new AssetsReplacerFromMemory(
                        cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

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
                new FileDialogFilter() { Name = "Text files (*.txt)", Extensions = new List<string>() { "txt" } },
                new FileDialogFilter() { Name = "All types (*.*)", Extensions = new List<string>() { "*.*" } }
            };

            string ucontExt = TextAssetHelper.GetUContainerExtension(cont);
            if (ucontExt != string.Empty)
            {
                string ucontExtNoDot = ucontExt[1..];
                string displayName = $"{ucontExtNoDot} files (*{ucontExt})";
                List<string> extensions = new List<string>() { ucontExtNoDot };
                ofd.Filters.Insert(0, new FileDialogFilter() { Name = displayName, Extensions = extensions });
            }

            string[] fileList = await ofd.ShowAsync(win);
            if (fileList == null || fileList.Length == 0)
                return false;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                byte[] byteData = File.ReadAllBytes(file);
                baseField["m_Script"].AsByteArray = byteData;

                byte[] savedAsset = baseField.WriteToByteArray();

                var replacer = new AssetsReplacerFromMemory(
                    cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

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

            int classId = am.ClassDatabase.FindAssetClassByName("TextAsset").ClassId;

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

                    string name = baseField["m_Name"].AsString;
                    byte[] byteData = baseField["m_Script"].AsByteArray;

                    name = Extensions.ReplaceInvalidPathChars(name);

                    string extension = ".txt";
                    string ucontExt = TextAssetHelper.GetUContainerExtension(cont);
                    if (ucontExt != string.Empty)
                    {
                        extension = ucontExt;
                    }

                    string file = Path.Combine(dir, $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}{extension}");

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
            string name = baseField["m_Name"].AsString;
            name = Extensions.ReplaceInvalidPathChars(name);

            sfd.Title = "Save text file";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "TXT file", Extensions = new List<string>() { "txt" } },
                new FileDialogFilter() { Name = "All types (*.*)", Extensions = new List<string>() { "*.*" } }
            };

            string defaultExtension = ".txt";

            string ucontExt = TextAssetHelper.GetUContainerExtension(cont);
            if (ucontExt != string.Empty)
            {
                string ucontExtNoDot = ucontExt[1..];
                string displayName = $"{ucontExtNoDot} files (*{ucontExt})";
                List<string> extensions = new List<string>() { ucontExtNoDot };
                sfd.Filters.Insert(0, new FileDialogFilter() { Name = displayName, Extensions = extensions });
                defaultExtension = ucontExt;
            }

            sfd.InitialFileName = $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}{defaultExtension}";

            string file = await sfd.ShowAsync(win);

            if (file != null && file != string.Empty)
            {
                byte[] byteData = baseField["m_Script"].AsByteArray;
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
