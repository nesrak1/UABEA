using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia.Plugins
{
    public class PluginManager
    {
        private List<PluginInfo> loadedPlugins;

        public PluginManager()
        {
            loadedPlugins = new List<PluginInfo>();
        }

        public bool LoadPlugin(string path)
        {
            //try
            //{
                Assembly asm = Assembly.LoadFrom(path);
                foreach (Type type in asm.GetTypes())
                {
                    if (typeof(UABEAPlugin).IsAssignableFrom(type))
                    {
                        object? typeInst = Activator.CreateInstance(type);
                        if (typeInst == null)
                            return false;

                        UABEAPlugin plugInst = (UABEAPlugin)typeInst;
                        PluginInfo plugInf = plugInst.Init();
                        loadedPlugins.Add(plugInf);
                        return true;
                    }
                }
            //}
            //catch
            //{
            //    return false;
            //}
            return false;
        }

        public void LoadPluginsInDirectory(string directory)
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.dll"))
            {
                LoadPlugin(file);
            }
        }

        public List<UABEAPluginMenuInfo> GetPluginsThatSupport(AssetsManager am, List<AssetExternal> selectedAssets)
        {
            List<UABEAPluginMenuInfo> menuInfos = new List<UABEAPluginMenuInfo>();
            foreach (var pluginInf in loadedPlugins)
            {
                foreach (var option in pluginInf.options)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        //these are separate actions even though they always work the same
                        UABEAPluginAction action = i == 0 ? UABEAPluginAction.Import : UABEAPluginAction.Export;
                        bool supported = option.SelectionValidForPlugin(am, action, selectedAssets, out string entryName);
                        if (supported)
                        {
                            UABEAPluginMenuInfo menuInf = new UABEAPluginMenuInfo(pluginInf, option, entryName);
                            menuInfos.Add(menuInf);
                        }
                    }
                }
            }
            return menuInfos;
            //List<IElementDescriptor> selectedDescriptors = new List<IElementDescriptor>();
            //foreach (AssetExternal ext in selectedAssets)
            //{
            //    selectedDescriptors.Add(new ImportDescriptor());
            //    selectedDescriptors.Add(new ExportDescriptor());
            //}
            //
            //List<PluginInfo> supportedPlugins = new List<PluginInfo>();
            //foreach (var plugin in loadedPlugins)
            //{
            //    foreach (var option in plugin.options)
            //    {
            //        option.supportCallback(pluginInterface, selectedDescriptors, new string[0], out string desc, out bool supportsBatch);
            //    }
            //}
            //return supportedPlugins;
        }
    }
}
