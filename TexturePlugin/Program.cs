using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Controls;
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
    public class TexturePlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo()
            {
                name = "Texture Import/Export",
                options = new List<UABEAPluginOption>
                {
                    new ImportTextureOption(),
                    new ExportTextureOption(),
                    new EditTextureOption()
                }
            };
            return info;
        }
    }
}
