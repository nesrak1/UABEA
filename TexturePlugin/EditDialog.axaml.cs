using AssetsTools.NET;
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
    public class EditDialog : Window
    {
        //controls
        private TextBox boxName;
        private ComboBox ddTextureFmt;
        private CheckBox chkHasMipMaps;
        private CheckBox chkIsReadable;
        private ComboBox ddFilterMode;
        private TextBox boxAnisotFilter;
        private TextBox boxMipMapBias;
        private ComboBox ddWrapModeU;
        private ComboBox ddWrapModeV;
        private TextBox boxLightMapFormat;
        private ComboBox ddColorSpace;
        private Button btnLoad;
        private Button btnSave;
        private Button btnCancel;

        private TextureFile tex;
        private AssetTypeValueField baseField;

        private byte[] modImageBytes;

        public EditDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            boxName = this.FindControl<TextBox>("boxName");
            ddTextureFmt = this.FindControl<ComboBox>("ddTextureFmt");
            chkHasMipMaps = this.FindControl<CheckBox>("chkHasMipMaps");
            chkIsReadable = this.FindControl<CheckBox>("chkIsReadable");
            ddFilterMode = this.FindControl<ComboBox>("ddFilterMode");
            boxAnisotFilter = this.FindControl<TextBox>("boxAnisotFilter");
            boxMipMapBias = this.FindControl<TextBox>("boxMipMapBias");
            ddWrapModeU = this.FindControl<ComboBox>("ddWrapModeU");
            ddWrapModeV = this.FindControl<ComboBox>("ddWrapModeV");
            boxLightMapFormat = this.FindControl<TextBox>("boxLightMapFormat");
            ddColorSpace = this.FindControl<ComboBox>("ddColorSpace");
            btnLoad = this.FindControl<Button>("btnLoad");
            btnSave = this.FindControl<Button>("btnSave");
            btnCancel = this.FindControl<Button>("btnCancel");
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

        public EditDialog(string name, TextureFile tex, AssetTypeValueField baseField) : this()
        {
            this.tex = tex;
            this.baseField = baseField;

            modImageBytes = null;

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
                ImportTexture(file); //lol thanks span
            }
        }

        private async void BtnSave_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (modImageBytes != null)
            {
                TextureFormat fmt = (TextureFormat)(ddTextureFmt.SelectedIndex + 1);
                byte[] encImageBytes = TextureEncoderDecoder.Encode(modImageBytes, tex.m_Width, tex.m_Height, fmt);

                if (encImageBytes == null)
                {
                    await MessageBoxUtil.ShowDialog(this, "Error", $"Failed to encode texture format {fmt}");
                    Close(false);
                    return;
                }

                AssetTypeValueField m_StreamData = baseField.Get("m_StreamData");
                m_StreamData.Get("offset").GetValue().Set(0);
                m_StreamData.Get("size").GetValue().Set(0);
                m_StreamData.Get("path").GetValue().Set("");

                baseField.Get("m_Name").GetValue().Set(boxName.Text);

                if (!baseField.Get("m_MipMap").IsDummy())
                    baseField.Get("m_MipMap").GetValue().Set(chkHasMipMaps.IsChecked ?? false);

                if (!baseField.Get("m_MipCount").IsDummy())
                    baseField.Get("m_MipCount").GetValue().Set(1);

                if (!baseField.Get("m_ReadAllowed").IsDummy())
                    baseField.Get("m_ReadAllowed").GetValue().Set(chkIsReadable.IsChecked ?? false);

                AssetTypeValueField m_TextureSettings = baseField.Get("m_TextureSettings");

                m_TextureSettings.Get("m_FilterMode").GetValue().Set(ddFilterMode.SelectedIndex);

                if (int.TryParse(boxAnisotFilter.Text, out int aniso))
                    m_TextureSettings.Get("m_Aniso").GetValue().Set(aniso);

                if (int.TryParse(boxMipMapBias.Text, out int mipBias))
                    m_TextureSettings.Get("m_MipBias").GetValue().Set(mipBias);

                m_TextureSettings.Get("m_WrapU").GetValue().Set(ddWrapModeU.SelectedIndex);
                m_TextureSettings.Get("m_WrapV").GetValue().Set(ddWrapModeV.SelectedIndex);

                if (boxLightMapFormat.Text.StartsWith("0x"))
                {
                    if (int.TryParse(boxLightMapFormat.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int lightFmt))
                        baseField.Get("m_LightmapFormat").GetValue().Set(lightFmt);
                }
                else
                {
                    if (int.TryParse(boxLightMapFormat.Text, out int lightFmt))
                        baseField.Get("m_LightmapFormat").GetValue().Set(lightFmt);
                }

                baseField.Get("m_ColorSpace").GetValue().Set(ddColorSpace.SelectedIndex);

                baseField.Get("m_TextureFormat").GetValue().Set((int)fmt);
                baseField.Get("m_CompleteImageSize").GetValue().Set(encImageBytes.Length);

                baseField.Get("m_Width").GetValue().Set(tex.m_Width);
                baseField.Get("m_Height").GetValue().Set(tex.m_Height);

                AssetTypeValueField image_data = baseField.Get("image data");
                image_data.GetValue().type = EnumValueTypes.ByteArray;
                image_data.templateField.valueType = EnumValueTypes.ByteArray;
                AssetTypeByteArray byteArray = new AssetTypeByteArray()
                {
                    size = (uint)encImageBytes.Length,
                    data = encImageBytes
                };
                image_data.GetValue().Set(byteArray);

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

        private void ImportTexture(string file)
        {
            using (Image<Rgba32> image = Image.Load<Rgba32>(file))
            {
                tex.m_Width = image.Width;
                tex.m_Height = image.Height;

                image.Mutate(i => i.Flip(FlipMode.Vertical));

                modImageBytes = new byte[tex.m_Width * tex.m_Height * 4];
                image.CopyPixelDataTo(modImageBytes);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
