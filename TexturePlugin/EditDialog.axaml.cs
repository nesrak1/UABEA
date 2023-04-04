using AssetsTools.NET;
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

namespace TexturePlugin
{
    public partial class EditDialog : Window
    {
        private TextureFile tex;
        private AssetTypeValueField baseField;
        private uint platform;

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

        public EditDialog(string name, TextureFile tex, AssetTypeValueField baseField, uint platform) : this()
        {
            this.tex = tex;
            this.baseField = baseField;
            this.platform = platform;

            imagePath = null;

            boxName.Text = name;
            ddTextureFmt.SelectedIndex = tex.m_TextureFormat - 1;
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
            if (imagePath != null)
            {
                TextureFormat fmt = (TextureFormat)(ddTextureFmt.SelectedIndex + 1);
                //byte[] encImageBytes = TextureEncoderDecoder.Encode(modImageBytes, tex.m_Width, tex.m_Height, fmt);

                byte[] platformBlob = TextureHelper.GetPlatformBlob(baseField);
                byte[] encImageBytes = TextureImportExport.Import(imagePath, fmt, out int width, out int height, platform, platformBlob);

                if (encImageBytes == null)
                {
                    await MessageBoxUtil.ShowDialog(this, "Error", $"Failed to encode texture format {fmt}");
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
                    baseField["m_MipCount"].AsInt = 1;

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

                baseField["m_Width"].AsInt = tex.m_Width;
                baseField["m_Height"].AsInt = tex.m_Height;

                AssetTypeValueField image_data = baseField["image data"];
                image_data.Value.ValueType = AssetValueType.ByteArray;
                image_data.TemplateField.ValueType = AssetValueType.ByteArray;
                image_data.AsByteArray = encImageBytes;

                Close(true);
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "Error",
                    "Texture reencoding is not supported atm.\n" +
                    "If you want to change the texture format,\n" +
                    "export it to png first then reimport it here.\n" +
                    "Sorry for the inconvenience.");

                Close(false);
            }
        }

        private void BtnCancel_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
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
