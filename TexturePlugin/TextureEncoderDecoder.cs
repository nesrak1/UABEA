using AssetsTools.NET.Texture;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace TexturePlugin
{
    public class TextureEncoderDecoder
    {
        // needs organization
        public static int RGBAToFormatByteSize(TextureFormat format, int width, int height)
        {
            int blockCountX, blockCountY;
            switch (format)
            {
                case TextureFormat.RGB9e5Float:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.RGBA32:
                    return width * height * 4;
                case TextureFormat.RGB24:
                    return width * height * 3;
                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA4444:
                case TextureFormat.RGB565:
                    return width * height * 2;
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                    return width * height;
                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                    return width * height * 2;
                case TextureFormat.RGHalf:
                    return width * height * 4;
                case TextureFormat.RGBAHalf:
                    return width * height * 8;
                case TextureFormat.RFloat:
                    return width * height * 4;
                case TextureFormat.RGFloat:
                    return width * height * 8;
                case TextureFormat.RGBAFloat:
                    return width * height * 16;
                case TextureFormat.YUY2:
                    return width * height * 2;
                // todo
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC2_RGB4:
                case TextureFormat.ETC_RGB4_3DS:
                case TextureFormat.ETC_RGBA8_3DS:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.ETC2_RGBA8:
                    blockCountX = (width + 3) / 4;
                    blockCountY = (height + 3) / 4;
                    switch (format)
                    {
                        case TextureFormat.EAC_R:
                        case TextureFormat.EAC_R_SIGNED:
                            return blockCountX * blockCountY * 8;
                        case TextureFormat.EAC_RG:
                        case TextureFormat.EAC_RG_SIGNED:
                            return blockCountX * blockCountY * 16;
                        case TextureFormat.ETC_RGB4:
                        case TextureFormat.ETC_RGB4_3DS:
                            return blockCountX * blockCountY * 8;
                        case TextureFormat.ETC2_RGB4:
                        case TextureFormat.ETC2_RGBA1:
                        case TextureFormat.ETC2_RGBA8:
                        case TextureFormat.ETC_RGBA8_3DS:
                            return blockCountX * blockCountY * 16;
                        default:
                            return 0; // can't happen
                    }
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGBA2:
                    blockCountX = (width + 7) / 8;
                    blockCountY = (height + 3) / 4;
                    goto pvrtc_all;
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                    blockCountX = (width + 3) / 4;
                    blockCountY = (height + 3) / 4;
                    goto pvrtc_all;
                pvrtc_all:
                    return blockCountX * blockCountY * 8;
                case TextureFormat.ASTC_RGB_4x4:
                case TextureFormat.ASTC_RGBA_4x4:
                    blockCountX = (width + 3) / 4;
                    blockCountY = (height + 3) / 4;
                    goto astc_all;
                case TextureFormat.ASTC_RGB_5x5:
                case TextureFormat.ASTC_RGBA_5x5:
                    blockCountX = (width + 4) / 5;
                    blockCountY = (height + 4) / 5;
                    goto astc_all;
                case TextureFormat.ASTC_RGB_6x6:
                case TextureFormat.ASTC_RGBA_6x6:
                    blockCountX = (width + 5) / 6;
                    blockCountY = (height + 5) / 6;
                    goto astc_all;
                case TextureFormat.ASTC_RGB_8x8:
                case TextureFormat.ASTC_RGBA_8x8:
                    blockCountX = (width + 7) / 8;
                    blockCountY = (height + 7) / 8;
                    goto astc_all;
                case TextureFormat.ASTC_RGB_10x10:
                case TextureFormat.ASTC_RGBA_10x10:
                    blockCountX = (width + 9) / 10;
                    blockCountY = (height + 9) / 10;
                    goto astc_all;
                case TextureFormat.ASTC_RGB_12x12:
                case TextureFormat.ASTC_RGBA_12x12:
                    blockCountX = (width + 11) / 12;
                    blockCountY = (height + 11) / 12;
                    goto astc_all;
                astc_all:
                    return blockCountX * blockCountY * 16;
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                case TextureFormat.BC4:
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                {
                    blockCountX = (width + 3) / 4;
                    blockCountY = (height + 3) / 4;
                    switch (format)
                    {
                        case TextureFormat.DXT1:
                            return blockCountX * blockCountY * 8;
                        case TextureFormat.DXT5:
                            return blockCountX * blockCountY * 16;
                        case TextureFormat.BC4:
                            return blockCountX * blockCountY * 8;
                        case TextureFormat.BC5:
                        case TextureFormat.BC6H:
                        case TextureFormat.BC7:
                            return blockCountX * blockCountY * 16;
                        default:
                            return 0; // can't happen
                    }
                }
                default:
                    return width * height * 16; // don't know
            }
        }

        private static byte[] DecodeAssetRipperTex(byte[] data, int width, int height, TextureFormat format)
        {
            byte[] dest = TextureFile.DecodeManaged(data, format, width, height);

            for (int i = 0; i < dest.Length; i += 4)
            {
                byte temp = dest[i];
                dest[i] = dest[i + 2];
                dest[i + 2] = temp;
            }
            return dest;
        }

        private static byte[] DecodePVRTexLib(byte[] data, int width, int height, TextureFormat format)
        {
            byte[] dest = new byte[width * height * 4];
            uint size = 0;
            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* destPtr = dest)
                {
                    IntPtr dataIntPtr = (IntPtr)dataPtr;
                    IntPtr destIntPtr = (IntPtr)destPtr;
                    size = PInvoke.DecodeByPVRTexLib(dataIntPtr, destIntPtr, (int)format, (uint)width, (uint)height);
                }
            }
            if (size > 0)
            {
                byte[] resizedDest = new byte[size];
                Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                dest = null;
                return resizedDest;
            }
            else
            {
                dest = null;
                return null;
            }
        }

        private static byte[] DecodeCrunch(byte[] data, int width, int height, TextureFormat format)
        {
            byte[] dest = new byte[width * height * 4];
            uint size = 0;
            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* destPtr = dest)
                {
                    IntPtr dataIntPtr = (IntPtr)dataPtr;
                    IntPtr destIntPtr = (IntPtr)destPtr;
                    size = PInvoke.DecodeByCrunchUnity(dataIntPtr, destIntPtr, (int)format, (uint)width, (uint)height, (uint)data.Length);
                }
            }
            if (size > 0)
                return dest; //big size is fine for now
            else
                return null;
        }

        private static byte[] EncodeISPC(byte[] data, int width, int height, TextureFormat format, int quality)
        {
            int expectedSize = RGBAToFormatByteSize(format, width, height);
            byte[] dest = new byte[expectedSize];
            uint size = 0;
            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* destPtr = dest)
                {
                    IntPtr dataIntPtr = (IntPtr)dataPtr;
                    IntPtr destIntPtr = (IntPtr)destPtr;
                    size = PInvoke.EncodeByISPC(dataIntPtr, destIntPtr, (int)format, quality, (uint)width, (uint)height);
                }
            }

            if (size > expectedSize)
            {
                throw new Exception($"ispc ({format}) encoded more data than expected!");
            }
            else if (size == expectedSize)
            {
                return dest;
            }
            else if (size > 0)
            {
                byte[] resizedDest = new byte[size];
                Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                dest = null;
                return resizedDest;
            }
            else
            {
                dest = null;
                return null;
            }
        }

        private static byte[] EncodePVRTexLib(byte[] data, int width, int height, TextureFormat format, int quality)
        {
            int expectedSize = RGBAToFormatByteSize(format, width, height);
            byte[] dest = new byte[expectedSize];
            uint size = 0;
            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* destPtr = dest)
                {
                    IntPtr dataIntPtr = (IntPtr)dataPtr;
                    IntPtr destIntPtr = (IntPtr)destPtr;
                    size = PInvoke.EncodeByPVRTexLib(dataIntPtr, destIntPtr, (int)format, quality, (uint)width, (uint)height);
                }
            }

            if (size > expectedSize)
            {
                throw new Exception($"pvrtexlib ({format}) encoded more data than expected!");
            }
            else if (size == expectedSize)
            {
                return dest;
            }
            else if (size > 0)
            {
                byte[] resizedDest = new byte[size];
                Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                dest = null;
                return resizedDest;
            }
            else
            {
                dest = null;
                return null;
            }
        }

        private static byte[] EncodeCrunch(byte[] data, int width, int height, TextureFormat format, int quality, int mips)
        {
            byte[] dest = Array.Empty<byte>();
            uint size = 0;
            unsafe
            {
                int checkoutId = -1;
                fixed (byte* dataPtr = data)
                {
                    // we don't know the size of the output yet
                    // write it to unmanaged memory first and copy to managed after we have the size
                    // ////////////
                    // setting ver to 1 fixes "The texture could not be loaded because it has been
                    // encoded with an older version of Crunch" not sure if this breaks older games though
                    // todo: determine version ranges
                    IntPtr dataIntPtr = (IntPtr)dataPtr;
                    size = PInvoke.EncodeByCrunchUnity(dataIntPtr, ref checkoutId, (int)format, quality, (uint)width, (uint)height, 1, mips);
                    if (size == 0)
                    {
                        return null;
                    }
                }

                dest = new byte[size];

                fixed (byte* destPtr = dest)
                {
                    IntPtr destIntPtr = (IntPtr)destPtr;
                    if (!PInvoke.PickUpAndFree(destIntPtr, size, checkoutId))
                    {
                        return null;
                    }
                }
            }

            if (size > 0)
            {
                byte[] resizedDest = new byte[size];
                Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                dest = null;
                return resizedDest;
            }
            else
            {
                dest = null;
                return null;
            }
        }

        public static byte[] Decode(byte[] data, int width, int height, TextureFormat format)
        {
            switch (format)
            {
                //crunch
                case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5Crunched:
                case TextureFormat.ETC_RGB4Crunched:
                case TextureFormat.ETC2_RGBA8Crunched:
                {
                    byte[] uncrunch = DecodeCrunch(data, width, height, format);
                    if (uncrunch == null)
                        return null;

                    format = format switch
                    {
                        TextureFormat.DXT1Crunched => TextureFormat.DXT1,
                        TextureFormat.DXT5Crunched => TextureFormat.DXT5,
                        TextureFormat.ETC_RGB4Crunched => TextureFormat.ETC_RGB4,
                        TextureFormat.ETC2_RGBA8Crunched => TextureFormat.ETC2_RGBA8,
                        _ => 0 //can't happen
                    };

                    byte[] res;
                    if (format == TextureFormat.DXT1 || format == TextureFormat.DXT5)
                        res = DecodeAssetRipperTex(uncrunch, width, height, format);
                    else //if (format == TextureFormat.ETC_RGB4 || format == TextureFormat.ETC2_RGBA8)
                        res = DecodePVRTexLib(uncrunch, width, height, format);

                    return res;
                }
                //pvrtexlib
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.RGBA32:
                case TextureFormat.RGB24:
                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA4444:
                case TextureFormat.RGB565:
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                /////////////////////////////////
                case TextureFormat.YUY2:
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC_RGB4_3DS:
                case TextureFormat.ETC_RGBA8_3DS:
                case TextureFormat.ETC2_RGB4:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.ASTC_RGB_4x4:
                case TextureFormat.ASTC_RGB_5x5:
                case TextureFormat.ASTC_RGB_6x6:
                case TextureFormat.ASTC_RGB_8x8:
                case TextureFormat.ASTC_RGB_10x10:
                case TextureFormat.ASTC_RGB_12x12:
                case TextureFormat.ASTC_RGBA_4x4:
                case TextureFormat.ASTC_RGBA_5x5:
                case TextureFormat.ASTC_RGBA_6x6:
                case TextureFormat.ASTC_RGBA_8x8:
                case TextureFormat.ASTC_RGBA_10x10:
                case TextureFormat.ASTC_RGBA_12x12:
                {
                    byte[] res = DecodePVRTexLib(data, width, height, format);
                    return res;
                }
                //assetripper.texture
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                case TextureFormat.BC6H:
                case TextureFormat.BC4:
                case TextureFormat.BC5:
                case TextureFormat.RGB9e5Float:
                case TextureFormat.RGBA64:
                {
                    byte[] res = DecodeAssetRipperTex(data, width, height, format);
                    return res;
                }
                default:
                    return null;
            }
        }

        public static byte[] EncodeMip(byte[] data, int width, int height, TextureFormat format, int quality, int mips = 1)
        {
            switch (format)
            {
                //crunch
                case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5Crunched:
                case TextureFormat.ETC_RGB4Crunched:
                case TextureFormat.ETC2_RGBA8Crunched:
                {
                    byte[] res = EncodeCrunch(data, width, height, format, quality, mips);
                    return res;
                }
                //pvrtexlib
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.RGBA32:
                case TextureFormat.RGB24:
                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA4444:
                case TextureFormat.RGB565:
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                /////////////////////////////////
                case TextureFormat.YUY2:
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC_RGB4_3DS:
                case TextureFormat.ETC_RGBA8_3DS:
                case TextureFormat.ETC2_RGB4:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.ASTC_RGB_4x4:
                case TextureFormat.ASTC_RGB_5x5:
                case TextureFormat.ASTC_RGB_6x6:
                case TextureFormat.ASTC_RGB_8x8:
                case TextureFormat.ASTC_RGB_10x10:
                case TextureFormat.ASTC_RGB_12x12:
                case TextureFormat.ASTC_RGBA_4x4:
                case TextureFormat.ASTC_RGBA_5x5:
                case TextureFormat.ASTC_RGBA_6x6:
                case TextureFormat.ASTC_RGBA_8x8:
                case TextureFormat.ASTC_RGBA_10x10:
                case TextureFormat.ASTC_RGBA_12x12:
                {
                    byte[] res = EncodePVRTexLib(data, width, height, format, quality);
                    return res;
                }
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                {
                    byte[] res = EncodeISPC(data, width, height, format, quality);
                    return res;
                }
                case TextureFormat.BC6H: //pls don't use
                case TextureFormat.BC4:
                case TextureFormat.BC5:
                    return null;
                case TextureFormat.RGB9e5Float: //pls don't use
                    return null;
                default:
                    return null;
            }
        }

        public static byte[] Encode(SixLabors.ImageSharp.Image<Rgba32> image, int width, int height, TextureFormat format, int quality = 5, int mips = 1)
        {
            using MemoryStream rawDataStream = new MemoryStream();

            if (format == TextureFormat.DXT1Crunched || format == TextureFormat.DXT5Crunched ||
                format == TextureFormat.ETC_RGB4Crunched || format == TextureFormat.ETC2_RGBA8Crunched)
            {
                byte[] rawRgbaData = new byte[width * height * 4];
                image.CopyPixelDataTo(rawRgbaData);
                byte[] rawEncodedData = EncodeMip(rawRgbaData, width, height, format, quality, mips);
                rawDataStream.Write(rawEncodedData);
            }
            else
            {
                int curWidth = width;
                int curHeight = height;
                for (int i = 0; i < mips; i++)
                {
                    byte[] rawRgbaData = new byte[curWidth * curHeight * 4];
                    image.CopyPixelDataTo(rawRgbaData);
                    byte[] rawEncodedData = EncodeMip(rawRgbaData, curWidth, curHeight, format, quality);
                    if (rawEncodedData == null)
                    {
                        return null;
                    }
                    rawDataStream.Write(rawEncodedData);

                    if (i < mips - 1)
                    {
                        curWidth >>= 1;
                        curHeight >>= 1;
                        image.Mutate(i => i.Resize(curWidth, curHeight));
                    }
                }
            }

            return rawDataStream.ToArray();
        }
    }
}
