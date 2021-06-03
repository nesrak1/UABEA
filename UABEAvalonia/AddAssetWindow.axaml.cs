using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public partial class AddAssetWindow : Window
    {
        //controls
        private ComboBox ddFileId;
        private TextBox boxPathId;
        private TextBox boxTypeId;
        private TextBox boxMonoId;
        private CheckBox chkCreateZerodAsset;
        private Button btnOk;
        private Button btnCancel;

        private AssetWorkspace workspace;

        public AddAssetWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            ddFileId = this.FindControl<ComboBox>("ddFileId");
            boxPathId = this.FindControl<TextBox>("boxPathId");
            boxTypeId = this.FindControl<TextBox>("boxTypeId");
            boxMonoId = this.FindControl<TextBox>("boxMonoId");
            chkCreateZerodAsset = this.FindControl<CheckBox>("chkCreateZerodAsset");
            btnOk = this.FindControl<Button>("btnOk");
            btnCancel = this.FindControl<Button>("btnCancel");
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        public AddAssetWindow(AssetWorkspace workspace) : this()
        {
            this.workspace = workspace;

            int index = 0;
            List<string> loadedFiles = new List<string>();
            foreach (AssetsFileInstance inst in workspace.LoadedFiles)
            {
                loadedFiles.Add($"{index++} - {Path.GetFileName(inst.path)}");
            }
            ddFileId.Items = loadedFiles;
            boxPathId.Text = "1"; //todo get last id (including new assets)
            boxTypeId.Text = "1";
            boxMonoId.Text = "-1";
        }

        private async void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            int fileId = ddFileId.SelectedIndex; //hopefully in order
            string pathIdText = boxPathId.Text;
            string typeIdText = boxTypeId.Text;
            string monoIdText = boxMonoId.Text;
            bool createBlankAsset = chkCreateZerodAsset.IsChecked ?? false;

            AssetsFileInstance file = workspace.LoadedFiles[fileId];

            AssetTypeTemplateField tempField = null;
            byte[] assetBytes;
            long pathId;
            int typeId;
            ushort monoId;

            if (!long.TryParse(pathIdText, out pathId))
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "Path ID was invalid.");
                return;
            }

            if (!file.file.typeTree.hasTypeTree)
            {
                ClassDatabaseFile cldb = workspace.am.classFile;
                ClassDatabaseType cldbType;
                bool needsTypeId;
                if (int.TryParse(typeIdText, out typeId))
                {
                    cldbType = AssetHelper.FindAssetClassByID(cldb, (uint)typeId);
                    needsTypeId = false;
                }
                else
                {
                    cldbType = AssetHelper.FindAssetClassByName(cldb, typeIdText);
                    needsTypeId = true;
                }

                if (cldbType == null)
                {
                    //maybe ask to continue if there's some weird custom type they need to add or smth
                    await MessageBoxUtil.ShowDialog(this, "Bad input", "Class type was invalid.");
                    return;
                }

                if (needsTypeId)
                {
                    typeId = cldbType.classId;
                }

                if (createBlankAsset)
                {
                    tempField = new AssetTypeTemplateField();
                    tempField.FromClassDatabase(cldb, cldbType, 0);
                }
            }
            else
            {
                TypeTree tt = file.file.typeTree;
                Type_0D ttType;
                bool needsTypeId;
                if (int.TryParse(typeIdText, out typeId))
                {
                    ttType = AssetHelper.FindTypeTreeTypeByID(tt, (uint)typeId);
                    needsTypeId = false;
                }
                else
                {
                    ttType = AssetHelper.FindTypeTreeTypeByName(tt, typeIdText);
                    needsTypeId = true;
                }

                if (ttType == null)
                {
                    await MessageBoxUtil.ShowDialog(this, "Bad input", "Class type was invalid.");
                    return;
                }

                if (needsTypeId)
                {
                    typeId = ttType.classId;
                }

                if (createBlankAsset)
                {
                    tempField = new AssetTypeTemplateField();
                    tempField.From0D(ttType, 0);
                }
            }

            if (monoIdText == "-1")
            {
                monoId = 0xffff;
            }
            else if (!ushort.TryParse(monoIdText, out monoId))
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "Mono ID was invalid.");
                return;
            }

            if (createBlankAsset)
            {
                AssetTypeValueField baseField = ValueBuilder.DefaultValueFieldFromTemplate(tempField);
                assetBytes = baseField.WriteToByteArray();
            }
            else
            {
                assetBytes = new byte[0];
            }

            workspace.AddReplacer(file, new AssetsReplacerFromMemory(0, pathId, typeId, monoId, assetBytes), new MemoryStream(assetBytes));

            Close(true);
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
