using AssetsTools.NET.Texture;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TexturePlugin
{
    public class Texture2DSwitchDeswizzler
    {
        // referring to block here as a compressed texture block, not a gob one
        const int GOB_X_BLOCK_COUNT = 4;
        const int GOB_Y_BLOCK_COUNT = 8;
        const int BLOCKS_IN_GOB = GOB_X_BLOCK_COUNT * GOB_Y_BLOCK_COUNT;

        /*
        sector:
        A
        B         

        gob (made of sectors):
        ABIJ
        CDKL
        EFMN
        GHOP

        gob blocks (example with height 2):
        ACEGIK... from left to right of image
        BDFHJL...
        --------- start new row of blocks
        MOQSUW...
        NPRTVX...
        */

        private static void CopyBlock(Image<Rgba32> srcImage, Image<Rgba32> dstImage, int sbx, int sby, int dbx, int dby, int blockSizeW, int blockSizeH)
        {
            for (int i = 0; i < blockSizeW; i++)
            {
                for (int j = 0; j < blockSizeH; j++)
                {
                    dstImage[dbx * blockSizeW + i, dby * blockSizeH + j] = srcImage[sbx * blockSizeW + i, sby * blockSizeH + j];
                }
            }
        }

        private static int ToNextNearestPo2(int x)
        {
            if (x < 0)
                return 0;

            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        private static int CeilDivide(int a, int b)
        {
            return (a + b - 1) / b;
        }

        internal static Image<Rgba32> SwitchUnswizzle(Image<Rgba32> srcImage, Size blockSize, int gobsPerBlock)
        {
            Image<Rgba32> dstImage = new Image<Rgba32>(srcImage.Width, srcImage.Height);

            int width = srcImage.Width;
            int height = srcImage.Height;

            int blockCountX = CeilDivide(width, blockSize.Width);
            int blockCountY = CeilDivide(height, blockSize.Height);

            int gobCountX = blockCountX / GOB_X_BLOCK_COUNT;
            int gobCountY = blockCountY / GOB_Y_BLOCK_COUNT;

            int srcX = 0;
            int srcY = 0;
            for (int i = 0; i < gobCountY / gobsPerBlock; i++)
            {
                for (int j = 0; j < gobCountX; j++)
                {
                    for (int k = 0; k < gobsPerBlock; k++)
                    {
                        for (int l = 0; l < BLOCKS_IN_GOB; l++)
                        {
                            // todo: use table for speedy boi
                            int gobX = ((l >> 3) & 0b10) | ((l >> 1) & 0b1);
                            int gobY = ((l >> 1) & 0b110) | (l & 0b1);
                            int gobDstX = j * GOB_X_BLOCK_COUNT + gobX;
                            int gobDstY = (i * gobsPerBlock + k) * GOB_Y_BLOCK_COUNT + gobY;
                            CopyBlock(srcImage, dstImage, srcX, srcY, gobDstX, gobDstY, blockSize.Width, blockSize.Height);

                            srcX++;
                            if (srcX >= blockCountX)
                            {
                                srcX = 0;
                                srcY++;
                            }
                        }
                    }
                }
            }

            return dstImage;
        }

        internal static Size TextureFormatToBlockSize(TextureFormat m_TextureFormat)
        {
            switch (m_TextureFormat)
            {
                case TextureFormat.Alpha8: return new Size(1, 1);
                case TextureFormat.ARGB4444: return new Size(8, 1);
                case TextureFormat.RGB24: return new Size(1, 1);
                case TextureFormat.RGBA32: return new Size(1, 1);
                case TextureFormat.ARGB32: return new Size(1, 1);
                case TextureFormat.ARGBFloat: return new Size(1, 1);
                case TextureFormat.RGB565: return new Size(8, 1);
                case TextureFormat.BGR24: return new Size(1, 1);
                case TextureFormat.R16: return new Size(8, 1);
                case TextureFormat.DXT1: return new Size(8, 4);
                case TextureFormat.DXT5: return new Size(4, 4);
                case TextureFormat.RGBA4444: return new Size(1, 1);
                case TextureFormat.BGRA32: return new Size(1, 1);
                case TextureFormat.BC6H: return new Size(4, 4);
                case TextureFormat.BC7: return new Size(4, 4);
                case TextureFormat.BC4: return new Size(8, 4);
                case TextureFormat.BC5: return new Size(4, 4);
                case TextureFormat.ASTC_RGB_4x4: return new Size(4, 4);
                case TextureFormat.ASTC_RGB_5x5: return new Size(5, 5);
                case TextureFormat.ASTC_RGB_6x6: return new Size(6, 6);
                case TextureFormat.ASTC_RGB_8x8: return new Size(8, 8);
                case TextureFormat.ASTC_RGB_10x10: return new Size(10, 10);
                case TextureFormat.ASTC_RGB_12x12: return new Size(12, 12);
                case TextureFormat.ASTC_RGBA_4x4: return new Size(4, 4);
                case TextureFormat.ASTC_RGBA_5x5: return new Size(5, 5);
                case TextureFormat.ASTC_RGBA_6x6: return new Size(6, 6);
                case TextureFormat.ASTC_RGBA_8x8: return new Size(8, 8);
                case TextureFormat.ASTC_RGBA_10x10: return new Size(10, 10);
                case TextureFormat.ASTC_RGBA_12x12: return new Size(12, 12);
                case TextureFormat.RG16: return new Size(16, 1);
                case TextureFormat.R8: return new Size(16, 1);
                default: throw new NotImplementedException();
            };
        }

        internal static Size SwitchGetPaddedTextureSize(TextureFormat textureFormat, int width, int height)
        {
            if (textureFormat != TextureFormat.R8 &&
                textureFormat != TextureFormat.R16 &&
                textureFormat != TextureFormat.RGB565 &&
                textureFormat != TextureFormat.ARGB4444 &&
                textureFormat != TextureFormat.Alpha8)
            {
                // this is bad code
                if (textureFormat == TextureFormat.ASTC_RGB_5x5)
                {
                    height = CeilDivide(height, 5);
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_6x6)
                {
                    height = CeilDivide(height, 6);
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_8x8)
                {
                    height = CeilDivide(height, 8);
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_10x10)
                {
                    height = CeilDivide(height, 10);
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_12x12)
                {
                    height = CeilDivide(height, 12);
                }

                height = ToNextNearestPo2(height);

                if (textureFormat == TextureFormat.ASTC_RGB_5x5)
                {
                    height = height * 5;
                    width = CeilDivide(width, 5 * 4) * 5 * 4;
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_6x6)
                {
                    height = height * 6;
                    width = CeilDivide(width, 6 * 4) * 6 * 4;
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_8x8)
                {
                    height = height * 8;
                    width = CeilDivide(width, 8 * 4) * 8 * 4;
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_10x10)
                {
                    height = height * 10;
                    width = CeilDivide(width, 10 * 4) * 10 * 4;
                }
                else if (textureFormat == TextureFormat.ASTC_RGB_12x12)
                {
                    height = height * 12;
                    width = CeilDivide(width, 12 * 4) * 12 * 4;
                }
            }

            return new Size(width, height);
        }
    }
}
