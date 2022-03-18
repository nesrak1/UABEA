using AssetsTools.NET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Tga;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
        public static bool Export(byte[] encData, string file, int width, int height, TextureFormat format)
        {
            string ext = Path.GetExtension(file);
            byte[] decData = TextureEncoderDecoder.Decode(encData, width, height, format);
            if (decData == null)
                return false;

            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(decData, width, height);
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
    }
}