using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace TobyCopy
{
    public class TobyCopyAssetOption : UABEAPluginOption
    {
        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            TobyCopyWindow tcMain = new TobyCopyWindow();
            await tcMain.ShowDialog(win);
            return true;
        }

        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Start TobyCopy";
            return true;
        }
    }

    public class TobyCopyPlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo();
            info.name = "TobyCopy";

            info.options = new List<UABEAPluginOption>();
            info.options.Add(new TobyCopyAssetOption());
            return info;
        }
    }
}
