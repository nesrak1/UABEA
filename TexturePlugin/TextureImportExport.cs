using AssetsTools.NET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TexturePlugin
{
    public class TextureImportExport
    {
        public static byte[] ImportPng(string file, TextureFormat format, out int width, out int height)
        {
            byte[] decData;
            using (Image<Rgba32> image = Image.Load<Rgba32>(file))
            {
                width = image.Width;
                height = image.Height;

                image.Mutate(i => i.Flip(FlipMode.Vertical));
                if (image.TryGetSinglePixelSpan(out var pixelSpan))
                {
                    decData = MemoryMarshal.AsBytes(pixelSpan).ToArray();
                }
                else
                {
                    return null; //rip
                }
            }

            byte[] encData = TextureEncoderDecoder.Encode(decData, width, height, format);
            return encData;
        }
        public static bool ExportPng(byte[] encData, string file, int width, int height, TextureFormat format)
        {
            byte[] decData = TextureEncoderDecoder.Decode(encData, width, height, format);
            if (decData == null)
                return false;

            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(decData, width, height);
            image.Mutate(i => i.Flip(FlipMode.Vertical));
            image.SaveAsPng(file);

            return true;
        }
    }
}