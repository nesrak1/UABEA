using AssetsTools.NET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Tga;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AssetsTools.NET.Texture;
using System.Xml.Linq;

namespace TexturePlugin
{
    public class TextureImportExport
    {
        public static byte[] Import(string file, TextureFormat format, out int width, out int height, uint platform = 0, byte[] platformBlob = null)
        {
            if (platform == 38 && platformBlob != null && platformBlob.Length != 0)
            {
                return ImportSwitch(file, format, out width, out height, platformBlob);
            }

            byte[] decData;
            using (Image<Rgba32> image = Image.Load<Rgba32>(file))
            {
                width = image.Width;
                height = image.Height;

                image.Mutate(i => i.Flip(FlipMode.Vertical));

                decData = new byte[width * height * 4];
                image.CopyPixelDataTo(decData);
            }

            byte[] encData = TextureEncoderDecoder.Encode(decData, width, height, format);
            return encData;
        }

        private static byte[] ImportSwitch(string file, TextureFormat format, out int width, out int height, byte[] platformBlob = null)
        {
            byte[] decData;
            int paddedWidth, paddedHeight;
            using (Image<Rgba32> image = Image.Load<Rgba32>(file))
            {
                width = image.Width;
                height = image.Height;

                format = GetCorrectedSwitchTextureFormat(format);
                int gobsPerBlock = Texture2DSwitchDeswizzler.GetSwitchGobsPerBlock(platformBlob);
                Size blockSize = Texture2DSwitchDeswizzler.TextureFormatToBlockSize(format);
                Size newSize = Texture2DSwitchDeswizzler.GetPaddedTextureSize(width, height, blockSize.Width, blockSize.Height, gobsPerBlock);
                paddedWidth = newSize.Width;
                paddedHeight = newSize.Height;

                image.Mutate(i => i.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.BoxPad,
                    Position = AnchorPositionMode.BottomLeft,
                    PadColor = Color.Fuchsia, // full alpha?
                    Size = newSize
                }).Flip(FlipMode.Vertical));

                Image<Rgba32> swizzledImage = Texture2DSwitchDeswizzler.SwitchSwizzle(image, blockSize, gobsPerBlock);

                decData = new byte[paddedWidth * paddedHeight * 4];
                swizzledImage.CopyPixelDataTo(decData);
            }

            byte[] encData = TextureEncoderDecoder.Encode(decData, paddedWidth, paddedHeight, format);
            return encData;
        }

        public static bool Export(byte[] encData, string file, int width, int height, TextureFormat format, uint platform = 0, byte[] platformBlob = null)
        {
            if (platform == 38 && platformBlob != null && platformBlob.Length != 0)
            {
                return ExportSwitch(encData, file, width, height, format, platformBlob);
            }

            byte[] decData = TextureEncoderDecoder.Decode(encData, width, height, format);
            if (decData == null)
                return false;

            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(decData, width, height);
            image.Mutate(i => i.Flip(FlipMode.Vertical));

            SaveImageAtPath(image, file);

            return true;
        }

        private static bool ExportSwitch(byte[] encData, string file, int width, int height, TextureFormat format, byte[] platformBlob = null)
        {
            int originalWidth = width;
            int originalHeight = height;

            format = GetCorrectedSwitchTextureFormat(format);
            int gobsPerBlock = Texture2DSwitchDeswizzler.GetSwitchGobsPerBlock(platformBlob);
            Size blockSize = Texture2DSwitchDeswizzler.TextureFormatToBlockSize(format);
            Size newSize = Texture2DSwitchDeswizzler.GetPaddedTextureSize(width, height, blockSize.Width, blockSize.Height, gobsPerBlock);
            width = newSize.Width;
            height = newSize.Height;

            byte[] decData = TextureEncoderDecoder.Decode(encData, width, height, format);
            if (decData == null)
                return false;

            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(decData, width, height);

            image = Texture2DSwitchDeswizzler.SwitchUnswizzle(image, blockSize, gobsPerBlock);
            if (originalWidth != width || originalHeight != height)
            {
                image.Mutate(i => i.Crop(originalWidth, originalHeight));
            }

            image.Mutate(i => i.Flip(FlipMode.Vertical));

            SaveImageAtPath(image, file);

            return true;
        }

        private static void SaveImageAtPath(Image<Rgba32> image, string path)
        {
            string ext = Path.GetExtension(path);
            switch (ext)
            {
                case ".png":
                    image.SaveAsPng(path);
                    break;
                case ".tga":
                    var encoder = new TgaEncoder();
                    encoder.BitsPerPixel = TgaBitsPerPixel.Pixel32;
                    image.SaveAsTga(path, encoder);
                    break;
            }
        }

        private static TextureFormat GetCorrectedSwitchTextureFormat(TextureFormat format)
        {
            // in older versions of unity, rgb24 has a platformblob which shouldn't
            // be possible. it turns out in this case, the image is just rgba32.
            if (format == TextureFormat.RGB24)
            {
                return TextureFormat.RGBA32;
            }
            else if (format == TextureFormat.BGR24)
            {
                return TextureFormat.BGRA32;
            }
            return format;
        }
    }
}