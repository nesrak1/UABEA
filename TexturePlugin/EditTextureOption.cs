using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Controls;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace TexturePlugin
{
    public class EditTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Edit texture";

            if (action != UABEAPluginAction.Import)
                return false;

            if (selection.Count != 1)
                return false;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != (int)AssetClassID.Texture2D)
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, cont);
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
            EditDialog dialog = new EditDialog(texFile.m_Name, texFile, texBaseField, cont.FileInstance);
            bool saved = await dialog.ShowDialog<bool>(win);
            if (saved)
            {
                byte[] savedAsset = texBaseField.WriteToByteArray();

                var replacer = new AssetsReplacerFromMemory(
                    cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

                workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));
                return true;
            }
            return false;
        }
    }
}
