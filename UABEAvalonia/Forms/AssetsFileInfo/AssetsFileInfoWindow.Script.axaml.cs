using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class AssetsFileInfoWindow
    {
        private void FillScriptInfo()
        {
            if (cbxFiles.SelectedItem == null)
                return;

            AssetsFileInstance selectedFile = activeFile;
            if (selectedFile == null)
                return;

            List<string> items = new List<string>();
            List<AssetPPtr> scriptTypes = selectedFile.file.Metadata.ScriptTypes;
            for (int i = 0; i < scriptTypes.Count; i++)
            {
                AssetPPtr pptr = scriptTypes[i];
                AssetTypeValueField? scriptBf = workspace.GetBaseField(selectedFile, pptr.FileId, pptr.PathId);
                if (scriptBf == null)
                    continue;

                string nameSpace = scriptBf["m_Namespace"].AsString;
                string className = scriptBf["m_ClassName"].AsString;

                string fullName;
                if (nameSpace != "")
                    fullName = $"{nameSpace}.{className}";
                else
                    fullName = className;

                items.Add($"{i} - {fullName}");
            }

            boxScriptInfoList.Items = items;
        }
    }
}
