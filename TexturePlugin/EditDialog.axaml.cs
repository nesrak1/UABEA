using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UABEAvalonia;
using Image = SixLabors.ImageSharp.Image;

namespace TexturePlugin
{
    public partial class EditDialog : Window
    {
        private TextureFile tex;
        private AssetTypeValueField baseField;
        private AssetsFileInstance fileInst;

        private string imagePath;

        public EditDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnLoad.Click += BtnLoad_Click;
            btnSave.Click += BtnSave_Click;
            btnCancel.Click += BtnCancel_Click;

            ddTextureFmt.Items = Enum.GetValues(typeof(TextureFormat));
            ddFilterMode.Items = Enum.GetValues(typeof(FilterMode));
            ddWrapModeU.Items = Enum.GetValues(typeof(WrapMode));
            ddWrapModeV.Items = Enum.GetValues(typeof(WrapMode));
            ddColorSpace.Items = Enum.GetValues(typeof(ColorSpace));
        }

        public EditDialog(string name, TextureFile tex, AssetTypeValueField baseField, AssetsFileInstance fileInst) : this()
        {
            this.tex = tex;
            this.baseField = baseField;
            this.fileInst = fileInst;

            imagePath = null;

            boxName.Text = name;
            ddTextureFmt.SelectedIndex = TextureFormatToIndex(tex.m_TextureFormat);
            chkHasMipMaps.IsChecked = tex.m_MipMap;
            chkIsReadable.IsChecked = tex.m_IsReadable;
            ddFilterMode.SelectedIndex = tex.m_TextureSettings.m_FilterMode;
            boxAnisotFilter.Text = tex.m_TextureSettings.m_Aniso.ToString();
            boxMipMapBias.Text = tex.m_TextureSettings.m_MipBias.ToString();
            ddWrapModeU.SelectedIndex = tex.m_TextureSettings.m_WrapU;
            ddWrapModeV.SelectedIndex = tex.m_TextureSettings.m_WrapV;
            boxLightMapFormat.Text = "0x" + tex.m_LightmapFormat.ToString("X2");
            ddColorSpace.SelectedIndex = tex.m_ColorSpace;
        }

        private async void BtnLoad_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open texture";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Texture file", Extensions = new List<string>() { "png", "tga" } }
            };

            string[] fileList = await ofd.ShowAsync(this);
            if (fileList == null || fileList.Length == 0)
                return;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                imagePath = file;
            }
        }

        private async void BtnSave_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            uint platform = fileInst.file.Metadata.TargetPlatform;
            byte[] platformBlob = TextureHelper.GetPlatformBlob(baseField);

            Image<Rgba32> imgToImport;
            if (imagePath == null)
            {
                byte[] data = TextureHelper.GetRawTextureBytes(tex, fileInst);
                imgToImport = TextureImportExport.Export(data, tex.m_Width, tex.m_Height, (TextureFormat)tex.m_TextureFormat, platform, platformBlob);
            }
            else
            {
                imgToImport = Image.Load<Rgba32>(imagePath);
            }

            TextureFormat fmt = (TextureFormat)IndexToTextureFormat(ddTextureFmt.SelectedIndex);

            int mips = 1;
            if (chkHasMipMaps.IsChecked.GetValueOrDefault())
            {
                if (imgToImport.Width == tex.m_Width && imgToImport.Height == tex.m_Height)
                {
                    mips = tex.m_MipCount;
                }
                else if (TextureHelper.IsPo2(imgToImport.Width) && TextureHelper.IsPo2(imgToImport.Height))
                {
                    mips = TextureHelper.GetMaxMipCount(imgToImport.Width, imgToImport.Height);
                }
            }

            int width = 0, height = 0;
            byte[] encImageBytes = null;
            string exceptionMessage = string.Empty;
            try
            {
                encImageBytes = TextureImportExport.Import(imgToImport, fmt, out width, out height, ref mips, platform, platformBlob);
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.ToString();
            }

            if (encImageBytes == null)
            {
                string dialogText = $"Failed to encode texture format {fmt}!";
                if (exceptionMessage != null)
                {
                    dialogText += "\n" + exceptionMessage;
                }
                await MessageBoxUtil.ShowDialog(this, "Error", dialogText);
                Close(false);
                return;
            }

            AssetTypeValueField m_StreamData = baseField["m_StreamData"];
            m_StreamData["offset"].AsInt = 0;
            m_StreamData["size"].AsInt = 0;
            m_StreamData["path"].AsString = "";

            baseField["m_Name"].AsString = boxName.Text;

            if (!baseField["m_MipMap"].IsDummy)
                baseField["m_MipMap"].AsBool = chkHasMipMaps.IsChecked ?? false;

            if (!baseField["m_MipCount"].IsDummy)
                baseField["m_MipCount"].AsInt = mips;

            if (!baseField["m_ReadAllowed"].IsDummy)
                baseField["m_ReadAllowed"].AsBool = chkIsReadable.IsChecked ?? false;

            AssetTypeValueField m_TextureSettings = baseField["m_TextureSettings"];

            m_TextureSettings["m_FilterMode"].AsInt = ddFilterMode.SelectedIndex;

            if (int.TryParse(boxAnisotFilter.Text, out int aniso))
                m_TextureSettings["m_Aniso"].AsInt = aniso;

            if (int.TryParse(boxMipMapBias.Text, out int mipBias))
                m_TextureSettings["m_MipBias"].AsInt = mipBias;

            m_TextureSettings["m_WrapU"].AsInt = ddWrapModeU.SelectedIndex;
            m_TextureSettings["m_WrapV"].AsInt = ddWrapModeV.SelectedIndex;

            if (boxLightMapFormat.Text.StartsWith("0x"))
            {
                if (int.TryParse(boxLightMapFormat.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int lightFmt))
                    baseField["m_LightmapFormat"].AsInt = lightFmt;
            }
            else
            {
                if (int.TryParse(boxLightMapFormat.Text, out int lightFmt))
                    baseField["m_LightmapFormat"].AsInt = lightFmt;
            }

            baseField["m_ColorSpace"].AsInt = ddColorSpace.SelectedIndex;

            baseField["m_TextureFormat"].AsInt = (int)fmt;
            baseField["m_CompleteImageSize"].AsInt = encImageBytes.Length;

            baseField["m_Width"].AsInt = width;
            baseField["m_Height"].AsInt = height;

            AssetTypeValueField image_data = baseField["image data"];
            image_data.Value.ValueType = AssetValueType.ByteArray;
            image_data.TemplateField.ValueType = AssetValueType.ByteArray;
            image_data.AsByteArray = encImageBytes;

            Close(true);
        }

        private void BtnCancel_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        // lazy and quick enum conversion
        private int TextureFormatToIndex(int format)
        {
            if (format >= 41)
                return format - 41 + 37;
            else
                return format - 1;
        }

        private int IndexToTextureFormat(int format)
        {
            if (format >= 37)
                return format + 41 - 37;
            else
                return format + 1;
        }
    }

    public enum FilterMode
    {
        Point,
        Bilinear,
        Trilinear
    }

    public enum WrapMode
    {
        Repeat,
        Clamp,
        Mirror,
        MirrorOnce
    }

    public enum ColorSpace
    {
        Gamma,
        Linear
    }
}
