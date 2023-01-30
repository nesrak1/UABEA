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

namespace TexturePlugin
{
    public class TextureImportExport
    {
        public static byte[] Import(string file, TextureFormat format, out int width, out int height)
        {
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

        public static bool Export(byte[] encData, string file, int width, int height, TextureFormat format, uint platform = 0, byte[] platformBlob = null)
        {
            int originalWidth = width;
            int originalHeight = height;

            bool unswizzleSwitch = false;
            int gobsPerBlock = 1;
            if (platform == 38 && platformBlob != null && platformBlob.Length != 0)
            {
                unswizzleSwitch = true;

                gobsPerBlock = 1 << BitConverter.ToInt32(platformBlob, 8);
                // apparently there is another value to worry about, but seeing as it's
                // always 0 and I have nothing else to test against, this will probably
                // work fine for now

                var newSize = Texture2DSwitchDeswizzler.SwitchGetPaddedTextureSize(format, width, height);
                width = newSize.Width;
                height = newSize.Height;
            }
            //bool unswizzlePs5 = false;
            //if (unswizzlePs5)
            //{
            //    height = CeilDivide(height, 128) * 128;
            //}

            string ext = Path.GetExtension(file);
            byte[] decData = TextureEncoderDecoder.Decode(encData, width, height, format);
            if (decData == null)
                return false;

            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(decData, width, height);
            if (unswizzleSwitch)
            {
                Size blockSize = Texture2DSwitchDeswizzler.TextureFormatToBlockSize(format);
                image = Texture2DSwitchDeswizzler.SwitchUnswizzle(image, blockSize, gobsPerBlock);
                image.Mutate(i => i.Crop(originalWidth, originalHeight));
            }
            image.Mutate(i => i.Flip(FlipMode.Vertical));

            switch (ext)
            {
                case ".png":
                    image.SaveAsPng(file);
                    break;
                case ".tga":
                    var encoder = new TgaEncoder();
                    encoder.BitsPerPixel = TgaBitsPerPixel.Pixel32;
                    image.SaveAsTga(file, encoder);
                    break;
            }

            return true;
        }

        //public static int CeilDivide(int a, int b)
        //{
        //    return (a + b - 1) / b;
        //}
    }
}