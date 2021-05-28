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
using System.Runtime.InteropServices;
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

        public FilterMode filterMode;

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
            boxLightMapFormat.Text = tex.m_LightmapFormat.ToString("X2");
            ddColorSpace.SelectedIndex = tex.m_ColorSpace;
        }

        private async void BtnLoad_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open texture";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "PNG file", Extensions = new List<string>() { "png" } }
            };

            string[] fileList = await ofd.ShowAsync(this);
            if (fileList.Length == 0)
                return;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                ImportTexture(file); //lol thanks span
            }
        }

        private void BtnSave_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (modImageBytes != null)
            {
                TextureFormat fmt = (TextureFormat)(ddTextureFmt.SelectedIndex + 1);
                byte[] encImageBytes = TextureEncoderDecoder.Encode(modImageBytes, tex.m_Width, tex.m_Height, fmt);

                AssetTypeValueField m_StreamData = baseField.Get("m_StreamData");
                m_StreamData.Get("offset").GetValue().Set(0);
                m_StreamData.Get("size").GetValue().Set(0);
                m_StreamData.Get("path").GetValue().Set("");

                baseField.Get("m_TextureFormat").GetValue().Set((int)fmt);

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
            }
            Close(true);
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
                if (image.TryGetSinglePixelSpan(out var pixelSpan))
                {
                    modImageBytes = MemoryMarshal.AsBytes(pixelSpan).ToArray();
                }
                else
                {
                    modImageBytes = null; //rip
                }
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
